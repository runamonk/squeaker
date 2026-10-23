using System.Runtime.InteropServices;
using static Squeaker.Native;

namespace Squeaker;

// Hides the cursor everywhere by swapping every system cursor for a see-through one, the way
// PowerToys does. SetSystemCursor is the only supported way to change the cursor in other apps.
// Putting them back is one call that reloads the user's cursor scheme, so it's safe to do anytime.
sealed class CursorHider
{
    // The OCR_* cursors: arrow, I-beam, wait, cross, up, the four sizes, size-all, no, hand, app-starting.
    static readonly uint[] CursorIds = [32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650];

    public bool IsHidden { get; private set; }

    public bool Hide()
    {
        if (IsHidden) return true;
        foreach (var id in CursorIds)
        {
            // SetSystemCursor takes the cursor over (and destroys it), so each one needs its own.
            var blank = CreateBlankCursor();
            if (blank == IntPtr.Zero || !SetSystemCursor(blank, id))
            {
                int error = Marshal.GetLastWin32Error();
                if (blank != IntPtr.Zero) DestroyCursor(blank);
                RestoreSystemCursors();
                Log.Write($"Couldn't hide the cursor (error {error}).");
                return false;
            }
        }
        IsHidden = true;
        return true;
    }

    public bool Show()
    {
        if (!IsHidden) return true;
        if (!RestoreSystemCursors())
        {
            Log.Write($"Couldn't bring the cursor back (error {Marshal.GetLastWin32Error()}).");
            return false;
        }
        IsHidden = false;
        return true;
    }

    public static bool RestoreSystemCursors()
    {
        if (!SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0)) return false;
        RefreshCurrentCursor();
        return true;
    }

    // Windows' own "Hide pointer while typing" (Mouse Properties > Pointer Options). It only works in
    // older apps, but there it overrides ours: shortcuts, excluded apps and show-after-typing.
    public static bool WindowsHidesWhileTyping =>
        SystemParametersInfo(SPI_GETMOUSEVANISH, 0, out int on, 0) && on != 0;

    // Same as unticking the box: saved to the profile and announced to other apps.
    public static bool TurnOffWindowsHiding() =>
        SystemParametersInfo(SPI_SETMOUSEVANISH, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

    // The window under the cursor only redraws it when asked, so ask, or it stays blank until the mouse moves.
    static void RefreshCurrentCursor()
    {
        if (!GetCursorPos(out var point)) return;
        var window = WindowFromPoint(point);
        if (window == IntPtr.Zero) return;

        var position = (IntPtr)((point.Y << 16) | (point.X & 0xFFFF));
        SendMessageTimeout(window, WM_NCHITTEST, IntPtr.Zero, position, SMTO_ABORTIFHUNG | SMTO_BLOCK, 100, out var hit);
        if (hit == IntPtr.Zero) hit = HTCLIENT;
        var lParam = (IntPtr)((WM_MOUSEMOVE << 16) | ((int)hit & 0xFFFF));
        SendMessageTimeout(window, WM_SETCURSOR, window, lParam, SMTO_ABORTIFHUNG | SMTO_BLOCK, 100, out _);
    }

    // All AND bits set and all XOR bits clear: every pixel shows what's underneath.
    static IntPtr CreateBlankCursor()
    {
        int width = GetSystemMetrics(SM_CXCURSOR), height = GetSystemMetrics(SM_CYCURSOR);
        if (width <= 0 || height <= 0) return IntPtr.Zero;
        int size = (width + 15) / 16 * 2 * height;
        var and = new byte[size];
        Array.Fill(and, (byte)0xFF);
        return CreateCursor(IntPtr.Zero, 0, 0, width, height, and, new byte[size]);
    }
}
