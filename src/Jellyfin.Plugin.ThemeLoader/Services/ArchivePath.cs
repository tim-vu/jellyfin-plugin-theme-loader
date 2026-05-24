using System;
using System.Collections.Generic;
using System.IO;

namespace Jellyfin.Plugin.ThemeLoader.Services;

internal static class ArchivePath
{
    public static string NormalizeCssReference(string cssDirectory, string reference)
    {
        var cleanReference = reference.Replace('\\', '/');

        string combined = string.IsNullOrWhiteSpace(cssDirectory)
            ? cleanReference
            : cssDirectory.TrimEnd('/') + "/" + cleanReference;

        return Normalize(combined);
    }

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("must not be empty", nameof(path));
        }

        path = path.Replace('\\', '/').Trim();

        if (path.StartsWith('/') || path.Contains(':'))
        {
            throw new InvalidDataException($"Path '{path}' is not relative.");
        }

        List<string> parts = [];
        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (parts.Count == 0)
                {
                    throw new InvalidDataException($"Path '{path}' escapes the theme root.");
                }

                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException($"Path '{path}' contains invalid characters.");
            }

            parts.Add(segment);
        }

        if (parts.Count == 0)
        {
            throw new InvalidDataException("Path is empty.");
        }

        return string.Join('/', parts);
    }
}
