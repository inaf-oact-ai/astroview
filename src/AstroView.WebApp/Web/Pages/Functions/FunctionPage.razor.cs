using AstroView.WebApp.App;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace AstroView.WebApp.Web.Pages.Functions;

public partial class FunctionPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [Parameter]
    public int CaesarJobId { get; set; }
    public int _caesarJobId { get; set; }

    private FunctionPageVm vm;

    public FunctionPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new FunctionPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _caesarJobId = CaesarJobId;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetName = await db.Datasets
                .AsNoTracking()
                .Where(r => r.Id == DatasetId)
                .Select(r => r.Name)
                .FirstAsync();

            await LoadFunction(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (DatasetId == _datasetId
            && CaesarJobId == _caesarJobId)
        {
            return;
        }

        _datasetId = DatasetId;
        _caesarJobId = CaesarJobId;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadFunction(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadFunction(AppDbContext db)
    {
        vm.Job = await db.CaesarJobs
            .AsNoTracking()
            .Include(r => r.User)
            .FirstAsync(r => r.Id == CaesarJobId);

        var jobDirectory = config.Value.GetCaesarJobOutputPath(vm.Job.DatasetId, vm.Job.Id);
        vm.JobOutputPath = Path.GetRelativePath(config.Value.Storage, jobDirectory).UnixFormat();
        vm.CaesarDatasetDirectoryUrl = config.Value.GetDisplayModePaths(vm.Job.DatasetId, vm.Job.DisplayModeId).RootDirectoryLink;
    }

    protected class FunctionPageVm
    {
        public string DatasetName { get; set; }
        public CaesarJobDbe Job { get; set; }
        public string JobOutputPath { get; set; }
        public string CaesarDatasetDirectoryUrl { get; set; }

        public FunctionPageVm()
        {
            DatasetName = "";
            Job = new CaesarJobDbe
            {
                User = new UserDbe
                {
                    UserName = "",
                }
            };
            JobOutputPath = "";
            CaesarDatasetDirectoryUrl = "";
        }
    }
}
