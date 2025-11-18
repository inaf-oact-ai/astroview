using AstroView.WebApp.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static AstroView.WebApp.Web.Pages.Functions.UmapPage;

namespace AstroView.WebApp.Web.Components;

public partial class DisplayModeSelector
{
    private readonly IDbContextFactory<AppDbContext> dbf;

    [Parameter]
    public int DatasetId { get; set; }

    [Parameter]
    public DisplayMode SelectedDisplayMode { get; set; } = null!;
    
    [Parameter]
    public EventCallback<DisplayMode> OnDisplayModeChanged { get; set; }

    private List<DisplayMode> DisplayModes { get; set; }

    public DisplayModeSelector(IDbContextFactory<AppDbContext> dbf)
    {
        this.dbf = dbf;

        DisplayModes = new List<DisplayMode>();
    }

    protected override async Task OnInitializedAsync()
    {
        using var db = await dbf.CreateDbContextAsync();

        var dataset = await db.Datasets.AsNoTracking().Where(r => r.Id == DatasetId).FirstAsync();

        var variations = await db.DisplayModes
            .AsNoTracking()
            .Where(r => r.DatasetId == DatasetId)
            .OrderBy(r => r.Extension)
            .ThenBy(r => r.Name)
            .Select(r => new DisplayMode { Name = r.Name, DisplayModeId = r.Id })
            .ToListAsync();

        DisplayModes.AddRange(variations);

        await SelectVariation(DisplayModes.First());
    }

    private async Task SelectVariation(DisplayMode variation)
    {
        SelectedDisplayMode = variation;

        await OnDisplayModeChanged.InvokeAsync(SelectedDisplayMode);
    }
}
