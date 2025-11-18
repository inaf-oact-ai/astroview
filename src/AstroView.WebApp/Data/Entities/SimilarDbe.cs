using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(CaesarJobId), nameof(ImageId), IsUnique = true)]
[Index(nameof(CaesarJobId), nameof(HighestScore))]
public class SimilarDbe
{
    public int Id { get; set; }
    public int CaesarJobId { get; set; }
    public int ImageId { get; set; }
    public string Json { get; set; } = null!;
    public double HighestScore { get; set; }

    public virtual CaesarJobDbe CaesarJob { get; set; } = null!;
    public virtual ImageDbe Image { get; set; } = null!;
}
