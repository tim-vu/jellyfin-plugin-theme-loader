using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class StatusEndpointTests(AppFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task GetStatus_Authenticated_ReturnsInitialState()
    {

        var response = await Fixture.AuthenticatedClient.GetAsync("/ThemeLoader/Status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<ThemeLoaderStatus>();

        Assert.NotNull(status);
        Assert.False(status.Enabled);
        Assert.Null(status.SelectedThemeId);
        Assert.Empty(status.Themes);
    }

    [Fact]
    public async Task GetStatus_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await Fixture.Client.GetAsync("/ThemeLoader/Status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadTheme_ThenStatus_ReturnsInstalledTheme()
    {
        var result = await Fixture.UploadThemeAsync();

        var response = await Fixture.AuthenticatedClient.GetAsync("/ThemeLoader/Status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<ThemeLoaderStatus>();

        Assert.NotNull(status);
        Assert.True(status.Enabled);
        Assert.Equal(result.Id, status.SelectedThemeId);
        var theme = Assert.Single(status.Themes);
        Assert.Equal(result.Id, theme.Id);
        Assert.Equal("Safe Theme Name", theme.Name);
        Assert.Equal("v0.1.0.0", theme.Version);
    }

    [Fact]
    public async Task SetEnabled_WithoutSelectedTheme_ReturnsBadRequest()
    {
        var response = await Fixture.AuthenticatedClient.PutAsJsonAsync("/ThemeLoader/Status", new EnableLoaderRequest { Enabled = true });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Theme cannot be enabled", problem?.Title);
    }

    [Fact]
    public async Task UploadTheme_ThenDisable_ReturnsNoContentAndStatusDisabled()
    {
        await Fixture.UploadThemeAsync();

        var updateResponse = await Fixture.AuthenticatedClient.PutAsJsonAsync("/ThemeLoader/Status", new EnableLoaderRequest { Enabled = false });
        var statusResponse = await Fixture.AuthenticatedClient.GetAsync("/ThemeLoader/Status");
        var status = await statusResponse.Content.ReadFromJsonAsync<ThemeLoaderStatus>();

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.NotNull(status);
        Assert.False(status.Enabled);
    }

    [Fact]
    public async Task SetSelectedTheme_UnknownId_ReturnsBadRequest()
    {
        var response = await Fixture.AuthenticatedClient.PutAsJsonAsync(
            "/ThemeLoader/Status/SelectedTheme",
            new SelectThemeRequest { Id = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
