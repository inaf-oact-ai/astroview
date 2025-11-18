using AstroView.WebApp.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using AstroView.WebApp.Data.Entities;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using AstroView.WebApp.App;

namespace AstroView.WebApp.Web.Pages;

public partial class ProfilePage
{
    private readonly UserManager<UserDbe> userManager;
    private readonly ProfilePageVm vm;

    public ProfilePage(
        UserManager<UserDbe> userManager,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.userManager = userManager;
        vm = new ProfilePageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authenticationState = await asp.GetAuthenticationStateAsync();
            vm.User = (await userManager.GetUserAsync(authenticationState.User))!;
            vm.UpdateProfileForm.DisplayName = vm.User.DisplayName;
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task UpdateProfile()
    {
        try
        {
            var authenticationState = await asp.GetAuthenticationStateAsync();
            var user = (await userManager.GetUserAsync(authenticationState.User))!;
            user.DisplayName = vm.UpdateProfileForm.DisplayName;
            var updateResult = await userManager.UpdateAsync(user);
            if (updateResult.Succeeded)
            {
                vm.UpdateProfileForm.ErrorMessage = "";

                await js.InvokeVoidAsync("showToast", "Profile has been updated");
            }
            else
            {
                vm.UpdateProfileForm.ErrorMessage = updateResult.Errors.First().Description;
            }
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ChangePassword()
    {
        try
        {
            if (vm.ChangePasswordForm.NewPassword != vm.ChangePasswordForm.NewPasswordConfirm)
            {
                vm.ChangePasswordForm.ErrorMessage = "Password confirmation doesn't match the password";
                return;
            }

            var changePasswordResult = await userManager.ChangePasswordAsync(vm.User,
                vm.ChangePasswordForm.CurrentPassword, vm.ChangePasswordForm.NewPassword);
            if (changePasswordResult.Succeeded)
            {
                vm.ChangePasswordForm.CurrentPassword = "";
                vm.ChangePasswordForm.NewPassword = "";
                vm.ChangePasswordForm.NewPasswordConfirm = "";
                vm.ChangePasswordForm.ErrorMessage = "";

                await js.InvokeVoidAsync("showToast", "Password has been changed");
            }
            else
            {
                vm.ChangePasswordForm.ErrorMessage = changePasswordResult.Errors.First().Description;
            }
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class ProfilePageVm
    {
        public UserDbe User { get; set; }
        public UpdateProfileFormVm UpdateProfileForm { get; set; }
        public ChangePasswordFormVm ChangePasswordForm { get; set; }

        public ProfilePageVm()
        {
            User = new UserDbe();
            UpdateProfileForm = new UpdateProfileFormVm();
            ChangePasswordForm = new ChangePasswordFormVm();
        }
    }

    protected class UpdateProfileFormVm
    {
        [Required]
        public string DisplayName { get; set; }

        public string ErrorMessage { get; set; }

        public UpdateProfileFormVm()
        {
            DisplayName = "";
            ErrorMessage = "";
        }
    }

    protected class ChangePasswordFormVm
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string NewPasswordConfirm { get; set; }

        public string ErrorMessage { get; set; }

        public ChangePasswordFormVm()
        {
            CurrentPassword = "";
            NewPassword = "";
            NewPasswordConfirm = "";
            ErrorMessage = "";
        }
    }
}
