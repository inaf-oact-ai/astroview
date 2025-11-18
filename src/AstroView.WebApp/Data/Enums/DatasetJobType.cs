namespace AstroView.WebApp.Data.Enums;

public enum DatasetJobType
{
    None = 0,
    AddImages = 1,
    ImportFeatures = 2,
    ProcessCaesarJobOutput = 3,
    CreateDatasetFromClusters = 4,
    RemoveDisplayMode = 5,
    GeneratePixPlot = 6,
    GenerateCaesarDataset = 7,
    GenerateRandomFeatures = 8,
    RenderDisplayMode = 9,
    ExportImageListToCsv = 10,
    ExportImageListToCaesarDataset = 11,
    ApplyPredictions = 12,
    ImportMetadataFromFile = 13,
}
