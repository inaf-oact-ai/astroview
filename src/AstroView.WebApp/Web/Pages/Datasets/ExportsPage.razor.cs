using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Data;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class ExportsPage
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

    private readonly ExportsPageVm vm;

    public ExportsPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new ExportsPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var dataset = await db.Datasets.Where(r => r.Id == DatasetId).FirstAsync();
            vm.DatasetName = dataset.Name;
            vm.DatasetModifiedDate = dataset.ModifiedDate;

            var displayModes = await db.DisplayModes.Where(r => r.DatasetId == DatasetId).ToListAsync();
            foreach (var displayMode in displayModes)
            {
                var paths = config.Value.GetDisplayModePaths(DatasetId, displayMode.Id);

                var item = new DisplayModeItem();
                item.CaesarDatasetCreatedAt = displayMode.CaesarDatasetCreatedAt;
                item.Id = displayMode.Id;
                item.Name = displayMode.Name;
                if (displayMode.CaesarDatasetJobStatus == Data.Enums.HangfireJobStatus.Completed)
                {
                    var caesarDatasetLink = paths.CaesarDatasetDownloadLink;
                    item.CaesarDatasetUrl = caesarDatasetLink;
                    item.CaesarDatasetSize = new FileInfo(paths.CaesarDatasetJson).FileSizeEx();
                }

                item.LocateDisplayModeUrl = paths.RootDirectoryLink;
                vm.DisplayModes.Add(item);
            }

            await LoadExportHistory(db);
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

            await LoadExportHistory(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RefreshExportHistory()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadExportHistory(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadExportHistory(AppDbContext db)
    {
        var count = await db.Exports.Where(r => r.DatasetId == DatasetId).CountAsync();

        vm.Paging.Calculate(PageNumber, PageSize, count);

        var exports = await db.Exports
            .Include(r => r.User)
            .AsNoTracking()
            .Where(r => r.DatasetId == DatasetId)
            .OrderByDescending(r => r.Date)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();

        var exportItems = new List<ExportItem>();
        foreach (var export in exports)
        {
            var relativePath = Path.GetRelativePath(config.Value.Storage, export.File).UnixFormat();

            var item = new ExportItem();
            item.DownloadUrl = $"/static/storage/{relativePath}";
            if (File.Exists(export.File))
            {
                var fi = new FileInfo(export.File);
                item.Size = fi.FileSizeEx();
            }
            else
            {
                item.Size = "0 Kb";
            }
            item.Filename = Path.GetFileName(export.File);
            item.Export = export;

            exportItems.Add(item);
        }

        vm.Exports = exportItems;
    }

    private class ExportsPageVm
    {
        public string DatasetName { get; set; }
        public DateTime DatasetModifiedDate { get; set; }
        public Paging Paging { get; set; }
        public List<ExportItem> Exports { get; set; }
        public List<DisplayModeItem> DisplayModes { get; set; }

        public ExportsPageVm()
        {
            DatasetName = "";

            Paging = new Paging();
            Exports = new List<ExportItem>();
            DisplayModes = new List<DisplayModeItem>();
        }
    }

    private class ExportItem
    {
        public ExportDbe Export { get; set; } = null!;
        public string DownloadUrl { get; set; } = null!;
        public string Size { get; set; } = null!;
        public string Filename { get; set; } = null!;
    }

    private class DisplayModeItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string CaesarDatasetUrl { get; set; }
        public string CaesarDatasetSize { get; set; }
        public string LocateDisplayModeUrl { get; set; } = null!;
        public DateTime? CaesarDatasetCreatedAt { get; set; }

        public DisplayModeItem()
        {
            CaesarDatasetUrl = "";
            CaesarDatasetSize = "";
        }
    }
}
