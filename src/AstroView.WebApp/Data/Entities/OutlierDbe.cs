using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(CaesarJobId), nameof(ImageId), IsUnique = true)]
[Index(nameof(CaesarJobId), nameof(IsOutlier))]
[Index(nameof(CaesarJobId), nameof(Score))]
public class OutlierDbe
{
    public int Id { get; set; }
    public int CaesarJobId { get; set; }
    public int ImageId { get; set; }
    public bool IsOutlier { get; set; }
    public double Score { get; set; }

    public virtual CaesarJobDbe CaesarJob { get; set; } = null!;
    public virtual ImageDbe Image { get; set; } = null!;
}
