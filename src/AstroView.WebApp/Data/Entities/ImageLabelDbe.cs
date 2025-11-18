using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(ImageId), nameof(LabelId), IsUnique = true)]
public class ImageLabelDbe
{
    public int Id { get; set; }
    public int ImageId { get; set; }
    public int LabelId { get; set; }
    public double Value { get; set; }

    public virtual ImageDbe Image { get; set; } = null!;
    public virtual LabelDbe Label { get; set; } = null!;

    public ImageLabelDbe CreateCopy()
    {
        return new ImageLabelDbe
        {
            ImageId = ImageId,
            LabelId = LabelId,
            Value = Value,
        };
    }
}
