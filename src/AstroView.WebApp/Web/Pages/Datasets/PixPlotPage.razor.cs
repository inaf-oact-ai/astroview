using AstroView.WebApp.App;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Data;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class PixPlotPage
{
    [Parameter]
    public int DatasetId { get; set; }

    [Parameter]
    public int DisplayModeId { get; set; }

    [SupplyParameterFromQuery]
    public int? CustomLayoutFromJobId { get; set; }

    [SupplyParameterFromQuery]
    public int? ClustersFromJobId { get; set; }

    private readonly PixPlotPageVm vm;

    public PixPlotPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider auth,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, auth, nav, dbf)
    {
        vm = new PixPlotPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        using var db = await dbf.CreateDbContextAsync();

        vm.Name = await db.Datasets.Where(r => r.Id == DatasetId).Select(r => r.Name).FirstAsync();

        if (CustomLayoutFromJobId != null)
        {
            var job = await db.CaesarJobs.FirstAsync(r => r.Id == CustomLayoutFromJobId);
            vm.CustomLayoutPath = "/static/storage/" + Path.Combine("datasets", job.DatasetId.ToString(),
                "caesar", "jobs", $"{job.Id}", "results", "layout.json").UnixFormat();
        }

        if (ClustersFromJobId != null)
        {
            var job = await db.CaesarJobs.FirstAsync(r => r.Id == ClustersFromJobId);
            vm.CustomHotspotsPath = "/static/storage/" + Path.Combine("datasets", job.DatasetId.ToString(),
                "caesar", "jobs", $"{job.Id}", "results", "hotspots.json").UnixFormat();
        }
    }

    private class PixPlotPageVm
    {
        public string Name { get; set; }
        public string CustomLayoutPath { get; set; }
        public string CustomHotspotsPath { get; set; }

        public PixPlotPageVm()
        {
            Name = "";
            CustomLayoutPath = "";
            CustomHotspotsPath = "";
        }
    }
}
