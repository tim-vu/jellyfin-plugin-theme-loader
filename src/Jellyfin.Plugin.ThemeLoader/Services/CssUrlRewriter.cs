using System;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ThemeLoader.Services;

internal static partial class CssUrlRewriter
{
    public static string RewriteEntryPointUrls(string css, string entrypointPath, string assetBasePath)
    {
        var cssDirectory = GetDirectory(entrypointPath);
        var normalizedAssetBasePath = "/" + assetBasePath.Trim('/') + "/";

        return CssUrlRegex().Replace(css, match => RewriteUrl(match, cssDirectory, normalizedAssetBasePath));
    }

    private static string RewriteUrl(Match match, string cssDirectory, string assetBasePath)
    {
        var url = match.Groups["url"].Value.Trim();
        if (!ShouldRewrite(url))
        {
            return match.Value;
        }

        SplitUrl(url, out string pathPart, out string suffix);
        var normalizedPath = ArchivePath.NormalizeCssReference(cssDirectory, Uri.UnescapeDataString(pathPart));

        return "url(\"" + assetBasePath + EscapePath(normalizedPath) + suffix + "\")";
    }

    private static bool ShouldRewrite(string url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
            !url.StartsWith('#') &&
            !url.StartsWith('/') &&
            !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("//", StringComparison.Ordinal) &&
            !Uri.TryCreate(url, UriKind.Absolute, out _);
    }

    private static string GetDirectory(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        int separatorIndex = normalizedPath.LastIndexOf('/');

        return separatorIndex < 0 ? string.Empty : normalizedPath[..separatorIndex];
    }

    private static string EscapePath(string path)
    {
        string[] parts = path.Split('/');
        for (int index = 0; index < parts.Length; index++)
        {
            parts[index] = Uri.EscapeDataString(parts[index]);
        }

        return string.Join('/', parts);
    }

    private static void SplitUrl(string url, out string pathPart, out string suffix)
    {
        int queryIndex = url.IndexOfAny(['?', '#']);
        if (queryIndex < 0)
        {
            pathPart = url;
            suffix = string.Empty;
            return;
        }

        pathPart = url[..queryIndex];
        suffix = url[queryIndex..];
    }

    [GeneratedRegex(@"url\(\s*(['""]?)(?<url>[^'"")]+)\1\s*\)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CssUrlRegex();
}
