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
    public DateTime? RunningSince { get; set; }
    public int? FailedStep { get; set; }
    public string? LastError { get; set; }

    public string? ContextRef { get; set; } //phase 5,6

    public string? Style { get; set; }
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
    public DateTime CreateAt {get; set;}
}