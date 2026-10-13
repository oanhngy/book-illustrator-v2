using server.Models;
using server.Storage;
namespace server.Endpoints;

public static class ImageEndpoints
{
    public static void MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{projectId:guid}/images/{imageId:guid}", GetImageAsync);
    }

    //k check user (URL chứa 2 guid k đoán đc)
    public static async Task<IResult> GetImageAsync(Guid projectId, Guid imageId, ProjectStore projectStore, ImageStore imageStore)
    {
        var notFound=Results.NotFound(new ApiError("IMAGE_NOT_FOUND", "Image not found"));
        var project=await projectStore.GetAsync(projectId);
        if(project is null) return notFound;

        //tìm ảnh torng images[] của đúng proj
        var image=project.Images.FirstOrDefault(i => i.Id==imageId.ToString());
        if(image is null) return notFound;

        //chống path traversal: link từ metadata do server tự ghi, k ghép chuỗi từ request
        var fullPath=imageStore.FullPathOf(image.Path);

        if(!File.Exists(fullPath)) return notFound; //mất file --> 404

        var bytes=await File.ReadAllBytesAsync(fullPath);
        return Results.File(bytes, image.MimeType);
    }
}