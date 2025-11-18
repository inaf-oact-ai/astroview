using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Models.Filters;
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
using System.Reflection.Emit;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class PredictionsPage
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

    private readonly PredictionsPageVm vm;

    public DisplayModeDbe DisplayMode { get; set; } = null!;

    public PredictionsPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new PredictionsPageVm();
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

            await LoadPredictions(db);
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

            await LoadPredictions(db);
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
                await js.InvokeVoidAsync("initPredictionLabelsDropdown", vm.Filter.PredictionLabelIds);
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

    private async Task LoadPredictions(AppDbContext db)
    {
        var query = db.Predictions
            .AsNoTracking()
            .Where(r => r.CaesarJobId == CaesarJobId);

        query = vm.Filter.ApplyToPredictionsQuery(query);

        var count = await query.CountAsync();
        vm.Paging.Calculate(PageNumber, PageSize, count);

        var ids = await query
            .OrderByDescending(r => r.Probability)
            .Select(r => r.Id)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();

        vm.Items = await db.Predictions
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .OrderByDescending(r => r.Probability)
            .Select(r => new ImageVm
            {
                ImageId = r.ImageId,
                ImageName = r.Image.Name,
                Labels = r.Image.Labels,
                Prediction = r,
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

            await LoadPredictions(db);
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

            var predictionLabelIds = await js.InvokeAsync<List<string>>("getPredictionLabelsDropdownValues");
            vm.Filter.PredictionLabelIds = predictionLabelIds.Select(int.Parse).ToList();

            PageNumber = 1;

            var uri = nav.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                { "PageNumber", PageNumber },
                { "PageSize", PageSize },
            });
            nav.NavigateTo(uri);

            await js.InvokeVoidAsync("hideFilterModal");

            await LoadPredictions(db);
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

            await LoadPredictions(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task MergeAll()
    {
        try
        {
            bool confirmed = await js.InvokeAsync<bool>("confirm", "[Merge All] Please confirm that you want to merge all predictions.");
            if (!confirmed)
                return;

            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await db.DatasetJobs
                .Where(r => r.DatasetId == DatasetId && r.Type == DatasetJobType.ApplyPredictions)
                .Where(r => r.JobStatus == HangfireJobStatus.None || r.JobStatus == HangfireJobStatus.Running)
                .AnyAsync();

            if (hasActiveJobs)
            {
                throw new ValidationException("Another job of type 'Apply Predictions' is running in background. " +
                    "Please wait until it completes");
            }

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.ApplyPredictions(
                DatasetId,
                CaesarJobId,
                new PredictionsImageFilter(),
                false,
                AppVm.UserId,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.ApplyPredictions,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            await db.SaveChangesAsync();

            await ShowBackgroundJobToast($"Merge predictions to dataset {vm.DatasetName}", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ReplaceAll()
    {
        try
        {
            bool confirmed = await js.InvokeAsync<bool>("confirm", "[Replace All] Please confirm that you want to replace all existing labels with predictions.");
            if (!confirmed)
                return;

            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await db.DatasetJobs
                .Where(r => r.DatasetId == DatasetId && r.Type == DatasetJobType.ApplyPredictions)
                .Where(r => r.JobStatus == HangfireJobStatus.None || r.JobStatus == HangfireJobStatus.Running)
                .AnyAsync();

            if (hasActiveJobs)
            {
                throw new ValidationException("Another job of type 'Apply Predictions' is running in background. " +
                    "Please wait until it completes");
            }

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.ApplyPredictions(
                DatasetId,
                CaesarJobId,
                new PredictionsImageFilter(),
                true,
                AppVm.UserId,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.ApplyPredictions,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            await db.SaveChangesAsync();

            await ShowBackgroundJobToast($"Merge predictions with replace to dataset {vm.DatasetName}", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task MergeFiltered()
    {
        try
        {
            bool confirmed = await js.InvokeAsync<bool>("confirm", "[Merge Filtered] Please confirm that you want to " +
                "merge all predictions for the filtered results.");
            if (!confirmed)
                return;

            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await db.DatasetJobs
                .Where(r => r.DatasetId == DatasetId && r.Type == DatasetJobType.ApplyPredictions)
                .Where(r => r.JobStatus == HangfireJobStatus.None || r.JobStatus == HangfireJobStatus.Running)
                .AnyAsync();

            if (hasActiveJobs)
            {
                throw new ValidationException("Another job of type 'Apply Predictions' is running in background. " +
                    "Please wait until it completes");
            }

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.ApplyPredictions(
                DatasetId,
                CaesarJobId,
                vm.Filter,
                false,
                AppVm.UserId,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.ApplyPredictions,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            await db.SaveChangesAsync();

            await ShowBackgroundJobToast($"Merge predictions to dataset {vm.DatasetName}", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ReplaceFiltered()
    {
        try
        {
            bool confirmed = await js.InvokeAsync<bool>("confirm", "[Replace Filtered] Please confirm that you want to " +
                "replace all existing labels with predictions for the filtered results.");
            if (!confirmed)
                return;

            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await db.DatasetJobs
                .Where(r => r.DatasetId == DatasetId && r.Type == DatasetJobType.ApplyPredictions)
                .Where(r => r.JobStatus == HangfireJobStatus.None || r.JobStatus == HangfireJobStatus.Running)
                .AnyAsync();

            if (hasActiveJobs)
            {
                throw new ValidationException("Another job of type 'Apply Predictions' is running in background. " +
                    "Please wait until it completes");
            }

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.ApplyPredictions(
                DatasetId,
                CaesarJobId,
                vm.Filter,
                true,
                AppVm.UserId,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.ApplyPredictions,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            await db.SaveChangesAsync();

            await ShowBackgroundJobToast($"Merge predictions with replace to dataset {vm.DatasetName}", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task Merge(int predictionId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var prediction = await db.Predictions
                .Include(r => r.Image).ThenInclude(r => r.Labels).ThenInclude(r => r.Label)
                .Include(r => r.Label)
                .FirstAsync(r => r.Id == predictionId);

            var label = prediction.Image.Labels.FirstOrDefault(r => r.LabelId == prediction.LabelId);
            if (label == null)
            {
                label = new ImageLabelDbe
                {
                    ImageId = prediction.ImageId,
                    LabelId = prediction.LabelId,
                };
                prediction.Image.Labels.Add(label);
            }

            label.Value = 0; // prediction.Probability; not needed

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = AppVm.UserId,
                DatasetId = DatasetId,
                Type = ChangeType.MergeSinglePrediction,
                Data = $"Merging single prediction from Job {CaesarJobId} to Dataset {DatasetId}. " +
                $"Merged label: {prediction.Label.Name}. " +
                $"Probability: {prediction.Probability}"
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await LoadPredictions(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task Replace(int predictionId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var prediction = await db.Predictions
                .Include(r => r.Image).ThenInclude(r => r.Labels).ThenInclude(r => r.Label)
                .Include(r => r.Label)
                .FirstAsync(r => r.Id == predictionId);

            var removedLabels = prediction.Image.Labels.Select(r => r.Label.Name).Aggregate((a, b) => a + "," + b);
            prediction.Image.Labels.Clear();

            prediction.Image.Labels.Add(new ImageLabelDbe
            {
                ImageId = prediction.ImageId,
                LabelId = prediction.LabelId,
                Value = 0, // prediction.Probability, not needed
            });

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = AppVm.UserId,
                DatasetId = DatasetId,
                Type = ChangeType.ReplaceSinglePrediction,
                Data = $"Replacing labels with single prediction from Job {CaesarJobId} to Dataset {DatasetId}. " +
                $"Merged label: {prediction.Label.Name}. " +
                $"Probability: {prediction.Probability}. Removed labels: {removedLabels}"
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await LoadPredictions(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class PredictionsPageVm
    {
        public string DatasetName { get; set; }
        public string DisplayModeName { get; set; }
        public List<ImageVm> Items { get; set; }
        public Paging Paging { get; set; }
        public PredictionsImageFilter Filter { get; set; }
        public Dictionary<int, LabelDbe> LabelsById { get; set; }
        public List<LabelDbe> Labels { get; set; }
        public List<DisplayModeDbe> DisplayModes { get; set; }

        public PredictionsPageVm()
        {
            DatasetName = "";
            DisplayModeName = "";
            Items = new List<ImageVm>();
            Paging = new Paging();
            Filter = new PredictionsImageFilter();
            LabelsById = new Dictionary<int, LabelDbe>();
            Labels = new List<LabelDbe>();
            DisplayModes = new List<DisplayModeDbe>();
        }
    }

    protected class ImageVm
    {
        public int ImageId { get; set; }
        public string ImageName { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public List<ImageLabelDbe> Labels { get; set; } = null!;
        public PredictionDbe Prediction { get; set; } = null!;
    }
}
