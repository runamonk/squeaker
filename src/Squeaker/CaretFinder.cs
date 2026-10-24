using System.Runtime.InteropServices;
using static Squeaker.Native;

namespace Squeaker;

// Finds the text caret in the app in front, in screen pixels. Asks through accessibility (MSAA),
// which covers classic apps (Windows answers for its own caret) and Chromium ones like VS Code and
// browsers, which have no Windows caret at all. It talks to the other app, so it's not for the hook thread.
static class CaretFinder
{
    static Guid IAccessibleId = typeof(IAccessible).GUID;

    public static RECT? Find()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return null;
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(GetWindowThreadProcessId(window, out _), ref info)) return null;
        var focus = info.hwndFocus != IntPtr.Zero ? info.hwndFocus : window;

        object? found = null;
        try
        {
            if (AccessibleObjectFromWindow(focus, OBJID_CARET, ref IAccessibleId, out found) != 0 ||
                found is not IAccessible caret ||
                caret.accLocation(out int x, out int y, out int width, out int height, 0) != 0 || height <= 0)
                return null;

            // An app with no caret can still answer, with somewhere stale. Only trust it inside the window.
            if (!GetWindowRect(window, out var bounds) ||
                x < bounds.Left || x >= bounds.Right || y < bounds.Top || y >= bounds.Bottom)
                return null;
            return new RECT { Left = x, Top = y, Right = x + width, Bottom = y + height };
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (found is not null && Marshal.IsComObject(found)) Marshal.ReleaseComObject(found);
        }
    }
}
