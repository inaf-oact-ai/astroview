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
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;

namespace AstroView.WebApp.Web.Pages;

public partial class FileExplorerPage
{
    [Parameter]
    public string Type { get; set; } = null!;
    private string _type { get; set; } = null!;

    [Parameter]
    public string? PageRoute { get; set; }
    private string? _pageRoute { get; set; }

    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private FileExplorerPageVm vm;

    public FileExplorerPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new FileExplorerPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _type = Type;
        _pageRoute = PageRoute;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            LoadFiles();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.Equals(_type, Type)
            && string.Equals(_pageRoute, PageRoute)
            && _pageNumber == PageNumber
            && _pageSize == PageSize)
        {
            return;
        }

        _type = Type;
        _pageRoute = PageRoute;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            LoadFiles();
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

    private void LoadFiles()
    {
        var route = PageRoute ?? "";

        vm.RouteParts.Clear();
        string storageDir;
        if (Type.ToLower() == "storage")
        {
            vm.RouteParts.Add(new RoutePart { Name = "Storage", Url = "/FileExplorer/Storage" });
            var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var url = new StringBuilder("/FileExplorer/Storage/");
                for (int j = 0; j <= i; j++)
                {
                    url.Append(parts[j]);
                    url.Append('/');
                }
                url.Remove(url.Length - 1, 1);
                vm.RouteParts.Add(new RoutePart { Name = part, Url = url.ToString() });
            }

            if (vm.RouteParts.Count > 0)
            {
                vm.PageUrl = nav.ToAbsoluteUri(vm.RouteParts.Last().Url).ToString();
            }
            else
            {
                vm.PageUrl = "/Storage";
            }

            storageDir = config.Value.Storage;
        }
        else
        {
            vm.RouteParts.Add(new RoutePart { Name = "Library", Url = "/FileExplorer/Library" });
            var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var url = new StringBuilder("/FileExplorer/Library/");
                for (int j = 0; j <= i; j++)
                {
                    url.Append(parts[j]);
                    url.Append('/');
                }
                url.Remove(url.Length - 1, 1);
                vm.RouteParts.Add(new RoutePart { Name = part, Url = url.ToString() });
            }

            if (vm.RouteParts.Count > 0)
            {
                vm.PageUrl = nav.ToAbsoluteUri(vm.RouteParts.Last().Url).ToString();
            }
            else
            {
                vm.PageUrl = "/Library";
            }

            storageDir = config.Value.Library;
        }

        var currentPath = Path.GetFullPath(Path.Combine(storageDir, route));

        var count = Directory.EnumerateFileSystemEntries(currentPath).Count();

        vm.Paging.Calculate(PageNumber, PageSize, count);

        vm.Items = Directory.EnumerateFileSystemEntries(currentPath)
            .OrderByDescending(Directory.Exists)
            .ThenBy(r => r)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .Select(path =>
            {
                path = path.UnixFormat();

                if (Directory.Exists(path))
                {
                    return new Item
                    {
                        Name = Path.GetFileName(path)!,
                        Path = path,
                        UrlSuffix = Path.GetRelativePath(storageDir, path).UnixFormat(),
                        CanBeChecked = true,
                        Type = ItemType.Directory,
                    };
                }
                else
                {
                    var fi = new FileInfo(path);
                    return new Item
                    {
                        Name = Path.GetFileName(path)!,
                        Size = fi.FileSizeEx(),
                        Path = path,
                        UrlSuffix = Path.GetRelativePath(storageDir, path).UnixFormat(),
                        CanBeChecked = path.EndsWith(".fits", StringComparison.CurrentCultureIgnoreCase),
                        CanBeImported = path.EndsWith(".json", StringComparison.CurrentCultureIgnoreCase),
                        Type = ItemType.File,
                    };
                }
            })
            .ToList();

        if (currentPath.Length > storageDir.Length)
        {
            var parentDirectory = Directory.GetParent(currentPath)!.FullName;

            vm.Items.Insert(0, new Item
            {
                Name = "..",
                UrlSuffix = Path.GetRelativePath(storageDir, parentDirectory).UnixFormat(),
                IsBackButton = true,
                CanBeChecked = false,
                Type = ItemType.BackButton,
            });
        }
    }

    protected async Task ToggleAll(bool state)
    {
        try
        {
            var selectedCount = vm.Items.Where(r => r.IsChecked).Count();
            if (selectedCount == vm.Items.Count)
            {
                vm.Items.ForEach(r => r.IsChecked = false);
                vm.ToggleAllChecked = false;
            }
            else
            {
                vm.Items.ForEach(r => r.IsChecked = true);
                vm.ToggleAllChecked = true;
            }
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task AddImages()
    {
        try
        {
            vm.IsBusy = true;

            using var db = await dbf.CreateDbContextAsync();

            var hasActiveJobs = await DbHelper.HasActiveDatasetJobs(AppVm.CurrentDatasetId, db);
            if (hasActiveJobs)
                throw new ValidationException("Please wait until all dataset jobs are completed");

            var state = await asp.GetAuthenticationStateAsync();
            var userId = state.User.GetUserId();
            var datasetId = AppVm.CurrentDatasetId;
            var checkedItems = vm.Items.Where(r => r.IsChecked && !r.IsBackButton).ToList();
            var paths = checkedItems.Select(r => r.Path.UnixFormat()).ToList();

            if (checkedItems.Any(r => r.Type == ItemType.Directory))
            {
                var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.AddImages(paths, datasetId, userId, null!));

                var datasetJob = new DatasetJobDbe
                {
                    DatasetId = datasetId,
                    UserId = userId,
                    Type = Data.Enums.DatasetJobType.AddImages,
                    JobId = jobId,
                    JobStatus = Data.Enums.HangfireJobStatus.None,
                    Date = DateTime.UtcNow,
                };
                db.DatasetJobs.Add(datasetJob);

                await db.SaveChangesAsync();

                await ShowBackgroundJobToast($"Add images to dataset {AppVm.CurrentDatasetName}", jobId);
            }
            else
            {
                await DbHelper.AddImagesToDataset(paths, datasetId, userId, db);
            }

            vm.Items.ForEach(r => r.IsChecked = false);
            vm.ToggleAllChecked = false;
            vm.IsBusy = false;
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowImportFileForm(Item item)
    {
        try
        {
            vm.ImportFileModel = new ImportFileModel();
            vm.ImportFileModel.SelectedFile = item;
            await js.InvokeVoidAsync("showModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task AddFileToNotes(Item item)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var user = await db.Users.FirstAsync(r => r.Id == AppVm.UserId);

            user.AddFileToNotes(item.Path);

            await db.SaveChangesAsync();

            await AppVm.UserNotesChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ImportFile()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.ImportMetadataFromFile(
                AppVm.CurrentDatasetId,
                vm.ImportFileModel.SelectedFile.Path,
                vm.ImportFileModel.ImportFeatures,
                vm.ImportFileModel.ImportLabels,
                vm.ImportFileModel.ImportMetadata,
                vm.ImportFileModel.ExtractNameFromFilepath,
                null!));

            var datasetJob = new DatasetJobDbe
            {
                DatasetId = AppVm.CurrentDatasetId,
                UserId = AppVm.UserId,
                Type = DatasetJobType.ImportMetadataFromFile,
                JobId = jobId,
                JobStatus = HangfireJobStatus.None,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = AppVm.UserId,
                DatasetId = AppVm.CurrentDatasetId,
                Type = Data.Enums.ChangeType.ImportMetadataFromFile,
                Data = $"Import Features={vm.ImportFileModel.ImportFeatures}, " +
                $"Labels={vm.ImportFileModel.ImportLabels}, " +
                $"Metadata={vm.ImportFileModel.ImportMetadata} " +
                $"ExtractNameFromFilepath={vm.ImportFileModel.ExtractNameFromFilepath} " +
                $"from File={vm.ImportFileModel.SelectedFile.Path}"
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == AppVm.CurrentDatasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();
            
            await js.InvokeVoidAsync("hideModal");
            await ShowBackgroundJobToast($"Import metadata to dataset {AppVm.CurrentDatasetName}", jobId);
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
            var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.CreateDatasetFromFile(
                vm.ImportFileModel.DatasetName,
                vm.ImportFileModel.SelectedFile.Path,
                AppVm.UserId,
                vm.ImportFileModel.ImportFeatures,
                vm.ImportFileModel.ImportLabels,
                vm.ImportFileModel.ImportMetadata,
                null!));
            
            await js.InvokeVoidAsync("hideModal");
            await ShowBackgroundJobToast($"Create dataset {vm.ImportFileModel.DatasetName} from a file", jobId);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class FileExplorerPageVm
    {
        public List<Item> Items { get; set; }
        public bool ToggleAllChecked { get; set; }
        public bool IsBusy { get; set; }
        public ImportFileModel ImportFileModel { get; set; }
        public List<RoutePart> RouteParts { get; set; }
        public Paging Paging { get; set; }
        public string PageUrl { get; set; }

        public FileExplorerPageVm()
        {
            Items = new List<Item>();
            ImportFileModel = new ImportFileModel();
            RouteParts = new List<RoutePart>();
            Paging = new Paging();
            PageUrl = "/";
        }
    }

    protected class Item
    {
        public string Name { get; set; } = null!;
        public string Size { get; set; } = null!;
        public string Path { get; set; } = null!;
        public string UrlSuffix { get; set; } = null!;
        public bool IsBackButton { get; set; }
        public bool IsChecked { get; set; }
        public bool CanBeChecked { get; set; }
        public bool CanBeImported { get; set; }

        public ItemType Type { get; set; }
    }

    protected enum ItemType
    {
        None = 0,
        File = 1,
        Directory = 2,
        BackButton = 3,
    }

    protected class ImportFileModel
    {
        [Required]
        public string DatasetName { get; set; }
        public bool ImportMetadata { get; set; }
        public bool ImportFeatures { get; set; }
        public bool ImportLabels { get; set; }
        public bool ExtractNameFromFilepath { get; set; }
        public Item SelectedFile { get; set; }

        public ImportFileModel()
        {
            ImportMetadata = true;
            ImportFeatures = true;
            ImportLabels = true;
            ExtractNameFromFilepath = false;

            DatasetName = "";

            SelectedFile = new Item
            {
                Name = "",
            };
        }
    }

    public class RoutePart
    {
        public string Name { get; set; } = null!;
        public string Url { get; set; } = null!;
    }
}