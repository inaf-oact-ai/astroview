using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using AstroView.WebApp.Web.Layout;
using Hangfire;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class DatasetPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private readonly DatasetPageVm vm;

    public DatasetPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new DatasetPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadDataset(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (DatasetId == _datasetId
            && PageNumber == _pageNumber
            && PageSize == _pageSize)
        {
            return;
        }

        _datasetId = DatasetId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadDataset(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadDataset(AppDbContext db)
    {
        var lastDay = DateTime.UtcNow.AddDays(-1);

        var dataset = await db.Datasets
            .AsNoTracking()
            .Where(r => r.Id == DatasetId)
            .Select(r => new
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                ImagesCount = r.Images.Count(),
                ChangesCount = r.Changes.Count(),
                JobsCount = r.Jobs.Count(),
                ShareType = r.ShareType,
                IsLocked = r.IsLocked,
                IsRemoved = r.IsRemoved,
                DatasetUserId = r.UserId,
                ModifiedDate = r.ModifiedDate,
            })
        .FirstAsync();

        vm.Id = dataset.Id;
        vm.Name = dataset.Name;
        vm.Description = dataset.Description;
        vm.ImagesCount = dataset.ImagesCount;
        vm.ChangesCount = dataset.ChangesCount;
        vm.JobsCount = dataset.JobsCount;
        vm.ShareType = dataset.ShareType;
        vm.IsLocked = dataset.IsLocked;
        vm.IsRemoved = dataset.IsRemoved;
        vm.DatasetUserId = dataset.DatasetUserId;

        vm.DatasetModifiedDate = dataset.ModifiedDate;

        vm.ImagesWithFeatures = await db.Images.CountAsync(r => r.DatasetId == DatasetId && r.HasFeatures);

        var firstImage = await db.Images.Where(r => r.DatasetId == DatasetId && r.HasFeatures).FirstOrDefaultAsync();
        if (firstImage != null)
        {
            var jArray = JArray.Parse(firstImage.Features!);
            vm.FeaturesCount = jArray.Count;
        }

        vm.DatasetName = vm.Name;
        vm.EditDatasetModal.Name = vm.Name;
        vm.EditDatasetModal.Description = vm.Description;
        vm.EditDatasetModal.ShareType = ((int)vm.ShareType).ToString();
        vm.FitsCount = await db.Images.Where(r => r.DatasetId == DatasetId).CountAsync(r => r.Path != null);

        await LoadDatasetJobs(db);
        await LoadDisplayModes(db);
        await LoadFunctionHistory(db);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await js.InvokeVoidAsync("initSettingsDropdown");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadFunctionHistory(AppDbContext db)
    {
        var count = await db.CaesarJobs.Where(r => r.DatasetId == DatasetId).CountAsync();

        vm.Paging.Calculate(PageNumber, PageSize, count);

        vm.Jobs = await db.CaesarJobs
            .AsNoTracking()
            .Include(r => r.DisplayMode)
            .Where(r => r.DatasetId == DatasetId)
            .OrderByDescending(r => r.StartedDate)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();
    }

    private async Task LoadDatasetJobs(AppDbContext db)
    {
        vm.DatasetJobs = await db.DatasetJobs
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.DatasetId == DatasetId)
            .Where(r => r.JobStatus != HangfireJobStatus.Completed && r.JobStatus != HangfireJobStatus.Failed)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    private async Task LoadDisplayModes(AppDbContext db)
    {
        vm.DisplayModes = await db.DisplayModes
            .AsNoTracking()
            .Where(r => r.DatasetId == DatasetId)
            .OrderBy(r => r.Extension)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }

    protected async Task DisplayDataset()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadDataset(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RemoveDisplayMode(int displayModeId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == displayModeId);

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.RemoveDisplayMode(displayMode.DatasetId, displayModeId, AppVm.UserId, null!));
            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.RemoveDisplayMode,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.RemoveDisplayMode,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"Removing display mode: {displayMode.Name}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            vm.DisplayModes.RemoveAll(r => r.Id == displayModeId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task SetDisplayModeDefault(int displayModeId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var displayModes = await db.DisplayModes.Where(r => r.DatasetId == DatasetId).ToListAsync();
            displayModes.ForEach(r => r.IsDefault = r.Id == displayModeId);

            await db.SaveChangesAsync();

            vm.DisplayModes.ForEach(r => r.IsDefault = r.Id == displayModeId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RemoveDataset()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);

            if (dataset.UserId != AppVm.UserId)
                return;

            dataset.IsRemoved = true;

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.RemoveDataset,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            AppVm.CurrentDatasetId = 0;

            await AppVm.SelectedDatasetChanged.InvokeAsync();

            nav.NavigateTo($"/Datasets");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowEditDatasetModal()
    {
        try
        {
            await js.InvokeVoidAsync("showEditDatasetModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task EditDataset()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var dataset = await db.Datasets.FirstAsync(r => r.Id == vm.Id);

            dataset.Name = vm.EditDatasetModal.Name;
            dataset.Description = vm.EditDatasetModal.Description;
            dataset.ShareType = Enum.Parse<DatasetShareType>(vm.EditDatasetModal.ShareType);

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.EditDatasetInfo,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"Name = {dataset.Name}; Description = {dataset.Description}; ShareType = {dataset.ShareType}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await js.InvokeVoidAsync("hideEditDatasetModal");

            await OnInitializedAsync();

            await AppVm.SelectedDatasetChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowGeneratePixPlotModal(int displayModeId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == displayModeId);

            if (displayMode.PixPlotJobId.Length > 0
                && (displayMode.PixPlotJobStatus == HangfireJobStatus.None
                || displayMode.PixPlotJobStatus == HangfireJobStatus.Running))
            {
                throw new ValidationException("PixPlot job is already running");
            }

            vm.GeneratePixPlotModal = new GeneratePixPlotModalVm();
            vm.GeneratePixPlotModal.DisplayModeId = displayModeId;

            await js.InvokeVoidAsync("showGeneratePixPlotModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task GeneratePixPlot()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == vm.GeneratePixPlotModal.DisplayModeId);

            if (displayMode.PixPlotJobId.Length > 0
                && (displayMode.PixPlotJobStatus == HangfireJobStatus.None
                || displayMode.PixPlotJobStatus == HangfireJobStatus.Running))
            {
                throw new ValidationException("PixPlot job is already running");
            }

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.GeneratePixPlotMap(
                DatasetId,
                vm.GeneratePixPlotModal.DisplayModeId,
                vm.GeneratePixPlotModal.Params.MinClusterSize,
                vm.GeneratePixPlotModal.Params.MaxClusters,
                vm.GeneratePixPlotModal.Params.AtlasCellSize,
                vm.GeneratePixPlotModal.Params.UmapNeighbors,
                vm.GeneratePixPlotModal.Params.UmapMinDist,
                vm.GeneratePixPlotModal.Params.UmapComponents,
                vm.GeneratePixPlotModal.Params.UmapMetric,
                vm.GeneratePixPlotModal.Params.PointgridFill,
                vm.GeneratePixPlotModal.Params.ImageMinSize,
                vm.GeneratePixPlotModal.Params.Seed,
                vm.GeneratePixPlotModal.Params.KmeansClusters,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.GeneratePixPlot,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            displayMode.PixPlotParamsJson = JsonConvert.SerializeObject(vm.AddDisplayModeModal.Params);
            displayMode.PixPlotJobId = jobId;
            displayMode.PixPlotJobStatus = HangfireJobStatus.Running;

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.GeneratePixPlot,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"Generating PixPlot for DisplayMode: {displayMode.Name}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await LoadDisplayModes(db);
            await LoadDatasetJobs(db);

            await js.InvokeVoidAsync("hideGeneratePixPlotModal");

            await ShowBackgroundJobToast($"Generate PixPlot for Display Mode: {displayMode.Name}", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task GenerateCaesarDataset(int displayModeId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.GenerateCaesarDataset(DatasetId, displayModeId, null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.GenerateCaesarDataset,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == displayModeId);
            displayMode.CaesarDatasetJobId = jobId;
            displayMode.CaesarDatasetJobStatus = HangfireJobStatus.Running;

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.GenerateCaesarDataset,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"Generating Caesar Dataset for DisplayMode: {displayMode.Name}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await LoadDisplayModes(db);
            await LoadDatasetJobs(db);

            await ShowBackgroundJobToast($"Generate Caesar Dataset for Display Mode: {displayMode.Name}", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowDisplayModeParams(int displayModeId)
    {
        try
        {
            var displayMode = vm.DisplayModes.First(r => r.Id == displayModeId);
            vm.DisplayModeParamsModal.DisplayMode = displayMode;
            vm.DisplayModeParamsModal.Params = JsonConvert.DeserializeObject<DisplayModeParams>(displayMode.ParamsJson)!;

            await js.InvokeVoidAsync("showDisplayModeParamsModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowPixPlotParams(int displayModeId)
    {
        try
        {
            var displayMode = vm.DisplayModes.First(r => r.Id == displayModeId);
            vm.PixPlotParamsModal.DisplayMode = displayMode;
            vm.PixPlotParamsModal.Params = JsonConvert.DeserializeObject<PixPlotParams>(displayMode.PixPlotParamsJson)!;

            await js.InvokeVoidAsync("showPixPlotParamsModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task Select()
    {
        try
        {
            AppVm.CurrentDatasetId = DatasetId;

            await AppVm.SelectedDatasetChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task Lock()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await DbHelper.HasActiveDatasetJobs(DatasetId, db);
            if (hasActiveJobs)
                throw new ValidationException("Please wait until all dataset jobs are completed");

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.IsLocked = true;

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.LockDataset,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await AppVm.SelectedDatasetChanged.InvokeAsync();

            vm.IsLocked = true;
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RemoveImagesWithoutFeatures()
    {
        try
        {
            vm.ShowTrimLoader = true;

            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await DbHelper.HasActiveDatasetJobs(DatasetId, db);
            if (hasActiveJobs)
                throw new ValidationException("Please wait until all dataset jobs are completed");

            var images = await db.Images
                .Where(r => r.DatasetId == DatasetId && r.HasFeatures == false)
                .ToListAsync();
            db.RemoveRange(images);

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.RemoveImagesWithoutFeatures,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"Images removed: {images.Count}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            vm.ShowTrimLoader = false;

            await LoadDataset(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task GenerateRandomFeatures()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await DbHelper.HasActiveDatasetJobs(DatasetId, db);
            if (hasActiveJobs)
                throw new ValidationException("Please wait until all dataset jobs are completed");

            const int dimensions = 100;

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.GenerateRandomFeatures(DatasetId, dimensions, null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.GenerateRandomFeatures,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.GenerateRandomFeatures,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"Dimensions: {dimensions}",
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await ShowBackgroundJobToast($"Generate random features for dataset {vm.DatasetName}", jobId);

            await LoadDatasetJobs(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RemoveAllLabels()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await DbHelper.HasActiveDatasetJobs(DatasetId, db);
            if (hasActiveJobs)
                throw new ValidationException("Please wait until all dataset jobs are completed");

            await db.ImageLabels.Where(r => r.Image.DatasetId == DatasetId).ExecuteDeleteAsync();

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.RemoveAllLabels,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"",
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await LoadDataset(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ResetCaesarDatasets()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var displayModes = await db.DisplayModes.Where(r => r.DatasetId == DatasetId).ToListAsync();
            foreach (var mode in displayModes)
            {
                mode.CaesarDatasetJobId = "";
                mode.CaesarDatasetJobStatus = HangfireJobStatus.None;
            }

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.ResestCaesarDatasets,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = $"",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await LoadDisplayModes(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowAddDisplayModeModal()
    {
        try
        {
            await js.InvokeVoidAsync("showAddDisplayModeModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task AddDisplayMode()
    {
        try
        {
            if (vm.AddDisplayModeModal.Name.IsEmpty())
                return;

            using var db = await dbf.CreateDbContextAsync();

            var displayModesCount = await db.DisplayModes.Where(r => r.DatasetId == DatasetId).CountAsync();

            var displayModeParamsJson = JsonConvert.SerializeObject(vm.AddDisplayModeModal.Params);
            var displayMode = new DisplayModeDbe
            {
                DatasetId = DatasetId,
                Name = vm.AddDisplayModeModal.Name,
                ParamsJson = displayModeParamsJson,
                ImagesPath = "",
                Extension = "png",
                RenderJobId = "",
                RenderJobStatus = HangfireJobStatus.None,
                PixPlotParamsJson = "",
                PixPlotJobId = "",
                PixPlotJobStatus = HangfireJobStatus.None,
                CaesarDatasetJobId = "",
                CaesarDatasetJobStatus = HangfireJobStatus.None,
                IsDefault = displayModesCount == 1,
            };
            db.DisplayModes.Add(displayMode);

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                Type = ChangeType.AddDisplayMode,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Data = displayModeParamsJson,
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            displayMode.ImagesPath = config.Value.GetDisplayModePaths(DatasetId, displayMode.Id).ImagesDirectory;

            Directory.CreateDirectory(displayMode.ImagesPath);

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.RenderDisplayMode(
                DatasetId,
                displayMode.Id,
                vm.AddDisplayModeModal.Params.Contrast,
                vm.AddDisplayModeModal.Params.SigmaLow,
                vm.AddDisplayModeModal.Params.SigmaUp,
                vm.AddDisplayModeModal.Params.UsePil,
                vm.AddDisplayModeModal.Params.SubtractBkg,
                vm.AddDisplayModeModal.Params.ClipData,
                vm.AddDisplayModeModal.Params.ZscaleData,
                vm.AddDisplayModeModal.Params.ApplyMinMax,
                AppVm.UserId,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.RenderDisplayMode,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            displayMode.RenderJobId = jobId;
            displayMode.RenderJobStatus = HangfireJobStatus.Running;

            await db.SaveChangesAsync();

            await LoadDisplayModes(db);
            await LoadDatasetJobs(db);

            await js.InvokeVoidAsync("hideAddDisplayModeModal");

            await ShowBackgroundJobToast($"Add display mode to dataset {vm.DatasetName}", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private class DatasetPageVm
    {
        public int Id { get; set; }
        public string DatasetUserId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DatasetName { get; set; }
        public int ImagesCount { get; set; }
        public int ImagesWithFeatures { get; set; }
        public int FeaturesCount { get; set; }
        public int ChangesCount { get; set; }
        public int JobsCount { get; set; }
        public DatasetShareType ShareType { get; set; }
        public bool ShowNewVersionLoader { get; set; }
        public bool IsLocked { get; set; }
        public bool IsRemoved { get; set; }

        public DateTime DatasetModifiedDate { get; set; }

        public Paging Paging { get; set; }
        public List<CaesarJobDbe> Jobs { get; set; }
        public EditDatasetModalVm EditDatasetModal { get; set; }
        public AddDisplayModeModalVm AddDisplayModeModal { get; set; }
        public DisplayModeParamsModalVm DisplayModeParamsModal { get; set; }
        public GeneratePixPlotModalVm GeneratePixPlotModal { get; set; }
        public PixPlotParamsModalVm PixPlotParamsModal { get; set; }

        public int FitsCount { get; set; }
        public List<DisplayModeDbe> DisplayModes { get; set; }
        public List<DatasetJobDbe> DatasetJobs { get; set; }

        public bool ShowTrimLoader { get; set; }

        public DatasetPageVm()
        {
            Name = "";
            DatasetUserId = "";
            Description = "";
            DatasetName = "";
            Paging = new Paging();
            Jobs = new List<CaesarJobDbe>();
            EditDatasetModal = new EditDatasetModalVm();
            AddDisplayModeModal = new AddDisplayModeModalVm();
            DisplayModeParamsModal = new DisplayModeParamsModalVm();
            GeneratePixPlotModal = new GeneratePixPlotModalVm();
            PixPlotParamsModal = new PixPlotParamsModalVm();
            DisplayModes = new List<DisplayModeDbe>();
            DatasetJobs = new List<DatasetJobDbe>();
            IsLocked = false;
        }
    }

    public class EditDatasetModalVm
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }
        public string Description { get; set; }

        [Required]
        public string ShareType { get; set; } = null!;

        public EditDatasetModalVm()
        {
            Name = "";
            Description = "";
        }
    }

    public class AddDisplayModeModalVm
    {
        public string Name { get; set; }
        public DisplayModeParams Params { get; set; }

        public AddDisplayModeModalVm()
        {
            Name = "";
            Params = new DisplayModeParams();
        }
    }

    public class DisplayModeParamsModalVm
    {
        public DisplayModeDbe DisplayMode { get; set; }
        public DisplayModeParams Params { get; set; }

        public DisplayModeParamsModalVm()
        {
            DisplayMode = new DisplayModeDbe();
            Params = new DisplayModeParams();
        }
    }

    public class PixPlotParamsModalVm
    {
        public DisplayModeDbe DisplayMode { get; set; }
        public PixPlotParams Params { get; set; }

        public PixPlotParamsModalVm()
        {
            DisplayMode = new DisplayModeDbe();
            Params = new PixPlotParams();
        }
    }

    public class GeneratePixPlotModalVm
    {
        public int DisplayModeId { get; set; }
        public PixPlotParams Params { get; set; }

        public GeneratePixPlotModalVm()
        {
            Params = new PixPlotParams();
        }
    }
}
