namespace server.Storage;

//lưu + định vị file ảnh trên disk
public class ImageStore
{
    //folder gốc chứa all ảnh
    private readonly string _imagesDir;
    public ImageStore(string imagesDir="data/images")
    {
        _imagesDir=imagesDir;
    }

    //ghi 1 hình xuống đĩa, trả link tương đối để lưu ImageRef.Path
    public async Task<string> SaveAsync(Guid projectId, string imageId, byte[] bytes, string mimeType)
    {
        var relativePath=$"{projectId}/{imageId}{ExtensionFor(mimeType)}";
        var fullPath=FullPathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        //atomic write như JsonStore
        var tempPath=fullPath+".tmp";
        await File.WriteAllBytesAsync(tempPath, bytes);
        File.Move(tempPath, fullPath, overwrite:true);
        return relativePath;
    }

    //đổi link tương đối (từ project.json) thành link thật
    public string FullPathOf(string relativePath) => Path.Combine(_imagesDir, relativePath);

    //đuôi file theo mime type
    private static string ExtensionFor(string mimeType) => mimeType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".bin"
    };
}