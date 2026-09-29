namespace server.Gemini;

//all error related to Gemini ném loại này --> Phase 6 chỉ cần catch GeminiException để ghi lastError, dễ phân biệt lỗi Gemini vs lỗi chính mình
public class GeminiException : Exception
{
    public int? StatusCode {get;}
    public GeminiException(string message, int? statusCode=null, Exception? inner=null) : base(message, inner)
    {
        StatusCode=statusCode;
    }
}