using System;
using System.Collections.Generic;
using System.IO;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Css.Values;

namespace Jellyfin.Plugin.ThemeLoader.Services;

internal static class CssValidator
{
    public static void Validate(
        string css,
        string entrypointPath,
        IReadOnlySet<string> availableFiles,
        Func<string, string>? readCss = null)
    {
        Validate(css, entrypointPath, availableFiles, readCss, new HashSet<string>(StringComparer.Ordinal));
    }

    private static void Validate(
        string css,
        string cssPath,
        IReadOnlySet<string> availableFiles,
        Func<string, string>? readCss,
        ISet<string> visitedCssFiles)
    {
        var stylesheet = CreateParser().ParseStyleSheet(css);
        var cssDirectory = GetArchiveDirectory(cssPath);

        foreach (var rule in stylesheet.Rules)
        {
            ValidateRule(rule, cssDirectory, availableFiles, readCss, visitedCssFiles);
        }
    }

    private static CssParser CreateParser()
    {
        return new CssParser(new CssParserOptions
        {
            IsIncludingUnknownRules = true,
            IsIncludingUnknownDeclarations = true,
            IsToleratingInvalidSelectors = false
        });
    }

    private static void ValidateRule(
        ICssRule rule,
        string cssDirectory,
        IReadOnlySet<string> availableFiles,
        Func<string, string>? readCss,
        ISet<string> visitedCssFiles)
    {
        if (rule is ICssImportRule importRule)
        {
            ValidateImport(importRule, cssDirectory, availableFiles, readCss, visitedCssFiles);
            return;
        }

        if (rule is ICssStyleRule styleRule)
        {
            ValidateDeclarations(styleRule.Style, cssDirectory, availableFiles);
        }

        if (rule is ICssFontFaceRule fontFaceRule)
        {
            ValidateDeclarations(fontFaceRule, cssDirectory, availableFiles);
        }

        if (rule is ICssGroupingRule groupingRule)
        {
            foreach (ICssRule child in groupingRule.Rules)
            {
                ValidateRule(child, cssDirectory, availableFiles, readCss, visitedCssFiles);
            }
        }
    }

    private static void ValidateImport(
        ICssImportRule importRule,
        string cssDirectory,
        IReadOnlySet<string> availableFiles,
        Func<string, string>? readCss,
        ISet<string> visitedCssFiles)
    {
        if (IsEmbedded(importRule.Href))
        {
            throw new InvalidDataException($"Embedded CSS import is not allowed: {importRule.Href}");
        }

        string importPath = NormalizeRelativeUrl(importRule.Href, cssDirectory, availableFiles);
        if (readCss is null || !visitedCssFiles.Add(importPath))
        {
            return;
        }

        Validate(readCss(importPath), importPath, availableFiles, readCss, visitedCssFiles);
    }

    private static void ValidateDeclarations(IEnumerable<ICssProperty> declarations, string cssDirectory, IReadOnlySet<string> availableFiles)
    {
        foreach (ICssProperty declaration in declarations)
        {
            ValidateCssValue(declaration.RawValue, cssDirectory, availableFiles);
        }
    }

    private static void ValidateCssValue(ICssValue value, string cssDirectory, IReadOnlySet<string> availableFiles)
    {
        if (value is CssUrlValue url)
        {
            ValidateUrl(url.Path, cssDirectory, availableFiles);
            return;
        }

        if (value is ICssMultipleValue multipleValue)
        {
            for (int index = 0; index < multipleValue.Count; index++)
            {
                ValidateCssValue(multipleValue[index], cssDirectory, availableFiles);
            }
        }
    }

    private static void ValidateUrl(string url, string cssDirectory, IReadOnlySet<string> availableFiles)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith('#'))
        {
            return;
        }

        _ = NormalizeRelativeUrl(url, cssDirectory, availableFiles);
    }

    private static string NormalizeRelativeUrl(string url, string cssDirectory, IReadOnlySet<string> availableFiles)
    {
        if (IsEmbedded(url))
        {
            return url;
        }

        if (IsRemote(url))
        {
            throw new InvalidDataException($"Remote CSS URL is not allowed: {url}");
        }

        if (url.StartsWith('/'))
        {
            throw new InvalidDataException($"CSS URL must be relative to the CSS file: {url}");
        }

        SplitUrl(url, out string pathPart, out _);
        var normalizedPath = ArchivePath.NormalizeCssReference(cssDirectory, Uri.UnescapeDataString(pathPart));

        if (!availableFiles.Contains(normalizedPath))
        {
            throw new FileNotFoundException($"CSS file reference '{url}' was not found in the theme package.", normalizedPath);
        }

        return normalizedPath;
    }

    private static bool IsEmbedded(string url)
    {
        return url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRemote(string url)
    {
        return url.StartsWith("//", StringComparison.Ordinal) || Uri.TryCreate(url, UriKind.Absolute, out _);
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

    private static string GetArchiveDirectory(string path)
    {
        int separatorIndex = path.LastIndexOf('/');

        return separatorIndex < 0 ? string.Empty : path[..separatorIndex];
    }
}