using AstroView.WebApp.Data.Enums;
using Microsoft.AspNetCore.Components;

namespace AstroView.WebApp.Web.Components;

public partial class HangfireJobStatusLabel
{
    [Parameter]
    public HangfireJobStatus Status { get; set; }
}
