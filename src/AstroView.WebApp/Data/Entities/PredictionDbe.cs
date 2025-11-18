using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(CaesarJobId), nameof(Probability))]
public class PredictionDbe
{
    public int Id { get; set; }
    public int CaesarJobId { get; set; }
    public int ImageId { get; set; }
    public int LabelId { get; set; }
    public double Probability { get; set; }

    public virtual CaesarJobDbe CaesarJob { get; set; } = null!;
    public virtual ImageDbe Image { get; set; } = null!;
    public virtual LabelDbe Label { get; set; } = null!;
}