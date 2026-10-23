namespace Squeaker.Core;

public enum CursorAction { None, Hide, Show }

public enum MouseInput { Move, ButtonOrWheel }

public readonly record struct Point(int X, int Y);

// Decides when the cursor hides and when it comes back. No Windows calls in here, so it can be
// tested. Ported from PowerToys' AutoHideCursorState.h, plus shortcuts and show-after-typing.
// Times are milliseconds from any steady clock.
public sealed class CursorState
{
    readonly SqueakerSettings _settings;
    long _lastMouse;
    long _lastKey;
    Point _hiddenAt;
    bool _hiddenByTyping;

    public bool IsHidden { get; private set; }

    public CursorState(SqueakerSettings settings, long now, Point cursor)
    {
        _settings = settings;
        _lastMouse = now;
        _hiddenAt = cursor;
    }

    public static bool IsModifier(uint virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12 or // Shift, Ctrl, Alt
        0x5B or 0x5C or         // left and right Win
        >= 0xA0 and <= 0xA5;    // left and right Shift, Ctrl, Alt

    // shortcutHeld: Ctrl, Alt or Win was already down when this key went down.
    public CursorAction OnKey(long now, Point cursor, uint virtualKey, bool shortcutHeld)
    {
        if (!_settings.HideOnTyping || IsModifier(virtualKey)) return CursorAction.None;
        if (_settings.IgnoreShortcuts && shortcutHeld) return CursorAction.None;

        _lastKey = now;
        if (IsHidden) return CursorAction.None;
        IsHidden = true;
        _hiddenByTyping = true;
        _hiddenAt = cursor;
        return CursorAction.Hide;
    }

    public CursorAction OnMouse(long now, Point cursor, MouseInput input)
    {
        _lastMouse = now;
        if (!IsHidden) return CursorAction.None;
        // A nudge of a pixel or two (a bumped desk, a sensor's jitter) doesn't count.
        if (input == MouseInput.Move && !MovedFar(cursor)) return CursorAction.None;

        IsHidden = false;
        return CursorAction.Show;
    }

    // Called every 100ms or so.
    public CursorAction OnTimer(long now, Point cursor)
    {
        if (IsHidden)
        {
            if (!_hiddenByTyping || _settings.ShowAfterTypingSeconds <= 0 ||
                now - _lastKey < _settings.ShowAfterTypingSeconds * 1000L)
                return CursorAction.None;

            // Back it comes; start the idle wait over, or idle hiding would take it straight away again.
            IsHidden = false;
            _lastMouse = now;
            return CursorAction.Show;
        }

        if (!_settings.HideOnIdle || now - _lastMouse < _settings.IdleSeconds * 1000L) return CursorAction.None;
        IsHidden = true;
        _hiddenByTyping = false;
        _hiddenAt = cursor;
        return CursorAction.Hide;
    }

    public CursorAction Stop()
    {
        if (!IsHidden) return CursorAction.None;
        IsHidden = false;
        return CursorAction.Show;
    }

    // Hiding didn't happen (it failed, or the app in front is excluded). Wait a full idle delay before trying again.
    public void HideFailed(long now)
    {
        IsHidden = false;
        _lastMouse = now;
    }

    public void ShowFailed() => IsHidden = true;

    bool MovedFar(Point cursor)
    {
        long dx = cursor.X - _hiddenAt.X, dy = cursor.Y - _hiddenAt.Y;
        long threshold = _settings.MoveThreshold;
        return dx * dx + dy * dy >= threshold * threshold;
    }
}
