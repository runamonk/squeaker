namespace Squeaker.Core;

// Hotkeys as people write them, e.g. "Ctrl+Alt+Shift+M", and as RegisterHotKey wants them.
// Keys are letters, digits and F1-F24, and there has to be a Ctrl, Alt or Win: Shift+M alone
// would eat a capital M every time you typed one.
public static class HotkeyText
{
    // RegisterHotKey's MOD_* values.
    public const uint Alt = 0x1, Ctrl = 0x2, Shift = 0x4, Win = 0x8;

    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": modifiers |= Ctrl; break;
                case "alt": modifiers |= Alt; break;
                case "shift": modifiers |= Shift; break;
                case "win": modifiers |= Win; break;
                default: return false;
            }
        }
        if ((modifiers & (Ctrl | Alt | Win)) == 0) return false;

        var key = parts[^1].ToUpperInvariant();
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')
            virtualKey = key[0]; // Letters and digits are their own virtual key codes.
        else if (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key[1..], out int f) && f is >= 1 and <= 24)
            virtualKey = (uint)(0x70 + f - 1);
        return virtualKey != 0;
    }

    // Null if it doesn't make a usable hotkey.
    public static string? Format(uint modifiers, string key)
    {
        var parts = new List<string>();
        if ((modifiers & Ctrl) != 0) parts.Add("Ctrl");
        if ((modifiers & Alt) != 0) parts.Add("Alt");
        if ((modifiers & Shift) != 0) parts.Add("Shift");
        if ((modifiers & Win) != 0) parts.Add("Win");
        parts.Add(key.ToUpperInvariant());
        var text = string.Join("+", parts);
        return TryParse(text, out _, out _) ? text : null;
    }
}
