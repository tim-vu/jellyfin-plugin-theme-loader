using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.ThemeLoader.Services;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public class CssValidatorTests
{
    [Fact]
    public void Validate_AllowsRelativePackageAssetUrls()
    {
        HashSet<string> files = ["style.css", "assets/bg.png", "fonts/theme.woff2"];

        CssValidator.Validate(
            "@media screen{.x{background-image:url('assets/bg.png')}}@font-face{font-family:Theme;src:url('fonts/theme.woff2')}",
            "style.css",
            files);
    }

    [Fact]
    public void Validate_AllowsRelativePathsFromNestedCss()
    {
        HashSet<string> files = ["css/index.css", "fonts/font_a.woff"];

        CssValidator.Validate("@font-face{font-family:Theme;src:url('../fonts/font_a.woff')}", "css/index.css", files);
    }

    [Fact]
    public void Validate_RejectsRootPluginAssetUrls()
    {
        HashSet<string> files = ["style.css", "fonts/theme.woff2"];

        Assert.Throws<InvalidDataException>(() => CssValidator.Validate(".x{src:url('/ThemeLoader/Assets/fonts/theme.woff2')}", "style.css", files));
    }

    [Theory]
    [InlineData("https://cdn.example/theme.png")]
    [InlineData("http://cdn.example/theme.png")]
    [InlineData("//cdn.example/theme.png")]
    [InlineData("data:image/png;base64,abc")]
    [InlineData("ftp://cdn.example/theme.png")]
    public void Validate_RejectsRemoteOrEmbeddedUrls(string url)
    {
        HashSet<string> files = ["style.css"];

        Assert.Throws<InvalidDataException>(() => CssValidator.Validate($".x{{background:url('{url}')}}", "style.css", files));
    }

    [Fact]
    public void Validate_RejectsMissingAsset()
    {
        HashSet<string> files = ["style.css"];

        Assert.Throws<FileNotFoundException>(() => CssValidator.Validate(".x{background:url('missing.png')}", "style.css", files));
    }

    [Fact]
    public void Validate_AllowsRelativeImports()
    {
        HashSet<string> files = ["style.css", "other.css", "fonts/theme.woff2"];
        Dictionary<string, string> cssFiles = new()
        {
            ["other.css"] = "@font-face{font-family:Theme;src:url('fonts/theme.woff2')}"
        };

        CssValidator.Validate("@import url('other.css');", "style.css", files, path => cssFiles[path]);
    }

    [Fact]
    public void Validate_RejectsRemoteImports()
    {
        HashSet<string> files = ["style.css"];

        Assert.Throws<InvalidDataException>(() => CssValidator.Validate("@import \"https://cdn.example/theme.css\";", "style.css", files));
    }
}
