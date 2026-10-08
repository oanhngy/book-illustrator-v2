using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Pipeline;
using server.Storage;
namespace server.Endpoints;

//bosy của POST.../steps/{steps}/run; chỉ B1 xài
public record RunStepRequest(string? Style);

public static class PipelineEndpoints
{
    //extension method
    public static void MapPipelineEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/projects/{id:guid}/steps/{step:int}/run", RunStepAsync);
    }

    public static async Task<IResult> RunStepAsync (
        Guid id,
        int step,
        [FromBody] RunStepRequest? request,
        [FromHeader (Name="X-User-Email")] string? userEmail,
        ProjectStore projectStore,
        PipelineService pipeline,
        Channel<StepJob> channel)
    {
        //400: lỗi của request, check trước khi đụng disk
        if(string.IsNullOrWhiteSpace(userEmail)) return Results.BadRequest(new ApiError("MISSING_USER", "X-User-Email header is required"));
        if(step<1 || step>5) return Results.BadRequest(new ApiError("INVALID_STEP", "Step must be between 1 and 5"));

        //404 cho both "k tồn tại" và "của user khác", check quyền ngoài lock vì chủ proj kbh đổi
        var project=await projectStore.GetAsync(id);
        var normalizedEmail=userEmail.Trim().ToLowerInvariant();
        if(project is null || project.UserEmail != normalizedEmail) return Results.NotFound(new ApiError("PROJECT_NOT_FOUND", "Project not found"));

        //B1
        var style=step==1 && !string.IsNullOrWhiteSpace(request?.Style) ? request.Style.Trim() : null;

        //claim: check thứ tự. trả trạng thái + vé
        var claim=await pipeline.ClaimStepAsync(id, step, style);

        //map --> http + error code (C5)
        switch(claim.Status)
        {
            case ClaimStatus.Claimed:
                await channel.Writer.WriteAsync(new StepJob(id, step, claim.Ticket!.Value)); //job mang đúng vé claim vừa trả
                return Results.Accepted(); //202

            case ClaimStatus.AlreadyRunning:
                return Results.Conflict(new ApiError("STEP_ALREADY_RUNNING", "Another step of this project is still running")); //409, chống double-click/2 tab
            
            case ClaimStatus.WrongOrder:
                return Results.Conflict(new ApiError("STEP_NOT_AVAILABLE", $"Step {step} is not the next step of this project"));
            
            default:
                return Results.NotFound(new ApiError("PROJECT_NOT_FOUND", "Project not found"));
        }
    }
}