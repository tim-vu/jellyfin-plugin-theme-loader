using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Models;

namespace Jellyfin.Plugin.ThemeLoader.Services;

public interface IThemeStorageService
{
  ThemeLoaderStatus GetStatus();

  Task<ThemeUploadResult> UploadThemeAsync(Stream zipStream, CancellationToken cancellationToken);

  void SetEnabled(bool enabled);

  void DeleteTheme();

  ThemeAsset GetAsset(string assetPath);
}

public sealed class ThemeAsset(Stream stream, string contentType) : IDisposable
{
  public Stream Stream { get; } = stream;

  public string ContentType { get; } = contentType;

  public void Dispose()
  {
    Stream.Dispose();
  }
}
