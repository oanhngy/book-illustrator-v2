using System.Text.Json;
using Microsoft.Extensions.Options;
using server.Gemini;
namespace server.Tests.Gemini;

//K PHẢI UNIT TEST ĐÚNG NGHĨA,dùng để xác nhận fixtures, interaction có nối đc từ text sang image k; mượn xUnit tận dụng GeminiOptions/GeminiClient/HttpClient có sẵn trong test
//sskip=k chạy auto, only manual
//cách manual= set GEMMINI_API_KEY thật, xóa chuỗi Skip, dotnet test --filter FullyQualifiedName~CaptureRealFixtures, xong gắn lại skip
public class GeminiClientLiveCaptureTests
{
    private static readonly string FixturesDir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "server", "fixtures"));

    [Fact(Skip="Manual only, using when need to capture real fixtures")]
    public async Task CaptureRealFixtures()
    {
        var apiKey=Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        Assert.False(string.IsNullOrWhiteSpace(apiKey), "set GEMINI_API_KEY before run this test");

        var options=Options.Create(new GeminiOptions { ApiKey=apiKey! });
        var http=new HttpClient{ BaseAddress=new Uri("https://generativelanguage.googleapis.com")};
        var client=new GeminiClient(http, options);

        //sách ngắn example
        const string bookText = """
            The Mole had been working very hard all the morning, spring-cleaning his little home.
            First with brooms, then with dusters; then on ladders and steps and chairs, with a brush
            and a pail of whitewash; till he had dust in his throat and eyes, and splashes of
            whitewash all over his black fur, and an aching back and weary arms.
            """;

        //1. upload
        var bookUri=await client.UploadBookAsync(bookText);
        //TESTING
        Console.WriteLine($"[DEBUG] bookUri = {bookUri}");

        //2. Style, đính BookUri
        var styleSchema=JsonDocument.Parse(
            """
            {
            "type": "object",
            "properties": {
                "style": {
                "type":"string"
                }
            },
            "required": ["style"]
            }
            """
        ).RootElement;
        var style=await client.GenerateJsonAsync(new GeminiJsonRequest
        {
           Model=options.Value.TextModel,
           // Inline text thay vì document+uri — v1 (book-illustrator cũ) làm y vậy, không lỗi"blobstore" vì không chạm Files API/document reference. bookUri vẫn gọi ở trên để
           // xác nhận Upload tự nó không lỗi, nhưng không dùng để tham chiếu ở đây.
           Prompt= $"Here's the book:\n\n{bookText}\n\nCan you define a art style that would fit the story but with a twist? Just give us the prompt for the art syle that will added to the furture prompts.",
           Schema=styleSchema
        });
        await WriteFixtureAsync("style.json", style.Data);

        //3. Characters
        var charactersSchema=JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "characters": {
                        "type":"array",
                        "items": {
                            "type":"object",
                            "properties": {
                                "name": {
                                    "type":"string"
                                },
                                "imagePrompt": {
                                    "type":"string"
                                }
                            },
                            "required": ["name", "imagePrompt"]
                        }
                    }
                },
                "required": ["characters"]
            }
            """).RootElement;
        var characters=await client.GenerateJsonAsync(new GeminiJsonRequest
        {
            Model=options.Value.TextModel,
            Prompt="Can you describe the main characters (only the adults) and prepare a prompt describing them with as much details as possible (use the descriptions from the book) so Nano Banana can generate images of them? Each prompt should be at least 50 words.",
            PreviousInteractionId=style.InteractionId,
            Schema=charactersSchema 
        });
        await WriteFixtureAsync("characters.json", characters.Data);

        //4,5 Portrait 1,2=điểm giao ##16
        var portrait1=await client.GenerateImageAsync(new GeminiImageRequest
        {
            Model=options.Value.ImageModel,
            Prompt="You are going to generate portrait images to illustrate The Wind in the Willows. Follow the established art style.",
            PreviousInteractionId=characters.InteractionId
        });
        await WriteImageFixtureAsync("portrait-1.json", portrait1);

        var portrait2=await client.GenerateImageAsync(new GeminiImageRequest
        {
            Model=options.Value.ImageModel,
            Prompt="Now generate a portrait for the second character, same art style.",
            PreviousInteractionId=portrait1.InteractionId
        });
        await WriteImageFixtureAsync("portrait-2.json", portrait2);

        //6. Chapters, nối từ Characters, NOT portrait
        var chaptersSchema = JsonDocument.Parse(
        """
        {
            "type": "object",
            "properties": {
                "chapters": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "title": {
                                "type": "string"
                            },
                            "summary": {
                                "type": "string"
                            },
                            "imagePrompt": {
                                "type": "string"
                            }
                        },
                        "required": ["title", "summary", "imagePrompt"]
                    }
                }
            },
            "required": ["chapters"]
        }
        """).RootElement;
        var chapters=await client.GenerateJsonAsync(new GeminiJsonRequest
        {
            Model=options.Value.TextModel,
            Prompt="Now, for each chapters of the book, give me a prompt to illustrate what happens in it. It should be a single image, not a multi-tiled page. Be very descriptive, especially of the characters. Also list all characters who appear in it.",
            PreviousInteractionId=characters.InteractionId,
            Schema=chaptersSchema
        });
        await WriteFixtureAsync("chapters.json", chapters.Data);

        //7. Illustrations, nối từ Portrait
        var illustration1=await client.GenerateImageAsync(new GeminiImageRequest
        {
            Model=options.Value.ImageModel,
            Prompt="Starting from now, illustrate the book's first chapter. Refer to your previous illustrations of the characters to keep them consistent.",
            PreviousInteractionId=portrait2.InteractionId
        });
        await WriteImageFixtureAsync("illustration-1.json", illustration1);
    }

    
    private static async Task WriteFixtureAsync(string fileName, JsonElement data)
    {
        var json=JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented=true
        });
        await File.WriteAllTextAsync(Path.Combine(FixturesDir, fileName), json);
    }

    private static async Task WriteImageFixtureAsync(string fileName, GeminiImageResult result)
    {
        var fixture=new
        {
            mimeType=result.MimeType,
            base64=Convert.ToBase64String(result.ImageBytes)
        };
        var json=JsonSerializer.Serialize(fixture, new JsonSerializerOptions
        {
            WriteIndented=true
        });
        await File.WriteAllTextAsync(Path.Combine(FixturesDir, fileName), json);
    }
}