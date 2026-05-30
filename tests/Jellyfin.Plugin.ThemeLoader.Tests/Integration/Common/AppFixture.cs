using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Controllers;
using Jellyfin.Plugin.ThemeLoader.Models;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using Jellyfin.Plugin.ThemeLoader.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public sealed class AppFixture : IDisposable
{
    private const string AuthenticationScheme = "Test";
    private readonly IHost _host;
    private readonly string _root;
    private readonly StateRepositoryFake _stateRepository;

    public AppFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "theme-loader-http-tests-" + Guid.NewGuid().ToString("N"));
        _stateRepository = new StateRepositoryFake();

        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services
                        .AddAuthentication(AuthenticationScheme)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(AuthenticationScheme, _ => { });
                    services.AddAuthorizationBuilder()
                        .AddPolicy("RequiresElevation", policy => policy.RequireAuthenticatedUser());
                    services
                        .AddControllers()
                        .AddApplicationPart(typeof(ThemeLoaderController).Assembly);

                    services.AddSingleton<IStateRepository>(_stateRepository);
                    services.AddScoped<IThemeService>(provider =>
                        new ThemeService(_root, provider.GetRequiredService<IStateRepository>()));
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .Start();

        Client = _host.GetTestClient();
        AuthenticatedClient = _host.GetTestClient();
        AuthenticatedClient.DefaultRequestHeaders.Add("X-Test-User", "admin");
    }

    public HttpClient Client { get; }

    public HttpClient AuthenticatedClient { get; }

    public void Reset()
    {
        _stateRepository.Reset();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        Directory.CreateDirectory(_root);
    }

    public async Task<ThemeUploadResult> UploadThemeAsync(params (string Path, string Contents)[] files)
    {
        if (files.Length == 0)
        {
            files =
            [
                ("style.css", ".x{background:url('images/bg.png')}"),
                ("images/bg.png", "png")
            ];
        }

        using var content = ThemeUploadContent.Create(
            """
            {
              "name": "Safe Theme Name",
              "version": "v0.1.0.0",
              "entrypoint": "style.css"
            }
            """,
            "theme.zip",
            files);

        var response = await AuthenticatedClient.PostAsync("/ThemeLoader/Themes", content);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ThemeUploadResult>())!;
    }

    public void Dispose()
    {
        Client.Dispose();
        AuthenticatedClient.Dispose();
        _host.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}