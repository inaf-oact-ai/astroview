using AstroView.WebApp.App;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using static AstroView.WebApp.Web.Pages.Datasets.SimilarsPage;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class IndividualSimilarsPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [Parameter]
    public int CaesarJobId { get; set; }
    public int _caesarJobId { get; set; }

    private readonly IndividualSimilarsPageVm vm;

    public IndividualSimilarsPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new IndividualSimilarsPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _caesarJobId = CaesarJobId;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetName = await db.Datasets.Where(r => r.Id == DatasetId).Select(r => r.Name).FirstAsync();

            var job = await db.CaesarJobs.FirstAsync(r => r.Id == CaesarJobId);

            var filePath = job.RequestJson.Split(" | ")[0];
            if (filePath.Contains(config.Value.Storage))
            {
                var relativePath = Path.GetRelativePath(config.Value.Storage, filePath).UnixFormat();
                vm.FileUrl = $"/static/storage/{relativePath}";
            }
            else
            {
                var relativePath = Path.GetRelativePath(config.Value.Library, filePath).UnixFormat();
                vm.FileUrl = $"/static/library/{relativePath}";
            }

            await LoadSimilars(db);
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

            await LoadSimilars(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadSimilars(AppDbContext db)
    {
        var job = await db.CaesarJobs.FirstAsync(r => r.Id == CaesarJobId);
        var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == job.DisplayModeId);
        if (displayMode.IsFits())
        {
            var defaultDisplayMode = await db.DisplayModes.FirstOrDefaultAsync(r => r.DatasetId == job.DatasetId && r.IsDefault);
            if (defaultDisplayMode != null)
            {
                displayMode = defaultDisplayMode;
            }
        }

        var isim = await db.IndividualSimilars.FirstAsync(r => r.CaesarJobId == CaesarJobId);
        var similars = JsonConvert.DeserializeObject<List<SimilarImage>>(isim.Json)!;
        if (vm.SortDesc)
        {
            similars = similars
                .Where(r => r.Score >= vm.MinScore && r.Score <= vm.MaxScore)
                .OrderByDescending(r => r.Score)
                .ToList();
        }
        else
        {
            similars = similars
                .Where(r => r.Score >= vm.MinScore && r.Score <= vm.MaxScore)
                .OrderBy(r => r.Score)
                .ToList();
        }

        vm.Children.Clear();
        foreach (var similar in similars)
        {
            var child = new ChildItemVm();
            child.ImageId = similar.ImageId;
            child.ImageName = similar.ImageName;
            child.Score = similar.Score;
            child.ImageUrl = displayMode.GetImageUrl(similar.ImageName, config.Value);
            vm.Children.Add(child);
        }
    }

    protected async Task DisplaySimilars()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadSimilars(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task SetSortDesc(bool value)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.SortDesc = value;

            await LoadSimilars(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class IndividualSimilarsPageVm
    {
        public string DatasetName { get; set; }
        public List<ChildItemVm> Children { get; set; }
        public string FileUrl { get; set; }

        public double MinScore { get; set; }
        public double MaxScore { get; set; }
        public bool SortDesc { get; set; }

        public IndividualSimilarsPageVm()
        {
            DatasetName = "";
            Children = new List<ChildItemVm>();
            FileUrl = "";

            MinScore = 0;
            MaxScore = 100;
            SortDesc = true;
        }
    }
}
