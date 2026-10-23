using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Squeaker.Core;
using static Squeaker.Native;

namespace Squeaker;

// Watches the keyboard and mouse and hides and shows the cursor, all on a thread of its own, like
// PowerToys' worker process. Low-level hooks are called on the thread that installed them, and
// every key press and mouse move on the system waits for the answer, so this thread does nothing
// that could keep them waiting: the hooks only decide, and the hiding happens after they return.
// While a full-screen or excluded app is in front it steps aside: no hooks at all, so a game's
// mouse doesn't wait on us. Settings are fixed for its lifetime; to change them, start a new one.
sealed class CursorWatcher : IDisposable
{
    const uint HideMessage = WM_APP + 1;
    const uint ShowMessage = WM_APP + 2;
    const uint TickMs = 100;
    const long StepAsideCheckMs = 500;

    readonly SqueakerSettings _settings;
    readonly Thread _thread;
    readonly ManualResetEventSlim _ready = new();
    readonly CursorHider _hider = new();
    // Kept in fields so the garbage collector leaves them alone while Windows holds them.
    readonly HookProc _keyboardProc;
    readonly HookProc _mouseProc;
    CursorState _state = null!;
    IntPtr _keyboardHook, _mouseHook;
    uint _threadId;
    bool _steppedAside;
    long _lastStepAsideCheck;
    // The last foreground process we looked up, so names cost one lookup per app switch.
    uint _lastPid;
    string? _lastPidName;

    public bool IsInstalled { get; private set; }

    public CursorWatcher(SqueakerSettings settings)
    {
        _settings = settings;
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        _thread = new Thread(Run) { IsBackground = true, Name = "Cursor watcher", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        _ready.Wait();
    }

    void Run()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE); // Makes the thread's message queue.
        _state = new CursorState(_settings, Now, CursorPosition());

        IsInstalled = Hook();
        _ready.Set();

        if (IsInstalled) Loop();

        Unhook();
        Apply(_state.Stop());
        _hider.Show();
    }

    bool Hook()
    {
        var module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        if (_settings.HideOnTyping) _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        if (_mouseHook != IntPtr.Zero && (_keyboardHook != IntPtr.Zero || !_settings.HideOnTyping)) return true;

        Log.Write($"Couldn't hook the keyboard and mouse (error {Marshal.GetLastWin32Error()}).");
        Unhook();
        return false;
    }

    void Unhook()
    {
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _keyboardHook = _mouseHook = IntPtr.Zero;
    }

    void Loop()
    {
        while (true)
        {
            // Wakes for messages (which is when the hooks get called) or every tick for the idle timer.
            MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, TickMs, QS_ALLINPUT, MWMO_INPUTAVAILABLE);

            // A batch at a time, so a flood of messages can't starve the timer.
            for (int i = 0; i < 64 && PeekMessage(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE); i++)
            {
                if (msg.message == WM_QUIT) return;
                if (msg.message == HideMessage) Apply(CursorAction.Hide);
                else if (msg.message == ShowMessage) Apply(CursorAction.Show);
                else DispatchMessage(ref msg);
            }

            if (Now - _lastStepAsideCheck >= StepAsideCheckMs) CheckStepAside();
            if (!_steppedAside) Apply(_state.OnTimer(Now, CursorPosition()));
        }
    }

    // Gets out of the way while a full-screen or excluded app is in front, and comes back after.
    void CheckStepAside()
    {
        _lastStepAsideCheck = Now;
        bool fullScreen = _settings.StepAsideForFullScreen && ForegroundIsFullScreen();
        bool stepAside = fullScreen || ForegroundExcluded();
        if (stepAside == _steppedAside) return;

        _steppedAside = stepAside;
        if (stepAside)
        {
            Unhook();
            Apply(_state.Stop());
            Log.Write($"Stepped aside for {ForegroundName() ?? "an app"} ({(fullScreen ? "full screen" : "excluded")}).");
        }
        else
        {
            Log.Write("Back on.");
            if (Hook()) _state = new CursorState(_settings, Now, CursorPosition());
        }
    }

    void Apply(CursorAction action)
    {
        if (action == CursorAction.Hide)
        {
            // Check now rather than at the next half-second check, so the first key in a game hides nothing.
            // It also catches hides the hooks queued just before stepping aside.
            CheckStepAside();
            if (_steppedAside || !_hider.Hide()) _state.HideFailed(Now);
        }
        else if (action == CursorAction.Show && !_hider.Show())
        {
            _state.ShowFailed();
        }
    }

    // Hooks only queue the work. Hiding means 13 SetSystemCursor calls, which isn't for in here.
    void Queue(CursorAction action)
    {
        if (action == CursorAction.Hide) PostThreadMessage(_threadId, HideMessage, IntPtr.Zero, IntPtr.Zero);
        else if (action == CursorAction.Show) PostThreadMessage(_threadId, ShowMessage, IntPtr.Zero, IntPtr.Zero);
    }

    IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var key = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            // Keys sent by other programs (AutoHotkey, remote tools, on-screen keyboards) aren't you typing.
            if ((key.flags & (LLKHF_INJECTED | LLKHF_LOWER_IL_INJECTED)) == 0)
                Queue(_state.OnKey(Now, CursorPosition(), key.vkCode, ShortcutHeld()));
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = (int)wParam;
            MouseInput? input = message == WM_MOUSEMOVE ? MouseInput.Move
                : message is >= WM_LBUTTONDOWN and <= WM_MOUSEHWHEEL ? MouseInput.ButtonOrWheel
                : null;
            if (input is { } kind)
            {
                var mouse = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                Queue(_state.OnMouse(Now, new Core.Point(mouse.pt.X, mouse.pt.Y), kind));
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    static bool ShortcutHeld() =>
        IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN);

    static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    bool ForegroundExcluded() =>
        _settings.ExcludedApps.Count > 0 && ForegroundName() is { } name && _settings.IsExcluded(name);

    string? ForegroundName()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        if (pid == _lastPid) return _lastPidName;

        _lastPid = pid;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            _lastPidName = process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            _lastPidName = null;
        }
        return _lastPidName;
    }

    // Covers its whole monitor and has no title bar: a game (exclusive or borderless), an F11
    // browser, a full-screen video. Ordinary maximized windows keep their title bar style, which
    // matters with an auto-hiding taskbar, where they cover the monitor too.
    static bool ForegroundIsFullScreen()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || (GetWindowLongPtr(window, GWL_STYLE) & WS_CAPTION) == WS_CAPTION ||
            !GetWindowRect(window, out var rect))
            return false;

        // The desktop covers the screen too.
        var name = new char[16];
        var className = new string(name, 0, GetClassName(window, name, name.Length));
        if (className is "Progman" or "WorkerW") return false;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        var monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return false;
        var screen = info.rcMonitor;
        return rect.Left <= screen.Left && rect.Top <= screen.Top && rect.Right >= screen.Right && rect.Bottom >= screen.Bottom;
    }

    static long Now => (long)GetTickCount64();

    static Core.Point CursorPosition()
    {
        GetCursorPos(out var point);
        return new(point.X, point.Y);
    }

    // Unhooks and puts the cursor back.
    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(2000);
        _ready.Dispose();
    }
}
