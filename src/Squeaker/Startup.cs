using Microsoft.Win32;

namespace Squeaker;

// "Start with Windows": the per-user Run key.
static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Squeaker";

    // The real exe, even when launched through `dotnet Squeaker.dll` from the debugger.
    public static string ExePath => Path.Combine(AppContext.BaseDirectory, "Squeaker.exe");

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && value.Contains(ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{ExePath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
