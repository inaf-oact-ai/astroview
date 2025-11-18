using AstroView.WebApp.App;
using AstroView.WebApp.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace AstroView.WebApp.Web.Pages;

public partial class HomePage
{
    private HomeVm vm;

    public HomePage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new HomeVm();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            vm.DatasetsCount = await db.Datasets.CountAsync();
            vm.ImagesCount = await db.Images.CountAsync();
            vm.LabelsCount = await db.Labels.CountAsync();
            
            // throw new Exception("Test error");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private class HomeVm
    {
        public int DatasetsCount { get; set; }
        public int ImagesCount { get; set; }
        public int ModelsCount { get; set; }
        public int LabelsCount { get; set; }

        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public int FilesCount { get; set; }

        public HomeVm()
        {
            Path = "";
        }
    }
}
