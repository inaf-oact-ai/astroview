using AstroView.WebApp.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(Date))]
[Index(nameof(Type))]
public class ChangeDbe
{
    public int Id { get; set; }
    public int DatasetId { get; set; }
    public string UserId { get; set; } = null!;
    public ChangeType Type { get; set; }
    public string Data { get; set; } = null!;
    public DateTime Date { get; set; }

    public virtual DatasetDbe Dataset { get; set; } = null!;
    public virtual UserDbe User { get; set; } = null!;

    public ChangeDbe CreateCopy()
    {
        var change = new ChangeDbe
        {
            DatasetId = DatasetId,
            Type = Type,
            Data = Data,
            UserId = UserId,
            Date = Date,
        };

        return change;
    }
}
