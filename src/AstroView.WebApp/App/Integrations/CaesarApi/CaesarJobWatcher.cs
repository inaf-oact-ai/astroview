using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;

namespace AstroView.WebApp.App.Integrations.CaesarApi;

public class CaesarJobWatcher : BackgroundService
{
    private readonly ILogger<CaesarJobWatcher> logger;
    private readonly IDbContextFactory<AppDbContext> dbf;
    private readonly CaesarApiClient caesarApi;

    public CaesarJobWatcher(
        ILogger<CaesarJobWatcher> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<AppConfig> config,
        IDbContextFactory<AppDbContext> dbf)
    {
        this.logger = logger;
        this.dbf = dbf;
        var httpClient = httpClientFactory.CreateClient();
        caesarApi = new CaesarApiClient(httpClient, config.Value.CaesarApi);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CaesarJobWatcher started");

        while (true)
        {
            try
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                using var db = await dbf.CreateDbContextAsync();

                var jobs = await db.CaesarJobs
                    .Where(r => r.ResultJobStatus == Data.Enums.HangfireJobStatus.None)
                    .ToListAsync();

                foreach (var job in jobs)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    logger.LogInformation($"Processing job {job.Id}");

                    var status = await caesarApi.GetJobStatus(job.CaesarJobId!);

                    job.CaesarJobState = status.state;
                    job.CaesarJobStatus = status.status;

                    if (status.state == "PENDING" || status.state == "STARTED" || status.state == "RUNNING")
                    {
                        // skip
                    }
                    else
                    {
                        var jobId = BackgroundJob.Enqueue<HangfireJobs>(r => r.ProcessCaesarJobOutput(job.Id, null!));
                        job.ResultJobId = jobId;
                        job.FinishedDate = DateTime.UtcNow;

                        var datasetJob = new DatasetJobDbe
                        {
                            DatasetId = job.DatasetId,
                            UserId = job.UserId,
                            Type = DatasetJobType.ProcessCaesarJobOutput,
                            JobId = jobId,
                            JobStatus = HangfireJobStatus.None,
                            Date = DateTime.UtcNow,
                        };
                        db.DatasetJobs.Add(datasetJob);

                        await db.SaveChangesAsync();
                    }

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process Caesar jobs");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
