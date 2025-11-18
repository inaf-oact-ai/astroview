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
using static AstroView.WebApp.Web.Pages.Functions.UmapPage;

namespace AstroView.WebApp.Web.Pages.Functions;

public partial class MorphologyClassifierPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    private readonly IHttpClientFactory httpClientFactory;
    private readonly MorphologyClassifierPageVm vm;

    public MorphologyClassifierPage(
        IHttpClientFactory httpClientFactory,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.httpClientFactory = httpClientFactory;

        vm = new MorphologyClassifierPageVm();
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
            request.app = CaesarApiClient.APP_CLASSIFIER_VIT;
            request.data_inputs.data = caesarDatasetPath;
            request.data_inputs.format = "abspath";
            request.job_options["model"] = vm.Parameters.Model;
            request.job_options["zscale"] = vm.Parameters.Zscale;
            request.job_options["zscale_contrast"] = vm.Parameters.ZscaleContrast;
            request.job_options["no-logredir"] = vm.Parameters.NoLogRedir;

            var client = httpClientFactory.CreateClient();
            var api = new CaesarApiClient(client, config.Value.CaesarApi);
            var response = await api.SubmitJob(request);
            var responseJson = JsonConvert.SerializeObject(response, Formatting.Indented);
            var requestJson = JsonConvert.SerializeObject(request, Formatting.Indented);

            var caesarJob = new CaesarJobDbe
            {
                DatasetId = DatasetId,
                UserId = AppVm.UserId,
                AppName = CaesarJob.MORPHOLOGY_CLASSIFIER,
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
                Type = ChangeType.ExecuteMorphologyClassifier,
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
            var description = await api.Describe(APP_CLASSIFIER_VIT);
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

    private class MorphologyClassifierPageVm
    {
        public string DatasetName { get; set; }
        public Paging Paging { get; set; }
        public bool ShowExecuteLoader { get; set; }
        public bool ShowLoadDescriptionLoader { get; set; }
        public bool ExecuteDisabled { get; set; }
        public string Description { get; set; }

        public MorphologyClassifierParameters Parameters { get; set; }

        public DisplayMode SelectedDisplayMode { get; set; } = null!;

        public MorphologyClassifierPageVm()
        {
            DatasetName = "";
            Paging = new Paging();
            Parameters = new MorphologyClassifierParameters();
            Description = "";
        }
    }

    private class MorphologyClassifierParameters
    {
        // Input
        public string Model { get; set; }

        // Pre-processing
        public bool Zscale { get; set; }
        public double ZscaleContrast { get; set; }

        // Run
        public bool NoLogRedir { get; set; }

        public MorphologyClassifierParameters()
        {
            Model = "smorphclass_multilabel";
            Zscale = true;
            ZscaleContrast = 0.25;
            NoLogRedir = true;
        }
    }
}
