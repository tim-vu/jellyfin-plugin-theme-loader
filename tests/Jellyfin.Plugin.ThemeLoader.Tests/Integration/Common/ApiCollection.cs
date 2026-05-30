using Xunit;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<AppFixture>
{
  public const string Name = "ApiCollection";
}
