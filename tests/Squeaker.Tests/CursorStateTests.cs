using Squeaker.Core;

namespace Squeaker.Tests;

public sealed class CursorStateTests
{
    const uint KeyA = 0x41, Shift = 0x10, LeftCtrl = 0xA2;
    static readonly Point Origin = new(100, 100);

    static CursorState Make(Action<SqueakerSettings>? change = null)
    {
        var settings = new SqueakerSettings();
        change?.Invoke(settings);
        return new CursorState(settings, 0, Origin);
    }

    [Fact]
    public void Typing_Hides_Once()
    {
        var state = Make();

        Assert.Equal(CursorAction.Hide, state.OnKey(10, Origin, KeyA, false));
        Assert.Equal(CursorAction.None, state.OnKey(20, Origin, KeyA, false));
        Assert.True(state.IsHidden);
    }

    [Fact]
    public void Typing_DoesNothing_WhenOff()
    {
        var state = Make(s => s.HideOnTyping = false);

        Assert.Equal(CursorAction.None, state.OnKey(10, Origin, KeyA, false));
    }

    [Theory]
    [InlineData(Shift)]
    [InlineData(LeftCtrl)]
    [InlineData(0x5B)] // left Win
    public void ModifiersAlone_DontHide(uint key)
    {
        Assert.Equal(CursorAction.None, Make().OnKey(10, Origin, key, false));
    }

    [Fact]
    public void Shortcuts_DontHide_WhenIgnored()
    {
        Assert.Equal(CursorAction.None, Make().OnKey(10, Origin, KeyA, shortcutHeld: true));
        Assert.Equal(CursorAction.Hide, Make(s => s.IgnoreShortcuts = false).OnKey(10, Origin, KeyA, shortcutHeld: true));
    }

    [Fact]
    public void SmallMove_KeepsItHidden_BigMoveShows()
    {
        var state = Make(s => s.MoveThreshold = 5);
        state.OnKey(10, Origin, KeyA, false);

        Assert.Equal(CursorAction.None, state.OnMouse(20, new(103, 103), MouseInput.Move));
        Assert.Equal(CursorAction.Show, state.OnMouse(30, new(105, 100), MouseInput.Move));
        Assert.False(state.IsHidden);
    }

    [Fact]
    public void Timer_Shows_WhenTheHookMissedTheMove()
    {
        var state = Make(s => { s.HideOnIdle = true; s.IdleSeconds = 2; });
        state.OnKey(10, Origin, KeyA, false);

        Assert.Equal(CursorAction.None, state.OnTimer(100, new(101, 100)));
        Assert.Equal(CursorAction.Show, state.OnTimer(200, new(110, 100)));
        // Counts as mouse activity, so idle hiding waits a full delay.
        Assert.Equal(CursorAction.None, state.OnTimer(2100, new(110, 100)));
        Assert.Equal(CursorAction.Hide, state.OnTimer(2200, new(110, 100)));
    }

    [Fact]
    public void Click_Shows_WithoutMoving()
    {
        var state = Make();
        state.OnKey(10, Origin, KeyA, false);

        Assert.Equal(CursorAction.Show, state.OnMouse(20, Origin, MouseInput.ButtonOrWheel));
    }

    [Fact]
    public void Idle_HidesAfterDelay_OnlyWhenOn()
    {
        Assert.Equal(CursorAction.None, Make().OnTimer(60_000, Origin));

        var state = Make(s => { s.HideOnIdle = true; s.IdleSeconds = 3; });
        state.OnMouse(1000, Origin, MouseInput.Move);
        Assert.Equal(CursorAction.None, state.OnTimer(3900, Origin));
        Assert.Equal(CursorAction.Hide, state.OnTimer(4000, Origin));
        Assert.Equal(CursorAction.None, state.OnTimer(9000, Origin));
    }

    [Fact]
    public void ShowAfterTyping_BringsItBack_AndRestartsIdleWait()
    {
        var state = Make(s => { s.ShowAfterTypingSeconds = 2; s.HideOnIdle = true; s.IdleSeconds = 5; });
        state.OnKey(10_000, Origin, KeyA, false);
        state.OnKey(11_000, Origin, KeyA, false);

        Assert.Equal(CursorAction.None, state.OnTimer(12_900, Origin));
        Assert.Equal(CursorAction.Show, state.OnTimer(13_000, Origin));
        // Idle hiding waits a full delay from there, not from the last mouse move long ago.
        Assert.Equal(CursorAction.None, state.OnTimer(17_900, Origin));
        Assert.Equal(CursorAction.Hide, state.OnTimer(18_000, Origin));
    }

    [Fact]
    public void ShowAfterTyping_LeavesIdleHidingAlone()
    {
        var state = Make(s => { s.ShowAfterTypingSeconds = 1; s.HideOnIdle = true; s.IdleSeconds = 1; });
        Assert.Equal(CursorAction.Hide, state.OnTimer(1000, Origin));

        Assert.Equal(CursorAction.None, state.OnTimer(60_000, Origin));
    }

    [Fact]
    public void HideFailed_WaitsAFullIdleDelay()
    {
        var state = Make(s => { s.HideOnIdle = true; s.IdleSeconds = 2; });
        Assert.Equal(CursorAction.Hide, state.OnTimer(2000, Origin));
        state.HideFailed(2000);

        Assert.False(state.IsHidden);
        Assert.Equal(CursorAction.None, state.OnTimer(3000, Origin));
        Assert.Equal(CursorAction.Hide, state.OnTimer(4000, Origin));
    }

    [Fact]
    public void MovedTo_DoesntCountAsTheMouseMoving()
    {
        var state = Make();
        state.OnKey(10, Origin, KeyA, false);
        var caret = new Point(900, 500);
        state.MovedTo(caret);

        Assert.Equal(CursorAction.None, state.OnMouse(20, caret, MouseInput.Move));
        Assert.Equal(CursorAction.None, state.OnTimer(100, caret));
        Assert.Equal(CursorAction.Show, state.OnMouse(30, new(910, 500), MouseInput.Move));
    }

    [Fact]
    public void HiddenByTyping_OnlyForTyping()
    {
        var typed = Make();
        Assert.False(typed.HiddenByTyping);
        typed.OnKey(10, Origin, KeyA, false);
        Assert.True(typed.HiddenByTyping);
        typed.OnMouse(20, Origin, MouseInput.ButtonOrWheel);
        Assert.False(typed.HiddenByTyping);

        var idle = Make(s => { s.HideOnIdle = true; s.IdleSeconds = 1; });
        Assert.Equal(CursorAction.Hide, idle.OnTimer(1000, Origin));
        Assert.False(idle.HiddenByTyping);
    }

    [Fact]
    public void Stop_ShowsIfHidden()
    {
        var state = Make();
        Assert.Equal(CursorAction.None, state.Stop());
        state.OnKey(10, Origin, KeyA, false);
        Assert.Equal(CursorAction.Show, state.Stop());
    }
}
