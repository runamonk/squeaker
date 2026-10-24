using System.Diagnostics;
using System.Runtime.InteropServices;
using Squeaker.Core;

// Installs a downloaded update once Squeaker has exited, then starts it again.
// Squeaker runs this from a temp copy, so nothing in the app folder is in use.
const uint OkCancel = 1, Warning = 0x30, Information = 0x40;
const int ClickedOk = 1;

var options = new Dictionary<string, string>();
for (var i = 0; i + 1 < args.Length; i += 2) options[args[i]] = args[i + 1];
if (!options.TryGetValue("--pid", out var pid) || !options.TryGetValue("--zip", out var zip) ||
    !options.TryGetValue("--app", out var app) || !options.TryGetValue("--from", out var from))
{
    MessageBoxW(0, "Use Check for updates in Squeaker's tray menu to update it.", "Squeaker", Information);
    return 1;
}
var exe = Path.GetFullPath(Path.Combine(app, "Squeaker.exe"));

try
{
    try
    {
        using var closing = Process.GetProcessById(int.Parse(pid));
        closing.WaitForExit(TimeSpan.FromMinutes(1));
    }
    catch (ArgumentException) { } // Already exited.
    // Squeaker's watchdog copy exits right after it, so give it a moment before asking.
    for (var i = 0; i < 50 && IsRunning(exe); i++) Thread.Sleep(100);
    while (IsRunning(exe))
        if (MessageBoxW(0, "Exit Squeaker to finish updating, then click OK.", "Squeaker update", OkCancel | Information) != ClickedOk)
            return 0;
    UpdateInstaller.Install(zip, app, from);
}
catch (Exception e)
{
    MessageBoxW(0, "Squeaker could not be updated.\n\n" + e.Message, "Squeaker update", Warning);
}

try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = app }); }
catch (Exception e) { MessageBoxW(0, "Squeaker could not be started.\n\n" + e.Message, "Squeaker update", Warning); }
return 0;

static bool IsRunning(string exe)
{
    foreach (var process in Process.GetProcessesByName("Squeaker"))
        using (process)
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { } // Exited or not ours.
        }
    return false;
}

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBoxW(nint owner, string text, string caption, uint type);
