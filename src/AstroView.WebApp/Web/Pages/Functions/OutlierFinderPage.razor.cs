using AstroView.WebApp.App;
using AstroView.WebApp.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using AstroView.WebApp.Data.Entities;
using Newtonsoft.Json;
using static AstroView.WebApp.App.Integrations.CaesarApi.CaesarApiClient;
using AstroView.WebApp.Data.Enums;
using AstroView.WebApp.App.Integrations.CaesarApi;
using static AstroView.WebApp.Web.Pages.Functions.UmapPage;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Utils;

namespace AstroView.WebApp.Web.Pages.Functions;

public partial class OutlierFinderPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    private readonly IHttpClientFactory httpClientFactory;
    private readonly OutlierFinderPageVm vm;

    public OutlierFinderPage(
        IHttpClientFactory httpClientFactory,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.httpClientFactory = httpClientFactory;

        vm = new OutlierFinderPageVm();
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
            request.app = CaesarApiClient.APP_OUTLIER_FINDER;
            request.data_inputs.data = caesarDatasetPath;
            request.data_inputs.format = "abspath";
            if (vm.Parameters.SelCols.Length > 0)
                request.job_options["selcols"] = vm.Parameters.SelCols;
            request.job_options["datalist-key"] = vm.Parameters.DatalistKey;
            request.job_options["normalize_minmax"] = vm.Parameters.NormalizeMinMax;
            request.job_options["anomaly-thr"] = vm.Parameters.AnomalyThr;
            request.job_options["contamination"] = vm.Parameters.Contamination;
            request.job_options["max-features"] = vm.Parameters.MaxFeatures;
            request.job_options["max-samples"] = vm.Parameters.MaxSamples;
            request.job_options["nestimators"] = vm.Parameters.Nestimators;
            request.job_options["no-logredir"] = vm.Parameters.NoLogRedir;
            request.job_options["no-save-ascii"] = vm.Parameters.NoSaveAsci;
            request.job_options["no-save-json"] = vm.Parameters.NoSaveJson;
            request.job_options["no-save-model"] = vm.Parameters.NoSaveModel;
            request.job_options["no-save-features"] = vm.Parameters.NoSaveFeatures;
            request.job_options["outfile"] = vm.Parameters.Outfile;
            request.job_options["outfile-json"] = vm.Parameters.OutfileJson;

            var client = httpClientFactory.CreateClient();
            var api = new CaesarApiClient(client, config.Value.CaesarApi);
            var response = await api.SubmitJob(request);
            var responseJson = JsonConvert.SerializeObject(response, Formatting.Indented);
            var requestJson = JsonConvert.SerializeObject(request, Formatting.Indented);

            var caesarJob = new CaesarJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                AppName = CaesarJob.OUTLIER_FINDER,
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
                Type = ChangeType.ExecuteOutlierFinder,
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
            var description = await api.Describe(APP_OUTLIER_FINDER);
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

    private class OutlierFinderPageVm
    {
        public string DatasetName { get; set; }
        public Paging Paging { get; set; }
        public bool ShowExecuteLoader { get; set; }
        public bool ShowLoadDescriptionLoader { get; set; }
        public bool ExecuteDisabled { get; set; }
        public string Description { get; set; }

        public OutlierFinderParameters Parameters { get; set; }

        public DisplayMode SelectedDisplayMode { get; set; } = null!;

        public OutlierFinderPageVm()
        {
            DatasetName = "";
            Paging = new Paging();
            Parameters = new OutlierFinderParameters();
            Description = "";
        }
    }

    private class OutlierFinderParameters
    {
        // Input
        public string SelCols { get; set; }
        public string DatalistKey { get; set; }

        // Pre-processing
        public bool NormalizeMinMax { get; set; }

        // Processing
        public double AnomalyThr { get; set; }
        public double Contamination { get; set; }
        public int MaxFeatures { get; set; }
        public double MaxSamples { get; set; }
        public int Nestimators { get; set; }

        // Run
        public bool NoLogRedir { get; set; }

        // Output
        public bool NoSaveAsci { get; set; }
        public bool NoSaveJson { get; set; }
        public bool NoSaveModel { get; set; }
        public bool NoSaveFeatures { get; set; }
        public string Outfile { get; set; }
        public string OutfileJson { get; set; }

        public OutlierFinderParameters()
        {
            SelCols = "";
            DatalistKey = "data";

            NormalizeMinMax = true;

            AnomalyThr = 0.9;
            Contamination = -1.0;
            MaxFeatures = 1;
            MaxSamples = -1.0;
            Nestimators = 100;

            NoLogRedir = true;

            NoSaveAsci = true;
            NoSaveJson = false;
            NoSaveModel = true;
            NoSaveFeatures = true;
            Outfile = "outlier_data";
            OutfileJson = "outlier_data.json";
        }
    }
}
