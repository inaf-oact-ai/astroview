using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;

namespace AstroView.WebApp.Web.Pages;

[Authorize(Roles = Defaults.Roles.Admin)]
public partial class UsersPage
{
    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private readonly UsersPageVm vm;
    private readonly UserManager<UserDbe> userManager;

    public UsersPage(
        UserManager<UserDbe> userManager,
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        this.userManager = userManager;
        this.vm = new UsersPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadUsers(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (PageNumber == _pageNumber
            && PageSize == _pageSize)
        {
            return;
        }

        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadUsers(db);
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

    protected async Task DisplayUsers()
    {
        using var db = await dbf.CreateDbContextAsync();

        await LoadUsers(db);
    }

    private async Task LoadUsers(AppDbContext db)
    {
        var usersCount = await db.Users.CountAsync();

        vm.Paging.Calculate(PageNumber, PageSize, usersCount);

        vm.Users = await db.Users
            .AsNoTracking()
            .OrderBy(r => r.Email)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();
    }

    protected async Task ShowAddUserModal()
    {
        try
        {
            vm.AddUserModal.Email = "";
            vm.AddUserModal.Password = "";
            vm.AddUserModal.ErrorMessage = "";

            await js.InvokeVoidAsync("showAddUserModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task AddUser()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var user = new UserDbe
            {
                UserName = vm.AddUserModal.Email,
                Email = vm.AddUserModal.Email,
                DisplayName = vm.AddUserModal.DisplayName,
            };

            var createUserResult = await userManager.CreateAsync(user, vm.AddUserModal.Password);
            if (createUserResult.Succeeded)
            {
                user = await userManager.FindByEmailAsync(vm.AddUserModal.Email);
                if (user == null)
                    throw new Exception("User is null");

                await userManager.AddToRoleAsync(user, Defaults.Roles.User);

                await js.InvokeVoidAsync("hideAddUserModal");

                await DisplayUsers();
            }
            else
            {
                vm.AddUserModal.ErrorMessage = createUserResult.Errors.First().Description;
            }
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task ShowChangePasswordModal(string userId)
    {
        try
        {
            var user = await userManager.FindByIdAsync(userId);

            vm.ChangePasswordModal.UserId = userId;
            vm.ChangePasswordModal.Email = user!.Email!;
            vm.ChangePasswordModal.Password = "";
            vm.ChangePasswordModal.ErrorMessage = "";

            await js.InvokeVoidAsync("showChangePasswordModal");
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
            var user = await userManager.FindByIdAsync(vm.ChangePasswordModal.UserId);
            var token = await userManager.GeneratePasswordResetTokenAsync(user!);
            var result = await userManager.ResetPasswordAsync(user!, token, vm.ChangePasswordModal.Password);

            if (result.Succeeded)
            {
                await js.InvokeVoidAsync("hideChangePasswordModal");

                await js.InvokeVoidAsync("showToast", "Password has been changed");
            }
            else
            {
                vm.ChangePasswordModal.ErrorMessage = result.Errors.First().Description;
            }
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task BlockUser(string userId)
    {
        try
        {
            var user = await userManager.FindByIdAsync(userId);
            await userManager.SetLockoutEndDateAsync(user!, new DateTime(3000, 1, 1));

            await DisplayUsers();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task UnblockUser(string userId)
    {
        try
        {
            var user = await userManager.FindByIdAsync(userId);
            await userManager.SetLockoutEndDateAsync(user!, null);

            await DisplayUsers();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class UsersPageVm
    {
        public Paging Paging { get; set; }
        public AddUserModalVm AddUserModal { get; set; }
        public ChangePasswordModalVm ChangePasswordModal { get; set; }
        public List<UserDbe> Users;

        public UsersPageVm()
        {
            Paging = new Paging();
            AddUserModal = new AddUserModalVm();
            ChangePasswordModal = new ChangePasswordModalVm();
            Users = new List<UserDbe>();
        }
    }

    protected class ChangePasswordModalVm
    {
        public string UserId { get; set; }

        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public ChangePasswordModalVm()
        {
            UserId = "";
            Email = "";
            Password = "";
            ErrorMessage = "";
        }
    }

    protected class AddUserModalVm
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string DisplayName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public AddUserModalVm()
        {
            Email = "";
            DisplayName = "";
            Password = "";
            ErrorMessage = "";
        }
    }
}
