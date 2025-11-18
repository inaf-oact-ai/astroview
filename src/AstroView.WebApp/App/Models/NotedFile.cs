namespace AstroView.WebApp.App.Models;

public class NotedFile
{
    public string Name { get; set; } = null!;
    public string Path { get; set; } = null!;
    public string Url { get; set; } = null!;
    public bool IsImage { get; set; }
}
