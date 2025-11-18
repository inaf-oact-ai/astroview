using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.App;

public static class Defaults
{
    public const int LabelingBatchSize = 250;

    public const string ADMIN_EMAIL = "admin@admin";
    private const string ADMIN_PASSWORD = "Password_123";
    private const string ADMIN_DISPLAY_NAME = "Administrator";

    public static async Task CreateDatabaseApplyMigrations(this IApplicationBuilder app)
    {
        using (var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.SetCommandTimeout(1200);
            await db.Database.MigrateAsync();
        }
    }

    public static async Task CreateDefaultRolesAndUsers(this IApplicationBuilder app)
    {
        using (var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (roleManager.Roles.Count() != 0)
                return;

            await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
            await roleManager.CreateAsync(new IdentityRole(Roles.User));

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserDbe>>();
            await userManager.CreateAsync(new UserDbe
            {
                UserName = ADMIN_EMAIL,
                Email = ADMIN_EMAIL,
                DisplayName = ADMIN_DISPLAY_NAME,
            }, ADMIN_PASSWORD);

            var admin = await userManager.FindByEmailAsync(ADMIN_EMAIL);
            if (admin == null)
                throw new Exception("Admin account was not created");

            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
    }

    public class Roles
    {
        public const string Admin = "Admin";
        public const string User = "User";
    }

    public static List<string> Colors { get; set; } = new List<string>
    {
        "Black",
        "Blue",
        "Brown",
        "Green",
        "Grey",
        "Olive",
        "Orange",
        "Pink",
        "Purple",
        "Red",
        "Teal",
        "Violet",
        "Yellow"
    };
}
