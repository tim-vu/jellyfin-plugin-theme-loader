using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Configuration;
using Jellyfin.Plugin.ThemeLoader.Models;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.StaticFiles;

namespace Jellyfin.Plugin.ThemeLoader.Services;

public sealed partial class ThemeStorageService : IThemeStorageService
{
    private const long MaxEntryBytes = 50L * 1024L * 1024L;
    private const long MaxTotalBytes = 100L * 1024L * 1024L;
    private const string ThemeManifestPath = "theme.json";
    private const int MetadataMaxLength = 64;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _rootDirectory;
    private readonly string _themesDirectory;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public ThemeStorageService(IServerApplicationPaths applicationPaths)
    {
        _rootDirectory = Path.Combine(applicationPaths.PluginConfigurationsPath, "ThemeLoader");
        _themesDirectory = Path.Combine(_rootDirectory, "themes");
    }

    public ThemeLoaderStatus GetStatus()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return new ThemeLoaderStatus
            {
                Enabled = false,
                Theme = null,
                InitializationFailed = false
            };
        }

        var selectedTheme = plugin.Configuration.GetSelectedTheme();
        ActiveThemeInfo? theme = selectedTheme is null
            ? null
            : new ActiveThemeInfo
            {
                Slug = selectedTheme.Slug,
                Name = selectedTheme.Name,
                Version = selectedTheme.Version,
                UploadedAt = selectedTheme.UploadedAtUtc
            };

        return new ThemeLoaderStatus
        {
            Enabled = plugin.Configuration.Enabled,
            Theme = theme,
            InitializationFailed = plugin.Configuration.InitializationFailed
        };
    }

    public async Task<ThemeUploadResult> UploadThemeAsync(Stream zipStream, CancellationToken cancellationToken)
    {
        var stagingDirectory = CreateStagingDirectory();

        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var extractedFiles = await ExtractZipAsync(zipStream, stagingDirectory, cancellationToken).ConfigureAwait(false);
            var manifest = await ReadManifestAsync(stagingDirectory, extractedFiles, cancellationToken).ConfigureAwait(false);
            ValidateThemeIdentity(manifest);

            var entrypoint = ArchivePath.NormalizeZipEntry(manifest.Entrypoint);

            if (!extractedFiles.Contains(entrypoint))
            {
                throw new FileNotFoundException($"Theme CSS entrypoint '{manifest.Entrypoint}' was not found.", entrypoint);
            }

            var cssPath = Path.Combine(stagingDirectory, PathFromArchive(entrypoint));
            var rawCss = await File.ReadAllTextAsync(cssPath, cancellationToken).ConfigureAwait(false);

            CssValidator.Validate(
                rawCss,
                entrypoint,
                extractedFiles.Where(file => file != ThemeManifestPath).ToHashSet(StringComparer.Ordinal),
                path => File.ReadAllText(Path.Combine(stagingDirectory, PathFromArchive(path))));

            var themeDirectory = Path.Combine(_themesDirectory, CreateThemeDirectoryName(manifest.Slug, manifest.Version));
            var plugin = RequirePlugin();
            var existingTheme = plugin.Configuration.InstalledThemes.FirstOrDefault(theme => theme.Slug == manifest.Slug);

            if (Directory.Exists(themeDirectory))
            {
                Directory.Delete(themeDirectory, recursive: true);
            }

            Directory.CreateDirectory(_themesDirectory);
            Directory.Move(stagingDirectory, themeDirectory);

            Directory.Delete(stagingDirectory, recursive: true);

            plugin.Configuration.InstalledThemes.RemoveAll(theme => theme.Slug == manifest.Slug);
            plugin.Configuration.InstalledThemes.Add(new InstalledTheme
            {
                Slug = manifest.Slug,
                Name = manifest.Name,
                Version = manifest.Version,
                DirectoryPath = themeDirectory,
                CssFilePath = Path.Combine(themeDirectory, PathFromArchive(entrypoint)),
                UploadedAtUtc = DateTimeOffset.UtcNow
            });
            plugin.Configuration.SelectedTheme = manifest.Slug;
            plugin.Configuration.Enabled = true;
            plugin.UpdateConfiguration(plugin.Configuration);

            return new ThemeUploadResult
            {
                ThemeSlug = manifest.Slug,
                ThemeName = manifest.Name,
                ThemeVersion = manifest.Version
            };
        }
        catch
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }

            throw;
        }
    }

    public void SetEnabled(bool enabled)
    {
        Plugin plugin = RequirePlugin();
        if (enabled && plugin.Configuration.GetSelectedTheme() is null)
        {
            throw new InvalidOperationException("No theme is installed.");
        }

        plugin.Configuration.Enabled = enabled;
        plugin.UpdateConfiguration(plugin.Configuration);
    }

    public void DeleteTheme()
    {
        var plugin = RequirePlugin();
        var selectedTheme = plugin.Configuration.GetSelectedTheme();
        if (selectedTheme is not null && Directory.Exists(selectedTheme.DirectoryPath))
        {
            Directory.Delete(selectedTheme.DirectoryPath, recursive: true);
        }

        plugin.Configuration.Enabled = false;
        if (selectedTheme is not null)
        {
            plugin.Configuration.InstalledThemes.RemoveAll(theme => theme.Slug == selectedTheme.Slug);
        }

        plugin.Configuration.SelectedTheme = null;
        plugin.UpdateConfiguration(plugin.Configuration);
    }

    public ThemeAsset GetAsset(string assetPath)
    {
        var selectedTheme = Plugin.Instance?.Configuration.GetSelectedTheme()
            ?? throw new FileNotFoundException("Theme asset was not found.", assetPath);
        var normalizedPath = ArchivePath.NormalizeZipEntry(Uri.UnescapeDataString(assetPath));
        var normalizedCssPath = Path.GetRelativePath(selectedTheme.DirectoryPath, selectedTheme.CssFilePath).Replace('\\', '/');

        if (normalizedPath == ThemeManifestPath || string.Equals(normalizedPath, normalizedCssPath, StringComparison.Ordinal))
        {
            throw new FileNotFoundException("Theme asset was not found.", normalizedPath);
        }

        var fullPath = Path.Combine(selectedTheme.DirectoryPath, PathFromArchive(normalizedPath));
        var rootedActiveDirectory = Path.GetFullPath(selectedTheme.DirectoryPath) + Path.DirectorySeparatorChar;
        var rootedFullPath = Path.GetFullPath(fullPath);

        if (!rootedFullPath.StartsWith(rootedActiveDirectory, StringComparison.Ordinal) || !File.Exists(rootedFullPath))
        {
            throw new FileNotFoundException("Theme asset was not found.", normalizedPath);
        }

        if (!_contentTypeProvider.TryGetContentType(rootedFullPath, out string? contentType))
        {
            contentType = "application/octet-stream";
        }

        return new ThemeAsset(File.OpenRead(rootedFullPath), contentType);
    }

    private static async Task<HashSet<string>> ExtractZipAsync(Stream zipStream, string stagingDirectory, CancellationToken cancellationToken)
    {
        var extractedFiles = new HashSet<string>(StringComparer.Ordinal);
        var totalBytes = 0L;

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var normalizedPath = ArchivePath.NormalizeZipEntry(entry.FullName);
            if (entry.Name.Equals(ThemeManifestPath, StringComparison.OrdinalIgnoreCase) && normalizedPath != ThemeManifestPath)
            {
                throw new InvalidDataException("theme.json must be in the theme ZIP root directory.");
            }

            if (entry.Length > MaxEntryBytes)
            {
                throw new InvalidDataException($"Theme file '{normalizedPath}' is too large.");
            }

            totalBytes += entry.Length;
            if (totalBytes > MaxTotalBytes)
            {
                throw new InvalidDataException("Theme package is too large.");
            }

            var outputPath = Path.Combine(stagingDirectory, PathFromArchive(normalizedPath));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            await using Stream source = entry.Open();
            await using FileStream destination = File.Create(outputPath);

            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            extractedFiles.Add(normalizedPath);
        }

        return extractedFiles;
    }

    private static async Task<ThemeManifest> ReadManifestAsync(
        string stagingDirectory,
        IReadOnlySet<string> extractedFiles,
        CancellationToken cancellationToken)
    {
        if (!extractedFiles.Contains(ThemeManifestPath))
        {
            throw new FileNotFoundException("Theme package must contain theme.json.", ThemeManifestPath);
        }

        var manifestJson = await File.ReadAllTextAsync(
            Path.Combine(stagingDirectory, ThemeManifestPath),
            cancellationToken).ConfigureAwait(false);

        var manifest = JsonSerializer.Deserialize<ThemeManifest>(manifestJson, JsonOptions);

        if (manifest is null)
        {
            throw new InvalidDataException("Theme manifest is invalid JSON.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Slug)
            || string.IsNullOrWhiteSpace(manifest.Name)
            || string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.Entrypoint))
        {
            throw new InvalidDataException("Theme manifest must include slug, name, version, and entrypoint.");
        }

        return manifest;
    }

    private static void ValidateThemeIdentity(ThemeManifest manifest)
    {
        ValidateDirectorySegment(manifest.Slug, "slug");
        ValidateDirectorySegment(manifest.Version, "version");
    }

    private static void ValidateDirectorySegment(string value, string fieldName)
    {
        if (value.Length > MetadataMaxLength || !ThemeDirectoryValueRegex().IsMatch(value))
        {
            throw new InvalidDataException(
                $"Theme {fieldName} must be 1-{MetadataMaxLength} characters and contain only letters, numbers, '.', '_', '+', or '-'.");
        }
    }

    private static string CreateStagingDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "Jellyfin.Plugin.ThemeLoader",
            "staging-" + Guid.NewGuid().ToString("N"));
    }

    private static string CreateThemeDirectoryName(string slug, string version)
    {
        return $"{slug}-{version}";
    }

    private static Plugin RequirePlugin()
    {
        return Plugin.Instance ?? throw new InvalidOperationException("Theme Loader plugin has not been initialized.");
    }

    private static string PathFromArchive(string archivePath)
    {
        return archivePath.Replace('/', Path.DirectorySeparatorChar);
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._+-]*$")]
    private static partial Regex ThemeDirectoryValueRegex();
}
