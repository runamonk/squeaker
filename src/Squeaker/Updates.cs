using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Threading;
using Squeaker.Core;

namespace Squeaker;

// Looks for a newer release on GitHub, downloads it, checks it against GitHub's checksum and hands
// it to SqueakerUpdater.exe, which installs it once we've exited and starts us again. Same as
// BrainPending's, except Squeaker runs for days, so it looks once a day rather than only at start.
sealed class Updates
{
    public static readonly string AppVersion =
        (typeof(Updates).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0-dev").Split('+')[0];

    const string LatestReleaseUrl = "https://api.github.com/repos/runamonk/Squeaker/releases/latest";
    const string UpdaterName = "SqueakerUpdater.exe";
    static readonly string UpdateFolder = Path.Combine(Path.GetTempPath(), "Squeaker-update");
    // Not straight away at sign-in, when everything else is starting too.
    static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(30);
    static readonly TimeSpan CheckEvery = TimeSpan.FromDays(1);

    readonly Func<SqueakerSettings> _settings;
    readonly Action<string?> _status;
    readonly Action _exit;
    readonly DispatcherTimer _timer;
    // Said "Not now" to; the daily check doesn't ask about it again. The menu still does.
    Version? _declined;
    bool _checking;

    // status: shown while downloading, null when done. exit: quits Squeaker so the update can go in.
    public Updates(Func<SqueakerSettings> settings, Action<string?> status, Action exit)
    {
        _settings = settings;
        _status = status;
        _exit = exit;

        // Left over from the last update. The updater may still be exiting, so this is best effort.
        try
        {
            if (Directory.Exists(UpdateFolder)) Directory.Delete(UpdateFolder, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        _timer = new DispatcherTimer { Interval = FirstCheck };
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = CheckEvery;
            if (_settings().CheckForUpdates) await Check(quiet: true);
        };
        _timer.Start();
    }

    // Quiet: only speaks up when there's an update. Problems go to the log instead.
    public async Task Check(bool quiet)
    {
        if (!Version.TryParse(AppVersion, out var current))
        {
            if (!quiet) await Dialog.Show("Check for updates", $"This is a development build ({AppVersion}), so it doesn't update itself.");
            return;
        }
        if (_checking) return;
        _checking = true;
        try
        {
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Squeaker/" + AppVersion);
            using var check = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var release = JsonDocument.Parse(await http.GetStringAsync(LatestReleaseUrl, check.Token));
            var tag = release.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v'), out var latest))
                throw new IOException($"The latest release has an unexpected version ({tag}).");
            if (latest <= current)
            {
                if (!quiet) await Dialog.Show("Check for updates", $"You're on the latest version ({AppVersion}).");
                return;
            }
            if (quiet && latest == _declined) return;

            var asset = release.RootElement.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(a => a.GetProperty("name").GetString()?.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase) == true);
            if (asset.ValueKind == JsonValueKind.Undefined) throw new IOException($"Version {latest} has no Windows download.");
            var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            if (digest == null || !digest.StartsWith("sha256:", StringComparison.Ordinal))
                throw new IOException($"Version {latest} has no checksum, so it can't be verified.");

            if (!await Dialog.Show("Update Squeaker", $"Version {latest} is available. You have {AppVersion}.\n\nSqueaker restarts to install it, which takes a few seconds.", "Update", "Not now"))
            {
                _declined = latest;
                return;
            }

            var updater = Path.Combine(AppContext.BaseDirectory, UpdaterName);
            if (!File.Exists(updater))
                throw new IOException($"{UpdaterName} is missing, so this copy can't update itself. Download version {latest} from GitHub instead.");
            Directory.CreateDirectory(UpdateFolder);
            var zip = Path.Combine(UpdateFolder, $"Squeaker-{latest}.zip");
            await Download(http, asset.GetProperty("browser_download_url").GetString()!, zip, digest["sha256:".Length..], latest);

            // The updater runs from temp so nothing in the app folder is in use while it installs.
            var copy = Path.Combine(UpdateFolder, UpdaterName);
            File.Copy(updater, copy, true);
            Process.Start(new ProcessStartInfo(copy)
            {
                ArgumentList = { "--pid", Environment.ProcessId.ToString(), "--zip", zip,
                    "--app", Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), "--from", AppVersion },
            })?.Dispose();
            Log.Write($"Updating from {AppVersion} to {latest}.");
            _exit();
        }
        // Everything: this ends up in a tray menu click, where anything uncaught takes the app down.
        catch (Exception ex)
        {
            string message = ex is OperationCanceledException ? "GitHub didn't answer in time. Try again later." : ex.Message;
            Log.Write($"Update check failed: {message}");
            if (!quiet) await Dialog.Show("Couldn't update Squeaker", message);
        }
        finally
        {
            _checking = false;
        }
    }

    async Task Download(HttpClient http, string url, string zip, string sha256, Version version)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var output = File.Create(zip))
            {
                var buffer = new byte[81920];
                long received = 0;
                var shown = -1;
                int read;
                while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    hash.AppendData(buffer, 0, read);
                    received += read;
                    var percent = total > 0 ? (int)(received * 100 / total) : 0;
                    if (percent == shown) continue;
                    shown = percent;
                    _status($"Squeaker: downloading {version}, {percent}%");
                }
            }
            if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The download didn't match its checksum, so it was thrown away. Try again later.");
        }
        catch
        {
            try { File.Delete(zip); } catch (IOException) { }
            throw;
        }
        finally
        {
            _status(null);
        }
    }
}
