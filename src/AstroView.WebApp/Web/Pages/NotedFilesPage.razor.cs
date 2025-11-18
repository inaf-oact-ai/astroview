using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.Data;
using AstroView.WebApp.Web.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace AstroView.WebApp.Web.Pages;

public partial class NotedFilesPage
{
    public NotedFilesPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
    }

    protected async Task RemoveAll()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var user = await db.Users.FirstAsync(r => r.Id == AppVm.UserId);

            user.ClearNotes();

            await db.SaveChangesAsync();

            await AppVm.UserNotesChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task Remove(NotedFile file)
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var user = await db.Users.FirstAsync(r => r.Id == AppVm.UserId);

            user.RemoveFileFromNotes(file.Path);

            await db.SaveChangesAsync();

            await AppVm.UserNotesChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }
}
