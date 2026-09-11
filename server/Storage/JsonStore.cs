using System.Text.Json;
namespace server.Storage;

// 1. System.Text.Json: serialize/deserialize, JsonSerializerOptions
// 2. Atomic write: ghi ra file tạm rồi File.Move(overwrite: true)
// 3. SemaphoreSlim + ConcurrentDictionary
public static class JsonStore
{
    //1. thiết lập serialize, deserialize
    private static readonly JsonSerializerOptions Options=new ()
    {
        PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
        WriteIndented=true,
    };

    //1. đọc file json tại path, null nếu chưa tồn tại, k throw
    public static async Task<T?> ReadAsync<T>(string path)
    {
        if(!File.Exists(path))
        {
            return default;
        }

        await using var stream=File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options);
    }

    //2. atomic write: ghi value xuống path, atomic (B5)
    public static async Task WriteAsync<T>(string path, T value)
    {
        var directory=Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath=path+".tmp";

        await using(var stream=File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, Options);
        }
        File.Move(tempPath,path, overwrite:true);
    }

}