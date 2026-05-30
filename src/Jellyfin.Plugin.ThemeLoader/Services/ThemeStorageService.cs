using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Models;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.StaticFiles;

namespace Jellyfin.Plugin.ThemeLoader.Services;

public sealed class ThemeService : IThemeService
{
    private const string ThemeManifestPath = "theme.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _themesDirectory;
    private readonly IStateRepository _stateRepository;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public ThemeService(IServerApplicationPaths applicationPaths, IStateRepository stateRepository)
        : this(Path.Combine(applicationPaths.PluginConfigurationsPath, "ThemeLoader"), stateRepository)
    {
    }

    internal ThemeService(string pluginConfigurationDirectory, IStateRepository stateRepository)
    {
        _themesDirectory = Path.Combine(pluginConfigurationDirectory, "themes");
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

            var themeId = Guid.NewGuid();
            var themeDirectoryName = CreateThemeDirectoryName(manifest);
            var themeDirectory = Path.Combine(_themesDirectory, themeDirectoryName);

            if (Directory.Exists(themeDirectory))
            {
                Directory.Delete(themeDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, themeDirectory);

            var state = _stateRepository.Get();
            foreach (var existingThemeId in state.InstalledThemes.Values
                .Where(theme => theme.Directory == themeDirectoryName)
                .Select(theme => theme.Id)
                .ToList())
            {
                state.RemoveTheme(existingThemeId);
            }

            state.AddTheme(
                themeId,
                manifest.Name,
                manifest.Version,
                entrypoint,
                themeDirectoryName);
            state.SelectTheme(themeId);
            state.Enable();
            _stateRepository.Save();

            return new ThemeUploadResult
            {
                Id = themeId,
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

        var themeDirectory = Path.Combine(_themesDirectory, theme.Directory);
        if (Directory.Exists(themeDirectory))
        {
            Directory.Delete(themeDirectory, recursive: true);
        }

        state.RemoveTheme(id);
        _stateRepository.Save();
    }

    public ThemeAsset GetAsset(string assetPath)
    {
        var state = _stateRepository.Get();

        var selectedTheme = state.SelectedTheme;
        if (!state.Enabled || selectedTheme is null)
        {
            throw new FileNotFoundException("Theme asset was not found.", assetPath);
        }

        var normalizedAssetPath = ArchivePath.Normalize(assetPath);
        var themeDirectory = Path.GetFullPath(Path.Combine(_themesDirectory, selectedTheme.Directory));
        var fullPath = Path.GetFullPath(Path.Combine(themeDirectory, PathFromArchive(normalizedAssetPath)));

        if (!IsPathInsideDirectory(fullPath, themeDirectory) || !File.Exists(fullPath))
        {
            throw new FileNotFoundException("Theme asset was not found.", assetPath);
        }

        if (!_contentTypeProvider.TryGetContentType(fullPath, out string? contentType))
        {
            contentType = "application/octet-stream";
        }

        return new ThemeAsset(File.OpenRead(fullPath), contentType);
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return path.StartsWith(normalizedDirectory, comparison);
    }

    private static async Task<HashSet<string>> ExtractZipAsync(Stream zipStream, string stagingDirectory, CancellationToken cancellationToken)
    {
        var extractedFiles = new HashSet<string>(StringComparer.Ordinal);

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

        ThemeManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ThemeManifest>(manifestJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Theme manifest is invalid JSON.", ex);
        }

        if (manifest is null)
        {
            throw new InvalidDataException("Theme manifest is invalid JSON.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name)
            || string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.Entrypoint))
        {
            throw new InvalidDataException("Theme manifest must include name, version, and entrypoint.");
        }

        return manifest;
    }

    private static string CreateThemeDirectoryName(ThemeManifest manifest)
    {
        return SlugifyName(manifest.Name) + "_" + ValidateRawVersion(manifest.Version);
    }

    internal static string SlugifyName(string name)
    {
        var normalized = name.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var needsSeparator = false;

        foreach (char character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (needsSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                }

                builder.Append(character);
                needsSeparator = false;
                continue;
            }

            needsSeparator = builder.Length > 0;
        }

        var slug = builder.ToString();
        if (slug.Length == 0)
        {
            throw new InvalidDataException("Theme name must contain at least one ASCII letter or digit.");
        }

        return slug;
    }

    internal static string ValidateRawVersion(string version)
    {
        version = version.Trim();
        if (version.Length == 0 || version is "." or "..")
        {
            throw new InvalidDataException("Theme version is not a safe directory segment.");
        }

        if (version.Contains('/') || version.Contains('\\') || version.Contains(':') || version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("Theme version is not a safe directory segment.");
        }

        return version;
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
