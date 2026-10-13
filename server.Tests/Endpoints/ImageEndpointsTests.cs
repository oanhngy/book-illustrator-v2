using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using server.Endpoints;
using server.Models;
using server.Storage;
namespace server.Tests.Endpoints;

public class ImageEndpointsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectStore _store;
    private readonly ImageStore _images;

    //BEFORE
    public ImageEndpointsTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-test-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
        _images=new ImageStore(Path.Combine(_tempDir, "images"));
    }

    //FTER
    public void Dispose()
    {
        if(Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive:true);
        }
    }

    //helper: 1 proj có 1 hình, file ảnh thật trên disk (ghi qua ImageStore như pipeline); trả project + id hình; byte + mimeType test tự chọn
    private async Task<(Project Project, Guid ImageId)> SeedProjectWithImageAsync(byte[] bytes, string mimeType)
    {
        var projectId=Guid.NewGuid();
        var imageId=Guid.NewGuid();
        var path=await _images.SaveAsync(projectId, imageId.ToString(), bytes, mimeType);
        var project=new Project
        {
            Id=projectId,
            UserEmail="owner@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
            Images=[ new ImageRef { 
                Id=imageId.ToString(), 
                Step=3, 
                Index=0, 
                Path=path, 
                MimeType=mimeType, 
                CreatedAt=DateTime.UtcNow } ],       
        };
        await _store.SaveAsync(project);
        return (project, imageId);
    }

    //helper: status code kết quả. k co1status riêng=200
    private static int StatusOf(IResult result) => (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;

    //LUỒNG CHÍNH: trả đúng byte + Content-Type
    [Fact]
    public async Task GetImage_ExistingImage_ReturnsFileBytesAndContentType()
    {
        //arrange
        byte[] bytes=[10,20,30,40];
        var(project, imageId)=await SeedProjectWithImageAsync(bytes, "image/png"); //image/png để handler viết cứng ../jpeg bị lộ

        //act
        var result=await ImageEndpoints.GetImageAsync(project.Id, imageId, _store, _images);

        //assert
        var file=Assert.IsType<FileContentHttpResult>(result);
        Assert.Equal(bytes, file.FileContents.ToArray());
        Assert.Equal("image/png", file.ContentType);
    }

    //ảnh chỉ đc lấy qua proj của nó; id/proj lạ all return 404
    [Fact]
    public async Task GetImage_WrongProjectOrUnknownImage_Returns404()
    {
        //arrange: 2 proj, mỗi cái 1 hình
        var(projectA, imageOfA)=await SeedProjectWithImageAsync([1], "image/png");
        var(projectB, imageOfB)=await SeedProjectWithImageAsync([2], "image/png");

        //act: lấy hình B qua địa chỉ của A
        var crossProject=await ImageEndpoints.GetImageAsync(projectA.Id, imageOfB, _store, _images);
        var unknownImage=await ImageEndpoints.GetImageAsync(projectA.Id, Guid.NewGuid(), _store, _images);
        var unknownProject=await ImageEndpoints.GetImageAsync(Guid.NewGuid(), imageOfA, _store, _images);

        //assert: all 404
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(crossProject));
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(unknownImage));
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(unknownProject));
    }

    //metadata còn nhưng file mất -->404
    [Fact]
    public async Task GetImage_FileMissingOnDisk_Returns404()
    {
        //arrange: có ảnh r xóa trên disk, giữ nguyên project.json
        var(project, imageId)=await SeedProjectWithImageAsync([1,2,3], "image/jpeg");
        File.Delete(_images.FullPathOf(project.Images[0].Path));

        //act
        var result=await ImageEndpoints.GetImageAsync(project.Id, imageId, _store, _images);

        //assert
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
    }

    //##24 + C4: response chi tiết trả url hình k lộ link trên disk
    [Fact]
    public async Task ToDetail_Images_ExposeUrlNotDiskPath()
    {
        //arrange
        var(project, imageId)=await SeedProjectWithImageAsync([1], "image/jpeg");

        //act
        var detail=ProjectEndpoints.ToDetail(project, "book text");
        var json=JsonSerializer.Serialize(detail, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        //assert: đúng route, k có key "path", k đường dẫn file
        var image=Assert.Single(detail.Images);
        Assert.Equal($"/api/projects/{project.Id}/images/{imageId}", image.Url);
        Assert.DoesNotContain("\"path\"", json);
        Assert.DoesNotContain(project.Images[0].Path, json);
    }
}