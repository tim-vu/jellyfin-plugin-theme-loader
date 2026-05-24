using Jellyfin.Plugin.ThemeLoader.Services;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public class ThemeHtmlInjectorTests
{
    [Fact]
    public void Inject_InsertsStyleBeforeBodyClose()
    {
        string result = ThemeHtmlInjector.Inject("<html><body><main></main></body></html>", "body{color:red;}");

        Assert.Contains("<style id=\"theme-loader-css\">body{color:red;}</style></body>", result);
    }

    [Fact]
    public void Inject_ReplacesExistingStyle()
    {
        string html = "<body><style id=\"theme-loader-css\">old</style><main></main></body>";

        string result = ThemeHtmlInjector.Inject(html, "new");

        Assert.DoesNotContain(">old<", result);
        Assert.Contains(">new<", result);
    }

    [Fact]
    public void Inject_DoesNotHtmlEncodeCss()
    {
        string css = ".x{background:url('assets/a.png?v=1&x=2')}";

        string result = ThemeHtmlInjector.Inject("<body></body>", css);

        Assert.Contains(css, result);
    }
}
