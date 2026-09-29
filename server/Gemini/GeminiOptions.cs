namespace server.Gemini;

//cấu hình GeminiClient, bind + đọc KEY ở task 4 (next), file này only định nghĩa hình dạng
public class GeminiOptions
{
    public const string SectionName="Gemini";
    public string ApiKey {get; set;}=string.Empty; //đọc biến GEMINI_API_KEY, k ghi appsettings tránh lộ key
    public string TextModel {get; set;}="gemini-3.1-flash-lite";
    public string ImageModel {get; set;}="gemini-3.1-flash-lite-image";
    public string SystemInstruction {get; set;}= "There must be no text on the image, it should not look like a cover page. " + "It should be an full illustration with no borders, titles, nor description. " + "Unless asked otherwise, stay family-friendly with uplifting colors. " + "Each produced should be a simple image, no panels."; //gửi lại all request --> GeminiClient giữ ở đây thay vì để vào request DTO
    public int TimeoutSeconds {get; set;}=120;
}