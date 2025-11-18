using AstroView.WebApp.App;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using AstroView.WebApp.Web.Layout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Data;
using System.Reflection.Emit;
using System.Text;
using System.Xml.Linq;

namespace AstroView.WebApp.Web.Api;

/*
 * Look for additional API controllers:
 * - CaesarApiMockController
 */

[Authorize]
[ApiController]
public class ApiController : Controller
{
    private readonly AppMemoryCache amc;
    private readonly ILogger<ApiController> _logger;
    private readonly SignInManager<UserDbe> signInManager;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IOptions<AppConfig> appConfig;
    private readonly AppDbContext db;

    public ApiController(
        AppMemoryCache amc,
        ILogger<ApiController> logger,
        SignInManager<UserDbe> signInManager,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AppConfig> appConfig,
        AppDbContext db)
    {
        this.amc = amc;
        this._logger = logger;
        this.signInManager = signInManager;
        this.httpContextAccessor = httpContextAccessor;
        this.appConfig = appConfig;
        this.db = db;
    }

    [HttpPost("api/logout")]
    public async Task Logout()
    {
        await signInManager.SignOutAsync();
    }

    [HttpGet("api/back-to-site")]
    public IActionResult BackToSite()
    {
        var userId = httpContextAccessor.HttpContext!.User.GetUserId();

        var lastVisitedPage = amc.GetUserLastVisitedPage(userId);
        if (lastVisitedPage == null)
        {
            return Redirect($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}");
        }
        else
        {
            return Redirect(lastVisitedPage);
        }
    }

    [HttpPost("api/datasets")]
    public async Task<int> CreateDataset([FromBody] CreateDatasetRequest request)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetUserId();

        var displayMode = DisplayModeDbe.CreateFitsDisplayMode();
        var now = DateTime.UtcNow;
        var dataset = new DatasetDbe
        {
            Name = request.Name,
            Description = "Created from PixPlot lasso",
            UserId = userId,
            CreatedDate = now,
            ModifiedDate = now,
            ShareType = DatasetShareType.Private,
            DisplayModes = new List<DisplayModeDbe> { displayMode }
        };
        db.Datasets.Add(dataset);

        await db.SaveChangesAsync();

        return dataset.Id;
    }

    [HttpPost("api/datasets/{datasetId}/add-images")]
    public async Task AddImagesToDataset([FromBody] AddImagesRequest request, [FromRoute] int datasetId)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetUserId();

        var dataset = await db.Datasets.FirstAsync(r => r.Id == datasetId);
        
        if (dataset.ShareType != DatasetShareType.ReadWrite && dataset.UserId != userId)
            throw new Exception("Permission denied");
        
        if (dataset.IsLocked)
            throw new Exception("Dataset is locked");

        var processed = 0;
        while (true)
        {
            var imageNames = request.imageNames
                .Skip(processed)
                .Take(Defaults.LabelingBatchSize)
                .ToList();

            if (imageNames.Count == 0)
                break;

            var images = await db.Images
                .AsNoTracking()
                .Include(r => r.Labels)
                .Where(r => r.DatasetId == request.SourceDatasetId && imageNames.Contains(r.Name))
                .ToListAsync();

            var imageCopies = images.Select(r => r.CreateCopy()).ToList();
            imageCopies.ForEach(r => r.DatasetId = datasetId);
            db.Images.AddRange(imageCopies);

            var change = new ChangeDbe
            {
                DatasetId = datasetId,
                UserId = userId,
                Date = DateTime.UtcNow,
                Type = ChangeType.AddImage,
                Data = $"Images added from PixPlot lasso selection: {imageNames.Count}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            processed += Defaults.LabelingBatchSize;
        }
    }

    [HttpGet("api/labels")]
    public async Task<List<LabelDbe>> GetLabels()
    {
        var labels = await db.Labels
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync();
        return labels;
    }

    [HttpGet("api/datasets/{datasetId}/image-labels")]
    public async Task<List<ImageLabelDto>> GetDatasetLabels(int datasetId, [FromQuery] int displayModeId)
    {
        var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == displayModeId);

        var imageLabels = await db.ImageLabels
            .Where(r => r.Image.DatasetId == datasetId)
            .Select(r => new ImageLabelDto
            {
                ImageName = displayMode.ImagesPath + "/" + r.Image.Name + "." + displayMode.Extension,
                LabelName = r.Label.Name,
            })
            .ToListAsync();
        return imageLabels;
    }

    [HttpPost("api/datasets/{datasetId}/image-labels/apply")]
    public async Task ApplyLabel(int datasetId, [FromBody] LabelImagesDto request)
    {
        var userId = HttpContext.User.GetUserId();
        var label = await db.Labels.FirstAsync(r => r.Id == request.LabelId);

        var processed = 0;
        var sql = new StringBuilder();
        while (true)
        {
            var images = request.ImageNames.Skip(processed).Take(Defaults.LabelingBatchSize).ToList();
            if (images.Count == 0)
                break;

            sql.Clear();
            var imageIds = await db.Images
                .AsNoTracking()
                .Where(r => r.DatasetId == datasetId && images.Contains(r.Name))
                .Select(r => r.Id)
                .ToListAsync();

            foreach (var imageId in imageIds)
            {
                sql.AppendLine(DbHelper.GetInsertLabelSql(imageId, request.LabelId, 0));
            }

            await db.Database.ExecuteSqlRawAsync(sql.ToString());

            processed += Defaults.LabelingBatchSize;
        }

        var change = new ChangeDbe
        {
            DatasetId = datasetId,
            UserId = userId,
            Date = DateTime.UtcNow,
            Type = ChangeType.PixPlotApplyLabel,
            Data = $"Label: {label.Name} Images: {request.ImageNames.Count}",
        };
        db.Changes.Add(change);

        var dataset = await db.Datasets.FirstAsync(r => r.Id == datasetId);
        dataset.ModifiedDate = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    [HttpPost("api/datasets/{datasetId}/image-labels/remove")]
    public async Task RemoveLabel(int datasetId, [FromBody] LabelImagesDto request)
    {
        var userId = HttpContext.User.GetUserId();
        var label = await db.Labels.FirstAsync(r => r.Id == request.LabelId);

        var processed = 0;
        while (true)
        {
            var images = request.ImageNames.Skip(processed).Take(Defaults.LabelingBatchSize).ToList();
            if (images.Count == 0)
                break;

            var imageIds = await db.Images
                .AsNoTracking()
                .Where(r => r.DatasetId == datasetId && images.Contains(r.Name))
                .Select(r => r.Id)
                .ToListAsync();

            await db.ImageLabels
                .Where(r => r.LabelId == request.LabelId && imageIds.Contains(r.Image.Id))
                .ExecuteDeleteAsync();

            processed += Defaults.LabelingBatchSize;
        }

        var change = new ChangeDbe
        {
            DatasetId = datasetId,
            UserId = userId,
            Date = DateTime.UtcNow,
            Type = ChangeType.PixPlotRemoveLabel,
            Data = $"Label: {label.Name} Images: {request.ImageNames.Count}",
        };
        db.Changes.Add(change);

        var dataset = await db.Datasets.FirstAsync(r => r.Id == datasetId);
        dataset.ModifiedDate = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}

public class CreateDatasetRequest
{
    public string Name { get; set; } = null!;
}

public class AddImagesRequest
{
    public int SourceDatasetId { get; set; }
    public List<string> imageNames { get; set; } = null!;
}

public class ImageLabelDto
{
    public string ImageName { get; set; } = null!;
    public string LabelName { get; set; } = null!;
}

public class LabelImagesDto
{
    public int LabelId { get; set; }
    public List<string> ImageNames { get; set; } = null!;
}