namespace server.Models;

public class Project
{
    public Guid Id { get; set; }
    public string UserEmail { get; set; } = string.Empty; //B2
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    //tiến độ: completedSteps + runningStep
    public int CompletedSteps { get; set; }
    public int? RunningStep { get; set; }
    public DateTime? RunningSince { get; set; } //UTC lúc claim; kiêm vé claim (DECISIONS ##19) --> chỉ claim được ghi
    public int? FailedStep { get; set; }
    public string? LastError { get; set; }

    //con trỏ ngữ cảnh, 3 field phẳng (DECISIONS ##13)
    public string? BookUri { get; set; } //kết quả UploadBookAsync; chưa dùng để tham chiếu vì lượt đầu inline sách (##17)
    public string? TextInteractionId { get; set; } //id lượt text gần nhất (B1, B2, B4) --> PreviousInteractionId của lượt text kế
    public string? ImageInteractionId { get; set; } //id lượt ảnh gần nhất (B3, B5) --> PreviousInteractionId của lượt ảnh kế

    public string? RequestedStyle { get; set; } //input: style user nhập ở B1, ghi lúc claim, null=để AI tự chọn (DECISIONS ##18)
    public string? Style { get; set; } //output: kết quả B1, chỉ có sau khi B1 xong
    public List<Character> Characters { get; set; } = [];
    public List<Chapter> Chapters { get; set; } = [];
    public List<ImageRef> Images { get; set; } = [];
}

public class Character
{
    public string Name { get; set; } = string.Empty;
    public string ImagePrompt { get; set; } = string.Empty;
}

public class Chapter
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ImagePrompt { get; set; } = string.Empty;
}

public class ImageRef
{
    public string Id {get; set;}=string.Empty;
    public int Step {get; set;} //bước nào sinh ảnh này, 3=portrait, 5=illustration
    public int Index {get; set;} //thứ tự character/chapter --> lọc resume theo item
    public string Path {get; set;}=string.Empty;
    public string MimeType {get; set;}=string.Empty;
    public DateTime CreatedAt {get; set;} //key JSON "createdAt" khớp B3
}