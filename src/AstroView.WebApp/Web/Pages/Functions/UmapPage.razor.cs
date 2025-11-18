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
using System.Data;
using static AstroView.WebApp.App.Integrations.CaesarApi.CaesarApiClient;

namespace AstroView.WebApp.Web.Pages.Functions;

public partial class UmapPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    private readonly IHttpClientFactory httpClientFactory;
    private readonly UmapPageVm vm;

    public UmapPage(
        IHttpClientFactory httpClientFactory,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.httpClientFactory = httpClientFactory;

        vm = new UmapPageVm();
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
            request.app = CaesarApiClient.APP_UMAP;
            request.data_inputs.data = caesarDatasetPath;
            request.data_inputs.format = "abspath";
            if (vm.Parameters.SelCols.Length > 0)
                request.job_options["selcols"] = vm.Parameters.SelCols;
            request.job_options["datalist-key"] = vm.Parameters.DatalistKey;
            request.job_options["normalize_minmax"] = vm.Parameters.NormalizeMinMax;
            request.job_options["nfeats"] = vm.Parameters.Nfeats;
            request.job_options["mindist"] = vm.Parameters.MinDist;
            request.job_options["nneighbors"] = vm.Parameters.Neighbours;
            request.job_options["run-supervised"] = vm.Parameters.RunSupervised;
            request.job_options["no-logredir"] = vm.Parameters.NoLogRedir;
            request.job_options["no-save-ascii"] = vm.Parameters.NoSaveAsci;
            request.job_options["no-save-json"] = vm.Parameters.NoSaveJson;
            request.job_options["no-save-model"] = vm.Parameters.NoSaveModel;
            request.job_options["outfile-sup"] = vm.Parameters.OutfileSup;
            request.job_options["outfile-unsup"] = vm.Parameters.OutfileUnsup;
            request.job_options["outfile-unsup-json"] = vm.Parameters.OutfileUnsupJson;

            var client = httpClientFactory.CreateClient();
            var api = new CaesarApiClient(client, config.Value.CaesarApi);
            var response = await api.SubmitJob(request);
            var responseJson = JsonConvert.SerializeObject(response, Formatting.Indented);
            var requestJson = JsonConvert.SerializeObject(request, Formatting.Indented);

            var caesarJob = new CaesarJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                AppName = CaesarJob.UMAP,
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
                Type = ChangeType.ExecuteUmap,
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
            var description = await api.Describe(APP_UMAP);
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

    private class UmapPageVm
    {
        public string DatasetName { get; set; }
        public Paging Paging { get; set; }
        public bool ShowExecuteLoader { get; set; }
        public bool ShowLoadDescriptionLoader { get; set; }
        public bool ExecuteDisabled { get; set; }
        public string Description { get; set; }

        public UmapParameters Parameters { get; set; }

        public DisplayMode SelectedDisplayMode { get; set; } = null!;

        public UmapPageVm()
        {
            DatasetName = "";
            Paging = new Paging();
            Parameters = new UmapParameters();
            Description = "";
        }
    }

    private class UmapParameters
    {
        // Input
        public string SelCols { get; set; }
        public string DatalistKey { get; set; }

        // Pre-processing
        public bool NormalizeMinMax { get; set; }

        // Processing
        public int Nfeats { get; set; }
        public double MinDist { get; set; }
        public int Neighbours { get; set; }

        // Run
        public bool RunSupervised { get; set; }
        public bool NoLogRedir { get; set; }

        // Output
        public bool NoSaveAsci { get; set; }
        public bool NoSaveJson { get; set; }
        public bool NoSaveModel { get; set; }
        public string OutfileSup { get; set; }
        public string OutfileUnsup { get; set; }
        public string OutfileUnsupJson { get; set; }

        public UmapParameters()
        {
            SelCols = "";
            DatalistKey = "data";

            NormalizeMinMax = true;

            Nfeats = 2;
            MinDist = 0.1;
            Neighbours = 15;

            RunSupervised = false;
            NoLogRedir = true;

            NoSaveAsci = true;
            NoSaveJson = false;
            NoSaveModel = true;
            OutfileSup = "featdata_umap_sup.dat";
            OutfileUnsup = "featdata_umap.dat";
            OutfileUnsupJson = "featdata_umap.json";
        }
    }
}
