using AstroView.WebApp.App;
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
using System.Data;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class ClustersPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [Parameter]
    public int CaesarJobId { get; set; }
    public int _caesarJobId { get; set; }

    private readonly ClustersPageVm vm;

    public ClustersPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new ClustersPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _caesarJobId = CaesarJobId;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetName = await db.Datasets.Where(r => r.Id == DatasetId).Select(r => r.Name).FirstAsync();
            vm.Labels = await db.Labels.OrderBy(r => r.Name).ToListAsync();

            await LoadClusters(db);
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

            await LoadClusters(db);
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
            var selectedClusters = vm.Clusters.Where(r => r.IsChecked).ToList();
            if (selectedClusters.Count == 0)
            {
                throw new ValidationException("Please select one or more clusters");
            }

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

            var clusters = vm.Clusters.Where(r => r.IsChecked).ToList();

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.CreateDatasetFromClusters(
                vm.CreateDatasetModel.DatasetName,
                clusters.Select(r => r.Id).ToList(),
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

    protected async Task ApplyLabel()
    {
        try
        {
            if (vm.SelectedLabelId.IsEmpty())
            {
                throw new ValidationException("Please select a label");
            }

            var clusterIds = vm.Clusters.Where(r => r.IsChecked).Select(r => r.Id).ToList();
            if (clusterIds.Count == 0)
            {
                throw new ValidationException("Please select one or more clusters");
            }

            var confirmed = await js.InvokeAsync<bool>("confirm", "[Apply Label] Please confirm that you want to " +
                "apply selected label from selected clusters.");
            if (!confirmed)
                return;

            var labelId = int.Parse(vm.SelectedLabelId);
            var label = vm.Labels.First(r => r.Id == labelId);

            vm.LabelingPercent = 0;
            vm.LabelingInProgress = true;

            using var db = await dbf.CreateDbContextAsync();

            var query = db.Clusters
                .Where(r => clusterIds.Contains(r.Id))
                .SelectMany(r => r.Items)
                .OrderBy(r => r.ImageId)
                .Select(r => r.ImageId);

            var count = await query.CountAsync();

            var sql = new StringBuilder();
            var processed = 0;
            while (true)
            {
                var imageIds = await query
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
                Data = $"Label: {label.Name} Cluster IDs: {clusterIds.ToListString()}",
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            vm.LabelingInProgress = false;

            await LoadClusters(db);
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

            var clusterIds = vm.Clusters.Where(r => r.IsChecked).Select(r => r.Id).ToList();
            if (clusterIds.Count == 0)
            {
                throw new ValidationException("Please select one or more clusters");
            }

            var confirmed = await js.InvokeAsync<bool>("confirm", "[Remove Label] Please confirm that you want to " +
                "remove selected label from selected clusters.");
            if (!confirmed)
                return;

            var labelId = int.Parse(vm.SelectedLabelId);
            var label = vm.Labels.First(r => r.Id == labelId);

            vm.LabelingPercent = 0;
            vm.LabelingInProgress = true;

            using var db = await dbf.CreateDbContextAsync();

            var query = db.Clusters
                .Where(r => clusterIds.Contains(r.Id))
                .SelectMany(r => r.Items)
                .OrderBy(r => r.ImageId)
                .Select(r => r.ImageId);

            var count = await query.CountAsync();

            var processed = 0;
            while (true)
            {
                var imageIds = await query
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
                Data = $"Label: {label.Name} Cluster IDs: {clusterIds.ToListString()}",
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            vm.LabelingInProgress = false;

            await LoadClusters(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task DisplayClusters()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadClusters(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadClusters(AppDbContext db)
    {
        var query = db.Clusters
            .AsNoTracking()
            .OrderBy(r => r.Index)
            .Where(r => r.CaesarJobId == CaesarJobId);

        if (vm.MinIndex != null)
            query = query.Where(r => r.Index >= vm.MinIndex.Value);

        if (vm.MaxIndex != null)
            query = query.Where(r => r.Index <= vm.MaxIndex.Value);

        if (vm.MinImages != null)
            query = query.Where(r => r.Items.Count >= vm.MinImages.Value);

        if (vm.MaxImages != null)
            query = query.Where(r => r.Items.Count <= vm.MaxImages.Value);

        var clusters = await query.Select(r => new ClusterVm
        {
            Id = r.Id,
            Name = r.Name,
            Index = r.Index,
            ImagesCount = r.Items.Count(),
            ImageName = r.Items.First().Image.Name,
        }).ToListAsync();

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

        foreach (var cluster in clusters)
        {
            cluster.ImageUrl = displayMode.GetImageUrl(cluster.ImageName, config.Value);
        }

        vm.Clusters = clusters;
    }

    private class ClustersPageVm
    {
        public string DatasetName { get; set; }
        public List<ClusterVm> Clusters { get; set; }
        public CreateDatasetModelVm CreateDatasetModel { get; set; }
        public List<LabelDbe> Labels { get; set; }

        public string SelectedLabelId { get; set; }
        public bool LabelingInProgress { get; set; }
        public double LabelingPercent { get; set; }

        public int? MinIndex { get; set; }
        public int? MaxIndex { get; set; }
        public int? MinImages { get; set; }
        public int? MaxImages { get; set; }

        public ClustersPageVm()
        {
            DatasetName = "";
            Clusters = new List<ClusterVm>();
            CreateDatasetModel = new CreateDatasetModelVm();
            Labels = new List<LabelDbe>();

            SelectedLabelId = "";
        }
    }

    private class ClusterVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int Index { get; set; }
        public int ImagesCount { get; set; }
        public bool IsChecked { get; set; }
        public string ImageName { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
    }

    public class CreateDatasetModelVm
    {
        [Required]
        public string DatasetName { get; set; }

        public CreateDatasetModelVm()
        {
            DatasetName = "";
        }
    }
}
