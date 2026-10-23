using System.ComponentModel;
using System.Diagnostics;

namespace Squeaker;

// A second copy of Squeaker that waits for the first to exit, however it goes, and then puts the
// cursors back. Without it, a crash or an End task while the cursor is hidden leaves it hidden
// until you sign out. PowerToys does the same with its worker process and supervisor.
static class Watchdog
{
    public const string Arg = "--watchdog";

    public static void Start()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Startup.ExePath, $"{Arg} {Environment.ProcessId}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log.Write($"Couldn't start the watchdog, so a crash could leave the cursor hidden: {ex.Message}");
        }
    }

    // Runs in the watchdog copy, in place of the app.
    public static void Run(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int pid)) return;
        try
        {
            using var parent = Process.GetProcessById(pid);
            parent.WaitForExit();
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
        CursorHider.RestoreSystemCursors();
    }
}
