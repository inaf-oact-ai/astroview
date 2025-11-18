using Microsoft.EntityFrameworkCore;

namespace AstroView.WebApp.Data.Entities;

[Index(nameof(Index))]
public class ClusterDbe
{
    public int Id { get; set; }
    public int CaesarJobId { get; set; }
    public int Index { get; set; } // Cluster identifier (=-1 if noise)
    public string Name { get; set; } = null!;

    public virtual CaesarJobDbe CaesarJob { get; set; } = null!;
    public virtual List<ClusterItemDbe> Items { get; set; } = null!;
}
