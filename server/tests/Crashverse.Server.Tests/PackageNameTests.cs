using Crashverse.Server.Domain;
using Xunit;

namespace Crashverse.Server.Tests;

public class PackageNameTests
{
    [Fact]
    public void DefaultPackageName_MatchesChosenAndroidPackage()
    {
        Assert.Equal("com.crashverse.game", new ServerOptions().PackageName);
    }
}
