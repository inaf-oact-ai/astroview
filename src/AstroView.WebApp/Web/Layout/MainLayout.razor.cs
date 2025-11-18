using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace AstroView.WebApp.Web.Layout;

public partial class MainLayout
{
    private readonly IJSRuntime js;
    private readonly AppMemoryCache amc;
    private readonly IOptions<AppConfig> config;
    private readonly NavigationManager nav;
    private readonly AuthenticationStateProvider asp;
    private readonly IDbContextFactory<AppDbContext> dbf;

    private readonly AppVm vm;

    public MainLayout(
        IJSRuntime js,
        AppMemoryCache amc,
        IOptions<AppConfig> config,
        NavigationManager nav,
        AuthenticationStateProvider asp,
        IDbContextFactory<AppDbContext> dbf)
    {
        this.js = js;
        this.amc = amc;
        this.config = config;
        this.nav = nav;
        this.asp = asp;
        this.dbf = dbf;

        vm = new AppVm();
        vm.SelectedDatasetChanged = EventCallback.Factory.Create(this, OnSelectedDatasetChanged);
        vm.UserNotesChanged = EventCallback.Factory.Create(this, OnUserNotesChanged);
        vm.ExceptionThrown = EventCallback.Factory.Create<Exception>(this, OnExceptionThrown);
        nav.LocationChanged += Nav_LocationChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        using var db = await dbf.CreateDbContextAsync();

        var userId = await asp.GetUserId();

        var user = await db.Users.FirstOrDefaultAsync(r => r.Id == userId);
        if (user == null)
        {
            nav.NavigateTo("/Login");
            return;
        }

        vm.UserId = userId;
        vm.IsAdmin = Defaults.ADMIN_EMAIL.Equals(user.Email, StringComparison.OrdinalIgnoreCase);
        vm.CurrentDatasetId = user.LastDatasetId ?? 0;

        await LoadSelectedDataset(db);
        await LoadUserNotes(db);
    }

    protected void Nav_LocationChanged(object? sender, LocationChangedEventArgs e)
    {
        try
        {
            if (e.Location.Contains("/jobs", StringComparison.CurrentCultureIgnoreCase))
                return;

            if (vm.UserId.IsEmpty())
                return;

            amc.SetUserLastVisitedPage(vm.UserId, nav.Uri.ToString());
        }
        catch (Exception ex)
        {
            OnExceptionThrown(ex).GetAwaiter().GetResult();
        }
    }

    protected async Task OnSelectedDatasetChanged()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadSelectedDataset(db);

            var user = await db.Users.FirstAsync(r => r.Id == vm.UserId);

            user.LastDatasetId = vm.CurrentDatasetId == 0 ? null : vm.CurrentDatasetId;

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            OnExceptionThrown(ex).GetAwaiter().GetResult();
        }
    }

    protected async Task OnUserNotesChanged()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadUserNotes(db);
        }
        catch (Exception ex)
        {
            OnExceptionThrown(ex).GetAwaiter().GetResult();
        }
    }

    private async Task LoadUserNotes(AppDbContext db)
    {
        var user = await db.Users.FirstAsync(r => r.Id == vm.UserId);

        vm.Files = user.GetNotedFiles(config.Value);
    }

    protected async Task OnExceptionThrown(Exception ex)
    {
        if (ex is ValidationException)
        {
            await js.InvokeVoidAsync("showWarningToast", ex.Message);
        }
        else
        {
            await js.InvokeVoidAsync("showErrorToast", ex.ToString());
        }
    }

    private async Task LoadSelectedDataset(AppDbContext db)
    {
        if (vm.CurrentDatasetId == 0)
            return;

        var dataset = await db.Datasets
            .Where(r => r.Id == vm.CurrentDatasetId)
            .Select(r => new
            {
                Name = r.Name,
                IsLocked = r.IsLocked,
            })
            .FirstAsync();

        vm.CurrentDatasetName = dataset.Name;
        vm.CurrentDatasetIsLocked = dataset.IsLocked;
    }
}

public class AppVm
{
    public bool IsAdmin { get; set; }
    public string UserId { get; set; } = "";

    public int CurrentDatasetId { get; set; }
    public string CurrentDatasetName { get; set; } = "";
    public bool CurrentDatasetIsLocked { get; set; }

    public EventCallback SelectedDatasetChanged { get; set; }
    public EventCallback UserNotesChanged { get; set; }
    public EventCallback<Exception> ExceptionThrown { get; set; }

    public List<NotedFile> Files { get; set; }

    public AppVm()
    {
        UserId = "";
        CurrentDatasetName = "";

        Files = new List<NotedFile>();
    }
}
