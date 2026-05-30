using System.IO;
using Jellyfin.Plugin.ThemeLoader.Services;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public class ArchivePathTests
{
    [Fact]
    public void NormalizeZipEntry_RejectsTraversal()
    {
        Assert.Throws<InvalidDataException>(() => ArchivePath.Normalize("../style.css"));
    }

    [Fact]
    public void NormalizeZipEntry_RejectsBackslashSeparators()
    {
        Assert.Throws<InvalidDataException>(() => ArchivePath.Normalize("css\\style.css"));
    }

    [Fact]
    public void NormalizeCssReference_AllowsParentInsideThemeRoot()
    {
        string result = ArchivePath.NormalizeCssReference("css/sub", "../images/bg.png");

        Assert.Equal("css/images/bg.png", result);
    }
}