using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Data;
using static AstroView.WebApp.Web.Pages.Functions.UmapPage;

namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class ChangesPage
{
    [Parameter]
    public int DatasetId { get; set; }
    private int _datasetId { get; set; }

    [SupplyParameterFromQuery]
    public int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    public int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private readonly ChangesPageVm vm;

    public ChangesPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new ChangesPageVm();
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
            vm.Users = await db.Changes
                .Where(r => r.DatasetId == DatasetId)
                .Select(r => r.User)
                .Distinct()
                .ToListAsync();

            await LoadChanges(db);
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

            await LoadChanges(db);
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

            await LoadChanges(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task DisplayChanges()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadChanges(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadChanges(AppDbContext db)
    {
        var query = db.Changes
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

        vm.Changes = await query
            .Include(r => r.User)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .Select(r => new Change
            {
                Id = r.Id,
                Date = r.Date,
                Type = r.Type,
                Data = r.Data,
                User = r.User,
            })
            .ToListAsync();
    }

    private class ChangesPageVm
    {
        public string DatasetName { get; set; }
        public Paging Paging { get; set; }
        public List<Change> Changes { get; set; }
        public bool SortDesc { get; set; }
        public DateTime MinDate { get; set; }
        public DateTime MaxDate { get; set; }
        public List<UserDbe> Users { get; set; }
        public string SelectedUserId { get; set; }

        public ChangesPageVm()
        {
            DatasetName = "";
            Paging = new Paging();
            Changes = new List<Change>();
            SortDesc = true;
            MinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            MaxDate = DateTime.UtcNow;
            Users = new List<UserDbe>();
            SelectedUserId = "";
        }
    }

    private class Change
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public ChangeType Type { get; set; }
        public string Data { get; set; } = null!;
        public UserDbe User { get; set; } = null!;
    }
}
