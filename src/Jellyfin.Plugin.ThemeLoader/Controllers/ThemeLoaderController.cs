using Jellyfin.Plugin.ThemeLoader.Models;
using Jellyfin.Plugin.ThemeLoader.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.ThemeLoader.Controllers;

[ApiController]
[Route("ThemeLoader")]
public sealed class ThemeLoaderController(IThemeStorageService themeStorageService) : ControllerBase
{
    private readonly IThemeStorageService _themeStorageService = themeStorageService;

    [HttpGet("Status")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ThemeLoaderStatus> GetStatus()
    {
        return _themeStorageService.GetStatus();
    }

    [HttpPut("Status")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult SetEnabled([FromBody] ThemeEnabledRequest request)
    {
        try
        {
            _themeStorageService.SetEnabled(request.Enabled);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "Theme cannot be enabled",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("Theme")]
    [Authorize(Policy = "RequiresElevation")]
    [RequestSizeLimit(128L * 1024L * 1024L)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ThemeUploadResult>> UploadTheme([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return InvalidThemeProblem("Theme ZIP is empty.");
        }

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return InvalidThemeProblem("Theme upload must be a ZIP file.");
        }

        await using Stream stream = file.OpenReadStream();

        try
        {
            return await _themeStorageService.UploadThemeAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException or JsonException)
        {
            return InvalidThemeProblem(ex.Message);
        }
    }


    [HttpDelete("Theme")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult DeleteTheme()
    {
        _themeStorageService.DeleteTheme();
        return NoContent();
    }

    [HttpGet("Assets/{**assetPath}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAsset(string assetPath)
    {
        try
        {
            var asset = _themeStorageService.GetAsset(assetPath);
            return File(asset.Stream, asset.ContentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidDataException)
        {
            return NotFound();
        }
    }

    private ObjectResult InvalidThemeProblem(string detail)
    {
        return Problem(
            title: "Invalid theme package",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
    }
}
