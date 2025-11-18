using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Utils;
using Microsoft.AspNetCore.Identity;

namespace AstroView.WebApp.Data.Entities;

public class UserDbe : IdentityUser
{
    public string DisplayName { get; set; }
    public int? LastDatasetId { get; set; }
    public string NotedFiles { get; set; } // Default value: ""
    public virtual DatasetDbe? Dataset { get; set; }
    public virtual List<DatasetDbe> Datasets { get; set; } = null!;
    public virtual List<CaesarJobDbe> CaesarJobs { get; set; } = null!;

    public UserDbe()
    {
        DisplayName = "";
        NotedFiles = "";
    }

    public void AddFileToNotes(string path)
    {
        // Move file to the top if it already exists
        if (NotedFiles.Contains(path))
        {
            RemoveFileFromNotes(path);
        }

        NotedFiles = NotedFiles.Insert(0, $"|{path}");
    }

    public void RemoveFileFromNotes(string path)
    {
        NotedFiles = NotedFiles.Replace(path, "").Replace("||", "|");
    }

    public void ClearNotes()
    {
        NotedFiles = "";
    }

    public List<NotedFile> GetNotedFiles(AppConfig config)
    {
        var files = new List<NotedFile>();
        
        foreach (var path in NotedFiles.Split("|", StringSplitOptions.RemoveEmptyEntries))
        {
            string url;
            if (path.Contains(config.Library))
            {
                var relativePath = Path.GetRelativePath(config.Library, path).UnixFormat();
                url = $"/static/library/{relativePath}";
            }
            else
            {
                var relativePath = Path.GetRelativePath(config.Storage, path).UnixFormat();
                url =  $"/static/storage/{relativePath}";
            }

            var file = new NotedFile();
            file.Name = Path.GetFileName(path);
            file.Path = path;
            file.Url = url;
            file.IsImage = Path.GetExtension(path).ToLower() == ".png";
            files.Add(file);
        }
     
        return files;
    }
}
