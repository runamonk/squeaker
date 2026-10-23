using Avalonia;
using System;
using System.Runtime.Versioning;

// Hooks, system cursors and the registry: this app only makes sense on Windows.
[assembly: SupportedOSPlatform("windows")]

namespace Squeaker;

class Program
{
    // Avalonia and SynchronizationContext-dependent APIs are unavailable until AppMain starts.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.FirstOrDefault() == Watchdog.Arg)
        {
            Watchdog.Run(args);
            return;
        }

        // Two copies would hide and show the cursor over each other.
        using var mutex = new Mutex(false, @"Local\Squeaker");
        if (!TryOwn(mutex)) return;

        // Belt and braces with the watchdog: put the cursor back on the way out, however we go.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CursorHider.RestoreSystemCursors();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            CursorHider.RestoreSystemCursors();
            Log.Write($"Crashed: {e.ExceptionObject}");
        };
        Watchdog.Start();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    static bool TryOwn(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            return true; // The last copy crashed; it's ours now.
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
