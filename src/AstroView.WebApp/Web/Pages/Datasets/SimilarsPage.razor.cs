using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Models.Filters;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Newtonsoft.Json;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class SimilarsPage
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

    private readonly SimilarsPageVm vm;

    public DisplayModeDbe DisplayMode { get; set; } = null!;

    public SimilarsPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new SimilarsPageVm();
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

            await LoadSimilars(db);
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

    private async Task LoadSimilars(AppDbContext db)
    {
        var query = db.Similars
            .AsNoTracking()
            .Where(r => r.CaesarJobId == CaesarJobId);

        query = vm.Filter.Apply(query);

        var count = await query.CountAsync();

        vm.Paging.Calculate(PageNumber, PageSize, count);

        var ids = await query
            .OrderByDescending(r => r.HighestScore)
            .Select(r => r.ImageId)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();

        var items = await query
            .Include(r => r.Image).ThenInclude(r => r.Labels)
            .Where(r => ids.Contains(r.ImageId))
            .ToListAsync();

        var itemVms = new List<ItemVm>();
        foreach (var item in items)
        {
            var itemVm = new ItemVm();
            itemVm.ImageId = item.ImageId;
            itemVm.ImageName = item.Image.Name;
            itemVm.ImageUrl = DisplayMode.GetImageUrl(item.Image.Name, config.Value);
            itemVm.Labels = item.Image.Labels;

            var childItems = JsonConvert.DeserializeObject<List<SimilarImage>>(item.Json)!;
            foreach (var childItem in childItems)
            {
                if (vm.Filter.ApplyScoreFilterOnChildren)
                {
                    if (vm.Filter.MinScore != null && childItem.Score < vm.Filter.MinScore)
                        continue;

                    if (vm.Filter.MaxScore != null && childItem.Score > vm.Filter.MaxScore)
                        continue;
                }

                var childItemVm = new ChildItemVm();
                childItemVm.ImageId = childItem.ImageId;
                childItemVm.ImageName = childItem.ImageName;
                childItemVm.Score = childItem.Score;
                childItemVm.ImageUrl = DisplayMode.GetImageUrl(childItem.ImageName, config.Value);
                itemVm.Children.Add(childItemVm);
            }

            itemVms.Add(itemVm);
        }

        vm.Items = itemVms;
    }

    protected async Task SetDisplayMode(int displayModeId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            DisplayMode = vm.DisplayModes.First(r => r.Id == displayModeId);

            await LoadSimilars(db);
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

            await LoadSimilars(db);
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

            await LoadSimilars(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class SimilarsPageVm
    {
        public string DatasetName { get; set; }
        public string DisplayModeName { get; set; }
        public List<ItemVm> Items { get; set; }
        public Paging Paging { get; set; }
        public SimilarsImageFilter Filter { get; set; }
        public Dictionary<int, LabelDbe> LabelsById { get; set; }
        public List<LabelDbe> Labels { get; set; }
        public List<DisplayModeDbe> DisplayModes { get; set; }

        public SimilarsPageVm()
        {
            DatasetName = "";
            DisplayModeName = "";
            Items = new List<ItemVm>();
            Paging = new Paging();
            Filter = new SimilarsImageFilter();
            LabelsById = new Dictionary<int, LabelDbe>();
            Labels = new List<LabelDbe>();
            DisplayModes = new List<DisplayModeDbe>();
        }
    }

    protected class ItemVm
    {
        public int ImageId { get; set; }
        public string ImageName { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public List<ImageLabelDbe> Labels { get; set; } = null!;
        public List<ChildItemVm> Children { get; set; }

        public ItemVm()
        {
            Children = new List<ChildItemVm>();
        }
    }

    public class ChildItemVm
    {
        public int ImageId { get; set; }
        public string ImageName { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public double Score { get; set; }
    }
}
