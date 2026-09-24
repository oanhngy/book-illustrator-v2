using System.Text.Json;
namespace server.Gemini;

//decision ##14: 2 class độc lập cho request object
public class GeminiJsonRequest
{
    public required string Model {get; set;}
    public required string Prompt {get; set;}
    public string? PreviousInteractionId {get; set;} //null=thread mới
    public required JsonElement Schema {get; set;} //Json schema thô trả định dạng dữ liệu trả về
}

public class GeminiImageRequest
{
    public required string Model {get; set;}
    public required string Prompt {get; set;}
    public string? PreviousInteractionId {get; set;}
}

//decision ##10: IGeminiClient trả DTO riêng
public class GeminiJsonResult
{
    public required string InteractionId {get; set;}
    public required JsonElement Data {get; set;}
}
public class GeminiImageResult
{
    public required string InteractionId {get; set;}
    public required byte[] ImageBytes {get; set;}
    public required string MimeType {get; set;}
}