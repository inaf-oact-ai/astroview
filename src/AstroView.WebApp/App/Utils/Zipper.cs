using System.Formats.Tar;
using System.IO.Compression;

namespace AstroView.WebApp.App.Utils;

public static class Zipper
{
    public static async Task Zip(string archive, string inputDir, CancellationToken ct = default)
    {
        await using var memoryStream = new MemoryStream();
        await using var tarStream = new MemoryStream();
        await TarFile.CreateFromDirectoryAsync(inputDir, tarStream, false, ct);
        await using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
        {
            tarStream.Seek(0, SeekOrigin.Begin);
            await tarStream.CopyToAsync(gzipStream, ct);
        }

        using (var stream = File.Create(archive))
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            memoryStream.WriteTo(stream);
        }
    }

    public static async Task Unzip(string archive, string outputDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        using var stream = File.OpenRead(archive);
        await using var memoryStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
        {
            await gzipStream.CopyToAsync(memoryStream, ct);
        }
        memoryStream.Seek(0, SeekOrigin.Begin);
        await TarFile.ExtractToDirectoryAsync(
            memoryStream,
            outputDir,
            overwriteFiles: true,
            cancellationToken: ct
        );
    }
}
