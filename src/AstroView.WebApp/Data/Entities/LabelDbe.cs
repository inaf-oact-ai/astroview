using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(Name), IsUnique = true)]
public class LabelDbe
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;

    public List<ImageLabelDbe> ImageLabels { get; set; } = null!;
}
