using server.Models;
using server.Storage;
namespace server.Tests.Storage;

public class ProjectStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectStore _store;

    //chạy BEFORE
    public ProjectStoreTests()
    {
        //folder riêng cho mỗi lần chạy test
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-test-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
    }

    //chạy AFTER
    public void Dispose()
    {
        if(Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive:true);
        }
    }

    [Fact]
    public async Task UpdateAsync_FiftyParallelCalls_NoUpdateLost()
    {
        //Arrange
        //project khởi điểm completedStep=0
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
        };
        await _store.SaveAsync(project);

        //Act
        //create 50 task chưa await, Select only build list lời gọi UpdateAsync
        //Task.WhenAll mới thực sự cho 50 task chạy đồng thời
        var tasks=Enumerable.Range(0,50).Select(_ =>
            //việc của hàm delegate/mutate
            _store.UpdateAsync(project.Id, p =>
            {
                //if lock sai -->2 luồng cùng đọc completedSteps=N, cùng cộng ra N+1 --> ghi đè --> total <50
                p.CompletedSteps++;
                return true;
            }));
        await Task.WhenAll(tasks);

        //Assert: đọc lại từ đĩa (k xài biến project cũ còn trong bộ nhớ), phải đúng 50
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(50, reloaded!.CompletedSteps);
        
    }
}