using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Squeaker.Core;

namespace Squeaker;

public partial class App : Application
{
    SqueakerSettings _settings = new();
    CursorWatcher? _watcher;
    SettingsWindow? _settingsWindow;
    bool _paused;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Tray only, no main window.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => _watcher?.Dispose();

            _settings = SqueakerSettings.Load(AppPaths.SettingsFile, out var problem);
            if (problem is not null) Log.Write(problem);
            Log.Write("Started.");
            if (CursorHider.WindowsHidesWhileTyping)
                Log.Write("Windows' own \"Hide pointer while typing\" is on; it overrides some settings in older apps. Settings can turn it off.");

            if (MenuItem("Start with Windows") is { } startup) startup.IsChecked = Startup.IsEnabled;
            ApplySettings();
        }

        base.OnFrameworkInitializationCompleted();
    }

    void SaveSettings()
    {
        try
        {
            _settings.Save(AppPaths.SettingsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Couldn't save settings: {ex.Message}");
        }
        ApplySettings();
    }

    // The watcher's settings are fixed, so start a fresh one. That also shows the cursor if it was hidden.
    void ApplySettings()
    {
        _watcher?.Dispose();
        _watcher = null;
        if (!_paused && (_settings.HideOnTyping || _settings.HideOnIdle))
            _watcher = new CursorWatcher(_settings.Clone());

        if (TrayIcon.GetIcons(this)?.FirstOrDefault() is { } tray)
        {
            tray.Icon = new WindowIcon(AssetLoader.Open(new Uri(_paused ? "avares://Squeaker/Assets/SqueakerPaused.ico" : "avares://Squeaker/Assets/Squeaker.ico")));
            tray.ToolTipText = _paused ? "Squeaker (paused)" : "Squeaker";
        }
    }

    void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings, SaveSettings);
            _settingsWindow.Closed += (_, _) =>
            {
                _settingsWindow = null;
                // The dialog can flip this too; keep the tray tick in step.
                if (MenuItem("Start with Windows") is { } startup) startup.IsChecked = Startup.IsEnabled;
            };
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    void Tray_Clicked(object? sender, EventArgs e) => ShowSettings();

    void Settings_Click(object? sender, EventArgs e) => ShowSettings();

    void Pause_Click(object? sender, EventArgs e)
    {
        _paused = !_paused;
        if (sender is NativeMenuItem item) item.IsChecked = _paused;
        ApplySettings();
    }

    void Startup_Click(object? sender, EventArgs e)
    {
        try
        {
            Startup.Set(!Startup.IsEnabled);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Log.Write($"Couldn't change start with Windows: {ex.Message}");
        }
        if (sender is NativeMenuItem item) item.IsChecked = Startup.IsEnabled;
    }

    void OpenFolder_Click(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(AppPaths.Folder);
        Process.Start(new ProcessStartInfo(AppPaths.Folder) { UseShellExecute = true });
    }

    NativeMenuItem? MenuItem(string header) =>
        TrayIcon.GetIcons(this)?.FirstOrDefault()?.Menu?.Items.OfType<NativeMenuItem>()
            .FirstOrDefault(i => i.Header == header);

    void Exit_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
