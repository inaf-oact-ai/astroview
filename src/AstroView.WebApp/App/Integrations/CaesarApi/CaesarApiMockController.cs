using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Enums;
using AstroView.WebApp.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using static AstroView.WebApp.App.Integrations.CaesarApi.CaesarApiClient;

namespace AstroView.WebApp.App.Integrations.CaesarApi;

[ApiController]
public class CaesarApiMockController : Controller
{
    private readonly AppDbContext db;
    private readonly IOptions<AppConfig> config;

    public CaesarApiMockController(AppDbContext db, IOptions<AppConfig> config)
    {
        this.db = db;
        this.config = config;
    }

    [HttpPost("caesar-api/upload")]
    public UploadFileResponse UploadFile(List<IFormFile> files)
    {
        return new UploadFileResponse
        {
            uuid = Guid.NewGuid().ToString("N"),
        };
    }

    [HttpGet("caesar-api/app/{appName}/describe")]
    public string GetAppDescription([FromRoute] string appName)
    {
        if (appName == CaesarApiClient.APP_UMAP)
        {
            return System.IO.File.ReadAllText(@"App/Integrations/CaesarApi/Mocks/umap_description.json");
        }
        else if (appName == CaesarApiClient.APP_HDBSCAN)
        {
            return System.IO.File.ReadAllText(@"App/Integrations/CaesarApi/Mocks/hdbscan_description.json");
        }
        else if (appName == CaesarApiClient.APP_OUTLIER_FINDER)
        {
            return System.IO.File.ReadAllText(@"App/Integrations/CaesarApi/Mocks/outlier-finder_description.json");
        }
        else if (appName == CaesarApiClient.APP_SIMILARITY_SEARCH)
        {
            return System.IO.File.ReadAllText(@"App/Integrations/CaesarApi/Mocks/similarity-search_description.json");
        }
        else if (appName == CaesarApiClient.APP_CLASSIFIER_VIT)
        {
            return System.IO.File.ReadAllText(@"App/Integrations/CaesarApi/Mocks/classifier-vit_description.json");
        }
        else
        {
            throw new Exception("Unknown app: " + appName);
        }
    }

    [HttpPost("caesar-api/job")]
    public SubmitJobResponse SubmitJob(SubmitJobRequest request)
    {
        return new SubmitJobResponse
        {
            app = request.app,
            data_inputs = request.data_inputs.data,
            job_id = Guid.NewGuid().ToString(),
            job_options = request.job_options,
            state = "PENDING",
            status = "Job submitted and registered with success",
            submit_date = DateTime.Now,
            tag = "",
        };
    }

    [HttpGet("caesar-api/job/{jobId}/status")]
    public JobStatusResponse GetJobStatus([FromRoute] string jobId)
    {
        return new JobStatusResponse
        {
            elapsed_time = "27.3435878754",
            exit_status = 0,
            job_id = jobId,
            pid = "11539",
            state = "SUCCESS",
            status = "Process terminated with success",
        };
    }

    [HttpGet("caesar-api/job/{jobId}/output")]
    public async Task<IActionResult> GetJobOutput([FromRoute] string jobId)
    {
        byte[] bytes;
        var rand = new Random();
        string resultFileName;
        var job = await db.CaesarJobs.FirstAsync(r => r.CaesarJobId == jobId);
        var caesarDatasetPath = config.Value.GetDisplayModePaths(job.DatasetId, job.DisplayModeId).CaesarDatasetJson;
        var caesarDatasetJson = System.IO.File.ReadAllText(caesarDatasetPath);
        var caesarDataset = JsonConvert.DeserializeObject<GenericCaesarDataset>(caesarDatasetJson)!;
        if (job.AppName == CaesarJob.UMAP)
        {
            resultFileName = "featdata_umap.json";

            foreach (var item in caesarDataset.data)
            {
                item.feats = JArray.Parse($"[{rand.NextDouble().ToString(CultureInfo.InvariantCulture)}, {rand.NextDouble().ToString(CultureInfo.InvariantCulture)}]");
            }
        }
        else if (job.AppName == CaesarJob.HDBSCAN)
        {
            resultFileName = "featdata_hdbscan.json";

            foreach (var item in caesarDataset.data)
            {
                item.clust_id = rand.Next(1, 6);
                item.clust_prob = 1.0;
                item.clust_outlier_score = rand.NextDouble();
            }
        }
        else if (job.AppName == CaesarJob.OUTLIER_FINDER)
        {
            resultFileName = "outlier_data.json";

            foreach (var item in caesarDataset.data)
            {
                item.outlier_score = rand.NextDouble();
                item.is_outlier = item.outlier_score.Value > 0.5 ? 1 : 0;
            }
        }
        else if (job.AppName == CaesarJob.SIMILARITY_SEARCH)
        {
            resultFileName = "featdata_simsearch.json";

            foreach (var item in caesarDataset.data)
            {
                item.nn_indices = Enumerable.Range(0, 5).Select(r => rand.Next(0, caesarDataset.data.Count)).ToArray();
                item.nn_scores = Enumerable.Range(0, 5).Select(r => rand.NextDouble()).OrderByDescending(r => r).ToArray();
            }
        }
        else if (job.AppName == CaesarJob.INDIVIDUAL_SIMILARITY_SEARCH)
        {
            resultFileName = "featdata_simsearch.json";

            caesarDataset.data = caesarDataset.data.Take(5).ToList();

            foreach (var item in caesarDataset.data)
            {
                item.nn_index = caesarDataset.data.IndexOf(item);
                item.nn_score = rand.NextDouble();
            }
        }
        else if (job.AppName == CaesarJob.MORPHOLOGY_CLASSIFIER)
        {
            resultFileName = "classifier_results.json";
            
            var morphTags = new[] { "BACKGROUND", "RADIO-GALAXY", "EXTENDED", "DIFFUSE", "DIFFUSE-LARGE", "ARTEFACT" };
            var morphClass = new[] { "1C-1P", "1C-2P", "1C-3P", "2C-2P", "2C-3P", "3C-3P" };

            var jobRequest = JsonConvert.DeserializeObject<SubmitJobRequest>(job.RequestJson)!;
            if ((string)jobRequest.job_options["model"] == "smorph-singlelabel-rgz")
            {
                // single label per image
                foreach (var item in caesarDataset.data)
                {
                    item.label_pred = morphTags[rand.Next(0, morphTags.Length)];
                    item.prob_pred = rand.NextDouble();
                }
            }
            else
            {
                // multiple labels per image
                foreach (var item in caesarDataset.data)
                {
                    var count = rand.Next(1, 7);
                    item.label_pred = morphTags.Take(count).ToArray();
                    item.prob_pred = Enumerable.Range(0, count).Select(r => rand.NextDouble()).OrderByDescending(r => r).ToArray();
                }
            }
        }
        else
        {
            throw new Exception("Unknown app: " + job.AppName);
        }

        var updatedCaesarDatasetJson = JsonConvert.SerializeObject(caesarDataset);
        var tempDir = Path.GetTempPath();
        var resultDir = Path.Combine(tempDir, $"job_{job.CaesarJobId}");
        Directory.CreateDirectory(resultDir);
        var resultFile = Path.Combine(resultDir, resultFileName);
        System.IO.File.WriteAllText(resultFile, updatedCaesarDatasetJson);
        var resultArchive = Path.Combine(tempDir, $"job_{job.CaesarJobId}.tar.gz");
        await Zipper.Zip(resultArchive, resultDir);

        bytes = System.IO.File.ReadAllBytes(resultArchive);

        return File(bytes, "application/gzip");
    }
}