using Prismedia.Api.Endpoints;

namespace Prismedia.Api.Tests;

public sealed class RestoreInProcessDecisionTests {
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void DefaultsToInProcessInDevelopmentOrOnWindows(bool isDevelopment, bool isWindows, bool expected) {
        Assert.Equal(expected, SettingsEndpoints.ShouldRestoreInProcess(null, isDevelopment, isWindows));
    }

    [Theory]
    [InlineData("false", true, true, false)]
    [InlineData("False", false, true, false)]
    [InlineData("true", false, false, true)]
    public void ExplicitSettingOverridesDefault(string configured, bool isDevelopment, bool isWindows, bool expected) {
        Assert.Equal(expected, SettingsEndpoints.ShouldRestoreInProcess(configured, isDevelopment, isWindows));
    }

    [Fact]
    public void UnparseableSettingFallsBackToDefault() {
        Assert.False(SettingsEndpoints.ShouldRestoreInProcess("sometimes", isDevelopment: false, isWindows: false));
        Assert.True(SettingsEndpoints.ShouldRestoreInProcess("sometimes", isDevelopment: false, isWindows: true));
    }
}
