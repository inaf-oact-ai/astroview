using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Models.Filters;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Web.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class OutliersPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [Parameter]
    public int CaesarJobId { get; set; }
    public int _caesarJobId { get; set; }

    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private readonly OutliersPageVm vm;

    public DisplayModeDbe DisplayMode { get; set; } = null!;

    public OutliersPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new OutliersPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _caesarJobId = CaesarJobId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetName = await db.Datasets.Where(r => r.Id == DatasetId).Select(r => r.Name).FirstAsync();
            vm.Labels = await db.Labels.OrderBy(r => r.Name).ToListAsync();
            vm.LabelsById = vm.Labels.ToDictionary(r => r.Id, r => r);

            vm.DisplayModes = await db.DisplayModes.Where(r => r.DatasetId == DatasetId).ToListAsync();

            var job = await db.CaesarJobs.FirstAsync(r => r.Id == CaesarJobId);
            var jobDisplayMode = vm.DisplayModes.FirstOrDefault(r => r.Id == job.DisplayModeId);

            vm.DisplayModeName = jobDisplayMode?.Name ?? "display mode was removed";

            if (jobDisplayMode == null || jobDisplayMode.IsFits())
            {
                DisplayMode = vm.DisplayModes.First(r => r.IsDefault);
            }
            else
            {
                DisplayMode = jobDisplayMode;
            }

            await LoadOutliers(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (DatasetId == _datasetId
            && CaesarJobId == _caesarJobId
            && PageNumber == _pageNumber
            && PageSize == _pageSize)
        {
            return;
        }

        _datasetId = DatasetId;
        _caesarJobId = CaesarJobId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadOutliers(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (firstRender)
            {
                await js.InvokeVoidAsync("initLabelsDropdown", vm.Filter.LabelIds);
            }
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task GoToPage()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var uri = nav.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                { "PageNumber", PageNumber },
                { "PageSize", PageSize },
            });
            nav.NavigateTo(uri);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadOutliers(AppDbContext db)
    {
        var query = db.Outliers
            .AsNoTracking()
            .Where(r => r.CaesarJobId == CaesarJobId);

        query = vm.Filter.Apply(query);

        if (vm.OutlierStatus == true)
        {
            query = query.Where(r => r.IsOutlier);
        }
        else if (vm.OutlierStatus == false)
        {
            query = query.Where(r => r.IsOutlier == false);
        }

        vm.ImagesCount = await query.CountAsync();
        vm.Paging.Calculate(PageNumber, PageSize, vm.ImagesCount);

        if (vm.SortDesc)
        {
            query = query.OrderByDescending(r => r.Score);
        }
        else
        {
            query = query.OrderBy(r => r.Score);
        }

        var ids = await query
            .Select(r => r.Id)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();

        vm.Items = await db.Outliers
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .OrderByDescending(r => r.Score)
            .Select(r => new ImageVm
            {
                ImageId = r.Image.Id,
                ImageName = r.Image.Name,
                Labels = r.Image.Labels,
                IsOutlier = r.IsOutlier,
                OutlierScore = r.Score,
            })
            .ToListAsync();

        foreach (var item in vm.Items)
        {
            item.ImageUrl = DisplayMode.GetImageUrl(item.ImageName, config.Value);
        }
    }

    protected async Task SetDisplayMode(int displayModeId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            DisplayMode = vm.DisplayModes.First(r => r.Id == displayModeId);

            await LoadOutliers(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowFilterModal()
    {
        try
        {
            await js.InvokeVoidAsync("showFilterModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ApplyFilter()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var labelIds = await js.InvokeAsync<List<string>>("getLabelsDropdownValues");
            vm.Filter.LabelIds = labelIds.Select(int.Parse).ToList();

            PageNumber = 1;

            var uri = nav.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                { "PageNumber", PageNumber },
                { "PageSize", PageSize },
            });
            nav.NavigateTo(uri);

            await js.InvokeVoidAsync("hideFilterModal");

            await LoadOutliers(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ClearFilter()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.Filter.Clear();

            PageNumber = 1;

            var uri = nav.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                { "PageNumber", PageNumber },
                { "PageSize", PageSize },
            });
            nav.NavigateTo(uri);

            await js.InvokeVoidAsync("hideFilterModal");

            await LoadOutliers(db);
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

            await LoadOutliers(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task SetOutlierStatus(bool? value)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.OutlierStatus = value;

            await LoadOutliers(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task DisplayOutliers()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadOutliers(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class OutliersPageVm
    {
        public string DatasetName { get; set; }
        public string DisplayModeName { get; set; }
        public List<ImageVm> Items { get; set; }
        public Paging Paging { get; set; }
        public OutliersImageFilter Filter { get; set; }
        public Dictionary<int, LabelDbe> LabelsById { get; set; }
        public List<LabelDbe> Labels { get; set; }
        public List<DisplayModeDbe> DisplayModes { get; set; }

        public int ImagesCount { get; set; }

        public bool? OutlierStatus { get; set; }
        public bool SortDesc { get; set; }

        public OutliersPageVm()
        {
            DatasetName = "";
            DisplayModeName = "";
            Items = new List<ImageVm>();
            Paging = new Paging();
            Filter = new OutliersImageFilter();
            LabelsById = new Dictionary<int, LabelDbe>();
            Labels = new List<LabelDbe>();
            DisplayModes = new List<DisplayModeDbe>();

            OutlierStatus = null;
            SortDesc = true;
        }
    }

    protected class ImageVm
    {
        public int ImageId { get; set; }
        public string ImageName { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public List<ImageLabelDbe> Labels { get; set; } = null!;
        public bool IsOutlier { get; set; }
        public double OutlierScore { get; set; }
    }
}
