using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[PrimaryKey(nameof(UserId), nameof(DatasetId))]
[Index(nameof(IsFavorite))]
public class DatasetOptionDbe
{
    public string UserId { get; set; } = null!;
    public int DatasetId { get; set; }
    public bool IsFavorite { get; set; }

    public virtual UserDbe User { get; set; } = null!;
    public virtual DatasetDbe Dataset { get; set; } = null!;
}
