using AstroView.WebApp.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(Type))]
[Index(nameof(Date))]
public class ExportDbe
{
    public int Id { get; set; }
    public int DatasetId { get; set; }
    public string UserId { get; set; } = null!;
    public string? JobId { get; set; } = null!;
    public HangfireJobStatus JobStatus { get; set; }
    public ExportType Type { get; set; }
    public string Details { get; set; } = null!;
    public string File { get; set; } = null!;
    public DateTime Date { get; set; }

    public virtual DatasetDbe Dataset { get; set; } = null!;
    public virtual UserDbe User { get; set; } = null!;
}
