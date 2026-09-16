using System.Collections.Concurrent;
using server.Models;
namespace server.Storage;

// 1. System.Text.Json: serialize/deserialize, JsonSerializerOptions
// 2. Atomic write: ghi ra file tạm rồi File.Move(overwrite: true)
// 3. SemaphoreSlim + ConcurrentDictionary

public class ProjectStore
{
    //data/projects/ gốc
    private readonly string _projectsDir;
    public ProjectStore(string projectsDir="data/projects")
    {
        _projectsDir=projectsDir;
    }

    //3. lock theo từng proj
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks=new();
    private string PathFor(Guid id) => Path.Combine(_projectsDir, $"{id}.json");
    
    //lấy semaphore của đúng proj, tạo nếu chưa có
    private static SemaphoreSlim LockFor(Guid id) => Locks.GetOrAdd(id, _ => new SemaphoreSlim(1,1));
    
    //đọc 1 proj theo id, null nếu k tồn tại
    public Task<Project?> GetAsync(Guid id) => JsonStore.ReadAsync<Project>(PathFor(id));

    //liệt kê all proj của 1 user, lọc the userEmail
    public async Task<List<Project>> ListByUserAsync(string userEmail)
    {
        if(!Directory.Exists(_projectsDir))
        {
            return [];
        }
        
        var result=new List<Project>();
        foreach(var file in Directory.EnumerateFiles(_projectsDir, "*.json"))
        {
            var project=await JsonStore.ReadAsync<Project>(file);
            if(project is not null && project.UserEmail==userEmail)
            {
                result.Add(project);
            }
        }
        return result;
    }

    //ghi mới 1 proj
    public Task SaveAsync(Project project) => JsonStore.WriteAsync(PathFor(project.Id), project);

    //read-modify-write atomic trong phạn vi lock, A5
    public async Task<bool> UpdateAsync(Guid id, Func<Project, bool> mutate)
    {
        var semaphore=LockFor(id);
        await semaphore.WaitAsync(); //k dùng async/await
        try
        {
            var project=await JsonStore.ReadAsync<Project>(PathFor(id));
            if(project is null)
            {
                return false; //proj k tồn tại, k có gì để sửa
            }

            var shouldSave=mutate(project);
            if(shouldSave)
            {
                await JsonStore.WriteAsync(PathFor(id), project);
            }
            return shouldSave;
        }
        finally
        {
            semaphore.Release(); //luôn nhả lock
        }
    }

    //TÁCH RIÊNG BOOK.TXT RA KHỎI PROJECT.JSON
    //method thuần, k I/O, only ghép chuỗi ra link data/projects/{id}.book.txt
    //private, only 2 method dưới xài
    private string BookTextPathFor(Guid id) => Path.Combine(_projectsDir, $"{id}.book.txt");

    //2 hàm= follow Dependency rule A3 (layer trên chỉ kêu làm gì, k biết cách làm, cái đó layer dưới lo) bằng cách che cách lưu trư thật khỏi tầng gọi nó(ProjectEndpoints), ProjectEndpoints chỉ cần biết lưu bookText cho proj này
    public async Task SaveBookTextAsync(Guid id, string bookText)
    {
        var path=BookTextPathFor(id);
        var directory=Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        //atomic write như JsonStore dù k phải JSON
        var tempPath=path+".tmp";
        await File.WriteAllTextAsync(tempPath, bookText);
        File.Move(tempPath, path, overwrite:true);
    }

    public async Task<string?> GetBookTextAsync(Guid id)
    {
        var path=BookTextPathFor(id);
        return File.Exists(path) ? await File.ReadAllTextAsync(path):null;
    }
}