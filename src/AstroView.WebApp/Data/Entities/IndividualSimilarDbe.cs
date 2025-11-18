using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(CaesarJobId), nameof(FilePath), IsUnique = true)]
public class IndividualSimilarDbe
{
    public int Id { get; set; }
    public int CaesarJobId { get; set; }
    public string FilePath { get; set; } = null!;
    public string Json { get; set; } = null!;

    public virtual CaesarJobDbe CaesarJob { get; set; } = null!;
}
