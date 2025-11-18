using AstroView.WebApp.App.Utils;
using System.IO;

namespace AstroView.WebApp.App;

public class AppConfig
{
    public bool IsLinux { get; set; }
    public string Library { get; set; } = null!;
    public string Storage { get; set; } = null!;
    public string PythonExecutable { get; set; } = null!;
    public string CaesarApi { get; set; } = null!;

    public DatasetPaths GetDatasetPaths(int datasetId)
    {
        return new DatasetPaths(datasetId, Storage);
    }

    public DisplayModePaths GetDisplayModePaths(int datasetId, int displayModeId)
    {
        return new DisplayModePaths(datasetId, displayModeId, Storage);
    }

    public string GetCaesarJobOutputPath(int datasetId, int jobId)
    {
        return Path.Combine(Storage, "datasets", datasetId.ToString(), "caesar", "jobs", jobId.ToString()).UnixFormat();
    }

    public class DatasetPaths
    {
        private readonly int datasetId;
        private readonly string storagePath;

        public DatasetPaths(int datasetId, string storagePath)
        {
            this.datasetId = datasetId;
            this.storagePath = storagePath;
        }

        public string RootDirectory
        {
            get
            {
                return Path.Combine(storagePath, "datasets", datasetId.ToString()).UnixFormat();
            }
        }

        public string ExportsCsvDirectory
        {
            get
            {
                return Path.Combine(RootDirectory, "exports", "csv").UnixFormat();
            }
        }

        public string ExportsJsonDirectory
        {
            get
            {
                return Path.Combine(RootDirectory, "exports", "caesar-datasets").UnixFormat();
            }
        }

        public string FitsListTxt
        {
            get
            {
                return Path.Combine(RootDirectory, "fitsList.txt").UnixFormat();
            }
        }
    }

    public class DisplayModePaths
    {
        private readonly int datasetId;
        private readonly int displayModeId;
        private readonly string storagePath;

        public DisplayModePaths(int datasetId, int displayModeId, string storagePath)
        {
            this.datasetId = datasetId;
            this.displayModeId = displayModeId;
            this.storagePath = storagePath;
        }

        public string MapDirectory
        {
            get
            {
                return Path.Combine(storagePath,
                    "datasets", datasetId.ToString(),
                    "display-modes", displayModeId.ToString(),
                    "map").UnixFormat();
            }
        }

        public string MapImagesTxt
        {
            get
            {
                return Path.Combine(MapDirectory, "images.txt").UnixFormat();
            }
        }

        public string MapFeaturesTxt
        {
            get
            {
                return Path.Combine(MapDirectory, "features.txt").UnixFormat();
            }
        }

        public string RootDirectoryLink { get { return GetStorageRelatedLink(RootDirectory); } }
        public string RootDirectory
        {
            get
            {
                return Path.Combine(storagePath,
                    "datasets", datasetId.ToString(),
                    "display-modes", displayModeId.ToString()).UnixFormat();
            }
        }

        public string ImagesDirectory
        {
            get
            {
                return Path.Combine(RootDirectory, "images").UnixFormat();
            }
        }

        public string CaesarDatasetJsonLink { get { return GetStorageRelatedLink(CaesarDatasetJson); } }
        public string CaesarDatasetJson
        {
            get
            {
                return Path.Combine(RootDirectory, "caesar-dataset.json").UnixFormat();
            }
        }

        private string GetStorageRelatedLink(string path)
        {
            return Path.Combine("/FileExplorer/storage/", Path.GetRelativePath(storagePath, path)).UnixFormat();
        }

        public string CaesarDatasetDownloadLink
        {
            get
            {
                return Path.Combine("/static/storage/", Path.GetRelativePath(storagePath, CaesarDatasetJson)).UnixFormat();
            }
        }
    }
}