using AstroView.WebApp.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(Name))]
public class DatasetDbe
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DatasetShareType ShareType { get; set; }
    public bool IsLocked { get; set; }
    public bool IsRemoved { get; set; }
    public DateTime ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }

    public virtual UserDbe User { get; set; } = null!;
    public virtual List<ImageDbe> Images { get; set; } = null!;
    public virtual List<ChangeDbe> Changes { get; set; } = null!;
    public virtual List<DatasetOptionDbe> Options { get; set; } = null!;
    public virtual List<UserDbe> Users { get; set; } = null!;
    public virtual List<CaesarJobDbe> CaesarJobs { get; set; } = null!;
    public virtual List<DisplayModeDbe> DisplayModes { get; set; } = null!;
    public virtual List<ExportDbe> Exports { get; set; } = null!;
    public virtual List<DatasetJobDbe> Jobs { get; set; } = null!;
}
