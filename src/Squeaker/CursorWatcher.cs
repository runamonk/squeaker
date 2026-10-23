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
// Settings are fixed for its lifetime; to change them, dispose it and start a new one.
sealed class CursorWatcher : IDisposable
{
    const uint HideMessage = WM_APP + 1;
    const uint ShowMessage = WM_APP + 2;
    const uint TickMs = 100;

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
    // The last foreground process we looked up, so excluded apps cost one lookup per app switch.
    uint _lastPid;
    bool _lastPidExcluded;

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

        var module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        if (_settings.HideOnTyping) _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        IsInstalled = _mouseHook != IntPtr.Zero && (_keyboardHook != IntPtr.Zero || !_settings.HideOnTyping);
        if (!IsInstalled) Log.Write($"Couldn't hook the keyboard and mouse (error {Marshal.GetLastWin32Error()}).");
        _ready.Set();

        if (IsInstalled) Loop();

        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        Apply(_state.Stop());
        _hider.Show();
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

            Apply(_state.OnTimer(Now, CursorPosition()));
        }
    }

    void Apply(CursorAction action)
    {
        if (action == CursorAction.Hide)
        {
            if (ForegroundExcluded() || !_hider.Hide()) _state.HideFailed(Now);
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

    bool ForegroundExcluded()
    {
        if (_settings.ExcludedApps.Count == 0) return false;
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        if (pid == _lastPid) return _lastPidExcluded;

        _lastPid = pid;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            _lastPidExcluded = _settings.IsExcluded(process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            _lastPidExcluded = false;
        }
        return _lastPidExcluded;
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
