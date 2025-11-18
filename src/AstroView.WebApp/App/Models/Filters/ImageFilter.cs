using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;

namespace AstroView.WebApp.App.Models.Filters;

public class ImageFilter
{
    public string Name { get; set; }
    public string Telescope { get; set; }
    public string Survey { get; set; }
    public string Project { get; set; }
    public double? Ra { get; set; }
    public double? Dec { get; set; }
    public double? GLon { get; set; }
    public double? Glat { get; set; }
    public List<int> LabelIds { get; set; }

    public ImageFilter()
    {
        Name = "";
        Telescope = "";
        Survey = "";
        Project = "";
        LabelIds = new List<int>();
    }

    public bool IsApplied()
    {
        return Name.IsNotEmpty()
            || Telescope.IsNotEmpty()
            || Survey.IsNotEmpty()
            || Project.IsNotEmpty()
            || Ra != null
            || Dec != null
            || GLon != null
            || Glat != null
            || LabelIds.Count > 0;
    }

    public void Clear()
    {
        Name = "";
        Telescope = "";
        Survey = "";
        Project = "";
        Ra = null;
        Dec = null;
        GLon = null;
        Glat = null;
        LabelIds.Clear();
    }

    public string GetDescription(List<LabelDbe> allLabels)
    {
        var builder = new StringBuilder();

        if (Name.IsNotEmpty())
            builder.Append($"Name={Name};");
        if (Telescope.IsNotEmpty())
            builder.Append($"Telescope={Telescope};");
        if (Survey.IsNotEmpty())
            builder.Append($"Survey={Survey};");
        if (Project.IsNotEmpty())
            builder.Append($"Project={Project};");
        if (Ra != null)
            builder.Append($"Ra={Ra};");
        if (Dec != null)
            builder.Append($"Dec={Dec};");
        if (GLon != null)
            builder.Append($"GLon={GLon};");
        if (Glat != null)
            builder.Append($"Glat={Glat};");

        if (LabelIds.Count > 0)
        {
            builder.Append($"Labels=");
            foreach (var labelId in LabelIds)
            {
                var label = allLabels.First(r => r.Id == labelId);
                builder.Append($"{label.Name},");
            }
            builder.Remove(builder.Length - 1, 1);
        }

        return builder.ToString();
    }

    public IQueryable<ImageDbe> Apply(IQueryable<ImageDbe> query)
    {
        var name = Name?.Trim();
        if (name != null && name.IsNotEmpty())
        {
            if (name.StartsWith("*") && name.EndsWith("*"))
            {
                query = query.Where(r => r.Name.Contains(name.Trim('*')));
            }
            else if (name.EndsWith("*"))
            {
                query = query.Where(r => r.Name.StartsWith(name.Trim('*')));
            }
            else if (name.StartsWith("*"))
            {
                query = query.Where(r => r.NameReversed.StartsWith(new string(name.Trim('*').ReverseStr().ToArray())));
            }
            else
            {
                query = query.Where(r => r.Name == name);
            }
        }

        if (Telescope.IsNotEmpty())
            query = query.Where(r => r.Telescope != null && r.Telescope.Contains(Telescope.Trim()));

        if (Survey.IsNotEmpty())
            query = query.Where(r => r.Survey != null && r.Survey.Contains(Survey.Trim()));

        if (Project.IsNotEmpty())
            query = query.Where(r => r.Project != null && r.Project.Contains(Project.Trim()));

        if (Ra != null)
            query = query.Where(r => r.Ra != null && r.Ra == Ra);

        if (Dec != null)
            query = query.Where(r => r.Dec != null && r.Dec == Dec);

        if (GLon != null)
            query = query.Where(r => r.L != null && r.L == GLon);

        if (Glat != null)
            query = query.Where(r => r.B != null && r.B == Glat);

        if (LabelIds.Count > 0)
        {
            foreach (var labelId in LabelIds)
            {
                query = query.Where(r => r.Labels.Any(t => t.LabelId == labelId));
            }
        }

        return query;
    }
}
