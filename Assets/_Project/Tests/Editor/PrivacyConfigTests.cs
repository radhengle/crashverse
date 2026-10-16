#if UNITY_INCLUDE_TESTS
using Crashverse.EditorTools;
using Crashverse.UI;
using NUnit.Framework;

namespace Crashverse.Tests.EditMode
{
    public class PrivacyConfigTests
    {
        [TearDown] public void Reset() => PrivacyConfig.ResetForTests();

        [Test] public void Release_build_fails_when_url_empty() => Assert.NotNull(PrivacyUrlBuildGuard.Check(false, "{\"privacyUrl\":\"\"}"));
        [Test] public void Release_build_fails_when_config_missing() => Assert.NotNull(PrivacyUrlBuildGuard.Check(false, null));
        [Test] public void Release_build_fails_on_non_https() => Assert.NotNull(PrivacyUrlBuildGuard.Check(false, "{\"privacyUrl\":\"http://example.com/privacy\"}"));
        [Test] public void Release_build_fails_on_malformed_json() => Assert.NotNull(PrivacyUrlBuildGuard.Check(false, "not json"));
        [Test] public void Release_build_passes_with_https_url() => Assert.IsNull(PrivacyUrlBuildGuard.Check(false, "{\"privacyUrl\":\" https://example.com/privacy \"}"));
        [Test] public void Development_build_passes_when_empty() => Assert.IsNull(PrivacyUrlBuildGuard.Check(true, "{\"privacyUrl\":\"\"}"));

        [Test] public void Shipped_config_parses() => Assert.DoesNotThrow(() => PrivacyConfig.Parse(System.IO.File.ReadAllText(PrivacyUrlBuildGuard.ConfigPath)));
        [Test] public void Empty_url_means_no_link() { PrivacyConfig.OverrideForTests(""); Assert.False(PrivacyConfig.HasUrl); }
        [Test] public void Https_url_means_link() { PrivacyConfig.OverrideForTests("https://example.com/p"); Assert.True(PrivacyConfig.HasUrl); }
    }
}
#endif
