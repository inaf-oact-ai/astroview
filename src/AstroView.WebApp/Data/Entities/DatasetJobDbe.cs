using AstroView.WebApp.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(JobId))]
[Index(nameof(JobStatus))]
[Index(nameof(Date))]
public class DatasetJobDbe
{
    public int Id { get; set; }
    public int DatasetId { get; set; }
    public string UserId { get; set; } = null!;
    public DatasetJobType Type { get; set; }
    public string JobId { get; set; } = null!;
    public HangfireJobStatus JobStatus { get; set; }
    public DateTime Date { get; set; }

    public virtual DatasetDbe Dataset { get; set; } = null!;
    public virtual UserDbe User { get; set; } = null!;
}
