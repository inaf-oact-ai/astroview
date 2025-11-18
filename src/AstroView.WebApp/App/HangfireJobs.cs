using AstroView.WebApp.App.Integrations.CaesarApi;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Models.Filters;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AstroView.WebApp.App;

public class HangfireJobs
{
    private readonly AppDbContext db;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly AppConfig config;

    public HangfireJobs(
        AppDbContext db,
        IOptions<AppConfig> config,
        IHttpClientFactory httpClientFactory)
    {
        this.db = db;
        this.httpClientFactory = httpClientFactory;
        this.config = config.Value;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task RemoveDisplayMode(int datasetId, int displayModeId, string userId, PerformContext context)
    {
        context.WriteLine($"Method execution started");
        context.WriteLine($"Removing Display Mode {displayModeId} in Dataset {datasetId}");

        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        try
        {
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var displayModePath = config.GetDisplayModePaths(datasetId, displayModeId).RootDirectory;

            context.WriteLine($"Removing directory {displayModePath}");

            if (Directory.Exists(displayModePath))
                Directory.Delete(displayModePath, recursive: true);

            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task RenderDisplayMode(
        int datasetId,
        int displayModeId,
        double contrast,
        double sigmaLow,
        double sigmaUp,
        bool usePil,
        bool subtractBkg,
        bool clipData,
        bool zscaleData,
        bool applyMinMax,
        string userId,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");

        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == displayModeId);
        try
        {
            displayMode.RenderJobStatus = HangfireJobStatus.Running;
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var fitsPaths = await db.Images.Where(r => r.DatasetId == datasetId).Select(r => r.Path).ToListAsync();
            var datasetPaths = config.GetDatasetPaths(datasetId);
            var fitsList = datasetPaths.FitsListTxt;

            Directory.CreateDirectory(datasetPaths.RootDirectory);

            await File.WriteAllLinesAsync(fitsList, fitsPaths);

            var imagesDir = config.GetDisplayModePaths(datasetId, displayModeId).ImagesDirectory;
            if (Directory.Exists(imagesDir))
                Directory.Delete(imagesDir, recursive: true);

            Directory.CreateDirectory(imagesDir);

            context.WriteLine("Executing fits2png.py");

            var replaceFiles = true;
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "fits2png.py");
            var cmd = $"{config.PythonExecutable} {scriptPath} {fitsList} {imagesDir} {replaceFiles} " +
                $"{contrast.ToString(CultureInfo.InvariantCulture)} {sigmaLow.ToString(CultureInfo.InvariantCulture)} {sigmaUp.ToString(CultureInfo.InvariantCulture)} {usePil} {subtractBkg} {clipData} {zscaleData} {applyMinMax}";

            var startInfo = new ProcessStartInfo();
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            if (config.IsLinux)
            {
                startInfo.FileName = "/bin/bash";
                startInfo.Arguments = $"-c \"{cmd}\"";
            }
            else
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/c \"{cmd}\"";
            }

            context.WriteLine("Executing python script");

            var process = new Process();
            process.StartInfo = startInfo;
            process.OutputDataReceived += (s, e) => context.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => context.WriteLine(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            context.WriteLine("Executing fits2png.py - done");

            displayMode.RenderJobStatus = HangfireJobStatus.Completed;
            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            displayMode.RenderJobStatus = HangfireJobStatus.Failed;
            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ImportMetadataFromFile(
        int datasetId,
        string file,
        bool importFeatures,
        bool importLabels,
        bool importMetadata,
        bool extractNameFromFilepath,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");

        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        try
        {
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var labelDbes = await db.Labels.ToListAsync();
            var datasetDbe = await db.Datasets.FirstAsync(r => r.Id == datasetId);

            context.WriteLine($"Parsing started");

            var itemsProcessed = 0;
            var serializer = new JsonSerializer();
            using (var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs))
            using (var reader = new JsonTextReader(sr))
            {
                reader.Read(); // json start
                reader.Read(); // property name
                reader.Read(); // array start

                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.StartObject)
                        continue;

                    var genericItem = serializer.Deserialize<GenericCaesarDatasetItem>(reader)!;
                    var item = genericItem.ToCaesarDatasetItem();

                    // Insert new labels
                    var distinctUpperLabels = item.label!
                        .EmptyIfNull()
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .Select(r => r.ToUpper())
                        .Distinct();

                    foreach (var upperLabel in distinctUpperLabels)
                    {
                        var labelDbe = labelDbes.FirstOrDefault(r => r.Name == upperLabel);
                        if (labelDbe == null)
                        {
                            labelDbe = new LabelDbe
                            {
                                Name = upperLabel,
                                Color = "blue",
                            };
                            db.Labels.Add(labelDbe);

                            labelDbes.Add(labelDbe);
                        }
                    }

                    ImageDbe? dsImageDbe = null;

                    var name = item.sname;
                    if (extractNameFromFilepath)
                    {
                        name = Path.GetFileNameWithoutExtension(item.filepaths[0]);
                    }

                    dsImageDbe = await db.Images
                        .Include(r => r.Labels)
                        .FirstOrDefaultAsync(r => r.DatasetId == datasetId && r.Name == name);

                    if (importFeatures)
                    {
                        if (dsImageDbe == null)
                        {
                        }
                        else
                        {
                            if (item.feats != null)
                            {
                                dsImageDbe.Features = item.feats.ToString(Formatting.None);
                                dsImageDbe.HasFeatures = true;
                            }
                        }
                    }
                    if (importLabels)
                    {
                        // This is REPLACE mode
                        if (dsImageDbe == null)
                        {
                        }
                        else
                        {
                            dsImageDbe.Labels.Clear();

                            var imageLabels = item.label!
                                .EmptyIfNull()
                                .Where(r => !string.IsNullOrWhiteSpace(r))
                                .Select(r => r.ToUpper())
                                .Distinct();
                            foreach (var upperLabel in imageLabels)
                            {
                                var labelDbe = labelDbes.First(r => r.Name == upperLabel);
                                dsImageDbe.Labels.Add(new ImageLabelDbe
                                {
                                    Label = labelDbe,
                                    Value = 0,
                                });
                            }
                        }
                    }

                    if (importMetadata)
                    {
                        if (dsImageDbe == null)
                        {
                        }
                        else
                        {
                            dsImageDbe.Telescope = item.telescope;
                            dsImageDbe.Survey = item.survey;
                            dsImageDbe.Project = item.project;
                            dsImageDbe.Nx = item.nx;
                            dsImageDbe.Ny = item.ny;
                            dsImageDbe.Dx = item.dx;
                            dsImageDbe.Dy = item.dy;
                            dsImageDbe.Ra = item.ra;
                            dsImageDbe.Dec = item.dec;
                            dsImageDbe.L = item.l;
                            dsImageDbe.B = item.b;
                            dsImageDbe.Nsources = item.nsources;

                            if (item.bkg != null && !double.IsNaN(item.bkg.Value))
                                dsImageDbe.Bkg = item.bkg;

                            if (item.rms != null && !double.IsNaN(item.rms.Value))
                                dsImageDbe.Rms = item.rms;
                        }
                    }

                    itemsProcessed++;

                    if (itemsProcessed % 500 == 0)
                    {
                        await db.SaveChangesAsync();

                        var percent = fs.Position * 1.0 / fs.Length * 100;

                        context.WriteLine($"Items processed: {itemsProcessed} ({percent:0.00}%)");
                    }
                }
            }

            datasetJob.JobStatus = HangfireJobStatus.Completed;

            await db.SaveChangesAsync();

            context.WriteLine($"Parsing completed");
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task CreateDatasetFromFile(
        string datasetName,
        string file,
        string userId,
        bool importFeatures,
        bool importLabels,
        bool importMetadata,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");
        context.WriteLine($"Creating dataset from file {file}");

        try
        {
            var displayMode = DisplayModeDbe.CreateFitsDisplayMode();

            var datasetJob = new DatasetJobDbe
            {
                UserId = userId,
                Type = DatasetJobType.ImportMetadataFromFile,
                JobId = context.BackgroundJob.Id,
                JobStatus = HangfireJobStatus.Running,
                Date = DateTime.UtcNow,
            };
            db.DatasetJobs.Add(datasetJob);

            var now = DateTime.UtcNow;
            var dataset = new DatasetDbe
            {
                Name = datasetName,
                Description = $"Created from file {file}",
                UserId = userId,
                CreatedDate = now,
                ModifiedDate = now,
                ShareType = DatasetShareType.Private,
                DisplayModes = new List<DisplayModeDbe> { displayMode },
                Jobs = new List<DatasetJobDbe> { datasetJob },
            };
            db.Datasets.Add(dataset);

            await db.SaveChangesAsync();

            context.WriteLine($"Parsing started");

            var labels = await db.Labels.ToListAsync();

            await CaesarDataset.EnumerateItems(file, async (item, index, readPercent) =>
            {
                var path = item.filepaths[0];
                if (path.EndsWith(".fits", StringComparison.CurrentCultureIgnoreCase) == false)
                    throw new Exception("Dataset can only be created from Caesar Datasets made of FITS files");

                // Insert new labels
                var distinctUpperLabels = item.label!
                    .EmptyIfNull()
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.ToUpper())
                    .Distinct();

                foreach (var upperLabel in distinctUpperLabels)
                {
                    var label = labels.FirstOrDefault(r => r.Name == upperLabel);
                    if (label == null)
                    {
                        label = new LabelDbe
                        {
                            Name = upperLabel,
                            Color = "blue",
                        };
                        db.Labels.Add(label);

                        labels.Add(label);
                    }
                }

                var name = Path.GetFileNameWithoutExtension(item.filepaths[0]);

                var image = new ImageDbe
                {
                    DatasetId = dataset.Id,
                    Name = name,
                    NameReversed = name.ReverseStr(),
                    Path = path,
                    Labels = new List<ImageLabelDbe>(),
                };
                db.Images.Add(image);

                if (importFeatures)
                {
                    if (item.feats != null)
                    {
                        image.Features = item.feats.ToString(Formatting.None);
                        image.HasFeatures = true;
                    }
                }

                if (importLabels)
                {
                    var imageLabels = item.label!
                        .EmptyIfNull()
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .Select(r => r.ToUpper())
                        .Distinct();
                    foreach (var upperLabel in imageLabels)
                    {
                        var label = labels.First(r => r.Name == upperLabel);
                        image.Labels.Add(new ImageLabelDbe
                        {
                            Label = label,
                            Value = 0,
                        });
                    }
                }

                if (importMetadata)
                {
                    image.Telescope = item.telescope;
                    image.Survey = item.survey;
                    image.Project = item.project;
                    image.Nx = item.nx;
                    image.Ny = item.ny;
                    image.Dx = item.dx;
                    image.Dy = item.dy;
                    image.Ra = item.ra;
                    image.Dec = item.dec;
                    image.L = item.l;
                    image.B = item.b;
                    image.Nsources = item.nsources;
                    image.Bkg = item.bkg;
                    image.Rms = item.rms;
                }

                if (index % 1500 == 0 && index > 0)
                {
                    await db.SaveChangesAsync();

                    context.WriteLine($"Items processed: {index} ({readPercent:0.00}%)");
                }
            });

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = userId,
                DatasetId = dataset.Id,
                Type = Data.Enums.ChangeType.ImportMetadataFromFile,
                Data = $"Import Features={importFeatures}, " +
                $"Labels={importLabels}, " +
                $"Metadata={importMetadata} " +
                $"from File={file}"
            };
            db.Changes.Add(change);

            datasetJob.JobStatus = HangfireJobStatus.Completed;

            dataset.ModifiedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();

            context.WriteLine($"Parsing completed");
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            var datasetJob = await db.DatasetJobs.FirstOrDefaultAsync(r => r.JobId == context.BackgroundJob.Id);
            if (datasetJob != null)
            {
                datasetJob.JobStatus = HangfireJobStatus.Failed;
                await db.SaveChangesAsync();
            }
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task CreateDatasetFromClusters(
        string datasetName,
        List<int> clusterIds,
        string userId,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");
        context.WriteLine($"Creating dataset from cluster IDs {clusterIds.ToListString()}");

        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        try
        {
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var displayMode = DisplayModeDbe.CreateFitsDisplayMode();
            var now = DateTime.UtcNow;
            var dataset = new DatasetDbe
            {
                Name = datasetName,
                Description = $"Created from cluster IDs: {clusterIds.ToListString()}",
                UserId = userId,
                CreatedDate = now,
                ModifiedDate = now,
                ShareType = DatasetShareType.Private,
                DisplayModes = new List<DisplayModeDbe> { displayMode }
            };
            db.Datasets.Add(dataset);
            await db.SaveChangesAsync();

            foreach (var id in clusterIds)
            {
                context.WriteLine($"Processing cluster ID {id}");

                var query = db.ClusterItems
                    .AsNoTracking()
                    .Include(r => r.Image)
                    .ThenInclude(r => r.Labels)
                    .Where(r => r.ClusterId == id)
                    .Select(r => r.Image);

                var count = await query.CountAsync();

                const int BATCH_SIZE = 250;
                var processed = 0;
                while (true)
                {
                    var imageIds = await query
                        .OrderBy(r => r.Id)
                        .Select(r => r.Id)
                        .Skip(processed)
                        .Take(BATCH_SIZE)
                        .ToListAsync();

                    if (imageIds.Count == 0)
                        break;

                    var images = await query
                        .Where(r => imageIds.Contains(r.Id))
                        .ToListAsync();

                    foreach (var image in images)
                    {
                        var imageCopy = image.CreateCopy();
                        imageCopy.DatasetId = dataset.Id;
                        db.Images.Add(imageCopy);
                    }

                    await db.SaveChangesAsync();

                    processed += imageIds.Count;

                    context.WriteLine($"Images imported: {processed} / {count}");
                }

                context.WriteLine($"Images imported: {count} / {count}");
            }

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = userId,
                DatasetId = dataset.Id,
                Type = ChangeType.CreateDatasetFromClusters,
                Data = $"Creating dataset from cluster IDs {clusterIds.ToListString()}"
            };
            db.Changes.Add(change);

            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ApplyPredictions(
        int datasetId,
        int caesarJobId,
        PredictionsImageFilter filter,
        bool replace,
        string userId,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");
        context.WriteLine($"Applying predictions from Job {caesarJobId} to Dataset {datasetId}. Replace mode: {replace}");

        try
        {
            var job = await db.CaesarJobs
                .Where(r => r.Id == caesarJobId && r.AppName == CaesarJob.MORPHOLOGY_CLASSIFIER)
                .FirstOrDefaultAsync();
            if (job == null)
                throw new Exception("MORPHOLOGY_CLASSIFIER job was not found");

            var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var query = db.Images.Where(r => r.DatasetId == datasetId);

            query = filter.ApplyToImagesQuery(query);

            var count = await query.CountAsync();

            const int BATCH_SIZE = 250;
            var processed = 0;
            while (true)
            {
                var imageIds = await query
                    .OrderBy(r => r.Id)
                    .Select(r => r.Id)
                    .Skip(processed)
                    .Take(BATCH_SIZE)
                    .ToListAsync();

                if (imageIds.Count == 0)
                {
                    context.WriteLine($"Images processed: {count} / {count}");
                    break;
                }

                var images = await query
                    .Include(r => r.Labels)
                    .Include(r => r.Predictions.Where(t => t.CaesarJobId == caesarJobId))
                    .Where(r => imageIds.Contains(r.Id))
                    .ToListAsync();

                foreach (var image in images)
                {
                    var predictions = filter.ApplyToPredictionsList(image.Predictions);
                    if (predictions.Count == 0)
                        continue;

                    var newLabels = predictions.Select(r => new ImageLabelDbe
                    {
                        ImageId = image.Id,
                        LabelId = r.LabelId,
                        Value = 0, // r.Probability, not needed
                    }).ToList();

                    if (replace)
                    {
                        image.Labels.Clear();
                    }
                    else
                    {
                        var newLabelIds = newLabels.Select(r => r.LabelId).ToList();
                        image.Labels.RemoveAll(r => newLabelIds.Contains(r.LabelId));
                    }

                    image.Labels.AddRange(newLabels);
                }

                await db.SaveChangesAsync();

                db.ChangeTracker.Clear();

                processed += imageIds.Count;

                context.WriteLine($"Images processed: {processed} / {count}");
            }

            var allLabels = await db.Labels.ToListAsync();

            var change = new ChangeDbe
            {
                Date = DateTime.UtcNow,
                UserId = userId,
                DatasetId = datasetId,
                Type = replace ? ChangeType.MergePredictionsWithReplace : ChangeType.MergePredictions,
                Data = $"Applying predictions from Job {caesarJobId} to Dataset {datasetId}. " +
                       $"Replace mode: {replace}. " +
                       $"Filter: {filter.GetDescription(allLabels)}"
            };
            db.Changes.Add(change);

            var dataset = await db.Datasets.FirstAsync(r => r.Id == datasetId);
            dataset.ModifiedDate = DateTime.UtcNow;

            datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();

            context.WriteLine($"Method execution failed: {ex}");
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task GenerateRandomFeatures(int datasetId, int dimensions, PerformContext context)
    {
        context.WriteLine($"Method execution started");
        context.WriteLine($"Generating fake features for Dataset {datasetId}");

        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        try
        {
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var imagesCount = await db.Images.Where(r => r.DatasetId == datasetId).CountAsync();

            const int batchSize = 1000;
            var featuresBuilder = new StringBuilder();
            var rand = new Random();
            var processed = 0;
            while (true)
            {
                var imageIds = await db.Images
                    .AsNoTracking()
                    .Where(r => r.DatasetId == datasetId)
                    .OrderBy(r => r.Id)
                    .Skip(processed)
                    .Take(batchSize)
                    .Select(r => r.Id)
                    .ToListAsync();

                if (imageIds.Count == 0)
                    break;

                var images = await db.Images
                    .Where(r => imageIds.Contains(r.Id))
                    .ToListAsync();

                foreach (var image in images)
                {
                    // Generate fake features for PixPlot
                    featuresBuilder.Clear();
                    featuresBuilder.Append('[');
                    for (var i = 0; i < dimensions - 1; i++)
                    {
                        featuresBuilder.AppendFormat("{0},", rand.NextDouble().ToString(CultureInfo.InvariantCulture));
                    }
                    featuresBuilder.AppendFormat("{0}", rand.NextDouble().ToString(CultureInfo.InvariantCulture));
                    featuresBuilder.Append(']');

                    image.Features = featuresBuilder.ToString();
                    image.HasFeatures = true;
                }

                await db.SaveChangesAsync();

                processed += imageIds.Count;

                context.WriteLine($"Processed: {processed} / {imagesCount}");
            }

            context.WriteLine($"Processed: {imagesCount} / {imagesCount}");

            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task GeneratePixPlotMap(
        int datasetId,
        int displayModeId,
        int minClusterSize,
        int maxClusters,
        int atlasCellSize,
        int umapNeighbors,
        double umapMinDist,
        int umapComponents,
        string umapMetric,
        double pointgridFill,
        int imageMinSize,
        int seed,
        int kmeansClusters,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");

        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == displayModeId);
        Process? process = null;
        try
        {
            displayMode.PixPlotJobStatus = HangfireJobStatus.Running;
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var featuresMissing = await db.Images
                .Where(r => r.DatasetId == datasetId)
                .AnyAsync(r => !r.HasFeatures);

            if (featuresMissing)
                throw new Exception("Error: all images must have features");

            var paths = config.GetDisplayModePaths(datasetId, displayModeId);

            var mapDirectory = paths.MapDirectory;

            Directory.CreateDirectory(mapDirectory);

            context.WriteLine($"Map directory: {mapDirectory}");

            var imagesTxt = paths.MapImagesTxt;

            context.WriteLine($"Writing images file: {imagesTxt}");

            using (var sw = File.CreateText(imagesTxt))
            {
                const int batchSize = 5000;
                var skip = 0;
                while (true)
                {
                    var imagePaths = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == datasetId)
                        .OrderBy(r => r.Id)
                        .Skip(skip)
                        .Take(batchSize)
                        .Select(r => displayMode.GetImagePath(r.Name))
                        .ToListAsync();

                    if (imagePaths.Count == 0)
                        break;

                    foreach (var path in imagePaths)
                    {
                        sw.WriteLine(path);
                    }

                    skip += batchSize;
                }
            }

            var featuresTxt = paths.MapFeaturesTxt;

            context.WriteLine($"Writing images file: {featuresTxt}");

            using (var sw = File.CreateText(featuresTxt))
            {
                sw.Write("{");

                const int batchSize = 5000;
                var isFirst = true;
                var skip = 0;
                while (true)
                {
                    var imageIds = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == datasetId)
                        .OrderBy(r => r.Id)
                        .Skip(skip)
                        .Take(batchSize)
                        .Select(r => r.Id)
                        .ToListAsync();

                    if (imageIds.Count == 0)
                        break;

                    var images = await db.Images
                        .AsNoTracking()
                        .Where(r => imageIds.Contains(r.Id))
                        .Select(r => new { Path = displayMode.GetImagePath(r.Name), Features = r.Features! })
                        .ToListAsync();

                    context.WriteLine($"Processed: {skip}");

                    foreach (var image in images)
                    {
                        if (isFirst)
                        {
                            isFirst = false;
                            sw.Write($"\n\"{image.Path}\":{image.Features}");
                        }
                        else
                        {
                            sw.Write($",\n\"{image.Path}\":{image.Features}");
                        }

                    }

                    skip += batchSize;
                }

                sw.Write("\n}");
            }

            context.WriteLine("Executing pixplot.py");

            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "pixplot.py");
            var cmd = $"{config.PythonExecutable} {scriptPath} " +
                $"--images \"{imagesTxt}\" " +
                $"--features \"{featuresTxt}\" " +
                $"--min_cluster_size \"{minClusterSize}\" " +
                $"--max_clusters \"{maxClusters}\" " +
                $"--cell_size \"{atlasCellSize}\" " +
                $"--n_neighbors \"{umapNeighbors}\" " +
                $"--min_dist \"{umapMinDist.ToString(CultureInfo.InvariantCulture)}\" " +
                $"--n_components \"{umapComponents}\" " +
                $"--metric \"{umapMetric}\" " +
                $"--pointgrid_fill \"{pointgridFill.ToString(CultureInfo.InvariantCulture)}\" " +
                $"--min_size \"{imageMinSize}\" " +
                $"--seed \"{seed}\" " +
                $"--n_clusters \"{kmeansClusters}\" " +
                $"--out_dir \"{mapDirectory}\"";

            var startInfo = new ProcessStartInfo();
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            if (config.IsLinux)
            {
                startInfo.FileName = "/bin/bash";
                startInfo.Arguments = $"-c \"{cmd}\"";
            }
            else
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/c \"{cmd}\"";
            }

            context.WriteLine("Executing python script");

            var hasErrors = false;

            process = new Process();
            process.StartInfo = startInfo;
            process.OutputDataReceived += (s, e) => context.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) =>
            {
                context.WriteLine(e.Data);

                if (e.Data != null && e.Data.Contains("ValueError"))
                {
                    hasErrors = true;
                }
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            context.WriteLine("Executing pixplot.py - done");

            displayMode.PixPlotJobStatus = hasErrors ? HangfireJobStatus.Failed : HangfireJobStatus.Completed;
            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            displayMode.PixPlotJobStatus = HangfireJobStatus.Failed;
            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }
        finally
        {
            if (process != null)
            {
                process.Kill();
                process.Dispose();
            }
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ProcessCaesarJobOutput(int jobId, PerformContext context)
    {
        context.WriteLine($"Method execution started");

        var job = await db.CaesarJobs.FirstAsync(r => r.Id == jobId);
        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        try
        {
            job.ResultJobStatus = HangfireJobStatus.Running;
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            context.WriteLine($"Caesar Job Id: {job.CaesarJobId}");
            context.WriteLine($"Caesar Job State: {job.CaesarJobState}");
            context.WriteLine($"Caesar Job Status: {job.CaesarJobStatus}");

            // Download output archive
            var jobDirectory = config.GetCaesarJobOutputPath(job.DatasetId, job.Id);
            Directory.CreateDirectory(jobDirectory);

            context.WriteLine($"Output directory: {jobDirectory}");

            var outputFile = Path.Combine(jobDirectory, "output.tar.gz").UnixFormat();
            var httpClient = httpClientFactory.CreateClient();
            var caesarApi = new CaesarApiClient(httpClient, config.CaesarApi);

            context.WriteLine($"Downloading output file: {outputFile}");

            await caesarApi.DownloadJobOutput(job.CaesarJobId!, outputFile);

            context.WriteLine("Extracting archive contents");

            // Unzip output archive
            var outputDir = Path.Combine(jobDirectory, "output").UnixFormat();

            if (Directory.Exists(outputDir))
            {
                context.WriteLine($"Clearing output directory '{outputDir}'");
                Directory.Delete(outputDir, recursive: true);
            }

            context.WriteLine($"Extracting '{outputFile}' to output directory '{outputDir}'");

            await Zipper.Unzip(outputFile, outputDir);

            var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == job.DisplayModeId);

            // Process output files
            if (job.AppName == CaesarJob.UMAP)
            {
                var files = Directory.GetFiles(outputDir, "featdata_umap.json", SearchOption.AllDirectories).ToList();
                if (files.Count != 1)
                    throw new Exception("featdata_umap.json file was not found in output");

                var featsJson = File.ReadAllText(files.First());
                var caesarDataset = JsonConvert.DeserializeObject<CaesarDataset>(featsJson)!;
                var feats = caesarDataset.data.Select(r => r.feats).ToList();
                var json = JsonConvert.SerializeObject(feats);

                var resultsDir = Path.Combine(jobDirectory, "results").UnixFormat();
                if (Directory.Exists(resultsDir))
                    Directory.Delete(resultsDir, true);

                Directory.CreateDirectory(resultsDir);

                var layoutFile = Path.Combine(resultsDir, "layout.json").UnixFormat();

                await File.WriteAllTextAsync(layoutFile, json);
            }
            else if (job.AppName == CaesarJob.HDBSCAN)
            {
                var files = Directory.GetFiles(outputDir, "featdata_hdbscan.json", SearchOption.AllDirectories).ToList();
                if (files.Count != 1)
                    throw new Exception("featdata_hdbscan.json file was not found in output");

                Dictionary<string, int> imageIndexByPathDic;
                Dictionary<string, int> imageIdByPathDic;
                if (displayMode.IsFits())
                {
                    var images = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id).Select(r => new
                        {
                            r.Id,
                            r.Path,
                        })
                        .ToListAsync();

                    imageIndexByPathDic = images
                        .Select((img, index) => new { img.Path, Index = index })
                        .ToDictionary(r => r.Path, r => r.Index);

                    imageIdByPathDic = images
                        .Select(r => new { ImageId = r.Id, r.Path })
                        .ToDictionary(r => r.Path, r => r.ImageId);
                }
                else
                {
                    var imagesTxt = config.GetDisplayModePaths(job.DatasetId, displayMode.Id).MapImagesTxt;
                    var imagePaths = await File.ReadAllLinesAsync(imagesTxt);
                    imageIndexByPathDic = imagePaths
                        .Select((path, index) => new { Path = path, Index = index })
                        .ToDictionary(r => r.Path, r => r.Index);

                    imageIdByPathDic = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .Select(r => new { ImageId = r.Id, Path = displayMode.GetImagePath(r.Name) })
                        .ToDictionaryAsync(r => r.Path, r => r.ImageId);
                }

                var featsJson = File.ReadAllText(files.First());
                var caesarDataset = JsonConvert.DeserializeObject<CaesarDataset>(featsJson)!;

                var hotspots = new List<PixPlotHotspot>();

                var clusterItemsDic = caesarDataset.data
                    .GroupBy(r => r.clust_id!.Value)
                    .ToDictionary(r => r.Key, r => r.ToList());

                await db.ClusterItems.Where(r => r.Cluster.CaesarJobId == job.Id).ExecuteDeleteAsync();
                await db.Clusters.Where(r => r.CaesarJobId == job.Id).ExecuteDeleteAsync();

                foreach (var clusterId in clusterItemsDic.Keys)
                {
                    var clusterItems = clusterItemsDic[clusterId];

                    var thumbPath = clusterItems[0].filepaths.First().UnixFormat();
                    var clusterName = "Cluster " + clusterId;

                    var hotspot = new PixPlotHotspot();
                    hotspot.images = new int[clusterItems.Count];
                    hotspot.img = thumbPath;
                    hotspot.img_name = Path.GetFileName(thumbPath);
                    hotspot.label = clusterName;
                    hotspot.layout = "umap";
                    hotspots.Add(hotspot);

                    var clusterDbe = new ClusterDbe();
                    clusterDbe.Index = clusterId;
                    clusterDbe.CaesarJobId = job.Id;
                    clusterDbe.Name = clusterName;
                    clusterDbe.Items = new List<ClusterItemDbe>();
                    db.Clusters.Add(clusterDbe);

                    for (var i = 0; i < clusterItems.Count; i++)
                    {
                        var clusterItem = clusterItems[i];

                        var imagePath = clusterItem.filepaths.First().UnixFormat();
                        hotspot.images[i] = imageIndexByPathDic[imagePath];

                        var clusterItemDbe = new ClusterItemDbe();
                        clusterItemDbe.ImageId = imageIdByPathDic[imagePath];
                        var clusterProb = clusterItem.clust_prob!.Value;
                        var clusterOutlierScore = clusterItem.clust_outlier_score!.Value;

                        clusterItemDbe.Probability = double.IsNaN(clusterProb) ? 0 : clusterProb;
                        clusterItemDbe.OutlierScore = double.IsNaN(clusterOutlierScore) ? 0 : clusterOutlierScore;
                        clusterDbe.Items.Add(clusterItemDbe);
                    }
                }
                var json = JsonConvert.SerializeObject(hotspots);

                var resultsDir = Path.Combine(jobDirectory, "results");
                Directory.CreateDirectory(resultsDir);
                var hotspotsFile = Path.Combine(resultsDir, "hotspots.json");
                await File.WriteAllTextAsync(hotspotsFile, json);

                await db.SaveChangesAsync();
            }
            else if (job.AppName == CaesarJob.OUTLIER_FINDER)
            {
                var files = Directory.GetFiles(outputDir, "outlier_data.json", SearchOption.AllDirectories).ToList();
                if (files.Count != 1)
                    throw new Exception("outlier_data.json file was not found in output");

                Dictionary<string, int> dsImages;

                if (displayMode.IsFits())
                {
                    dsImages = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id)
                        .Select(r => new { r.Path, r.Id })
                        .ToDictionaryAsync(r => r.Path, r => r.Id);
                }
                else
                {
                    dsImages = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id)
                        .Select(r => new { Path = displayMode.GetImagePath(r.Name), r.Id })
                        .ToDictionaryAsync(r => r.Path, r => r.Id);
                }

                await db.Outliers.Where(r => r.CaesarJobId == job.Id).ExecuteDeleteAsync();

                var outliers = new List<OutlierDbe>();

                var file = files.First();
                await CaesarDataset.EnumerateItems(file, async (item, index, readPercent) =>
                {
                    var imageId = dsImages[item.filepaths.First()];
                    var outlier = new OutlierDbe
                    {
                        CaesarJobId = job.Id,
                        ImageId = imageId,
                        IsOutlier = item.is_outlier!.Value == 1,
                        Score = item.outlier_score!.Value,
                    };
                    outliers.Add(outlier);

                    if (index % 1000 == 0 && index > 0)
                    {
                        context.WriteLine($"Items processed: {index} ({readPercent:0.00}%)");

                        await db.SaveChangesAsync();

                        db.ChangeTracker.Clear();
                    }
                });

                await db.Outliers.AddRangeAsync(outliers);

                await db.SaveChangesAsync();
            }
            else if (job.AppName == CaesarJob.SIMILARITY_SEARCH)
            {
                var files = Directory.GetFiles(outputDir, "featdata_simsearch.json", SearchOption.AllDirectories).ToList();
                if (files.Count != 1)
                    throw new Exception("featdata_simsearch.json file was not found in output");

                List<DatasetImg> dsImages;
                if (displayMode.IsFits())
                {
                    dsImages = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id)
                        .Select(r => new DatasetImg { Id = r.Id, ImagePath = r.Path, ImageName = r.Name })
                        .ToListAsync();
                }
                else
                {
                    dsImages = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id)
                        .Select(r => new DatasetImg { Id = r.Id, ImagePath = displayMode.GetImagePath(r.Name), ImageName = r.Name })
                        .ToListAsync();
                }

                var imagesByPathDic = dsImages.ToDictionary(r => r.ImagePath, r => r);

                var dsImagesByIndexDic = dsImages
                    .Select((r, index) => new { Image = r, Index = index })
                    .ToDictionary(r => r.Index, r => r.Image);

                await db.Similars.Where(r => r.CaesarJobId == job.Id).ExecuteDeleteAsync();

                var file = files.First();
                await CaesarDataset.EnumerateItems(file, async (item, index, readPercent) =>
                {
                    var similarImages = new List<SimilarImage>();
                    for (var i = 0; i < item.nn_indices!.Length; i++)
                    {
                        var dsIndex = item.nn_indices[i];
                        var image = dsImagesByIndexDic[dsIndex];
                        var score = item.nn_scores![i];
                        var simg = new SimilarImage();
                        simg.ImageId = image.Id;
                        simg.ImageName = image.ImageName;
                        simg.Score = double.IsNaN(score) ? 0 : score;
                        similarImages.Add(simg);
                    }
                    var json = JsonConvert.SerializeObject(similarImages);

                    var similarDbe = new SimilarDbe();
                    similarDbe.CaesarJobId = job.Id;
                    similarDbe.ImageId = imagesByPathDic[item.filepaths.First()].Id;
                    similarDbe.Json = json;
                    similarDbe.HighestScore = similarImages.Max(r => r.Score);
                    db.Similars.Add(similarDbe);

                    if (index % 1000 == 0 && index > 0)
                    {
                        context.WriteLine($"Items processed: {index} ({readPercent:0.00}%)");

                        await db.SaveChangesAsync();

                        db.ChangeTracker.Clear();
                    }
                });

                await db.SaveChangesAsync();
            }
            else if (job.AppName == CaesarJob.INDIVIDUAL_SIMILARITY_SEARCH)
            {
                var files = Directory.GetFiles(outputDir, "featdata_simsearch.json", SearchOption.AllDirectories).ToList();
                if (files.Count != 1)
                    throw new Exception("featdata_simsearch.json file was not found in output");

                List<DatasetImg> dsImages;
                if (displayMode.IsFits())
                {
                    dsImages = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id)
                        .Select(r => new DatasetImg { Id = r.Id, ImagePath = r.Path, ImageName = r.Name })
                        .ToListAsync();
                }
                else
                {
                    dsImages = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id)
                        .Select(r => new DatasetImg { Id = r.Id, ImagePath = displayMode.GetImagePath(r.Name), ImageName = r.Name })
                        .ToListAsync();
                }

                var dsImagesByPathDic = dsImages.ToDictionary(r => r.ImagePath, r => r);

                var dsImagesByIndexDic = dsImages
                    .Select((r, index) => new { Image = r, Index = index })
                    .ToDictionary(r => r.Index, r => r.Image);

                await db.IndividualSimilars.Where(r => r.CaesarJobId == job.Id).ExecuteDeleteAsync();

                var similarImages = new List<SimilarImage>();

                var file = files.First();
                await CaesarDataset.EnumerateItems(file, async (item, index, readPercent) =>
                {
                    var dsIndex = item.nn_index!.Value;
                    var dsImage = dsImagesByIndexDic[dsIndex];
                    var simg = new SimilarImage();
                    simg.ImageId = dsImage.Id;
                    simg.ImageName = dsImage.ImageName;
                    simg.Score = item.nn_score!.Value;
                    similarImages.Add(simg);

                    if (index % 1000 == 0 && index > 0)
                    {
                        context.WriteLine($"Items processed: {index} ({readPercent:0.00}%)");

                        await db.SaveChangesAsync();

                        db.ChangeTracker.Clear();
                    }
                });

                var json = JsonConvert.SerializeObject(similarImages);

                var filePath = job.RequestJson.Split(" | ")[0];

                var similarDbe = new IndividualSimilarDbe();
                similarDbe.CaesarJobId = job.Id;
                similarDbe.FilePath = filePath;
                similarDbe.Json = json;
                db.IndividualSimilars.Add(similarDbe);

                await db.SaveChangesAsync();
            }
            else if (job.AppName == CaesarJob.MORPHOLOGY_CLASSIFIER)
            {
                var files = Directory.GetFiles(outputDir, "classifier_results.json", SearchOption.AllDirectories).ToList();
                if (files.Count != 1)
                    throw new Exception("classifier_results.json file was not found in output");

                Dictionary<string, int> dsImages;
                if (displayMode.IsFits())
                {
                    dsImages = await db.Images
                        .AsNoTracking()
                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id)
                        .Select(r => new { r.Path, r.Id })
                        .ToDictionaryAsync(r => r.Path, r => r.Id);
                }
                else
                {
                    dsImages = await db.Images
                        .AsNoTracking()

                        .Where(r => r.DatasetId == job.DatasetId)
                        .OrderBy(r => r.Id)
                        .Select(r => new { Path = displayMode.GetImagePath(r.Name), r.Id })
                        .ToDictionaryAsync(r => r.Path, r => r.Id);
                }

                await db.Predictions.Where(r => r.CaesarJobId == job.Id).ExecuteDeleteAsync();

                var labels = await db.Labels.ToListAsync();
                var random = new Random();

                var file = files.First();
                await CaesarDataset.EnumerateItems(file, async (item, index, readPercent) =>
                {
                    if (item.label_pred == null)
                        return;

                    for (var i = 0; i < item.label_pred.Length; i++)
                    {
                        var labelName = item.label_pred[i].ToUpper();
                        var prob = item.prob_pred![i];

                        var label = labels.FirstOrDefault(r => r.Name == labelName);
                        if (label == null)
                        {
                            label = new LabelDbe
                            {
                                Name = labelName,
                                Color = Defaults.Colors[random.Next(Defaults.Colors.Count)].ToLower(),
                            };
                            db.Labels.Add(label);

                            await db.SaveChangesAsync();
                        }

                        var pred = new PredictionDbe();
                        pred.CaesarJobId = job.Id;
                        pred.ImageId = dsImages[item.filepaths[0]];
                        pred.LabelId = label.Id;
                        pred.Probability = prob;

                        db.Predictions.Add(pred);
                    }

                    if (index % 1000 == 0 && index > 0)
                    {
                        context.WriteLine($"Items processed: {index} ({readPercent:0.00}%)");

                        await db.SaveChangesAsync();

                        db.ChangeTracker.Clear();
                    }
                });

                await db.SaveChangesAsync();
            }
            else
            {
                throw new Exception("Unsupported Caesar app: " + job.AppName);
            }

            // Querying entities again since we clear change tracker

            job = await db.CaesarJobs.FirstAsync(r => r.Id == jobId);
            job.ResultJobStatus = HangfireJobStatus.Completed;
            job.FinishedDate = DateTime.UtcNow;

            datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
            datasetJob.JobStatus = HangfireJobStatus.Completed;

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            job.ResultJobStatus = HangfireJobStatus.Failed;
            job.FinishedDate = DateTime.UtcNow;
            job.Error = "Failed to process output: " + ex;
            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task AddImages(List<string> paths, int datasetId, string userId, PerformContext context)
    {
        context.WriteLine($"Method execution started");

        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        try
        {
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            await DbHelper.AddImagesToDataset(paths, datasetId, userId, db, imagesProcessed =>
            {
                context.WriteLine($"Images processed: {imagesProcessed}");
            });

            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task GenerateCaesarDataset(
        int datasetId,
        int displayModeId,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");

        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == displayModeId);
        try
        {
            displayMode.CaesarDatasetJobStatus = HangfireJobStatus.Running;
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var query = db.Images.AsNoTracking().Where(r => r.DatasetId == datasetId);
            var outputFile = config.GetDisplayModePaths(datasetId, displayModeId).CaesarDatasetJson;

            await GenerateCaesarDataset(query, displayMode, outputFile, context);

            displayMode.CaesarDatasetJobStatus = HangfireJobStatus.Completed;
            displayMode.CaesarDatasetCreatedAt = DateTime.UtcNow;
            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            displayMode.CaesarDatasetJobStatus = HangfireJobStatus.Failed;
            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExportImageListToCsv(
        int datasetId,
        int exportId,
        ImageFilter filter,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");

        var export = await db.Exports.FirstAsync(r => r.Id == exportId);
        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        try
        {
            export.JobStatus = HangfireJobStatus.Running;
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var labels = await db.Labels.ToListAsync();
            var filterDescription = filter.GetDescription(labels);
            context.WriteLine($"Exporting image list to CSV (tab-separated). Filter: {filterDescription}");

            var displayModes = await db.DisplayModes.Where(r => r.DatasetId == datasetId).ToListAsync();
            var query = db.Images
                .AsNoTracking()
                .Where(r => r.DatasetId == datasetId);
            query = filter.Apply(query);
            var count = await query.CountAsync();
            var builder = new StringBuilder();
            using (var writer = File.CreateText(export.File))
            {
                writer.WriteLine("sep=\t");
                writer.Write($"Name");
                writer.Write($"\tFITS");
                writer.Write($"\tTelescope");
                writer.Write($"\tSurvey");
                writer.Write($"\tProject");
                writer.Write($"\tNx");
                writer.Write($"\tNy");
                writer.Write($"\tDx");
                writer.Write($"\tDy");
                writer.Write($"\tRa");
                writer.Write($"\tDec");
                writer.Write($"\tL");
                writer.Write($"\tB");
                writer.Write($"\tNSources");
                writer.Write($"\tBkg");
                writer.Write($"\tRms");
                writer.Write($"\tFeatures");

                foreach (var displayMode in displayModes)
                {
                    writer.Write($"\t{displayMode.Name}");
                }
                writer.WriteLine();

                const int BATCH_SIZE = 500;
                var processed = 0;
                while (true)
                {
                    var imageIds = await query
                        .OrderBy(r => r.Id)
                        .Select(r => r.Id)
                        .Skip(processed)
                        .Take(BATCH_SIZE)
                        .ToListAsync();

                    if (imageIds.Count == 0)
                        break;

                    var images = await query
                        .Include(r => r.Labels)
                        .Where(r => imageIds.Contains(r.Id))
                        .ToListAsync();

                    foreach (var image in images)
                    {
                        builder.Clear();
                        builder.Append(image.Name);
                        builder.Append($"\t{image.Path}");
                        builder.Append($"\t{image.Telescope}");
                        builder.Append($"\t{image.Survey}");
                        builder.Append($"\t{image.Project}");
                        builder.Append($"\t{image.Nx}");
                        builder.Append($"\t{image.Ny}");
                        builder.Append($"\t{image.Dx}");
                        builder.Append($"\t{image.Dy}");
                        builder.Append($"\t{image.Ra}");
                        builder.Append($"\t{image.Dec}");
                        builder.Append($"\t{image.L}");
                        builder.Append($"\t{image.B}");
                        builder.Append($"\t{image.Nsources}");
                        builder.Append($"\t{image.Bkg}");
                        builder.Append($"\t{image.Rms}");
                        builder.Append($"\t{image.Features}");
                        foreach (var displayMode in displayModes)
                        {
                            builder.Append($"\t{displayMode.GetImagePath(image.Name)}");
                        }

                        writer.WriteLine(builder.ToString());
                    }

                    processed += imageIds.Count;

                    context.WriteLine($"Processed: {processed} / {count}");
                }

                context.WriteLine($"Processed: {count} / {count}");

                export.JobStatus = HangfireJobStatus.Completed;
                datasetJob.JobStatus = HangfireJobStatus.Completed;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            export.JobStatus = HangfireJobStatus.Failed;
            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExportImageListToCaesarDataset(
        int datasetId,
        int exportId,
        ImageFilter filter,
        int displayModeId,
        PerformContext context)
    {
        context.WriteLine($"Method execution started");

        var export = await db.Exports.FirstAsync(r => r.Id == exportId);
        var datasetJob = await db.DatasetJobs.FirstAsync(r => r.JobId == context.BackgroundJob.Id);
        try
        {
            export.JobStatus = HangfireJobStatus.Running;
            datasetJob.JobStatus = HangfireJobStatus.Running;
            await db.SaveChangesAsync();

            var labels = await db.Labels.ToListAsync();
            var filterDescription = filter.GetDescription(labels);
            context.WriteLine($"Exporting image list to JSON (Caesar Dataset). Filter: {filterDescription}");

            var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == displayModeId);
            var query = db.Images
                .AsNoTracking()
                .Where(r => r.DatasetId == datasetId);
            query = filter.Apply(query);
            await GenerateCaesarDataset(query, displayMode, export.File, context);

            export.JobStatus = HangfireJobStatus.Completed;
            datasetJob.JobStatus = HangfireJobStatus.Completed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.WriteLine($"Method execution failed: {ex}");

            export.JobStatus = HangfireJobStatus.Failed;
            datasetJob.JobStatus = HangfireJobStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }

        context.WriteLine($"Method execution ended");
    }

    private async Task GenerateCaesarDataset(
        IQueryable<ImageDbe> query,
        DisplayModeDbe displayMode,
        string outputFile,
        PerformContext context)
    {
        var count = await query.CountAsync();

        if (File.Exists(outputFile))
        {
            context.WriteLine($"Deleting existing file: {outputFile}");
            File.Delete(outputFile);
        }

        context.WriteLine($"Writing {count} images to Caesar Dataset: {outputFile}");

        var outputFileDirectory = Path.GetDirectoryName(outputFile)!;
        Directory.CreateDirectory(outputFileDirectory);

        var serializer = new JsonSerializer();
        using (var fs = File.Open(outputFile, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var sw = new StreamWriter(fs))
        using (var writer = new JsonTextWriter(sw))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("data");
            writer.WriteStartArray();

            const int batchSize = 1000;
            var processed = 0;
            while (true)
            {
                var imageIds = await query
                        .OrderBy(r => r.Id)
                        .Skip(processed)
                        .Take(batchSize)
                        .Select(r => r.Id)
                        .ToListAsync();

                if (imageIds.Count == 0)
                    break;

                var images = await db.Images
                    .Include(r => r.Labels).ThenInclude(r => r.Label)
                    .Where(r => imageIds.Contains(r.Id))
                    .ToListAsync();

                var items = new List<CaesarDatasetItem>();
                foreach (var image in images)
                {
                    var path = displayMode.IsFits() ? image.Path : displayMode.GetImagePath(image.Name);
                    var item = new CaesarDatasetItem
                    {
                        filepaths = new[] { path },
                        sname = image.Name,
                        // id - perhaps there is no sense to write intenal IDs here
                        label = image.Labels.Select(r => r.Label.Name).ToArray(),
                        telescope = image.Telescope,
                        survey = image.Survey,
                        project = image.Project,
                        nx = image.Nx,
                        ny = image.Ny,
                        dx = image.Dx,
                        dy = image.Dy,
                        ra = image.Ra,
                        dec = image.Dec,
                        l = image.L,
                        b = image.B,
                        nsources = image.Nsources,
                        bkg = image.Bkg,
                        rms = image.Rms,
                        feats = JToken.Parse(image.Features!),
                    };
                    items.Add(item);
                }

                foreach (var item in items)
                {
                    serializer.Serialize(writer, item);
                }

                processed += imageIds.Count;

                context.WriteLine($"Images processed: {processed} / {count}");
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        context.WriteLine($"Images processed: {count} / {count}");
    }
}

public class SimilarImage
{
    public int ImageId { get; set; }
    public string ImageName { get; set; } = null!;
    public double Score { get; set; }
}

public class DatasetImg
{
    public int Id { get; set; }
    public string ImagePath { get; set; } = null!;
    public string ImageName { get; set; } = null!;
}