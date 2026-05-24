using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Models;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.StaticFiles;

namespace Jellyfin.Plugin.ThemeLoader.Services;

public sealed class ThemeStorageService : IThemeStorageService
{
    private const long MaxEntryBytes = 50L * 1024L * 1024L;
    private const long MaxTotalBytes = 100L * 1024L * 1024L;
    private const string ThemeManifestPath = "theme.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _themesDirectory;
    private readonly IStateRepository _stateRepository;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public ThemeStorageService(IServerApplicationPaths applicationPaths, IStateRepository stateRepository)
    {
        _themesDirectory = Path.Combine(applicationPaths.PluginConfigurationsPath, "ThemeLoader", "themes");
        _stateRepository = stateRepository;
    }

    public ThemeLoaderStatus GetStatus()
    {
        var state = _stateRepository.Get();
        var themes = state.InstalledThemes.Values
            .Select(theme => new ThemeInfo
            {
                Id = theme.Id,
                Name = theme.Name,
                Version = theme.Version,
                UploadedAt = theme.UploadedAtUtc
            })
            .OrderBy(t => t.UploadedAt)
            .ToList();

        return new ThemeLoaderStatus
        {
            Enabled = state.Enabled,
            SelectedThemeId = state.SelectedThemeId,
            Themes = themes,
            InitializationFailed = state.InitializationFailed
        };
    }

    public async Task<ThemeUploadResult> UpdateTheme(Stream zipStream, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_themesDirectory);
        var stagingDirectory = CreateStagingDirectory();

        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var extractedFiles = await ExtractZipAsync(zipStream, stagingDirectory, cancellationToken).ConfigureAwait(false);
            var manifest = await ReadManifestAsync(stagingDirectory, extractedFiles, cancellationToken).ConfigureAwait(false);
            ValidateThemeIdentity(manifest);

            var entrypoint = ArchivePath.Normalize(manifest.Entrypoint);

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

            var themeDirectory = Path.Combine(_themesDirectory, manifest.Id.ToString());

            if (Directory.Exists(themeDirectory))
            {
                Directory.Delete(themeDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, themeDirectory);

            var state = _stateRepository.Get();
            state.AddTheme(
                manifest.Id,
                manifest.Name,
                manifest.Version,
                Path.Combine(themeDirectory, PathFromArchive(entrypoint)),
                themeDirectory);
            state.SelectTheme(manifest.Id);
            state.Enable();
            _stateRepository.Save();

            return new ThemeUploadResult
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Version = manifest.Version
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
        var state = _stateRepository.Get();
        
        if (!enabled)
        {
            state.Disable();
            _stateRepository.Save();
            return;
        }

        state.Enable();
        _stateRepository.Save();
    }

    public void SelectedTheme(Guid id)
    {
        var state = _stateRepository.Get();
        state.SelectTheme(id);
        _stateRepository.Save();
    }

    public void RemoveTheme(Guid id)
    {
        var state = _stateRepository.Get();
        var theme = state.InstalledThemes.GetValueOrDefault(id);

        if (theme is null)
        {
            throw new InvalidOperationException("Theme with Id not found.");
        }

        if (Directory.Exists(theme.Directory))
        {
            Directory.Delete(theme.Directory, recursive: true);
        }

        state.RemoveTheme(id);
        _stateRepository.Save();
    }

    public ThemeAsset GetAsset(string assetPath)
    {
        var selectedTheme = _stateRepository.Get().SelectedTheme ?? throw new FileNotFoundException("Theme asset was not found.", assetPath);
        var normalizedPath = ArchivePath.Normalize(Uri.UnescapeDataString(assetPath));
        var normalizedCssPath = Path.GetRelativePath(selectedTheme.Directory, selectedTheme.EntrypointPath).Replace('\\', '/');

        if (normalizedPath == ThemeManifestPath || string.Equals(normalizedPath, normalizedCssPath, StringComparison.Ordinal))
        {
            throw new FileNotFoundException("Theme asset was not found.", normalizedPath);
        }

        var fullPath = Path.Combine(selectedTheme.Directory, PathFromArchive(normalizedPath));
        var rootedActiveDirectory = Path.GetFullPath(selectedTheme.Directory) + Path.DirectorySeparatorChar;
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

            var normalizedPath = ArchivePath.Normalize(entry.FullName);
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

        if (manifest.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(manifest.Name)
            || string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.Entrypoint))
        {
            throw new InvalidDataException("Theme manifest must include id, name, version, and entrypoint.");
        }

        return manifest;
    }

    private static void ValidateThemeIdentity(ThemeManifest manifest)
    {
        if (manifest.Id == Guid.Empty)
        {
            throw new InvalidDataException("Theme id must not be empty.");
        }
    }

    private string CreateStagingDirectory()
    {
        return Path.Combine(
            _themesDirectory,
            ".staging",
            "staging-" + Guid.NewGuid().ToString("N"));
    }

    private static string PathFromArchive(string archivePath)
    {
        return archivePath.Replace('/', Path.DirectorySeparatorChar);
    }
}
