namespace AstroView.WebApp.Web.Pages.Datasets;

public partial class DatasetPage
{
    // Backward compatibility: This class is serialized and stored in database
    public class DisplayModeParams
    {
        public double Contrast { get; set; }
        public double SigmaLow { get; set; }
        public double SigmaUp { get; set; }
        public bool UsePil { get; set; }
        public bool SubtractBkg { get; set; }
        public bool ClipData { get; set; }
        public bool ZscaleData { get; set; }
        public bool ApplyMinMax { get; set; }

        public DisplayModeParams()
        {
            Contrast = 0.25;
            SigmaLow = 5;
            SigmaUp = 30;
            UsePil = true;
            SubtractBkg = false;
            ClipData = false;
            ZscaleData = false;
            ApplyMinMax = true;
        }
    }
}
