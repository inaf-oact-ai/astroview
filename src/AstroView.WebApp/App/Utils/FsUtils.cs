namespace AstroView.WebApp.App.Utils;

public static class FsUtils
{
    public static int GetFilesCount(List<string> paths)
    {
        var filesCount = 0;
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                filesCount += Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).Count();
            }
            else
            {
                filesCount++;
            }
        }

        return filesCount;
    }

    public static bool HasMoreFiles(List<string> paths, int value)
    {
        var files = 0;
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                files += Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).Count();
            }
            else
            {
                files++;
            }

            if (files > value)
                return true;
        }

        return false;
    }
}
