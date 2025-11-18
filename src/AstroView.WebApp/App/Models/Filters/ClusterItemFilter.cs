using AstroView.WebApp.App.Utils;
using AstroView.WebApp.Data.Entities;
using System.Data;
using System.Text;

namespace AstroView.WebApp.App.Models.Filters;

public class ClusterItemFilter
{
    public string Name { get; set; }
    public string Telescope { get; set; }
    public string Survey { get; set; }
    public string Project { get; set; }
    public double? Ra { get; set; }
    public double? Dec { get; set; }
    public double? GLon { get; set; }
    public double? Glat { get; set; }

    public double? MinScore { get; set; }
    public double? MaxScore { get; set; }

    public double? MinProbability { get; set; }
    public double? MaxProbability { get; set; }

    public List<int> LabelIds { get; set; }

    public ClusterItemFilter()
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
            || MinScore != null
            || MaxScore != null
            || MinProbability != null
            || MaxProbability != null
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
        MinScore = null;
        MaxScore = null;
        MinProbability = null;
        MaxProbability = null;
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
        if (MinScore != null)
            builder.Append($"Score>={MinScore};");
        if (MaxScore != null)
            builder.Append($"Score<={MaxScore};");   
        if (MinProbability != null)
            builder.Append($"Probability>={MinProbability};");
        if (MaxProbability != null)
            builder.Append($"Probability<={MaxProbability};");

        if (LabelIds.Count > 0)
        {
            builder.Append($"Labels=");
            foreach (var labelId in LabelIds)
            {
                var label = allLabels.First(r => r.Id == labelId);
                builder.Append($"{label.Name},");
            }
            builder.Remove(builder.Length - 1, 1);
            builder.Append(';');
        }

        return builder.ToString();
    }

    public IQueryable<ClusterItemDbe> Apply(IQueryable<ClusterItemDbe> query)
    {
        var name = Name?.Trim();
        if (name != null && name.IsNotEmpty())
        {
            if (name.StartsWith("*") && name.EndsWith("*"))
            {
                query = query.Where(r => r.Image.Name.Contains(name.Trim('*')));
            }
            else if (name.EndsWith("*"))
            {
                query = query.Where(r => r.Image.Name.StartsWith(name.Trim('*')));
            }
            else if (name.StartsWith("*"))
            {
                query = query.Where(r => r.Image.NameReversed.StartsWith(new string(name.Trim('*').ReverseStr().ToArray())));
            }
            else
            {
                query = query.Where(r => r.Image.Name == name);
            }
        }

        if (Telescope.IsNotEmpty())
            query = query.Where(r => r.Image.Telescope != null && r.Image.Telescope.Contains(Telescope.Trim()));

        if (Survey.IsNotEmpty())
            query = query.Where(r => r.Image.Survey != null && r.Image.Survey.Contains(Survey.Trim()));

        if (Project.IsNotEmpty())
            query = query.Where(r => r.Image.Project != null && r.Image.Project.Contains(Project.Trim()));

        if (Ra != null)
            query = query.Where(r => r.Image.Ra != null && r.Image.Ra == Ra);

        if (Dec != null)
            query = query.Where(r => r.Image.Dec != null && r.Image.Dec == Dec);

        if (GLon != null)
            query = query.Where(r => r.Image.L != null && r.Image.L == GLon);

        if (Glat != null)
            query = query.Where(r => r.Image.B != null && r.Image.B == Glat);

        if (MinScore != null)
            query = query.Where(r => r.OutlierScore >= MinScore);

        if (MaxScore != null)
            query = query.Where(r => r.OutlierScore <= MaxScore);

        if (MinProbability != null)
            query = query.Where(r => r.Probability >= MinProbability);

        if (MaxProbability != null)
            query = query.Where(r => r.Probability <= MaxProbability);

        if (LabelIds.Count > 0)
        {
            foreach (var labelId in LabelIds)
            {
                query = query.Where(r => r.Image.Labels.Any(t => t.LabelId == labelId));
            }
        }

        return query;
    }
}
