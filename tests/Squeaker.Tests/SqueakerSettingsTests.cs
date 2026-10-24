using Squeaker.Core;

namespace Squeaker.Tests;

public sealed class SqueakerSettingsTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), "SqueakerTests", Guid.NewGuid().ToString("N"));
    string SettingsPath => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    [Fact]
    public void MissingFile_WritesDefaults()
    {
        var settings = SqueakerSettings.Load(SettingsPath, out var problem);

        Assert.Null(problem);
        Assert.True(File.Exists(SettingsPath));
        Assert.True(settings.HideOnTyping);
        Assert.False(settings.HideOnIdle);
        Assert.True(settings.CheckForUpdates);
    }

    [Fact]
    public void SavedSettings_RoundTrip()
    {
        new SqueakerSettings { HideOnIdle = true, IdleSeconds = 12, MoveToCaret = true, ExcludedApps = ["mstsc"] }.Save(SettingsPath);

        var loaded = SqueakerSettings.Load(SettingsPath, out var problem);

        Assert.Null(problem);
        Assert.True(loaded.HideOnIdle);
        Assert.True(loaded.MoveToCaret);
        Assert.Equal(12, loaded.IdleSeconds);
        Assert.Equal(["mstsc"], loaded.ExcludedApps);
    }

    [Fact]
    public void BrokenFile_UsesDefaults_AndLeavesItAlone()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "{ not json");

        var settings = SqueakerSettings.Load(SettingsPath, out var problem);

        Assert.NotNull(problem);
        Assert.True(settings.HideOnTyping);
        Assert.Equal("{ not json", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void HandEditedNumbers_AreClamped()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, """{ "idleSeconds": 0, "moveThreshold": 9999, "showAfterTypingSeconds": -4, "excludedApps": null }""");

        var settings = SqueakerSettings.Load(SettingsPath, out _);

        Assert.Equal(SqueakerSettings.MinIdleSeconds, settings.IdleSeconds);
        Assert.Equal(100, settings.MoveThreshold);
        Assert.Equal(0, settings.ShowAfterTypingSeconds);
        Assert.Empty(settings.ExcludedApps);
    }

    [Theory]
    [InlineData("mstsc")]
    [InlineData("MSTSC.exe")]
    public void Excluded_IgnoresCaseAndExe(string process)
    {
        Assert.True(new SqueakerSettings { ExcludedApps = ["mstsc.exe"] }.IsExcluded(process));
    }
}
