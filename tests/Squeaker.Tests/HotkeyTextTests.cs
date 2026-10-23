using Squeaker.Core;

namespace Squeaker.Tests;

public sealed class HotkeyTextTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Shift+M", HotkeyText.Ctrl | HotkeyText.Alt | HotkeyText.Shift, 'M')]
    [InlineData("win+5", HotkeyText.Win, '5')]
    [InlineData(" Alt + f12 ", HotkeyText.Alt, 0x7B)]
    [InlineData("Ctrl+F1", HotkeyText.Ctrl, 0x70)]
    public void Parses(string text, uint modifiers, uint key)
    {
        Assert.True(HotkeyText.TryParse(text, out uint parsedModifiers, out uint parsedKey));
        Assert.Equal(modifiers, parsedModifiers);
        Assert.Equal(key, parsedKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("M")]
    [InlineData("Shift+M")] // Would eat every capital M.
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Space")]
    [InlineData("Ctrl+F25")]
    [InlineData("Hyper+M")]
    public void Rejects(string text)
    {
        Assert.False(HotkeyText.TryParse(text, out _, out _));
    }

    [Fact]
    public void Formats_InAFixedOrder()
    {
        Assert.Equal("Ctrl+Alt+Shift+Win+F5", HotkeyText.Format(HotkeyText.Win | HotkeyText.Shift | HotkeyText.Alt | HotkeyText.Ctrl, "f5"));
        Assert.Null(HotkeyText.Format(HotkeyText.Shift, "M"));
    }

    [Fact]
    public void Settings_DropABrokenHotkey()
    {
        var settings = new SqueakerSettings { PauseHotkey = "Shift+M" };
        settings.Normalize();
        Assert.Equal("", settings.PauseHotkey);
    }
}
