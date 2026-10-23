using System.Text.Json;

namespace Squeaker.Core;

public sealed class SqueakerSettings
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Same limits PowerToys puts on its idle delay.
    public const int MinIdleSeconds = 1;
    public const int MaxIdleSeconds = 60;

    // Hide the cursor as soon as you start typing.
    public bool HideOnTyping { get; set; } = true;
    // Hide the cursor when the mouse hasn't been touched for IdleSeconds.
    public bool HideOnIdle { get; set; }
    public int IdleSeconds { get; set; } = 5;
    // Keys pressed with Ctrl, Alt or Win held (shortcuts, Alt+Tab) leave the cursor alone.
    public bool IgnoreShortcuts { get; set; } = true;
    // How far (pixels) the mouse has to move to bring the cursor back, so a bumped desk doesn't.
    public int MoveThreshold { get; set; } = 2;
    // Bring the cursor back this long after the last key, without touching the mouse. 0: only the mouse brings it back.
    public int ShowAfterTypingSeconds { get; set; }
    // Process names Squeaker steps aside for, e.g. "mstsc" or "game.exe".
    public List<string> ExcludedApps { get; set; } = [];
    // Step aside (unhook entirely) while a full-screen app, like a game or a video, is in front.
    public bool StepAsideForFullScreen { get; set; } = true;
    // Pauses and resumes Squeaker, e.g. "Ctrl+Alt+Shift+M". Empty: no hotkey.
    public string PauseHotkey { get; set; } = "Ctrl+Alt+Shift+M";

    public bool IsExcluded(string processName)
    {
        static string Trim(string name) => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        var name = Trim(processName);
        return ExcludedApps.Any(e => string.Equals(Trim(e), name, StringComparison.OrdinalIgnoreCase));
    }

    // Pulls hand-edited numbers back into range.
    public void Normalize()
    {
        ExcludedApps ??= [];
        if (!HotkeyText.TryParse(PauseHotkey, out _, out _)) PauseHotkey = "";
        IdleSeconds = Math.Clamp(IdleSeconds, MinIdleSeconds, MaxIdleSeconds);
        MoveThreshold = Math.Clamp(MoveThreshold, 1, 100);
        ShowAfterTypingSeconds = Math.Clamp(ShowAfterTypingSeconds, 0, 600);
    }

    public SqueakerSettings Clone()
    {
        var copy = (SqueakerSettings)MemberwiseClone();
        copy.ExcludedApps = [.. ExcludedApps];
        return copy;
    }

    // Missing file: write defaults. Broken file: use defaults but leave the file alone so nothing is lost.
    public static SqueakerSettings Load(string path, out string? problem)
    {
        problem = null;
        if (!File.Exists(path))
        {
            var fresh = new SqueakerSettings();
            fresh.Save(path);
            return fresh;
        }

        SqueakerSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<SqueakerSettings>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException ex)
        {
            problem = $"Couldn't read settings, using defaults: {ex.Message}";
            return new SqueakerSettings();
        }

        settings ??= new SqueakerSettings();
        settings.Normalize();
        return settings;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
