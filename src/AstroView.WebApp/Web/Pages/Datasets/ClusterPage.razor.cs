using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Models.Filters;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using Hangfire;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;
using System.Text;
using static AstroView.WebApp.Web.Pages.Datasets.ClustersPage;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class ClusterPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [Parameter]
    public int CaesarJobId { get; set; }
    public int _caesarJobId { get; set; }

    [Parameter]
    public int DatasetClusterId { get; set; }
    public int _datasetClusterId { get; set; }

    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private readonly ClusterPageVm vm;

    public DisplayModeDbe DisplayMode { get; set; } = null!;

    public ClusterPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new ClusterPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _caesarJobId = CaesarJobId;
        _datasetClusterId = DatasetClusterId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetName = await db.Datasets.Where(r => r.Id == DatasetId).Select(r => r.Name).FirstAsync();
            vm.ClusterName = await db.Clusters.Where(r => r.Id == DatasetClusterId).Select(r => r.Name).FirstAsync();
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

            await LoadCluster(db);
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
            && DatasetClusterId == _datasetClusterId
            && PageNumber == _pageNumber
            && PageSize == _pageSize)
        {
            return;
        }

        _datasetId = DatasetId;
        _caesarJobId = CaesarJobId;
        _datasetClusterId = DatasetClusterId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadCluster(db);
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
            await js.InvokeVoidAsync("onAfterRender");

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

    protected async Task ApplyLabel()
    {
        try
        {
            if (vm.SelectedLabelId.IsEmpty())
            {
                throw new ValidationException("Please select a label");
            }

            var confirmed = await js.InvokeAsync<bool>("confirm", "[Apply Label] Please confirm that you want to " +
                "apply selected label to the filter results.");
            if (!confirmed)
                return;

            var labelId = int.Parse(vm.SelectedLabelId);
            var label = vm.LabelsById[labelId];

            vm.LabelingPercent = 0;
            vm.LabelingInProgress = true;

            using var db = await dbf.CreateDbContextAsync();

            var query = db.ClusterItems
                .AsNoTracking()
                .Where(r => r.ClusterId == DatasetClusterId);

            query = vm.Filter.Apply(query);

            var count = await query.CountAsync();

            var sql = new StringBuilder();
            var processed = 0;
            while (true)
            {
                var imageIds = await query
                    .OrderBy(r => r.Id)
                    .Select(r => r.Image.Id)
                    .Skip(processed)
                    .Take(Defaults.LabelingBatchSize)
                    .ToListAsync();

                if (imageIds.Count == 0)
                    break;

                sql.Clear();
                foreach (var imageId in imageIds)
                {
                    sql.AppendLine(DbHelper.GetInsertLabelSql(imageId, labelId, 0));
                }

                await db.Database.ExecuteSqlRawAsync(sql.ToString());

                processed += Defaults.LabelingBatchSize;

                vm.LabelingPercent = processed * 100.0 / count;

                StateHasChanged();
            }

            var change = new ChangeDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Date = DateTime.UtcNow,
                Type = ChangeType.BulkApplyLabel,
                Data = $"Label: {label.Name} Cluster ID: {DatasetClusterId}",
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            vm.LabelingInProgress = false;

            await LoadCluster(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RemoveLabel()
    {
        try
        {
            if (vm.SelectedLabelId.IsEmpty())
            {
                throw new ValidationException("Please select a label");
            }

            var confirmed = await js.InvokeAsync<bool>("confirm", "[Remove Label] Please confirm that you want to " +
                "remove selected label from the filter results.");
            if (!confirmed)
                return;

            var labelId = int.Parse(vm.SelectedLabelId);
            var label = vm.LabelsById[labelId];

            vm.LabelingPercent = 0;
            vm.LabelingInProgress = true;

            using var db = await dbf.CreateDbContextAsync();

            var query = db.ClusterItems
                .AsNoTracking()
                .Where(r => r.ClusterId == DatasetClusterId);

            query = vm.Filter.Apply(query);

            var count = await query.CountAsync();

            var processed = 0;
            while (true)
            {
                var imageIds = await query
                    .OrderBy(r => r.Id)
                    .Select(r => r.Image.Id)
                    .Skip(processed)
                    .Take(Defaults.LabelingBatchSize)
                    .ToListAsync();

                if (imageIds.Count == 0)
                    break;

                await db.ImageLabels
                    .Where(r => r.LabelId == labelId && imageIds.Contains(r.Image.Id))
                    .ExecuteDeleteAsync();

                processed += Defaults.LabelingBatchSize;

                vm.LabelingPercent = processed * 100.0 / count;

                StateHasChanged();
            }

            var change = new ChangeDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Date = DateTime.UtcNow,
                Type = ChangeType.BulkRemoveLabel,
                Data = $"Label: {label.Name} Cluster ID: {DatasetClusterId}",
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            vm.LabelingInProgress = false;

            await LoadCluster(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowCreateDatasetModal()
    {
        try
        {
            await js.InvokeVoidAsync("showModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task CreateDataset()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.CreateDatasetFromClusters(
                vm.CreateDatasetModel.DatasetName,
                new List<int> { DatasetClusterId },
                AppVm.UserId,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.CreateDatasetFromClusters,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            await db.SaveChangesAsync();

            await js.InvokeVoidAsync("hideModal");

            await ShowBackgroundJobToast($"Create dataset {vm.CreateDatasetModel.DatasetName} from selected clusters", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadCluster(AppDbContext db)
    {
        var query = db.ClusterItems
            .Include(r => r.Image)
            .Where(r => r.ClusterId == DatasetClusterId)
            .AsNoTracking();

        query = vm.Filter.Apply(query);

        var count = await query.CountAsync();

        vm.Paging.Calculate(PageNumber, PageSize, count);

        vm.Items = await query
            .OrderByDescending(r => r.OutlierScore)
            .Select(r => new ClusterItemVm
            {
                Id = r.ImageId,
                Image = r.Image,
                Probability = r.Probability,
                OutlierScore = r.OutlierScore,
                Labels = r.Image.Labels,
            })
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();

        foreach (var item in vm.Items)
        {
            item.ImageUrl = DisplayMode.GetImageUrl(item.Image.Name, config.Value);
        }
    }

    protected async Task SetDisplayMode(int displayModeId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            DisplayMode = vm.DisplayModes.First(r => r.Id == displayModeId);

            await LoadCluster(db);
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

            await LoadCluster(db);
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

            await LoadCluster(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }


    private class ClusterPageVm
    {
        public string DatasetName { get; set; }
        public string ClusterName { get; set; }
        public string DisplayModeName { get; set; }
        public List<ClusterItemVm> Items { get; set; }
        public Paging Paging { get; set; }
        public ClusterItemFilter Filter { get; set; }
        public Dictionary<int, LabelDbe> LabelsById { get; set; }
        public List<LabelDbe> Labels { get; set; }
        public List<DisplayModeDbe> DisplayModes { get; set; }

        public CreateDatasetModelVm CreateDatasetModel { get; set; }

        public string SelectedLabelId { get; set; }
        public bool LabelingInProgress { get; set; }
        public double LabelingPercent { get; set; }

        public ClusterPageVm()
        {
            DatasetName = "";
            ClusterName = "";
            DisplayModeName = "";
            Items = new List<ClusterItemVm>();
            Paging = new Paging();
            Filter = new ClusterItemFilter();
            LabelsById = new Dictionary<int, LabelDbe>();
            Labels = new List<LabelDbe>();
            DisplayModes = new List<DisplayModeDbe>();

            CreateDatasetModel = new CreateDatasetModelVm();

            SelectedLabelId = "";
        }
    }

    private class ClusterItemVm
    {
        public int Id { get; set; }
        public ImageDbe Image { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public List<ImageLabelDbe> Labels { get; set; } = null!;
        public double Probability { get; set; }
        public double OutlierScore { get; set; }
    }
}
