namespace AstroView.WebApp.App.Models;

public class PixPlotParams
{
    public int MinClusterSize { get; set; }
    public int MaxClusters { get; set; }
    public int AtlasCellSize { get; set; }
    public int UmapNeighbors { get; set; }
    public double UmapMinDist { get; set; }
    public int UmapComponents { get; set; }
    public string UmapMetric { get; set; }
    public double PointgridFill { get; set; }
    public int ImageMinSize { get; set; }
    public int Seed { get; set; }
    public int KmeansClusters { get; set; }

    public PixPlotParams()
    {
        MinClusterSize = 20;
        MaxClusters = 10;
        AtlasCellSize = 32;
        UmapNeighbors = 15;
        UmapMinDist = 0.01;
        UmapComponents = 2;
        UmapMetric = "correlation";
        PointgridFill = 0.05;
        ImageMinSize = 100;
        Seed = 24;
        KmeansClusters = 4;
    }
}
