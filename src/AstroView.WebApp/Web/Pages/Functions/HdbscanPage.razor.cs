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

public partial class HdbscanPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    private readonly IHttpClientFactory httpClientFactory;
    private readonly HdbscanPageVm vm;

    public HdbscanPage(
        IHttpClientFactory httpClientFactory,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.httpClientFactory = httpClientFactory;

        vm = new HdbscanPageVm();
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
            request.app = CaesarApiClient.APP_HDBSCAN;
            request.data_inputs.data = caesarDatasetPath;
            request.data_inputs.format = "abspath";
            if (vm.Parameters.SelCols.Length > 0)
                request.job_options["selcols"] = vm.Parameters.SelCols;
            request.job_options["reduce-dim-method"] = vm.Parameters.ReduceDimMethod;
            request.job_options["datalist-key"] = vm.Parameters.DatalistKey;
            request.job_options["normalize_minmax"] = vm.Parameters.NormalizeMinMax;
            request.job_options["reduce-dim"] = vm.Parameters.ReduceDim;
            request.job_options["cluster-selection-epsilon"] = vm.Parameters.ClusterSelectionEpsilon;
            request.job_options["min-cluster-size"] = vm.Parameters.MinClusterSize;
            request.job_options["min-samples"] = vm.Parameters.MinSamples;
            request.job_options["pca-ncomps"] = vm.Parameters.PcaNcomps;
            request.job_options["pca-varthr"] = vm.Parameters.PcaVarthr;
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
                AppName = CaesarJob.HDBSCAN,
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
                Type = ChangeType.ExecuteHdbscan,
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
            var description = await api.Describe(APP_HDBSCAN);
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

    private class HdbscanPageVm
    {
        public string DatasetName { get; set; }
        public Paging Paging { get; set; }
        public bool ShowExecuteLoader { get; set; }
        public bool ShowLoadDescriptionLoader { get; set; }
        public bool ExecuteDisabled { get; set; }
        public string Description { get; set; }

        public HdbscanParameters Parameters { get; set; }

        public DisplayMode SelectedDisplayMode { get; set; } = null!;

        public HdbscanPageVm()
        {
            DatasetName = "";
            Paging = new Paging();
            Parameters = new HdbscanParameters();
            Description = "";
        }
    }

    private class HdbscanParameters
    {
        // Input
        public string SelCols { get; set; }
        public string ReduceDimMethod { get; set; }
        public string DatalistKey { get; set; }

        // Pre-processing
        public bool NormalizeMinMax { get; set; }
        public bool ReduceDim { get; set; }

        // Processing
        public double ClusterSelectionEpsilon { get; set; }
        public int MinClusterSize { get; set; }
        public int MinSamples { get; set; }
        public int PcaNcomps { get; set; }
        public double PcaVarthr { get; set; }

        // Run
        public bool NoLogRedir { get; set; }

        // Output
        public bool NoSaveAsci { get; set; }
        public bool NoSaveJson { get; set; }
        public bool NoSaveModel { get; set; }
        public bool NoSaveFeatures { get; set; }
        public string Outfile { get; set; }
        public string OutfileJson { get; set; }

        public HdbscanParameters()
        {
            SelCols = "";
            ReduceDimMethod = "pca";
            DatalistKey = "data";

            NormalizeMinMax = true;
            ReduceDim = false;

            ClusterSelectionEpsilon = 0;
            MinClusterSize = 5;
            MinSamples = -1;
            PcaNcomps = -1;
            PcaVarthr = 0.9;

            NoLogRedir = true;

            NoSaveAsci = true;
            NoSaveJson = false;
            NoSaveModel = true;
            NoSaveFeatures = true;
            Outfile = "featdata_hdbscan";
            OutfileJson = "featdata_hdbscan.json";
        }
    }
}
