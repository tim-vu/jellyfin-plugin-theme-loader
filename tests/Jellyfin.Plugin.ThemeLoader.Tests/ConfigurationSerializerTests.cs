using System;
using System.IO;
using System.Xml.Serialization;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public class ConfigurationSerializerTests
{
    [Fact]
    public void XmlSerializer_RoundTripsThemeLoaderConfiguration()
    {
        var firstThemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondThemeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var state = new StateDto
        {
            Enabled = true,
            InitializationFailed = true,
            SelectedThemeId = secondThemeId,
            InstalledThemes =
            {
                new StateDto.ThemeDto
                {
                    Id = firstThemeId,
                    Name = "Blue Theme",
                    Version = "1.0.0",
                    IndexFilename = "theme.css",
                    Directory = "Blue_Theme_1.0.0",
                    UploadedAtUtc = new DateTime(2026, 5, 24, 10, 15, 0, DateTimeKind.Utc)
                },
                new StateDto.ThemeDto
                {
                    Id = secondThemeId,
                    Name = "Green Theme",
                    Version = "2.1.0",
                    IndexFilename = "index.css",
                    Directory = "Green_Theme_2.1.0",
                    UploadedAtUtc = new DateTime(2026, 5, 24, 11, 30, 0, DateTimeKind.Utc)
                }
            }
        };

        var serializer = new XmlSerializer(typeof(StateDto));
        using var stream = new MemoryStream();

        serializer.Serialize(stream, state);
        stream.Position = 0;
        var result = Assert.IsType<StateDto>(serializer.Deserialize(stream));

        Assert.Equivalent(state, result, strict: true);
    }
}