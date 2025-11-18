using AstroView.WebApp.App;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;

namespace AstroView.WebApp.Web.Pages;

[AllowAnonymous]
public partial class LoginPage
{
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    private readonly SignInManager<UserDbe> signInManager;
    private string? errorMessage;

    public LoginPage(
        SignInManager<UserDbe> signInManager,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.signInManager = signInManager;

        Input = new InputModel();
    }

    protected override async Task OnInitializedAsync()
    {
        if (HttpContext != null && HttpMethods.IsGet(HttpContext.Request.Method))
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }

        if (errorMessage != null)
        {
            await js.InvokeVoidAsync("showToast", errorMessage);
        }
    }

    public async Task LoginUser()
    {
        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            nav.NavigateTo("/");
        }
        else if (result.IsLockedOut)
        {
            errorMessage = "This account has been locked out, please try again later";

        }
        else
        {
            errorMessage = "Invalid username or password";
        }
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; } = true;
    }
}
