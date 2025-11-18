namespace AstroView.WebApp.Data.Entities;

public class ClusterItemDbe
{
    public int Id { get; set; }
    public int ClusterId { get; set; }
    public int ImageId { get; set; }
    public double Probability { get; set; } // Cluster membership score (=0 if noise)
    public double OutlierScore { get; set; }

    public virtual ClusterDbe Cluster { get; set; } = null!;
    public virtual ImageDbe Image { get; set; } = null!;
}
