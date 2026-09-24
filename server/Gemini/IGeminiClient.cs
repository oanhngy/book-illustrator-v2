namespace server.Gemini;
public interface IGeminiClient
{
    //upload sách lên File API --> trả về book.uri --> lưu vô Project.BookUri
    Task<string> UploadBookAsync(string bookText);

    Task<GeminiJsonResult> GenerateJsonAsync(GeminiJsonRequest request);

    Task<GeminiImageResult> GenerateImageAsync(GeminiImageRequest request);
}