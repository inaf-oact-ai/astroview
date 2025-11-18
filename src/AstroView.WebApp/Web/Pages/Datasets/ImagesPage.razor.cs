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
using System.Data;
using System.Text;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class ImagesPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [SupplyParameterFromQuery]
    private int? DisplayModeId { get; set; }
    private int? _displayModeId { get; set; }

    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    public bool IsLoading { get; set; }

    private readonly ImagesPageVm vm;

    public ImagesPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new ImagesPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _displayModeId = DisplayModeId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetName = await db.Datasets.Where(r => r.Id == DatasetId).Select(r => r.Name).FirstAsync();
            vm.Labels = await db.Labels.OrderBy(r => r.Name).ToListAsync();
            vm.LabelsById = vm.Labels.ToDictionary(r => r.Id, r => r);
            vm.DisplayModes = await db.DisplayModes.Where(r => r.DatasetId == DatasetId).ToListAsync();

            DisplayModeDbe? displayMode;
            if (DisplayModeId == null)
            {
                displayMode = vm.DisplayModes.FirstOrDefault(r => r.IsDefault);
                if (displayMode == null)
                    displayMode = vm.DisplayModes.First();
            }
            else
            {
                displayMode = vm.DisplayModes.First(r => r.Id == DisplayModeId);
            }

            DisplayModeId = displayMode.Id;
            _displayModeId = displayMode.Id;

            await LoadImages(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (DatasetId == _datasetId
            && DisplayModeId == _displayModeId
            && PageNumber == _pageNumber
            && PageSize == _pageSize)
        {
            return;
        }

        _datasetId = DatasetId;
        _displayModeId = DisplayModeId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadImages(db);
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
                await js.InvokeVoidAsync("onAfterRender");
                await js.InvokeVoidAsync("initLabelsDropdown", vm.Filter.LabelIds);
            }
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadImages(AppDbContext db)
    {
        IsLoading = true;
        StateHasChanged();

        var query = GetFilterQuery(db);

        vm.ImagesCount = query.Count();
        vm.Paging.Calculate(PageNumber, PageSize, vm.ImagesCount);

        var imageIds = await query
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();

        vm.Images = await query
            .Include(r => r.Labels)
            .Select(r => new ImageVm
            {
                Id = r.Id,
                ImageName = r.Name,
                Labels = r.Labels,
            })
            .Where(r => imageIds.Contains(r.Id))
            .ToListAsync();

        // display mode is null when leaving the page
        var displayMode = vm.DisplayModes.FirstOrDefault(r => r.Id == _displayModeId);
        if (displayMode != null)
        {
            foreach (var image in vm.Images)
            {
                image.Url = displayMode.GetImageUrl(image.ImageName, config.Value);
                image.IsFits = displayMode.IsFits();
            }
        }

        IsLoading = false;
        StateHasChanged();
    }

    private IQueryable<ImageDbe> GetFilterQuery(AppDbContext db)
    {
        var query = db.Images
            .AsNoTracking()
            .Where(r => r.DatasetId == DatasetId);

        query = vm.Filter.Apply(query);

        return query;
    }

    protected async Task OnImageChecked(bool state, ImageVm image)
    {
        try
        {
            image.IsChecked = state;
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task SetDisplayMode(int? displayModeId)
    {
        try
        {
            DisplayModeId = displayModeId;

            var uri = nav.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { "PageNumber", PageNumber },
            { "PageSize", PageSize },
            { "DisplayModeId", DisplayModeId },
        });
            nav.NavigateTo(uri);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RemoveSelectedImages()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var selected = vm.Images.Where(r => r.IsChecked).ToList();
            if (selected.Count == 0)
                return;

            var ids = selected.Select(r => r.Id).ToList();
            await db.Images.Where(r => r.DatasetId == DatasetId && ids.Contains(r.Id)).ExecuteDeleteAsync();

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = AppVm.UserId,
                DatasetId = DatasetId,
                Type = ChangeType.RemoveImages,
                Data = selected.Select(r => r.ImageName).Aggregate((a, b) => a + ";" + b)
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await LoadImages(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RemoveSearchResultImages()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var query = db.Images
                .AsNoTracking()
                .Where(r => r.DatasetId == DatasetId);

            query = vm.Filter.Apply(query);

            var count = await query.CountAsync();

            const int BATCH_SIZE = 500;
            while (true)
            {
                var imageIds = await query
                    .OrderBy(r => r.Id)
                    .Select(r => r.Id)
                    .Take(BATCH_SIZE)
                    .ToListAsync();

                if (imageIds.Count == 0)
                    break;

                await query
                    .Where(r => imageIds.Contains(r.Id))
                    .ExecuteDeleteAsync();
            }

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = AppVm.UserId,
                DatasetId = DatasetId,
                Type = ChangeType.RemoveSearchResultImages,
                Data = $"Images removed: {count}. Filter: {vm.Filter.GetDescription(vm.Labels)}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await LoadImages(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RemoveAllImages()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await DbHelper.HasActiveDatasetJobs(DatasetId, db);
            if (hasActiveJobs)
                throw new ValidationException("Please wait until all dataset jobs are completed");

            await db.Images.Where(r => r.DatasetId == DatasetId).ExecuteDeleteAsync();

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = AppVm.UserId,
                DatasetId = DatasetId,
                Type = ChangeType.RemoveAllImages,
                Data = $"Images removed: {vm.Images.Count}"
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await LoadImages(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task DownloadImage(string imageName)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            string path;
            var displayMode = await db.DisplayModes.FirstAsync(r => r.DatasetId == DatasetId && r.Id == DisplayModeId);
            if (displayMode.IsFits())
            {
                path = await db.Images.Where(r => r.DatasetId == DatasetId && r.Name == imageName).Select(r => r.Path).FirstAsync();
            }
            else
            {
                path = displayMode.GetImagePath(imageName);
            }

            var fileStream = File.OpenRead(path);

            using var streamRef = new DotNetStreamReference(stream: fileStream);

            await js.InvokeVoidAsync("downloadFileFromStream", imageName + "." + displayMode.Extension, streamRef);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task AddFileToNotes(string imageName)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            string path;
            if (DisplayModeId != null)
            {
                var displayMode = await db.DisplayModes.FirstAsync(r => r.DatasetId == DatasetId && r.Id == DisplayModeId);
                path = displayMode.GetImagePath(imageName);
            }
            else
            {
                path = await db.Images.Where(r => r.DatasetId == DatasetId && r.Name == imageName).Select(r => r.Path).FirstAsync();
            }

            var user = await db.Users.FirstAsync(r => r.Id == AppVm.UserId);

            user.AddFileToNotes(path);

            await db.SaveChangesAsync();

            await AppVm.UserNotesChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ExportToCsv()
    {
        try
        {
            var directory = config.Value.GetDatasetPaths(DatasetId).ExportsCsvDirectory;
            var filename = $"images_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_fff")}.csv";
            var file = Path.Combine(directory, filename).UnixFormat();

            Directory.CreateDirectory(directory);

            using var db = await dbf.CreateDbContextAsync();

            var export = new ExportDbe
            {
                DatasetId = DatasetId,
                Date = DateTime.UtcNow,
                UserId = AppVm.UserId,
                File = file,
                Type = ExportType.ImageListToCsv,
                Details = $"Filter: {vm.Filter.GetDescription(vm.Labels)}",
            };
            db.Exports.Add(export);

            await db.SaveChangesAsync();

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.ExportImageListToCsv(
                DatasetId,
                export.Id,
                vm.Filter,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.ExportImageListToCsv,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            export.JobId = jobId;

            var change = new ChangeDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Date = export.Date,
                Type = ChangeType.ExportImageListToCsv,
                Data = $"Filter: {vm.Filter.GetDescription(vm.Labels)}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await ShowBackgroundJobToast($"Export image list to CSV", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ExportToCaesarDataset()
    {
        try
        {
            var directory = config.Value.GetDatasetPaths(DatasetId).ExportsJsonDirectory;
            var filename = $"images_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_fff")}.json";
            var file = Path.Combine(directory, filename).UnixFormat();

            Directory.CreateDirectory(directory);

            using var db = await dbf.CreateDbContextAsync();

            var export = new ExportDbe
            {
                DatasetId = DatasetId,
                Date = DateTime.UtcNow,
                UserId = AppVm.UserId,
                File = file,
                Type = ExportType.ImageListToCaesarDataset,
                Details = $"Filter: {vm.Filter.GetDescription(vm.Labels)}",
            };
            db.Exports.Add(export);

            await db.SaveChangesAsync();

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.ExportImageListToCaesarDataset(
                DatasetId,
                export.Id,
                vm.Filter,
                DisplayModeId!.Value,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.ExportImageListToCaesarDataset,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            export.JobId = jobId;

            var change = new ChangeDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Date = export.Date,
                Type = ChangeType.ExportImageListToCaesarDataset,
                Data = $"Filter: {vm.Filter.GetDescription(vm.Labels)}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            await ShowBackgroundJobToast($"Export image list to JSON", jobId);
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
                { "DisplayModeId", DisplayModeId },
            });
            nav.NavigateTo(uri);
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
            _pageNumber = 1;

            var uri = nav.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                { "PageNumber", PageNumber },
                { "PageSize", PageSize },
                { "DisplayModeId", DisplayModeId },
            });
            nav.NavigateTo(uri);

            await js.InvokeVoidAsync("initLabelsDropdown", vm.Filter.LabelIds);
            await js.InvokeVoidAsync("hideFilterModal");

            await LoadImages(db);
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
            _pageNumber = 1;

            var uri = nav.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                { "PageNumber", PageNumber },
                { "PageSize", PageSize },
                { "DisplayModeId", DisplayModeId },
            });
            nav.NavigateTo(uri);

            await js.InvokeVoidAsync("initLabelsDropdown", vm.Filter.LabelIds);
            await js.InvokeVoidAsync("hideFilterModal");

            await LoadImages(db);
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
                return;

            bool confirmed = await js.InvokeAsync<bool>("confirm", "[Apply Label] Please confirm that you want to apply selected label to the search results.");
            if (!confirmed)
                return;

            var labelId = int.Parse(vm.SelectedLabelId);
            var label = vm.Labels.First(r => r.Id == labelId);

            vm.LabelingPercent = 0;
            vm.LabelingInProgress = true;

            using var db = await dbf.CreateDbContextAsync();

            var query = GetFilterQuery(db);

            var count = await query.CountAsync();

            var sql = new StringBuilder();
            var processed = 0;
            while (true)
            {
                var imageIds = await query
                    .AsNoTracking()
                    .Select(r => r.Id)
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
                Data = $"Label: {label.Name} Filter: {vm.Filter.GetDescription(vm.Labels)}",
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            vm.LabelingInProgress = false;

            await LoadImages(db);
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
                return;

            bool confirmed = await js.InvokeAsync<bool>("confirm", "[Apply Label] Please confirm that you want to remove selected label from the search results.");
            if (!confirmed)
                return;

            var labelId = int.Parse(vm.SelectedLabelId);
            var label = vm.Labels.First(r => r.Id == labelId);

            vm.LabelingPercent = 0;
            vm.LabelingInProgress = true;

            using var db = await dbf.CreateDbContextAsync();

            var query = GetFilterQuery(db);

            var count = await query.CountAsync();

            var processed = 0;
            while (true)
            {
                var imageIds = await query
                    .AsNoTracking()
                    .Select(r => r.Id)
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
                Data = $"Label: {label.Name} Filter: {vm.Filter.GetDescription(vm.Labels)}",
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            vm.LabelingInProgress = false;

            await LoadImages(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class ImagesPageVm
    {
        public string DatasetName { get; set; }
        public List<ImageVm> Images { get; set; }
        public Dictionary<int, LabelDbe> LabelsById { get; set; }
        public Paging Paging { get; set; }
        public int ImagesCount { get; set; }
        public List<LabelDbe> Labels { get; set; }
        public List<DisplayModeDbe> DisplayModes { get; set; }
        public ImageFilter Filter { get; set; }

        public string SelectedLabelId { get; set; }
        public bool LabelingInProgress { get; set; }
        public double LabelingPercent { get; set; }

        public ImagesPageVm()
        {
            DatasetName = "";
            Images = new List<ImageVm>();
            LabelsById = new Dictionary<int, LabelDbe>();
            Paging = new Paging();
            Labels = new List<LabelDbe>();
            DisplayModes = new List<DisplayModeDbe>();
            Filter = new ImageFilter();

            SelectedLabelId = "";
        }
    }

    protected class ImageVm
    {
        public int Id { get; set; }
        public string ImageName { get; set; } = null!;
        public List<ImageLabelDbe> Labels { get; set; } = null!;
        public string Url { get; set; } = null!;
        public bool IsFits { get; set; }
        public bool IsChecked { get; set; }

        public ImageVm()
        {
            Url = "";
        }
    }
}
