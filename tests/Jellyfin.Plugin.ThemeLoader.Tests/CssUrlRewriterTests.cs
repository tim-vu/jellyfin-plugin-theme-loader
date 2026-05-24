using Jellyfin.Plugin.ThemeLoader.Services;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public sealed class CssUrlRewriterTests
{
    [Fact]
    public void RewriteEntryPointUrls_RewritesRelativeUrlFromRootEntrypoint()
    {
        string result = CssUrlRewriter.RewriteEntryPointUrls(
            ".x{src:url('./font.woff')} .y{background:url(images/bg.png?v=1#hero)}",
            "theme.css",
            "ThemeLoader/Assets");

        Assert.Contains("url(\"/ThemeLoader/Assets/font.woff\")", result);
        Assert.Contains("url(\"/ThemeLoader/Assets/images/bg.png?v=1#hero\")", result);
    }

    [Fact]
    public void RewriteEntryPointUrls_RewritesRelativeUrlFromNestedEntrypoint()
    {
        string result = CssUrlRewriter.RewriteEntryPointUrls(
            "@font-face{src:url('../fonts/font a.woff2')}",
            "css/index.css",
            "ThemeLoader/Assets");

        Assert.Contains("url(\"/ThemeLoader/Assets/fonts/font%20a.woff2\")", result);
    }

    [Fact]
    public void RewriteEntryPointUrls_LeavesEmbeddedAndHashUrls()
    {
        const string Css = ".x{background:url('data:image/png;base64,abc')} .y{mask:url(#shape)}";

        string result = CssUrlRewriter.RewriteEntryPointUrls(Css, "theme.css", "ThemeLoader/Assets");

        Assert.Equal(Css, result);
    }
}
