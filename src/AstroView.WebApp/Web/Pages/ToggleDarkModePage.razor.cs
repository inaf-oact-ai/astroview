using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Security.Claims;
using AstroView.WebApp.App;

namespace AstroView.WebApp.Web.Pages;

public partial class ToggleDarkModePage
{
    private readonly SignInManager<UserDbe> signInManager;
    private readonly UserManager<UserDbe> userManager;

    public ToggleDarkModePage(
        SignInManager<UserDbe> signInManager,
        UserManager<UserDbe> userManager,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.signInManager = signInManager;
        this.userManager = userManager;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var value = "true";
            var state = await asp.GetAuthenticationStateAsync();
            var user = await userManager.GetUserAsync(state.User);

            if (user == null)
                return;

            var darkModeClaims = state.User.Claims.Where(r => r.Type == "dark-mode");
            if (darkModeClaims.Any())
            {
                value = darkModeClaims.First().Value == "true" ? "false" : "true";
                await userManager.RemoveClaimsAsync(user, darkModeClaims);
            }

            await userManager.AddClaimAsync(user, new Claim("dark-mode", value));

            await signInManager.RefreshSignInAsync(user);

            nav.NavigateTo("/Profile");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }
}
