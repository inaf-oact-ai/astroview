using AstroView.WebApp.App;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Newtonsoft.Json.Linq;
using System.Reflection.Emit;
using System.Text;
using static AstroView.WebApp.Web.Pages.Functions.UmapPage;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class ImagePage
{
    [Parameter]
    public int DatasetId { get; set; }
    public int _datasetId { get; set; }

    [Parameter]
    public int ImageId { get; set; }
    public int _imageId { get; set; }

    private readonly ImagePageVm vm;

    public ImagePage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new ImagePageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _imageId = ImageId;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetName = await db.Datasets.Where(r => r.Id == DatasetId).Select(r => r.Name).FirstAsync();

            vm.Labels = await db.Labels.OrderBy(r => r.Name).ToListAsync();

            await LoadImage(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (DatasetId == _datasetId
            && ImageId == _imageId)
        {
            return;
        }

        _datasetId = DatasetId;
        _imageId = ImageId;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadImage(db);
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
            if (vm.LabelEditModeEnabled)
            {
                var activeLabelIds = vm.Image.Labels.Select(r => r.LabelId.ToString()).ToList();

                await js.InvokeVoidAsync("initLabelsDropdown", activeLabelIds);
            }
            else
            {
                await js.InvokeVoidAsync("destroyLabelsDropdown");
            }
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task EnableLabelEditMode()
    {
        try
        {
            vm.LabelEditModeEnabled = true;
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task DisableLabelEditMode()
    {
        try
        {
            vm.LabelEditModeEnabled = false;
        }
        catch (Exception ex)
        {

            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task SaveLabels()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var stringIds = await js.InvokeAsync<List<string>>("getLabelsDropdownValues");
            var ids = stringIds.Select(int.Parse).ToList();

            var hasChanges = false;

            var log = new StringBuilder($"Editing labels of {vm.Image.Name}. ");

            var labels = await db.Labels.ToListAsync();
            var imageLabels = await db.ImageLabels
                .Include(r => r.Label)
                .Where(r => r.ImageId == vm.Image.Id)
                .ToListAsync();

            foreach (var imageLabel in imageLabels)
            {
                if (ids.Contains(imageLabel.LabelId))
                {
                    // keep
                }
                else
                {
                    // remove label
                    db.ImageLabels.Remove(imageLabel);

                    log.Append($"Label removed: {imageLabel.Label.Name}; ");
                    hasChanges = true;
                }
            }

            foreach (var id in ids)
            {
                if (imageLabels.Any(r => r.LabelId == id))
                {
                    // keep
                }
                else
                {
                    // add label
                    var imageLabel = new ImageLabelDbe
                    {
                        ImageId = vm.Image.Id,
                        LabelId = id,
                        Value = 0,
                    };
                    db.ImageLabels.Add(imageLabel);

                    var labelName = labels.First(r => r.Id == id);
                    log.Append($"Label added: {labelName}; ");
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                var change = new ChangeDbe
                {
                    DatasetId = DatasetId,
                    UserId = AppVm.UserId,
                    Date = DateTime.UtcNow,
                    Type = ChangeType.BulkRemoveLabel,
                    Data = log.ToString(),
                };
                db.Changes.Add(change);

                var dataset = await db.Datasets.FirstAsync(r => r.Id == DatasetId);
                dataset.ModifiedDate = DateTime.UtcNow;

                await db.SaveChangesAsync();
            }

            vm.LabelEditModeEnabled = false;

            await js.InvokeVoidAsync("destroyLabelsDropdown");

            await LoadImage(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task DownloadImage()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var image = await db.Images.Where(r => r.DatasetId == DatasetId && r.Id == ImageId).FirstAsync();

            var fileStream = File.OpenRead(image.Path);

            using var streamRef = new DotNetStreamReference(stream: fileStream);

            await js.InvokeVoidAsync("downloadFileFromStream", image.Name + ".fits", streamRef);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadImage(AppDbContext db)
    {
        vm.Image = await db.Images
            .AsNoTracking()
            .Include(r => r.Labels).ThenInclude(r => r.Label)
            .FirstAsync(r => r.Id == ImageId && r.DatasetId == DatasetId);

        vm.Image.Labels = vm.Image.Labels.OrderBy(r => r.Label.Name).ToList();

        vm.FeaturesCount = JArray.Parse(vm.Image.Features ?? "[]").Count;

        var renders = new List<RenderVm>();

        var displayModes = await db.DisplayModes
            .AsNoTracking()
            .Where(r => r.DatasetId == DatasetId)
            .ToListAsync();

        foreach (var displayMode in displayModes)
        {
            if (displayMode.IsFits())
                continue;

            renders.Add(new RenderVm
            {
                ImageUrl = displayMode.GetImageUrl(vm.Image.Name, config.Value),
                DisplayMode = displayMode,
            });
        }

        vm.Renders = renders;
    }

    private class ImagePageVm
    {
        public bool LabelEditModeEnabled { get; set; }
        public string DatasetName { get; set; } = null!;
        public ImageDbe Image { get; set; }
        public List<LabelDbe> Labels { get; set; }
        public List<RenderVm> Renders { get; set; }
        public int FeaturesCount { get; set; }

        public ImagePageVm()
        {
            Image = new ImageDbe
            {
                Labels = new List<ImageLabelDbe>(),
            };
            Labels = new List<LabelDbe>();
            Renders = new List<RenderVm>();
        }
    }

    private class RenderVm
    {
        public string ImageUrl { get; set; } = null!;
        public DisplayModeDbe DisplayMode { get; set; } = null!;
    }
}
