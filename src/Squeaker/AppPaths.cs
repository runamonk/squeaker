namespace Squeaker;

static class AppPaths
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Squeaker");

    public static string SettingsFile => Path.Combine(Folder, "settings.json");
    public static string LogFile => Path.Combine(Folder, "squeaker.log");
}
