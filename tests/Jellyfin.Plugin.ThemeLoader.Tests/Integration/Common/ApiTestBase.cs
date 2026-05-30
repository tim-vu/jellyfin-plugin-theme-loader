using System.Net.Http.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Models;

namespace Jellyfin.Plugin.ThemeLoader.Tests.Integration;

public abstract class ApiTestBase
{
    protected ApiTestBase(AppFixture fixture)
    {
        Fixture = fixture;
        Fixture.Reset();
    }

    protected AppFixture Fixture { get; }

    protected async Task<ThemeLoaderStatus> GetStatusAsync()
    {
        var response = await Fixture.AuthenticatedClient.GetAsync("/ThemeLoader/Status");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ThemeLoaderStatus>())!;
    }
}
