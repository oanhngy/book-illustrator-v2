using System.Text.Json;
using Microsoft.Extensions.Options;
using server.Gemini;
using server.Models;
using server.Storage;
namespace server.Pipeline;

public class PipelineService
{
    //shema ép Gemini trả đúng cho B1
    //static only=parse 1 lần cho cả app; Clone() để JsonEle sống độc lập vs JsonDocument
    private static readonly JsonElement StyleSchema=JsonDocument.Parse(
        """
        {
            "type": "object",
            "properties": {
                "style": { "type": "string" }
            },
            "required": ["style"]
        }
        """).RootElement.Clone();

    private const int MaxCharacters=2; //cap B2 Character

    //schema B2
    private static readonly JsonElement CharactersSchema=JsonDocument.Parse(
        """
        {
            "type": "object",
            "properties": {
                "characters": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string" },
                            "imagePrompt": { "type": "string" }
                        },
                        "required": ["name", "imagePrompt"]
                    }
                }
            },
            "required": ["characters"]
        }
        """).RootElement.Clone();

    private readonly ProjectStore _store;
    private readonly IGeminiClient _gemini; //đổi fake/real mà pipeline k biết
    private readonly GeminiOptions _options;
    private readonly ILogger<PipelineService> _logger;
    
    public PipelineService(ProjectStore store, IGeminiClient gemini, IOptions<GeminiOptions> options, ILogger<PipelineService> logger)
    {
        _store=store;
        _gemini=gemini;
        _options=options.Value;
        _logger=logger;
    }

    //A5, thay cho UPDATE...WHERE completedSteps=n-1 AND runningStep IS NULL
    public async Task<ClaimResult> ClaimStepAsync(Guid projectId, int step, string? requestedStyle)
    {
        //2 biến ngoài lambda
        var status=ClaimStatus.NotFound;
        DateTime? ticket=null;

    await _store.UpdateAsync(projectId, p =>
    {
        if(p.RunningStep is not null)
        {
            status=ClaimStatus.AlreadyRunning;
            return false;
        }    

        if(p.CompletedSteps != step-1)
        {
            status=ClaimStatus.WrongOrder;
            return false;
        }

        var now=DateTime.UtcNow;
        p.RunningStep=step;
        p.RunningSince=now;
        //claim lại bước đã lỗi=retry
        p.FailedStep=null;
        p.LastError=null;

        if(step==1)
        {
            p.RequestedStyle=requestedStyle;
        }

        status=ClaimStatus.Claimed;
        ticket=now;
        return true; //true=UpdateAsync ghi atomic xuống đĩa
    });
    return new ClaimResult(status, ticket);
    }

    //worker gọi hàm này cho mỗi job (A5, b9); KBH ném exception ra ngoài, mọi lỗi thành failedStep/lastError ở disk; k auto-retry
    public async Task RunStepAsync(StepJob job)
    {
        try
        {
            //dispatch=switch+private method
            switch(job.Step)
            {
                case 1: //style
                    await RunStyleAsync(job);
                    break;
                case 2: //character
                    await RunCharactersAsync(job);
                    break;
                default: throw new InvalidOperationException($"Step {job.Step} is not implemented");
            }
        }
        //catch rộng
        catch (Exception ex)
        {
            await FailAsync(job, ex);
        }
    }

    //B1-STYLE, only place gửi text sách
    private async Task RunStyleAsync(StepJob job)
    {
        // var project=await _store.GetAsync(job.ProjectId); //đọc snapshot ngoài lock

        // //check vé sớm: job mất claim khi xếp hàng -->bỏ, khỏi gọi API=tiết kiệm
        // if(project is null || project.RunningSince != job.Ticket)
        // {
        //     _logger.LogWarning("Step {Step} of project {ProjectId} skipped: claim ticket no longer valid", job.Step, job.ProjectId);
        //     return;
        // }

        //đọc snapshot + check vé sớm, null=mất claim --> bỏ, k gọi API
        var project=await LoadIfOwnerAsync(job);
        if(project is null) return;
        
        //thiếu sách=lỗi data--> ném, RunStepAsync bắt+ghi lỗi
        var bookText=await _store.GetBookTextAsync(job.ProjectId) 
            ?? throw new InvalidOperationException($"Book text of project {job.ProjectId} is missing");

        //2 nhánh prompt
        //rỗng=k nhập
        var userStyle=string.IsNullOrWhiteSpace(project.RequestedStyle) ? null : project.RequestedStyle.Trim();
        var instruction=userStyle is null
            ? "Can you define a art style that would fit the story but with a twist? Just give us the prompt for the art syle that will added to the furture prompts."
            : $"The art style will be: \"{userStyle}\". Keep that in mind when generating future prompts. Keep quiet for now, instructions will follow.";

        //Gemini gọi ngoài lock
        var result=await _gemini.GenerateJsonAsync(new GeminiJsonRequest
        {
            Model=_options.TextModel,
            Prompt=$"Here's the book:\n\n{bookText}\n\n{instruction}", //nhét book inline, k dùng document+uri
            PreviousInteractionId=null,
            BookUri=null,
            Schema=StyleSchema
        });

        //user nhập style==>style=cái user nhập, bỏ text AI; k nhập-->lấy text AI sinh
        var style=userStyle ?? ReadStyle(result.Data);

        //ghi kqua + đóng trong 1 lần, ghi vé
        //TextInteractionId lưu ở 2 nhánh
        await CompleteAsync(job, p =>
        {
            p.Style=style;
            p.TextInteractionId=result.InteractionId;
        });
    }

    //B2-CHARACTERS
    private async Task RunCharactersAsync(StepJob job)
    {
        var project=await LoadIfOwnerAsync(job);
        if(project is null) return;

        //B1 done mà k có interaction id--> throw
        var previousId=project.TextInteractionId
            ?? throw new InvalidOperationException($"Project {job.ProjectId} has no text interaction to continue");

        var result=await _gemini.GenerateJsonAsync(new GeminiJsonRequest
        {
            Model=_options.TextModel,
            Prompt="Can you describe the main characters (only the adults) and prepare a prompt describing them with as much details as possible (use the descriptions from the book) so Nano Banana can generate images of them? Each prompt should be at least 50 words. "
                + $"Return at most {MaxCharacters} characters, the most important ones first.",
            PreviousInteractionId=previousId, //nối B1
            Schema=CharactersSchema
        });

        //cap ở server, Gem có thể nhớ nhiều hơn, nhưng chỉ LƯU+VẼ max 2
        var characters=ReadCharacters(result.Data).Take(MaxCharacters).ToList();

        await CompleteAsync(job, p =>
        {
            p.Characters=characters;
            p.TextInteractionId=result.InteractionId;
        });
    }

    //đọc snapshot ngoài lock + check vé sớm
    //phần đầu B1, B2
    private async Task<Project?> LoadIfOwnerAsync(StepJob job)
    {
        var project=await _store.GetAsync(job.ProjectId);
        if(project is null || project.RunningSince != job.Ticket)
        {
            _logger.LogWarning("Step {Step} of project {ProjectId} skipped: claim ticket no longer valid", job.Step, job.ProjectId);
            return null;
        }
        return project;
    }

    //lấy list nvat ra JSON, thiếu name/imagePrompt bỏ qua, k có item nào-->throw
    private static List<Character> ReadCharacters(JsonElement data)
    {
        var characters=new List<Character>();
        if(data.ValueKind==JsonValueKind.Object && data.TryGetProperty("characters", out var array) && array.ValueKind==JsonValueKind.Array)
        {
            foreach(var item in array.EnumerateArray())
            {
                var name=ReadString(item, "name");
                var imagePrompt=ReadString(item, "imagePrompt");
                if(name is not null && imagePrompt is not null)
                {
                    characters.Add(new Character
                    {
                        Name=name,
                        ImagePrompt=imagePrompt
                    });
                }
            }
        }
        if(characters.Count==0)
        {
            throw new GeminiException("Gemini response does not contain any character");
        }
        return characters;
    }

    //đọc gtri của property dạng string trong các obj , sai dạng/thiếu/rỗng --> null
    private static string? ReadString(JsonElement obj, string propertyName)
    {
        if(obj.ValueKind==JsonValueKind.Object && obj.TryGetProperty(propertyName, out var value) && value.ValueKind==JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString();
        }
        return null;
    }

    //in case schema sai dạng, coi như lỗi-->user retry
    private static string ReadStyle(JsonElement data)
    {
        if(data.ValueKind==JsonValueKind.Object
            && data.TryGetProperty("style", out var styleElement)
            && styleElement.ValueKind==JsonValueKind.String
            && !string.IsNullOrWhiteSpace(styleElement.GetString()))
        {
            return styleElement.GetString()!;
        }
        throw new GeminiException("Gemini response does not contain a style");
    }

    //luật so vé, mọi lần ghi của 1 chạy qua đây, same as phase 7
    private async Task<bool> WriteIfOwnerAsync(StepJob job, Action<Project> apply)
    {
        var written=await _store.UpdateAsync(job.ProjectId, p =>
        {
            //so vé TRONG LOCK, lệch=lượt này bị reset/thay thế--> false, k ghi gì
            if(p.RunningSince != job.Ticket)
            {
                return false;
            }
            apply(p);
            return true;
        });

        //mất claim: bỏ kqua, ghi log, k ném
        if(!written)
        {
            _logger.LogWarning("Step {Step} of project {ProjectId} lost its claim, result discarded", job.Step, job.ProjectId);
        }
        return written;
    }

    //xong bước: dữ liệu + đóng state cùng 1 lần ghi atomic
    private Task<bool> CompleteAsync(StepJob job, Action<Project> applyResult)
    {
        return WriteIfOwnerAsync(job, p =>
        {
            applyResult(p);
            p.CompletedSteps=job.Step;
            p.RunningStep=null;
            p.RunningSince=null;
        });
    }

    //bước lỗi: ghi failedStep + lastError, mở lại proj để retry, có so vé
    private async Task FailAsync(StepJob job, Exception ex)
    {
        _logger.LogError(ex, "Step {Step} of project {ProjectId} failed", job.Step, job.ProjectId);
        var message=ex is GeminiException ? ex.Message:"Unexpected error while running step"; //lỗi Gem--> hiện nguyên văn, lỗi # k lộ chi tiết

        await WriteIfOwnerAsync(job, p =>
        {
            p.FailedStep=job.Step;
            p.LastError=message;
            p.RunningStep=null;
            p.RunningSince=null;
        });
    }
}