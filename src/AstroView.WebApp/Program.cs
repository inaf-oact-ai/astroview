using AstroView.WebApp.App;
using AstroView.WebApp.App.Integrations.CaesarApi;
using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using AstroView.WebApp.Web;
using Hangfire;
using Hangfire.Console;
using Hangfire.MySql;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Transactions;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ASP.NET
builder.Services.AddMvc();
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Blazor
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Authentication
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddSingleton<IEmailSender<UserDbe>, IdentityNoOpEmailSender>();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services
    .ConfigureApplicationCookie(options => options.LoginPath = "/Login")
    .AddIdentityCore<UserDbe>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Database
var serverVersion = new MySqlServerVersion(new Version(8, 4, 4));
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseMySql(connectionString, serverVersion);
    options.EnableSensitiveDataLogging(true);
    options.EnableDetailedErrors();
    // options.LogTo(Console.WriteLine, LogLevel.Information);
});

// App Settings
builder.Services.Configure<AppConfig>(builder.Configuration.GetSection("AppConfig"));

// App Services
builder.Services.AddSingleton<AppMemoryCache>();
builder.Services.AddScoped<HangfireJobs>();
builder.Services.AddHostedService<CaesarJobWatcher>();

// Hangfire
builder.Services.AddHangfireServer();
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseConsole()
    .UseStorage(
        new MySqlStorage(
            connectionString,
            new MySqlStorageOptions
            {
                TransactionIsolationLevel = IsolationLevel.ReadCommitted,
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CountersAggregateInterval = TimeSpan.FromMinutes(5),
                PrepareSchemaIfNecessary = true,
                DashboardJobListLimit = 50000,
                TransactionTimeout = TimeSpan.FromMinutes(1),
                TablesPrefix = "Hangfire",
                InvisibilityTimeout = TimeSpan.FromDays(14), // Tasks will not run longer than 14 days
            }))
    .WithJobExpirationTimeout(TimeSpan.FromDays(14)));

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Creating database and applying migrations");

await Defaults.CreateDatabaseApplyMigrations(app);

logger.LogInformation("Creating default roles and users");

await Defaults.CreateDefaultRolesAndUsers(app);

if (app.Environment.IsDevelopment())
{
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

logger.LogInformation("Reading library and storage configs");

var library = builder.Configuration["AppConfig:Library"];
if (string.IsNullOrWhiteSpace(library))
    throw new Exception("Library path not set in application config");
if (library.EndsWith('/'))
    throw new Exception("Library path should not end with slash");
if (library.Contains('\\'))
    throw new Exception("Library path is incorrect, please use forward slashes instead of back slashes");

var storage = builder.Configuration["AppConfig:Storage"];
if (string.IsNullOrWhiteSpace(storage))
    throw new Exception("Storage path not set in application config");
if (library.EndsWith('/'))
    throw new Exception("Storage path should not end with slash");
if (library.Contains('\\'))
    throw new Exception("Storage path is incorrect, please use forward slashes instead of back slashes");

app.UseStaticFiles();

logger.LogInformation("Mapping library and storage routes");

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(library),
    RequestPath = "/static/library"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storage),
    RequestPath = "/static/storage"
});

app.UseAntiforgery();

logger.LogInformation("Mapping controllers");

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

logger.LogInformation("Enabling Hangfire dashboard");

app.UseHangfireDashboard("/Jobs", new DashboardOptions
{
    DashboardTitle = "AstroView Jobs",
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DarkModeEnabled = true,
    AppPath = "/api/back-to-site"
});

logger.LogInformation("Running app");

app.Run();

logger.LogInformation("App is up");
