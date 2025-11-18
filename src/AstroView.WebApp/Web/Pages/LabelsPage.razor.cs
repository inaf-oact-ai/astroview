using AstroView.WebApp.App;
using AstroView.WebApp.App.Models;
using AstroView.WebApp.Data;
using AstroView.WebApp.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace AstroView.WebApp.Web.Pages;

public partial class LabelsPage
{
    [SupplyParameterFromQuery]
    private int? PageNumber { get; set; }
    private int? _pageNumber { get; set; }

    [SupplyParameterFromQuery]
    private int? PageSize { get; set; }
    private int? _pageSize { get; set; }

    private LabelsPageVm vm;

    public LabelsPage(
        IJSRuntime js,
        IOptions<AppConfig> config,
        AuthenticationStateProvider asp,
        NavigationManager nav,
        IDbContextFactory<AppDbContext> dbf)
        : base(js, config, asp, nav, dbf)
    {
        vm = new LabelsPageVm();
    }

    protected override async Task OnInitializedAsync()
    {
        _pageNumber = PageNumber;
        _pageSize = PageSize;

        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadLabels(db);
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

            await LoadLabels(db);
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

    protected async Task DisplayLabels()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            await LoadLabels(db);
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    private async Task LoadLabels(AppDbContext db)
    {
        var labelsCount = await db.Labels.CountAsync();

        vm.Paging.Calculate(PageNumber, PageSize, labelsCount);

        vm.Labels = await db.Labels
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Skip(vm.Paging.SkipRecordsCount)
            .Take(vm.Paging.PageSize)
            .ToListAsync();
    }

    protected async Task ShowEditForm(int labelId)
    {
        try
        {
            if (labelId == 0)
            {
                vm.EditLabelModel.Id = 0;
                vm.EditLabelModel.Name = "";
                vm.EditLabelModel.Color = "blue";
            }
            else
            {
                var label = vm.Labels.First(r => r.Id == labelId);
                vm.EditLabelModel.Id = label.Id;
                vm.EditLabelModel.Name = label.Name;
                vm.EditLabelModel.Color = label.Color;
            }

            await js.InvokeVoidAsync("showModal");
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task SaveLabel()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            LabelDbe label;
            if (vm.EditLabelModel.Id == 0)
            {
                label = new LabelDbe();
                db.Labels.Add(label);
            }
            else
            {
                label = await db.Labels.FirstAsync(r => r.Id == vm.EditLabelModel.Id);
            }

            label.Name = vm.EditLabelModel.Name.ToUpper();
            label.Color = vm.EditLabelModel.Color.ToLower();

            await db.SaveChangesAsync();

            await js.InvokeVoidAsync("hideModal");

            await DisplayLabels();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected async Task RandomizeColors()
    {
        try
        {
            using var db = await dbf.CreateDbContextAsync();

            var labels = await db.Labels.ToListAsync();

            var random = new Random();
            foreach (var label in labels)
            {
                label.Color = Defaults.Colors[random.Next(Defaults.Colors.Count)].ToLower();
            }

            await db.SaveChangesAsync();

            await DisplayLabels();
        }
        catch (Exception ex)
        {
            await AppVm.ExceptionThrown.InvokeAsync(ex);
        }
    }

    protected class LabelsPageVm
    {
        public Paging Paging { get; set; }
        public List<LabelDbe> Labels { get; set; }
        public EditLabelModel EditLabelModel { get; set; }

        public LabelsPageVm()
        {
            Paging = new Paging();
            Labels = new List<LabelDbe>();
            EditLabelModel = new EditLabelModel();
        }
    }

    protected class EditLabelModel
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Color { get; set; }

        public EditLabelModel()
        {
            Name = "";
            Color = "";
        }
    }
}
