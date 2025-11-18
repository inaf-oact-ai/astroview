using AstroView.WebApp.App;
using AstroView.WebApp.App.Integrations.CaesarApi;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using static AstroView.WebApp.App.Integrations.CaesarApi.CaesarApiClient;
using static AstroView.WebApp.Web.Pages.Functions.UmapPage;

namespace AstroView.WebApp.Web.Pages.Functions;

public partial class SimilaritySearchPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    private readonly IHttpClientFactory httpClientFactory;
    private readonly SimilaritySearchPageVm vm;

    public SimilaritySearchPage(
        IHttpClientFactory httpClientFactory,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.httpClientFactory = httpClientFactory;

        vm = new SimilaritySearchPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var dataset = await db.Datasets.AsNoTracking().Where(r => r.Id == DatasetId).FirstAsync();

            vm.DatasetName = dataset.Name;
            vm.ExecuteDisabled = !dataset.IsLocked;
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (DatasetId == _datasetId)
        {
            return;
        }

        _datasetId = DatasetId;

        try
        {

        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task Execute()
    {
        vm.ShowExecuteLoader = true;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var caesarDatasetPath = config.Value.GetDisplayModePaths(DatasetId, vm.SelectedDisplayMode.DisplayModeId).CaesarDatasetJson;

            var request = new SubmitJobRequest();
            request.app = CaesarApiClient.APP_SIMILARITY_SEARCH;
            request.data_inputs.data = new List<string> { caesarDatasetPath };
            request.data_inputs.format = new List<string> { "abspath" };
            request.job_options["model"] = vm.Parameters.Model;
            if (vm.Parameters.SelCols.Length > 0)
                request.job_options["selcols"] = vm.Parameters.SelCols;
            request.job_options["datalist-key"] = vm.Parameters.DatalistKey;
            request.job_options["score-thr"] = vm.Parameters.ScoreThr;
            request.job_options["zscale"] = vm.Parameters.Zscale;
            request.job_options["zscale-contrast"] = vm.Parameters.ZscaleContrast;
            request.job_options["imgsize"] = vm.Parameters.ImgSize;
            request.job_options["k"] = vm.Parameters.K;
            request.job_options["large-data-thr"] = vm.Parameters.LargeDataThr;
            request.job_options["M"] = vm.Parameters.M;
            request.job_options["nlist"] = vm.Parameters.Nlist;
            request.job_options["nprobe"] = vm.Parameters.Nprobe;
            request.job_options["no-logredir"] = vm.Parameters.NoLogRedir;
            // request.job_options["outfile"] = vm.Parameters.Outfile; // api returns empty result if we specify this

            var client = httpClientFactory.CreateClient();
            var api = new CaesarApiClient(client, config.Value.CaesarApi);
            var response = await api.SubmitJob(request);
            var responseJson = JsonConvert.SerializeObject(response, Formatting.Indented);
            var requestJson = JsonConvert.SerializeObject(request, Formatting.Indented);

            var caesarJob = new CaesarJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                AppName = CaesarJob.SIMILARITY_SEARCH,
                RequestJson = requestJson,
                ResponseJson = responseJson,
                CaesarJobId = response.job_id,
                CaesarJobState = response.state,
                CaesarJobStatus = response.status,
                StartedDate = DateTime.UtcNow,
                DisplayModeId = vm.SelectedDisplayMode.DisplayModeId,
            };
            db.CaesarJobs.Add(caesarJob);

            var change = new ChangeDbe
            {
                Type = ChangeType.ExecuteSimilaritySearch,
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                Date = DateTime.UtcNow,
                Data = $"Request: {requestJson}; Response: {responseJson}",
            };
            db.Changes.Add(change);

            await db.SaveChangesAsync();

            nav.NavigateTo($"/Datasets/{DatasetId}");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
        finally
        {
            vm.ShowExecuteLoader = false;
        }
    }

    private async Task LoadDescription()
    {
        vm.ShowLoadDescriptionLoader = true;

        try
        {
            var client = httpClientFactory.CreateClient();
            var api = new CaesarApiClient(client, config.Value.CaesarApi);
            var description = await api.Describe(APP_SIMILARITY_SEARCH);
            vm.Description = description.FormatJson();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
        finally
        {
            vm.ShowLoadDescriptionLoader = false;
        }
    }

    private async Task OnDisplayModeChanged(DisplayMode variation)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var displayMode = await db.DisplayModes.FirstAsync(r => r.Id == variation.DisplayModeId);
            vm.ExecuteDisabled = displayMode.CaesarDatasetJobStatus != HangfireJobStatus.Completed;

            vm.SelectedDisplayMode = variation;
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private class SimilaritySearchPageVm
    {
        public string DatasetName { get; set; }
        public Paging Paging { get; set; }
        public bool ShowExecuteLoader { get; set; }
        public bool ShowLoadDescriptionLoader { get; set; }
        public bool ExecuteDisabled { get; set; }
        public string Description { get; set; }

        public SimilaritySearchParameters Parameters { get; set; }

        public DisplayMode SelectedDisplayMode { get; set; } = null!;

        public SimilaritySearchPageVm()
        {
            DatasetName = "";
            Paging = new Paging();
            Parameters = new SimilaritySearchParameters();
            Description = "";
        }
    }

    private class SimilaritySearchParameters
    {
        // Model
        public string Model { get; set; }

        // Input
        public string SelCols { get; set; }
        public string DatalistKey { get; set; }

        // Pre-processing
        public double ScoreThr { get; set; }
        public bool Zscale { get; set; }
        public double ZscaleContrast { get; set; }

        // Processing
        public int ImgSize { get; set; }
        public int K { get; set; }
        public int LargeDataThr { get; set; }
        public int M { get; set; }
        public int Nlist { get; set; }
        public int Nprobe { get; set; }

        // Run
        public bool NoLogRedir { get; set; }

        // Output
        public string Outfile { get; set; }

        public SimilaritySearchParameters()
        {
            Model = "simclr-smgps";

            SelCols = "";
            DatalistKey = "data";

            ScoreThr = 0;
            Zscale = false;
            ZscaleContrast = 0.25;

            ImgSize = 224;
            K = 5;
            LargeDataThr = 1;
            M = 8;
            Nlist = 100;
            Nprobe = 10;

            NoLogRedir = true;

            Outfile = "N/A";
        }
    }
}
