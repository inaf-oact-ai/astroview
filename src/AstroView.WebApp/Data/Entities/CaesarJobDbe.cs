using AstroView.WebApp.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(StartedDate))]
[Index(nameof(AppName))]
public class CaesarJobDbe
{
    public int Id { get; set; }
    public int DatasetId { get; set; }
    public string UserId { get; set; } = null!;
    public string AppName { get; set; } = null!;
    public string RequestJson { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public string? ResultJobId { get; set; }
    public HangfireJobStatus ResultJobStatus { get; set; }
    public string? Error { get; set; }
    public string? CaesarJobId { get; set; }
    public string CaesarJobState { get; set; } = null!;
    public string CaesarJobStatus { get; set; } = null!;
    public DateTime StartedDate { get; set; }
    public DateTime? FinishedDate { get; set; }
    public int DisplayModeId { get; set; }

    public virtual DatasetDbe Dataset { get; set; } = null!;
    public virtual UserDbe User { get; set; } = null!;
    public virtual DisplayModeDbe DisplayMode { get; set; } = null!;
}
