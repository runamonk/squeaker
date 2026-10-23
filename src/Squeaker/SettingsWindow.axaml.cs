using Avalonia.Controls;
using Avalonia.Interactivity;
using Squeaker.Core;

namespace Squeaker;

// Changes only apply on Save.
public partial class SettingsWindow : Window
{
    readonly SqueakerSettings _settings;
    readonly Action _save;

    // For the XAML previewer.
    public SettingsWindow() : this(new SqueakerSettings(), () => { }) { }

    public SettingsWindow(SqueakerSettings settings, Action save)
    {
        InitializeComponent();
        _settings = settings;
        _save = save;

        StartWithWindowsBox.IsChecked = Startup.IsEnabled;
        HideOnTypingBox.IsChecked = settings.HideOnTyping;
        IgnoreShortcutsBox.IsChecked = settings.IgnoreShortcuts;
        HideOnIdleBox.IsChecked = settings.HideOnIdle;
        IdleSecondsBox.Value = settings.IdleSeconds;
        MoveThresholdBox.Value = settings.MoveThreshold;
        ShowAfterTypingBox.Value = settings.ShowAfterTypingSeconds;
        ExcludedBox.Text = string.Join(Environment.NewLine, settings.ExcludedApps);
        WindowsHidingPanel.IsVisible = CursorHider.WindowsHidesWhileTyping;

        // The options that only matter with their parent switched on.
        HideOnTypingBox.IsCheckedChanged += (_, _) => UpdateEnabled();
        HideOnIdleBox.IsCheckedChanged += (_, _) => UpdateEnabled();
        UpdateEnabled();
    }

    void UpdateEnabled()
    {
        IgnoreShortcutsBox.IsEnabled = HideOnTypingBox.IsChecked == true;
        ShowAfterTypingBox.IsEnabled = HideOnTypingBox.IsChecked == true;
        IdleSecondsBox.IsEnabled = HideOnIdleBox.IsChecked == true;
    }

    void Save_Click(object? sender, RoutedEventArgs e)
    {
        _settings.HideOnTyping = HideOnTypingBox.IsChecked == true;
        _settings.IgnoreShortcuts = IgnoreShortcutsBox.IsChecked == true;
        _settings.HideOnIdle = HideOnIdleBox.IsChecked == true;
        _settings.IdleSeconds = (int)(IdleSecondsBox.Value ?? SqueakerSettings.MinIdleSeconds);
        _settings.MoveThreshold = (int)(MoveThresholdBox.Value ?? 2);
        _settings.ShowAfterTypingSeconds = (int)(ShowAfterTypingBox.Value ?? 0);
        _settings.ExcludedApps = (ExcludedBox.Text ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _settings.Normalize();
        _save();

        bool startWithWindows = StartWithWindowsBox.IsChecked == true;
        if (startWithWindows != Startup.IsEnabled)
        {
            try
            {
                Startup.Set(startWithWindows);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                Log.Write($"Couldn't change start with Windows: {ex.Message}");
            }
        }
        Close();
    }

    // Takes effect straight away, not on Save: it's a Windows setting, not one of ours.
    void TurnOffWindowsHiding_Click(object? sender, RoutedEventArgs e)
    {
        if (!CursorHider.TurnOffWindowsHiding()) Log.Write("Couldn't turn off Windows' own \"Hide pointer while typing\".");
        WindowsHidingPanel.IsVisible = CursorHider.WindowsHidesWhileTyping;
    }

    void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
