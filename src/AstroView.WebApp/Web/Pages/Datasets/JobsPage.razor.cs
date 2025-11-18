using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Data;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class JobsPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private readonly JobsPageVm vm;

    public JobsPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new JobsPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _datasetId = DatasetId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetName = await db.Datasets.Where(r => r.Id == DatasetId).Select(r => r.Name).FirstAsync();

            vm.Users = await db.DatasetJobs
                .Where(r => r.DatasetId == DatasetId)
                .Select(r => r.User)
                .Distinct()
                .ToListAsync();

            await LoadJobs(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (DatasetId == _datasetId
            && PageNumber == _pageNumber
            && PageSize == _pageSize)
        {
            return;
        }

        _datasetId = DatasetId;
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadJobs(db);
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
            await js.InvokeVoidAsync("onAfterRender");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task GoToPage()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var uri = nav.GetUriWithQueryParameters(new Dictionary<string, object?>
            {
                { "PageNumber", PageNumber },
                { "PageSize", PageSize },
            });
            nav.NavigateTo(uri);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task SetSortDesc(bool value)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.SortDesc = value;

            await LoadJobs(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task MarkAsCompleted(int jobId)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var job = await db.DatasetJobs.FirstAsync(r => r.Id == jobId);
            job.JobStatus = Data.Enums.HangfireJobStatus.Completed;
            await db.SaveChangesAsync();

            await LoadJobs(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }    
    
    protected async Task DisplayJobs()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadJobs(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadJobs(AppDbContext db)
    {
        var query = db.DatasetJobs
            .AsNoTracking()
            .Where(r => r.DatasetId == DatasetId)
            .Where(r => r.Date >= vm.MinDate && r.Date <= vm.MaxDate);

        if (vm.SelectedUserId != "")
        {
            query = query.Where(r => r.UserId == vm.SelectedUserId);
        }

        var count = await query.CountAsync();

        vm.Paging.Calculate(PageNumber, PageSize, count);

        if (vm.SortDesc)
        {
            query = query.OrderByDescending(r => r.Date);
        }
        else
        {
            query = query.OrderBy(r => r.Date);
        }

        vm.Jobs = await query
            .Include(r => r.User)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();
    }

    protected class JobsPageVm
    {
        public string DatasetName { get; set; }
        public Paging Paging { get; set; }
        public List<DatasetJobDbe> Jobs { get; set; }
        public bool SortDesc { get; set; }
        public DateTime MinDate { get; set; }
        public DateTime MaxDate { get; set; }
        public List<UserDbe> Users { get; set; }
        public string SelectedUserId { get; set; }

        public JobsPageVm()
        {
            DatasetName = "";
            Paging = new Paging();
            Jobs = new List<DatasetJobDbe>();
            SortDesc = true;
            MinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            MaxDate = DateTime.UtcNow;
            Users = new List<UserDbe>();
            SelectedUserId = "";
        }
    }
}
