using Microsoft.AspNetCore.Components;

namespace AstroView.WebApp.Web.Components;

public partial class Breadcrumbs
{
    [Parameter]
    public string PageName { get; set; }

    [Parameter]
    public int DatasetId { get; set; }

    [Parameter]
    public string DatasetName { get; set; }

    [Parameter]
    public int ImageId { get; set; }

    [Parameter]
    public string ImageName { get; set; }

    [Parameter]
    public int FunctionId { get; set; }

    [Parameter]
    public int CaesarJobId { get; set; }

    [Parameter]
    public int DatasetClusterId { get; set; }

    [Parameter]
    public string FunctionName { get; set; }

    private PageItem Root { get; set; }
    private List<PageItem> Sequence { get; set; }

    public Breadcrumbs()
    {
        PageName = "";
        DatasetName = "";
        ImageName = "";
        FunctionName = "";

        Sequence = new List<PageItem>();

        Root = Home;
        Home.AddChild(Datasets);
        Home.AddChild(FileExplorer);
        Home.AddChild(Labels);
        Home.AddChild(Users);
        Home.AddChild(Profile);
        Home.AddChild(NotedFiles);
        Home.AddChild(Help);

        Dataset.AddChild(DatasetImages);
        Dataset.AddChild(DatasetChanges);
        Dataset.AddChild(DatasetJobs);
        Dataset.AddChild(DatasetOutliers);
        Dataset.AddChild(DatasetSimilars);
        Dataset.AddChild(DatasetIndividualSimilars);
        Dataset.AddChild(DatasetPredictions);
        Dataset.AddChild(DatasetFunction);
        Dataset.AddChild(DatasetFunctionUmap);
        Dataset.AddChild(DatasetFunctionHdbscan);
        Dataset.AddChild(DatasetFunctionOutlierFinder);
        Dataset.AddChild(DatasetFunctionSimilaritySearch);
        Dataset.AddChild(DatasetFunctionIndividualSimilaritySearch);
        Dataset.AddChild(DatasetFunctionMorphologyClassifier);
        Dataset.AddChild(DatasetClusters);
        Dataset.AddChild(DatasetExports);

        Datasets.AddChild(Dataset);
        DatasetImages.AddChild(DatasetImage);
        DatasetClusters.AddChild(DatasetCluster);

        Help.AddChild(CreatingDatasets);
        Help.AddChild(CallingCaesarApi);
        Help.AddChild(WorkingWithPixPlot);
        Help.AddChild(TroubleshootingBackgroundJobs);
        Help.AddChild(ViewingChangeHistory);
        Help.AddChild(ExportingData);
    }

    private static readonly PageItem Home = new PageItem { Name = "Home", TitlePattern = "Home", UrlPattern = "/", };
    private static readonly PageItem Datasets = new PageItem { Name = "Datasets", TitlePattern = "Datasets", UrlPattern = "/Datasets", };
    private static readonly PageItem Dataset = new PageItem { Name = "Dataset", TitlePattern = "{DatasetName}", UrlPattern = "/Datasets/{DatasetId}", };
    private static readonly PageItem DatasetImages = new PageItem { Name = "DatasetImages", TitlePattern = "Images", UrlPattern = "/Datasets/{DatasetId}/Images", };
    private static readonly PageItem DatasetImage = new PageItem { Name = "DatasetImage", TitlePattern = "{ImageName}", UrlPattern = "/Datasets/{DatasetId}/Images/{ImageId}", };
    private static readonly PageItem DatasetChanges = new PageItem { Name = "DatasetChanges", TitlePattern = "Changes", UrlPattern = "/Datasets/{DatasetId}/Changes", };
    private static readonly PageItem DatasetJobs = new PageItem { Name = "DatasetJobs", TitlePattern = "Jobs", UrlPattern = "/Datasets/{DatasetId}/Jobs", };
    private static readonly PageItem DatasetOutliers = new PageItem { Name = "DatasetOutliers", TitlePattern = "Outliers", UrlPattern = "/Datasets/{DatasetId}/Outliers/{CaesarJobId}", };
    private static readonly PageItem DatasetSimilars = new PageItem { Name = "DatasetSimilars", TitlePattern = "Similars", UrlPattern = "/Datasets/{DatasetId}/Similars/{CaesarJobId}", };
    private static readonly PageItem DatasetIndividualSimilars = new PageItem { Name = "DatasetIndividualSimilars", TitlePattern = "Individual Similars", UrlPattern = "/Datasets/{DatasetId}/InividualSimilars/{CaesarJobId}", };
    private static readonly PageItem DatasetPredictions = new PageItem { Name = "DatasetPredictions", TitlePattern = "Predictions", UrlPattern = "/Datasets/{DatasetId}/Predictions/{CaesarJobId}", };
    private static readonly PageItem DatasetFunction = new PageItem { Name = "DatasetFunction", TitlePattern = "Function", UrlPattern = "/Datasets/{DatasetId}/Functions/{FunctionId}", };
    private static readonly PageItem DatasetFunctionUmap = new PageItem { Name = "DatasetFunctionUmap", TitlePattern = "UMAP", UrlPattern = "/Datasets/{DatasetId}/Umap", };
    private static readonly PageItem DatasetFunctionHdbscan = new PageItem { Name = "DatasetFunctionHdbscan", TitlePattern = "HDBSCAN", UrlPattern = "/Datasets/{DatasetId}/Hdbscan", };
    private static readonly PageItem DatasetFunctionOutlierFinder = new PageItem { Name = "DatasetFunctionOutlierFinder", TitlePattern = "Outlier Finder", UrlPattern = "/Datasets/{DatasetId}/OutlierFinder", };
    private static readonly PageItem DatasetFunctionSimilaritySearch = new PageItem { Name = "DatasetFunctionSimilaritySearch", TitlePattern = "Similarity Search", UrlPattern = "/Datasets/{DatasetId}/SimilaritySearch", };
    private static readonly PageItem DatasetFunctionIndividualSimilaritySearch = new PageItem { Name = "DatasetFunctionIndividualSimilaritySearch", TitlePattern = "Individual Similarity Search", UrlPattern = "/Datasets/{DatasetId}/IndividualSimilaritySearch", };
    private static readonly PageItem DatasetFunctionMorphologyClassifier = new PageItem { Name = "DatasetFunctionMorphologyClassifier", TitlePattern = "Source Morphology ViT Classifier", UrlPattern = "/Datasets/{DatasetId}/MorphologyClassifier", };
    private static readonly PageItem DatasetClusters = new PageItem { Name = "DatasetClusters", TitlePattern = "Clusters", UrlPattern = "/Datasets/{DatasetId}/Hdbscan/{CaesarJobId}/Clusters", };
    private static readonly PageItem DatasetCluster = new PageItem { Name = "DatasetCluster", TitlePattern = "Cluster", UrlPattern = "/Datasets/{DatasetId}/Hdbscan/{CaesarJobId}/Clusters/{DatasetClusterId}", };
    private static readonly PageItem DatasetExports = new PageItem { Name = "DatasetExports", TitlePattern = "Exports", UrlPattern = "/Datasets/{DatasetId}/Exports", };
    private static readonly PageItem FileExplorer = new PageItem { Name = "File Explorer", TitlePattern = "File Explorer", UrlPattern = "/FileExplorer", };
    private static readonly PageItem Labels = new PageItem { Name = "Labels", TitlePattern = "Labels", UrlPattern = "/Labels", };
    private static readonly PageItem Users = new PageItem { Name = "Users", TitlePattern = "Users", UrlPattern = "/Users", };
    private static readonly PageItem Profile = new PageItem { Name = "Profile", TitlePattern = "Profile", UrlPattern = "/Profile", };
    private static readonly PageItem NotedFiles = new PageItem { Name = "NotedFiles", TitlePattern = "Noted Files", UrlPattern = "/NotedFiles", };
    private static readonly PageItem Help = new PageItem { Name = "Help", TitlePattern = "Help", UrlPattern = "/Help", };
    private static readonly PageItem CreatingDatasets = new PageItem { Name = "CreatingDatasets", TitlePattern = "Creating Datasets", UrlPattern = "/Help/CreatingDatasets", };
    private static readonly PageItem CallingCaesarApi = new PageItem { Name = "CallingCaesarApi", TitlePattern = "Calling Caesar API", UrlPattern = "/Help/CallingCaesarApi", };
    private static readonly PageItem WorkingWithPixPlot = new PageItem { Name = "WorkingWithPixPlot", TitlePattern = "Working with PixPlot", UrlPattern = "/Help/WorkingWithPixPlot", };
    private static readonly PageItem TroubleshootingBackgroundJobs = new PageItem { Name = "TroubleshootingBackgroundJobs", TitlePattern = "Troubleshooting Background Jobs", UrlPattern = "/Help/TroubleshootingBackgroundJobs", };
    private static readonly PageItem ViewingChangeHistory = new PageItem { Name = "ViewingChangeHistory", TitlePattern = "Viewing Change History", UrlPattern = "/Help/ViewingChangeHistory", };
    private static readonly PageItem ExportingData = new PageItem { Name = "ExportingData", TitlePattern = "Exporting Data", UrlPattern = "/Help/ExportingData", };

    protected override void OnInitialized()
    {
        PageItem? item;
        if(Root.Name == PageName)
        {
            item = Root;
        }
        else
        {
            item = FindPageItem(PageName, Root.Children);
            if (item == null)
                throw new Exception("Page was not found: " + PageName);
        }

        while (item != null)
        {
            Sequence.Insert(0, item);
            item = item.Parent;
        }
    }

    private PageItem? FindPageItem(string name, List<PageItem> items)
    {
        foreach (var item in items)
        {
            if (item.Name == name)
                return item;

            var found = FindPageItem(name, item.Children);
            if (found != null)
                return found;
        }

        return null;
    }

    private string GetTitle(PageItem item)
    {
        return item.TitlePattern
            .Replace("{DatasetName}", DatasetName)
            .Replace("{FunctionName}", FunctionName)
            .Replace("{ImageName}", ImageName);
    }

    private string GetUrl(PageItem item)
    {
        return item.UrlPattern
            .Replace("{DatasetId}", DatasetId.ToString())
            .Replace("{FunctionId}", FunctionId.ToString())
            .Replace("{CaesarJobId}", CaesarJobId.ToString())
            .Replace("{ImageId}", ImageId.ToString());
    }

    private class PageItem
    {
        public string Name { get; set; }
        public string TitlePattern { get; set; }
        public string UrlPattern { get; set; }

        public PageItem? Parent { get; set; }
        public List<PageItem> Children { get; set; }

        public PageItem()
        {
            Name = "";
            TitlePattern = "";
            UrlPattern = "";
            Children = new List<PageItem>();
        }

        public PageItem AddChild(PageItem item)
        {
            item.Parent = this;

            Children.Add(item);

            return this;
        }
    }
}
