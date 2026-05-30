using Jellyfin.Plugin.ThemeLoader.Models;
using Jellyfin.Plugin.ThemeLoader.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.ThemeLoader.Controllers;

[ApiController]
[Route("ThemeLoader")]
public sealed class ThemeLoaderController(IThemeService themeStorageService) : ControllerBase
{
    [HttpGet("Status")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ThemeLoaderStatus> GetStatus()
    {
        return themeStorageService.GetStatus();
    }

    [HttpPut("Status")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult SetEnabled([FromBody] EnableLoaderRequest request)
    {
        try
        {
            themeStorageService.SetEnabled(request.Enabled);
            return NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(
                title: "Theme cannot be enabled",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("Status/SelectedTheme")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult SetSelectedTheme([FromBody] SelectThemeRequest request)
    {
        try
        {
            themeStorageService.SelectedTheme(request.Id);
            return NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(
                title: "Theme was not found",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("Themes")]
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

        await using var stream = file.OpenReadStream();

        try
        {
            return await themeStorageService.UpdateTheme(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException)
        {
            return InvalidThemeProblem(ex.Message);
        }
    }


    [HttpDelete("Theme/{id:guid}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult DeleteTheme(Guid id)
    {
        try
        {
            themeStorageService.RemoveTheme(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "Theme was not found",
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
    }

    [HttpGet("Assets/{**assetPath}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAsset(string assetPath)
    {
        try
        {
            var asset = themeStorageService.GetAsset(assetPath);
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