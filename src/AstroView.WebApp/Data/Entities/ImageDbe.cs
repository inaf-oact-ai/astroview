using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(DatasetId), nameof(HasFeatures))]
[Index(nameof(DatasetId), nameof(Name), IsUnique = true)]
[Index(nameof(DatasetId), nameof(NameReversed))]
[Index(nameof(Telescope))]
[Index(nameof(Survey))]
[Index(nameof(Project))]
[Index(nameof(Ra))]
[Index(nameof(Dec))]
[Index(nameof(L))]
[Index(nameof(B))]
public class ImageDbe
{
    public int Id { get; set; }
    public int DatasetId { get; set; }
    public string? Features { get; set; }
    public bool HasFeatures { get; set; }

    public string Name { get; set; } = null!; // Filename without extension
    public string NameReversed { get; set; } = null!; // Filename without extension, reversed
    public string Path { get; set; } = null!; // Path to FITS file

    // Metadata
    public string? Telescope { get; set; } // The name of the telescope that observed this image. It generally refers to the first channel/band in the case of multiple images (multi-band).
    public string? Survey { get; set; } // Name of the astronomical survey that observed this image. In fact, there can be multiple surveys for a given telescope.
    public string? Project { get; set; } // Name of the project within the survey that observed this image. There can be multiple observational projects within a given survey.
    public int? Nx { get; set; } // Width of the image in pixels
    public int? Ny { get; set; } // Height of the image in pixels
    public double? Dx { get; set; } // The size of the pixel along x in arcseconds.
    public double? Dy { get; set; } // The size of the y-long pixel in arcseconds.
    public double? Ra { get; set; } // Right-Ascension of the center of the image in equatorial coordinates FK5
    public double? Dec { get; set; } // Declination of the center of the image in equatorial coordinates FK5
    public double? L { get; set; } // Galactic longitude of the center of the image in galactic coordinates
    public double? B { get; set; } // Galactic latitude of the center of the image in galactic coordinates
    public int? Nsources { get; set; } // Number of compact sources extracted with an automatic algorithm (very approximate).
    public double? Bkg { get; set; } // Background level of the original image (FITS) in units of Jy/beam
    public double? Rms { get; set; } // Standard deviation (noise RMS) of the background of the original image (FITS) in units of Jy/beam

    public virtual DatasetDbe Dataset { get; set; } = null!;
    public virtual List<ImageLabelDbe> Labels { get; set; } = null!;
    public virtual List<OutlierDbe> Outliers { get; set; } = null!;
    public virtual List<SimilarDbe> Similars { get; set; } = null!;
    public virtual List<ClusterItemDbe> ClusterItems { get; set; } = null!;
    public virtual List<PredictionDbe> Predictions { get; set; } = null!;

    public ImageDbe CreateCopy()
    {
        var image = new ImageDbe
        {
            DatasetId = DatasetId,
            Features = Features,
            HasFeatures = HasFeatures,

            Name = Name,
            NameReversed = NameReversed,
            Path = Path,

            Telescope = Telescope,
            Survey = Survey,
            Project = Project,
            Nx = Nx,
            Ny = Ny,
            Dx = Dx,
            Dy = Dy,
            Ra = Ra,
            Dec = Dec,
            L = L,
            B = B,
            Nsources = Nsources,
            Bkg = Bkg,
            Rms = Rms,
        };

        image.Labels = Labels.Select(r => r.CreateCopy()).ToList();
        return image;
    }
}
