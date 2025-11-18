using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;

namespace AstroView.WebApp.App;

public static class DbHelper
{
    public static string GetInsertLabelSql(int imageId, int labelId, double probability)
    {
        return $"INSERT IGNORE INTO ImageLabels (ImageId, LabelId, Value) " +
            $"VALUES ({imageId}, {labelId}, {probability.ToString(CultureInfo.InvariantCulture)});";
    }

    public static async Task AddImagesToDataset(
        List<string> paths, // normalized paths - forward slashes only
        int datasetId,
        string userId,
        AppDbContext db,
        Action<int>? processedCallback = null)
    {
        if (paths.Any(r => r.Contains('\\')))
            throw new Exception("Paths must use forward slashes");

        const int reportFrequency = 1500;

        var changes = new List<ChangeDbe>();
        var processed = 0;
        var extension = ".fits";

        foreach (var path in paths)
        {
            var isDirectory = Directory.Exists(path);
            if (isDirectory)
            {
                var files = Directory.EnumerateFiles(path, "*.fits", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var filePath = file.UnixFormat();

                    await AddImageToDataset(filePath, datasetId, db);

                    processed++;

                    if (processed % reportFrequency == 0)
                    {
                        await db.SaveChangesAsync();

                        if (processedCallback != null)
                            processedCallback(processed);
                    }
                }
            }
            else
            {
                if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                await AddImageToDataset(path, datasetId, db);

                processed++;

                if (processed % reportFrequency == 0)
                {
                    await db.SaveChangesAsync();

                    if (processedCallback != null)
                        processedCallback(processed);
                }
            }
        }

        var change = new ChangeDbe
        {
            DatasetId = datasetId,
            UserId = userId,
            Type = ChangeType.AddImage,
            Data = string.Join('\n', paths),
            Date = DateTime.UtcNow,
        };

        db.Changes.Add(change);

        if (processedCallback != null)
            processedCallback(processed);

        await db.SaveChangesAsync();
    }

    public static async Task AddImageToDataset(string path, int datasetId, AppDbContext db)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var imageExists = await db.Images.AnyAsync(r => r.Name == name && r.DatasetId == datasetId);
        if (imageExists)
            return;

        var image = new ImageDbe
        {
            DatasetId = datasetId,
            Name = name,
            NameReversed = name.ReverseStr(),
            Path = path,
        };
        db.Images.Add(image);
    }

    public static async Task<bool> HasActiveDatasetJobs(int datasetId, AppDbContext db)
    {
        var hasActiveJobs = await db.DatasetJobs
            .Where(r => r.DatasetId == datasetId)
            .Where(r => r.JobStatus == HangfireJobStatus.None || r.JobStatus == HangfireJobStatus.Running)
            .AnyAsync();

        return hasActiveJobs;
    }
}
