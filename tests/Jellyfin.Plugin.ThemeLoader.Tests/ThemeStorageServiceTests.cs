using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Domain;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using Jellyfin.Plugin.ThemeLoader.Services;

using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public sealed class ThemeStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "theme-loader-tests-" + Guid.NewGuid().ToString("N"));
    private readonly TestStateRepository _stateRepository = new();

    [Fact]
    public async Task UpdateTheme_ManifestWithoutId_GeneratesIdAndSluggedDirectory()
    {
        var service = CreateService();
        await using var zip = CreateThemeZip(
            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "style.css"
            }
            """,
            ("style.css", ".x{background:url('images/bg.png')}"),
            ("images/bg.png", "png"));

        var result = await service.UpdateTheme(zip, CancellationToken.None);
        var installedTheme = Assert.Single(_stateRepository.State.InstalledThemes.Values);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(result.Id, installedTheme.Id);
        Assert.Equal(result.Id, _stateRepository.State.SelectedThemeId);
        Assert.True(_stateRepository.State.Enabled);
        Assert.Equal("style.css", installedTheme.IndexFilename);
        Assert.Equal("Safe_Theme_Name_v0.1.0.0", installedTheme.Directory);
        Assert.True(File.Exists(Path.Combine(_root, "themes", installedTheme.Directory, "images", "bg.png")));
        Assert.True(_stateRepository.Saved);
    }

    [Theory]
    [InlineData("v1/2")]
    [InlineData("v1\\2")]
    [InlineData("v1:2")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task UpdateTheme_RejectsUnsafeVersion(string version)
    {
        var service = CreateService();
        await using var zip = CreateThemeZip(
            $$"""
            {
              "name": "Safe Theme Name",
              "version": "{{version.Replace("\\", "\\\\")}}",
              "entrypoint": "style.css"
            }
            """,
            ("style.css", ""));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.UpdateTheme(zip, CancellationToken.None));
    }

    [Theory]
    [InlineData("""{ "version": "v0.1.0.0", "entrypoint": "style.css" }""")]
    [InlineData("""{ "name": "Safe Theme Name", "entrypoint": "style.css" }""")]
    [InlineData("""{ "name": "Safe Theme Name", "version": "v0.1.0.0" }""")]
    public async Task UpdateTheme_RejectsManifestMissingRequiredFields(string manifestJson)
    {
        var service = CreateService();
        await using var zip = CreateThemeZip(manifestJson, ("style.css", ""));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.UpdateTheme(zip, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateTheme_DuplicateNameAndVersion_ReplacesExistingTheme()
    {
        var service = CreateService();
        await using var firstZip = CreateThemeZip(
            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "style.css"
            }
            """,
            ("style.css", ".x{background:url('images/old.png')}"),
            ("images/old.png", "old"));

        var first = await service.UpdateTheme(firstZip, CancellationToken.None);

        await using var secondZip = CreateThemeZip(
            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "style.css"
            }
            """,
            ("style.css", ".x{background:url('images/new.png')}"),
            ("images/new.png", "new"));

        var second = await service.UpdateTheme(secondZip, CancellationToken.None);
        var installedTheme = Assert.Single(_stateRepository.State.InstalledThemes.Values);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(second.Id, installedTheme.Id);
        Assert.Equal(second.Id, _stateRepository.State.SelectedThemeId);
        Assert.True(File.Exists(Path.Combine(_root, "themes", installedTheme.Directory, "images", "new.png")));
        Assert.False(File.Exists(Path.Combine(_root, "themes", installedTheme.Directory, "images", "old.png")));
    }

    [Fact]
    public async Task GetAsset_ServesExistingFile()
    {
        var service = CreateService();
        await using var zip = CreateThemeZip(
            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "style.css"
            }
            """,
            ("style.css", ".x{background:url('images/bg.png')}"),
            ("images/bg.png", "png"));

        await service.UpdateTheme(zip, CancellationToken.None);

        using var asset = service.GetAsset("images/bg.png");

        Assert.Equal("image/png", asset.ContentType);
        Assert.Equal(3, asset.Stream.Length);
    }

    [Fact]
    public async Task UpdateTheme_PreservesEntrypointCssRelativeUrls()
    {
        var service = CreateService();
        await using var zip = CreateThemeZip(
            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "css/index.css"
            }
            """,
            ("css/index.css", ".x{background:url('../images/bg.png')}@font-face{src:url('../fonts/theme.woff2')}"),
            ("images/bg.png", "png"),
            ("fonts/theme.woff2", "font"));

        await service.UpdateTheme(zip, CancellationToken.None);
        var installedTheme = Assert.Single(_stateRepository.State.InstalledThemes.Values);

        var storedCss = await File.ReadAllTextAsync(Path.Combine(_root, "themes", installedTheme.Directory, installedTheme.IndexFilename));

        Assert.Contains("url('../images/bg.png')", storedCss);
        Assert.Contains("url('../fonts/theme.woff2')", storedCss);
    }

    [Fact]
    public async Task GetAsset_RejectsMissingFile()
    {
        var service = CreateService();
        await using var zip = CreateThemeZip(
            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "style.css"
            }
            """,
            ("style.css", ".x{background:url('images/bg.png')}"),
            ("images/bg.png", "png"));

        await service.UpdateTheme(zip, CancellationToken.None);

        Assert.Throws<FileNotFoundException>(() => service.GetAsset("images/missing.png"));
    }

    [Theory]
    [InlineData("Safe Theme Name", "Safe_Theme_Name")]
    [InlineData("Déjà Vu Theme", "Deja_Vu_Theme")]
    [InlineData("Theme   Name!", "Theme_Name")]
    public void SlugifyName_PreservesCapitalizationAndUsesUnderscores(string name, string expected)
    {
        Assert.Equal(expected, ThemeService.SlugifyName(name));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ThemeService CreateService()
    {
        return new ThemeService(_root, _stateRepository);
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

    private sealed class TestStateRepository : IStateRepository
    {
        public ThemeLoaderState State { get; } = new(
            enabled: false,
            selectedThemeId: null,
            installedThemes: new Dictionary<Guid, Theme>(),
            initializationFailed: false);

        public bool Saved { get; private set; }

        public ThemeLoaderState Get()
        {
            return State;
        }

        public void Save()
        {
            Saved = true;
        }
    }
}