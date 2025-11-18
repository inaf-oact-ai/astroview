using AstroView.WebApp.App;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(DatasetId), nameof(Name), IsUnique = true)]
[Index(nameof(DatasetId), nameof(IsDefault))]
public class DisplayModeDbe
{
    public int Id { get; set; }
    public int DatasetId { get; set; }
    public string Name { get; set; } = null!;
    public string ParamsJson { get; set; } = null!;
    public string ImagesPath { get; set; } = null!; // directory containing png files or empty string for fits
    public string Extension { get; set; } = null!; // png, fits
    public string RenderJobId { get; set; } = null!;
    public HangfireJobStatus RenderJobStatus { get; set; }
    public string PixPlotParamsJson { get; set; } = null!;
    public string PixPlotJobId { get; set; } = null!;
    public HangfireJobStatus PixPlotJobStatus { get; set; }
    public string CaesarDatasetJobId { get; set; } = null!;
    public HangfireJobStatus CaesarDatasetJobStatus { get; set; }
    public DateTime? CaesarDatasetCreatedAt { get; set; }
    public bool IsDefault { get; set; }

    public virtual DatasetDbe Dataset { get; set; } = null!;

    public string GetImagePath(string imageName)
    {
        return Path.Combine(ImagesPath, imageName + "." + Extension).UnixFormat();
    }

    public string GetImageUrl(string imageName, AppConfig config)
    {
        var imagePath = GetImagePath(imageName);
        var relativePath = Path.GetRelativePath(config.Storage, imagePath).UnixFormat();
        return $"/static/storage/{relativePath}";
    }

    public bool IsFits()
    {
        return Name == "FITS";
    }

    public static DisplayModeDbe CreateFitsDisplayMode()
    {
        return new DisplayModeDbe
        {
            Name = "FITS",
            ImagesPath = "",
            Extension = "fits",
            RenderJobId = "",
            PixPlotJobId = "",
            CaesarDatasetJobId = "",
            ParamsJson = "",
            PixPlotParamsJson = "",
        };
    }
}
