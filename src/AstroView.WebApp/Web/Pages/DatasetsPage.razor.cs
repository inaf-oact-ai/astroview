using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using AstroView.WebApp.Web.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace AstroView.WebApp.Web.Pages;

public partial class DatasetsPage
{
    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private DatasetsPageVm vm;

    public DatasetsPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new DatasetsPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadDatasets(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (PageNumber == _pageNumber
            && PageSize == _pageSize)
        {
            return;
        }

        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadDatasets(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task DisplayDatasets()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadDatasets(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadDatasets(AppDbContext db)
    {
        var query = db.Datasets
            .AsNoTracking()
            .Where(r => r.IsRemoved == false)
            .Where(r => AppVm.IsAdmin || r.UserId == AppVm.UserId || (int)r.ShareType > 1)
            .Where(r => string.IsNullOrWhiteSpace(vm.SearchTerm) || r.Name.Contains(vm.SearchTerm));

        var count = await query.CountAsync();

        vm.Paging.Calculate(PageNumber, PageSize, count);

        vm.Datasets = await query
            .Include(r => r.Options.Where(t => t.UserId == AppVm.UserId))
            .OrderByDescending(r => r.Options.First().IsFavorite)
            .ThenBy(r => r.Name)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .Select(r => new Dataset
            {
                Id = r.Id,
                Name = r.Name,
                ImagesCount = r.Images.Count(),
                ImagesWithFeatures = r.Images.Where(t => t.HasFeatures).Count(),
                CreatedDate = r.CreatedDate,
                Option = r.Options.FirstOrDefault(),
                CreatedBy = r.User.DisplayName,
                Image = r.Images.DefaultIfEmpty().FirstOrDefault(t => t!.HasFeatures),
            })
            .ToListAsync();

        foreach (var dataset in vm.Datasets)
        {
            if (dataset.Image == null) 
                continue;

            dataset.FeaturesCount = JArray.Parse(dataset.Image.Features!).Count;
        }
    }

    protected async Task ToggleFavorite(int datasetId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var userDatasetOption = await db.DatasetOptions.FirstOrDefaultAsync(r => r.UserId == AppVm.UserId && r.DatasetId == datasetId);
            if (userDatasetOption == null)
            {
                userDatasetOption = new DatasetOptionDbe
                {
                    UserId = AppVm.UserId,
                    DatasetId = datasetId,
                    IsFavorite = false,
                };
                db.DatasetOptions.Add(userDatasetOption);
            }

            userDatasetOption.IsFavorite = !userDatasetOption.IsFavorite;

            await db.SaveChangesAsync();

            await DisplayDatasets();
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

            var state = await asp.GetAuthenticationStateAsync();
            var userId = state.User.GetUserId();

            var shareType = Enum.Parse<DatasetShareType>(vm.CreateDatasetModal.ShareType);
            var displayMode = DisplayModeDbe.CreateFitsDisplayMode();
            var now = DateTime.UtcNow;
            var dataset = new DatasetDbe
            {
                Name = vm.CreateDatasetModal.Name,
                Description = vm.CreateDatasetModal.Description,
                UserId = userId,
                CreatedDate = now,
                ModifiedDate = now,
                ShareType = shareType,
                DisplayModes = new List<DisplayModeDbe> { displayMode }
            };
            db.Datasets.Add(dataset);

            var user = await db.Users.FirstAsync(r => r.Id == userId);
            user.Dataset = dataset;

            await db.SaveChangesAsync();

            AppVm.CurrentDatasetId = dataset.Id;

            await AppVm.SelectedDatasetChanged.InvokeAsync();

            await js.InvokeVoidAsync("hideCreateDatasetModal");

            nav.NavigateTo($"/Datasets/{dataset.Id}");
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
            await js.InvokeVoidAsync("showCreateDatasetModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class DatasetsPageVm
    {
        public string SearchTerm { get; set; }
        public Paging Paging { get; set; }
        public List<Dataset> Datasets { get; set; }
        public CreateDatasetModal CreateDatasetModal { get; set; }

        public DatasetsPageVm()
        {
            SearchTerm = "";
            Paging = new Paging();
            Datasets = new List<Dataset>();
            CreateDatasetModal = new CreateDatasetModal();
        }
    }

    protected class Dataset
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int ImagesCount { get; set; }
        public int ImagesWithFeatures { get; set; }
        public DateTime CreatedDate { get; set; }
        public DatasetOptionDbe? Option { get; set; }
        public string CreatedBy { get; set; } = null!;
        public ImageDbe? Image { get; set; }
        public int FeaturesCount { get; set; }
    }

    protected class CreateDatasetModal
    {
        [Required]
        public string Name { get; set; } = null!;
        public string Description { get; set; }

        [Required]
        public string ShareType { get; set; } = null!;

        public CreateDatasetModal()
        {
            Name = "";
            Description = "";
            ShareType = "1";
        }
    }
}
