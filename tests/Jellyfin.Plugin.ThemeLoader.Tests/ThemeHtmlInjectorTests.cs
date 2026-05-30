using System;
using Jellyfin.Plugin.ThemeLoader.Services;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public class ThemeHtmlInjectorTests
{
    [Fact]
    public void InjectStylesheet_InsertsLinkBeforeBodyClose()
    {
        string result = ThemeHtmlInjector.InjectStylesheet("<html><body><main></main></body></html>", "/ThemeLoader/Assets/style.css");

        Assert.Contains("<link id=\"theme-loader-css\" rel=\"stylesheet\" href=\"/ThemeLoader/Assets/style.css\" /></body>", result);
    }

    [Fact]
    public void InjectStylesheet_ReplacesExistingLink()
    {
        string html = "<body><link id=\"theme-loader-css\" rel=\"stylesheet\" href=\"old.css\" /><main></main></body>";

        string result = ThemeHtmlInjector.InjectStylesheet(html, "/ThemeLoader/Assets/new.css");

        Assert.DoesNotContain("old.css", result);
        Assert.Contains("/ThemeLoader/Assets/new.css", result);
        Assert.Contains("<main></main>", result);
        Assert.EndsWith("</body>", result);
    }
}
