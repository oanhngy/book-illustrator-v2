using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Storage;
namespace server.Endpoints;

//DTO cho POST /api/projects (C2)
public record CreateProjectRequest(string Title, string BookText);

//DTO GET /api/projects (C3) k bookText/characters/...
public record ProjectSummary (
    Guid Id,
    string Title,
    DateTime CreatedAt,
    int CompletedSteps,
    int? RunningStep,
    int? FailedStep,
    string Status
);

//DTO GET /api/projects/{id} (C4) đầy đủ + bookText/style/characters/chapters/images
public record ProjectDetail (
    Guid Id,
    string Title,
    DateTime CreatedAt,
    int CompletedSteps,
    int? RunningStep,
    int? FailedStep,
    string Status,
    string BookText,
    string? Style,
    List<Character> Characters,
    List<Chapter> Chapters,
    List <ImageRef> Images,
    string? LastError,
    bool CanForceRetry
);

public static class ProjectEndpoints
{
    //NFR-08: text max 500k, ép ở FE làm ở Phase 9 (phần này là server)
    private const int MaxBookTextLength=500_000;

    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        //POST /api/projects
        app.MapPost("/api/projects", async (
            CreateProjectRequest request,
            [FromHeader(Name="X-User-Email")] string? userEmail,
            ProjectStore projectStore) =>
        {
            if(string.IsNullOrWhiteSpace(userEmail)) return Results.BadRequest(new ApiError("MISSING_USER", "X-User-Email header is required"));

            if(string.IsNullOrWhiteSpace(request.Title)) return Results.BadRequest(new ApiError("INVALID_INPUT", "Title is required"));

            if(string.IsNullOrWhiteSpace(request.BookText)) return Results.BadRequest(new ApiError("INVALID_INPUT", "Book text is required"));

            if(request.BookText.Length>MaxBookTextLength) return Results.BadRequest(new ApiError("BOOK_TEXT_TOO_LONG", $"Book text must not exceed {MaxBookTextLength} character"));

            var project=new Project
            {
                Id=Guid.NewGuid(),
                UserEmail=userEmail.Trim().ToLowerInvariant(),
                Title=request.Title,
                CreatedAt=DateTime.UtcNow,
            };

            //ghi project.json 1st, then bookText
            await projectStore.SaveAsync(project);
            await projectStore.SaveBookTextAsync(project.Id, request.BookText);

            return Results.Created($"/api/projects/{project.Id}", ToSummary(project));
        });
        
        //GET /api/projects
        app.MapGet("/api/projects", async (
            [FromHeader(Name="X-User-Email")] string? userEmail,
            ProjectStore projectStore) =>
        {
            if(string.IsNullOrWhiteSpace(userEmail)) return Results.BadRequest(new ApiError("MISSING_USER", "X-User-Email header is required"));

            var projects=await projectStore.ListByUserAsync(userEmail.Trim().ToLowerInvariant());

            //newest 1st
            var summaries=projects.OrderByDescending(p => p.CreatedAt).Select(ToSummary).ToList();
            return Results.Ok(summaries);   
        });

        //GET /api/projects/{id}
        app.MapGet("/api/projects/{id:guid}", async (
            Guid id,
            [FromHeader(Name="X-User-Email")] string? userEmail,
            ProjectStore projectStore) =>
        {
            if(string.IsNullOrWhiteSpace(userEmail)) return Results.BadRequest(new ApiError("MISSING_USER", "X-HEader_Email header is required"));

            var project=await projectStore.GetAsync(id);
            var normalizedEmail=userEmail.Trim().ToLowerInvariant();

            //404 for both "not exist" + "belong to otehr user"=CHẶN USER A ĐỌC PROJ USER B
            if(project is null || project.UserEmail!=normalizedEmail) return Results.NotFound(new ApiError("PROJECT_NOT_FOUND", "Project not found"));

            var bookText=await projectStore.GetBookTextAsync(id) ?? string.Empty;
            return Results.Ok(ToDetail(project, bookText));
        });
    }

    //C3: BE định nghĩa rõ enum trạng thái
    //hàm thuần (pure method), 1 chỗ duy nhất định nghĩa status
    private static string ComputeStatus(Project project)
    {
        if(project.FailedStep is not null) return "failed";
        if(project.RunningStep is not null) return "running";
        if(project.CompletedSteps>=5) return "completed";
        if(project.CompletedSteps==0) return "draft";
        return "in_progress";
    }

    private static ProjectSummary ToSummary(Project p) => new (
        p.Id,
        p.Title,
        p.CreatedAt,
        p.CompletedSteps,
        p.RunningStep,
        p.FailedStep,
        ComputeStatus(p)
    );

    private static ProjectDetail ToDetail(Project p, string bookText) => new (
        p.Id,
        p.Title,
        p.CreatedAt,
        p.CompletedSteps,
        p.RunningStep,
        p.FailedStep,
        ComputeStatus(p),
        bookText,
        p.Style,
        p.Characters,
        p.Chapters,
        p.Images,
        p.LastError,
        CanForceRetry:false
    );
}





