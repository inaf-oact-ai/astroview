using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AstroView.WebApp.App.Integrations.CaesarApi;

public class CaesarDataset
{
    public List<CaesarDatasetItem> data { get; set; } = null!;

    public static async Task EnumerateItems(string file, Func<CaesarDatasetItem, int, double, Task> processItem)
    {
        var index = 0;
        var serializer = new JsonSerializer();
        using (var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var sr = new StreamReader(fs))
        using (var reader = new JsonTextReader(sr))
        {
            reader.Read(); // json start
            reader.Read(); // property name
            reader.Read(); // array start

            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.StartObject)
                    continue;

                var genericItem = serializer.Deserialize<GenericCaesarDatasetItem>(reader)!;
                var item = genericItem.ToCaesarDatasetItem();

                var readPercent = fs.Position * 1.0 / fs.Length * 100;

                await processItem(item, index, readPercent);

                index++;
            }
        }
    }
}

public class CaesarDatasetItem
{
    public string[] filepaths { get; set; } = null!;
    public string sname { get; set; } = null!; // image name without extension
    public long[]? id { get; set; } = null!; // caesar api label ids
    public string[]? label { get; set; } = null!; // caesar api labels
    public string? telescope { get; set; }
    public string? survey { get; set; }
    public string? project { get; set; }
    public int? nx { get; set; }
    public int? ny { get; set; }
    public double? dx { get; set; }
    public double? dy { get; set; }
    public double? ra { get; set; }
    public double? dec { get; set; }
    public double? l { get; set; }
    public double? b { get; set; }
    public int? nsources { get; set; }
    public double? bkg { get; set; }
    public double? rms { get; set; }
    public JToken? feats { get; set; }

    // HDBSCAN results
    public int? clust_id { get; set; }
    public double? clust_prob { get; set; }
    public double? clust_outlier_score { get; set; }

    // Outlier Finder results
    public int? is_outlier { get; set; }
    public double? outlier_score { get; set; }

    // Similarity Search results
    public int[]? nn_indices { get; set; }
    public double[]? nn_scores { get; set; }

    // Individual Similarity Search results
    public int? nn_index { get; set; }
    public double? nn_score { get; set; }

    // Morphology Classifier results
    public string[]? label_pred { get; set; }
    public double[]? prob_pred { get; set; }
}

public class GenericCaesarDataset
{
    public List<GenericCaesarDatasetItem> data { get; set; } = null!;
}

public class GenericCaesarDatasetItem
{
    public string[] filepaths { get; set; } = null!;
    public string sname { get; set; } = null!; // image name without extension
    public object? id { get; set; } = null!; // int or int[]
    public object? label { get; set; } = null!; // string or string[]
    public string? telescope { get; set; }
    public string? survey { get; set; }
    public string? project { get; set; }
    public int? nx { get; set; }
    public int? ny { get; set; }
    public double? dx { get; set; }
    public double? dy { get; set; }
    public double? ra { get; set; }
    public double? dec { get; set; }
    public double? l { get; set; }
    public double? b { get; set; }
    public int? nsources { get; set; }
    public double? bkg { get; set; }
    public double? rms { get; set; }
    public JToken? feats { get; set; }

    // HDBSCAN results
    public int? clust_id { get; set; }
    public double? clust_prob { get; set; }
    public double? clust_outlier_score { get; set; }

    // Outlier Finder results
    public int? is_outlier { get; set; }
    public double? outlier_score { get; set; }

    // Similarity Search results
    public int[]? nn_indices { get; set; }
    public double[]? nn_scores { get; set; }

    // Individual Similarity Search results
    public int? nn_index { get; set; }
    public double? nn_score { get; set; }

    // Morphology Classifier results
    public object? label_pred { get; set; } // string or string[]
    public object? prob_pred { get; set; } // double or double[]

    public CaesarDatasetItem ToCaesarDatasetItem()
    {
        var item = new CaesarDatasetItem
        {
            filepaths = filepaths,
            sname = sname,
            // id - mapped below
            // label - mapped below
            telescope = sname,
            survey = sname,
            project = sname,
            nx = nx,
            ny = ny,
            dx = dx,
            dy = dy,
            ra = ra,
            dec = dec,
            l = l,
            b = b,
            nsources = nsources,
            bkg = bkg,
            rms = rms,
            feats = feats,

            clust_id = clust_id,
            clust_prob = clust_prob,
            clust_outlier_score = clust_outlier_score,

            is_outlier = is_outlier,
            outlier_score = outlier_score,

            nn_indices = nn_indices,
            nn_scores = nn_scores,

            nn_index = nn_index,
            nn_score = nn_score,

            // label_pred - mapped below
            // prob_pred - mapped below
        };

        if (id == null)
        {
            item.id = null;
        }
        else if (id is long)
        {
            item.id = new[] { (long)id };
        }
        else
        {
            item.id = ((JArray)id).ToObject<long[]>();
        }

        if (label == null)
        {
            item.label = null;
        }
        else if (label is string)
        {
            item.label = new[] { (string)label };
        }
        else
        {
            item.label = ((JArray)label).ToObject<string[]>();
        }

        if (label_pred == null)
        {
            item.label_pred = null;
        }
        else if (label_pred is string)
        {
            item.label_pred = new[] { (string)label_pred };
        }
        else
        {
            item.label_pred = ((JArray)label_pred).ToObject<string[]>();
        }

        if (prob_pred == null)
        {
            item.prob_pred = null;
        }
        else if (prob_pred is double)
        {
            item.prob_pred = new[] { (double)prob_pred };
        }
        else
        {
            item.prob_pred = ((JArray)prob_pred).ToObject<double[]>();
        }

        return item;
    }
}