using Avalonia.Controls;
using Avalonia.Input;
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

        Title = $"Squeaker {Updates.AppVersion} Settings";
        StartWithWindowsBox.IsChecked = Startup.IsEnabled;
        CheckForUpdatesBox.IsChecked = settings.CheckForUpdates;
        HideOnTypingBox.IsChecked = settings.HideOnTyping;
        IgnoreShortcutsBox.IsChecked = settings.IgnoreShortcuts;
        MoveToCaretBox.IsChecked = settings.MoveToCaret;
        HideOnIdleBox.IsChecked = settings.HideOnIdle;
        IdleSecondsBox.Value = settings.IdleSeconds;
        MoveThresholdBox.Value = settings.MoveThreshold;
        ShowAfterTypingBox.Value = settings.ShowAfterTypingSeconds;
        ExcludedBox.Text = string.Join(Environment.NewLine, settings.ExcludedApps);
        StepAsideBox.IsChecked = settings.StepAsideForFullScreen;
        PauseHotkeyBox.Text = settings.PauseHotkey;
        // Tunnel, so the keys never reach the box as typing.
        PauseHotkeyBox.AddHandler(KeyDownEvent, PauseHotkey_KeyDown, RoutingStrategies.Tunnel);
        WindowsHidingPanel.IsVisible = CursorHider.WindowsHidesWhileTyping;

        // The options that only matter with their parent switched on.
        HideOnTypingBox.IsCheckedChanged += (_, _) => UpdateEnabled();
        HideOnIdleBox.IsCheckedChanged += (_, _) => UpdateEnabled();
        UpdateEnabled();
    }

    void UpdateEnabled()
    {
        IgnoreShortcutsBox.IsEnabled = HideOnTypingBox.IsChecked == true;
        MoveToCaretBox.IsEnabled = HideOnTypingBox.IsChecked == true;
        ShowAfterTypingBox.IsEnabled = HideOnTypingBox.IsChecked == true;
        IdleSecondsBox.IsEnabled = HideOnIdleBox.IsChecked == true;
    }

    void Save_Click(object? sender, RoutedEventArgs e)
    {
        _settings.HideOnTyping = HideOnTypingBox.IsChecked == true;
        _settings.IgnoreShortcuts = IgnoreShortcutsBox.IsChecked == true;
        _settings.MoveToCaret = MoveToCaretBox.IsChecked == true;
        _settings.HideOnIdle = HideOnIdleBox.IsChecked == true;
        _settings.IdleSeconds = (int)(IdleSecondsBox.Value ?? SqueakerSettings.MinIdleSeconds);
        _settings.MoveThreshold = (int)(MoveThresholdBox.Value ?? 2);
        _settings.ShowAfterTypingSeconds = (int)(ShowAfterTypingBox.Value ?? 0);
        _settings.ExcludedApps = (ExcludedBox.Text ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _settings.StepAsideForFullScreen = StepAsideBox.IsChecked == true;
        _settings.PauseHotkey = PauseHotkeyBox.Text ?? "";
        _settings.CheckForUpdates = CheckForUpdatesBox.IsChecked == true;
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

    void PauseHotkey_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Tab or Key.Escape) return; // Leave those for moving on and closing.
        e.Handled = true;
        if (e.Key is Key.Back or Key.Delete)
        {
            PauseHotkeyBox.Text = "";
            return;
        }

        // Ctrl, Alt and the like on their own: wait for the real key.
        string? key = e.Key switch
        {
            >= Key.A and <= Key.Z or >= Key.F1 and <= Key.F24 => e.Key.ToString(),
            >= Key.D0 and <= Key.D9 => ((char)('0' + (e.Key - Key.D0))).ToString(),
            _ => null,
        };
        if (key is null) return;

        uint modifiers = 0;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= HotkeyText.Ctrl;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= HotkeyText.Alt;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= HotkeyText.Shift;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= HotkeyText.Win;
        if (HotkeyText.Format(modifiers, key) is { } text) PauseHotkeyBox.Text = text;
    }

    void ClearHotkey_Click(object? sender, RoutedEventArgs e) => PauseHotkeyBox.Text = "";

    // Takes effect straight away, not on Save: it's a Windows setting, not one of ours.
    void TurnOffWindowsHiding_Click(object? sender, RoutedEventArgs e)
    {
        if (!CursorHider.TurnOffWindowsHiding()) Log.Write("Couldn't turn off Windows' own \"Hide pointer while typing\".");
        WindowsHidingPanel.IsVisible = CursorHider.WindowsHidesWhileTyping;
    }

    void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
