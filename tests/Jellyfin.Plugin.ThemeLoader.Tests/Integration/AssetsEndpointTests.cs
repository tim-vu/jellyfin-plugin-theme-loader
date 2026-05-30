using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AssetsEndpointTests(AppFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task UploadTheme_ThenGetAsset_ReturnsFile()
    {
        await Fixture.UploadThemeAsync(("style.css", ".x{background:url('images/bg.png')}"), ("images/bg.png", "png"));

        var response = await Fixture.Client.GetAsync("/ThemeLoader/Assets/images/bg.png");
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Encoding.UTF8.GetBytes("png"), body);
    }

    [Fact]
    public async Task GetAsset_PathEscapesThemeDirectory_ReturnsNotFound()
    {
        await Fixture.UploadThemeAsync(("style.css", ".x{background:url('images/bg.png')}"), ("images/bg.png", "png"));

        var response = await Fixture.Client.GetAsync("/ThemeLoader/Assets/%2e%2e/Safe_Theme_Name_v0.1.0.0/images/bg.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
