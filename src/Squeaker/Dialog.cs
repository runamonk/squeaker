using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace Squeaker;

// Avalonia has no message box, so here's a small one, like <div>'s. Squeaker has no main window
// to own it, so it stands on its own and stays on top.
static class Dialog
{
    // True if the first button was clicked. Without cancelText there's just the one button.
    public static Task<bool> Show(string title, string message, string confirmText = "OK", string? cancelText = null)
    {
        var dialog = new Window
        {
            Title = "Squeaker",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Squeaker/Assets/Squeaker.ico"))),
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        bool confirmed = false;
        var done = new TaskCompletionSource<bool>();
        dialog.Closed += (_, _) => done.TrySetResult(confirmed);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new(0, 8, 0, 0),
        };
        var confirm = new Button { Content = confirmText, Classes = { "primary", "action" }, IsDefault = true, IsCancel = cancelText is null };
        confirm.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };
        buttons.Children.Add(confirm);
        if (cancelText is not null)
        {
            var cancel = new Button { Content = cancelText, Classes = { "action" }, IsCancel = true };
            cancel.Click += (_, _) => dialog.Close();
            buttons.Children.Add(cancel);
        }

        dialog.Content = new StackPanel
        {
            Margin = new(24),
            Spacing = 12,
            MaxWidth = 420,
            Children =
            {
                new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Classes = { "muted" } },
                buttons,
            },
        };

        dialog.Show();
        dialog.Activate();
        return done.Task;
    }
}
