using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public static class ThemeUploadContent
{
    public static MultipartFormDataContent Create(
        string manifestJson,
        string filename,
        params (string Path, string Contents)[] files)
    {
        var zipStream = CreateThemeZip(manifestJson, files);
        var fileContent = new StreamContent(zipStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        return new MultipartFormDataContent
        {
            { fileContent, "file", filename }
        };
    }

    private static MemoryStream CreateThemeZip(string manifestJson, params (string Path, string Contents)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "theme.json", manifestJson);
            foreach (var file in files)
            {
                AddEntry(archive, file.Path, file.Contents);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void AddEntry(ZipArchive archive, string path, string contents)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(contents);
    }
}
