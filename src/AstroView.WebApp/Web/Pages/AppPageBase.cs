using AstroView.WebApp.App;
using AstroView.WebApp.Data;
using AstroView.WebApp.Web.Layout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace AstroView.WebApp.Web.Pages;

[Authorize]
public class AppPageBase : ComponentBase
{
    [CascadingParameter(Name = "AppVm")]
    protected AppVm AppVm { get; set; } = null!;

    protected readonly IJSRuntime js;
    protected readonly IOptions<AppConfig> config;
    protected readonly IDbContextFactory<AppDbContext> dbf;
    protected readonly AuthenticationStateProvider asp;
    protected readonly NavigationManager nav;

    public AppPageBase(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
    {
        this.js = js;
        this.config = config;
        this.asp = asp;
        this.nav = nav;
        this.dbf = dbf;
    }

    protected ValueTask ShowBackgroundJobToast(string description, string jobId)
    {
        var jobUrl = $"/Jobs/jobs/details/{jobId}";
        return js.InvokeVoidAsync("showBackgroundJobToast", description, jobUrl);
    }
}
