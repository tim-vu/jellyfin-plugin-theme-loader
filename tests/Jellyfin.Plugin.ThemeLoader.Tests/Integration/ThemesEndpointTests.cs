using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class ThemesEndpointTests(AppFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task UploadTheme_ValidZip_ReturnsResultAndStatusShowsInstalledTheme()
    {
        using var content = ThemeUploadContent.Create(

            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "style.css"
            }
            """,
            "theme.zip",
            ("style.css", ".x{background:url('images/bg.png')}"),
            ("images/bg.png", "png"));

        var response = await Fixture.AuthenticatedClient.PostAsync("/ThemeLoader/Themes", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ThemeUploadResult>();
        var status = await GetStatusAsync();

        Assert.NotNull(result);
        Assert.Equal("Safe Theme Name", result.Name);
        Assert.Equal("v0.1.0.0", result.Version);
        Assert.True(status.Enabled);
        Assert.Equal(result.Id, status.SelectedThemeId);
        var theme = Assert.Single(status.Themes);
        Assert.Equal(result.Id, theme.Id);
        Assert.Equal("Safe Theme Name", theme.Name);
        Assert.Equal("v0.1.0.0", theme.Version);
    }

    [Fact]
    public async Task UploadTheme_NonZipFilename_ReturnsBadRequest()
    {
        using var content = ThemeUploadContent.Create(
            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "style.css"
            }
            """,
            "theme.txt",
            ("style.css", ""));

        var response = await Fixture.AuthenticatedClient.PostAsync("/ThemeLoader/Themes", content);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        var status = await GetStatusAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid theme package", problem?.Title);
        Assert.Empty(status.Themes);
    }

    [Fact]
    public async Task UploadTheme_InvalidManifest_ReturnsBadRequest()
    {
        using var content = ThemeUploadContent.Create(
            """{ "name": "Safe Theme Name", "version": "v0.1.0.0" }""",
            "theme.zip",
            ("style.css", ""));

        var response = await Fixture.AuthenticatedClient.PostAsync("/ThemeLoader/Themes", content);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        var status = await GetStatusAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid theme package", problem?.Title);
        Assert.Empty(status.Themes);
    }

    [Fact]
    public async Task DeleteTheme_RemovesInstalledThemeAndFiles()
    {
        var result = await Fixture.UploadThemeAsync();

        var response = await Fixture.AuthenticatedClient.DeleteAsync($"/ThemeLoader/Theme/{result.Id}");
        var status = await GetStatusAsync();
        var assetResponse = await Fixture.Client.GetAsync("/ThemeLoader/Assets/images/bg.png");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(status.Themes);
        Assert.Null(status.SelectedThemeId);
        Assert.False(status.Enabled);
        Assert.Equal(HttpStatusCode.NotFound, assetResponse.StatusCode);
    }
}
