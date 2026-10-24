<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="branding/logo-dark.png">
    <img src="branding/logo-light.png" alt="Squeaker" width="520">
  </picture>
</p>

<p align="center">The mouse hides while you type.</p>

---

Squeaker is a Windows tray app in the spirit of PowerToys' CursorHide. Start typing and the
cursor gets out of the way; touch the mouse and it's back.

- **Hide while typing.** Shift, Ctrl, Alt and Win on their own don't count, and nor do keys sent
  by other programs (AutoHotkey, remote tools, on-screen keyboards). Shortcuts like Ctrl+C and
  Alt+Tab leave it alone too, unless you turn that off.
- **Move it to where you're typing** (optional): when typing hides it, it's parked just right
  of the text cursor, so it comes back there instead of wherever you left it. Works in classic
  apps and Chromium ones (VS Code, browsers); apps that don't say where their text cursor is are
  left alone.
- **Hide when idle** (optional): hides after the mouse hasn't moved for 1–60 seconds.
- **Comes back** when you move the mouse further than a couple of pixels, click or scroll; or,
  optionally, a few seconds after you stop typing.
- **Steps aside for games.** While a full-screen app is in front (a game, exclusive or
  borderless, or a full-screen video), Squeaker lets go of the keyboard and mouse completely: no
  hooks, so nothing added to mouse latency and no flickering cursor. It checks twice a second.
- **Excluded apps:** it steps aside the same way for these. Add windowed games here.
- **Pause hotkey:** Ctrl+Alt+Shift+M pauses and resumes it; change it in Settings.
- **Never gets stuck hidden.** A small watchdog copy of Squeaker waits for it to exit, however it
  goes (a crash, End task), and puts the cursors back.

## Updates

Squeaker looks for a new release on GitHub 30 seconds after it starts and once a day after that
(turn it off in Settings), or right away with **Check for updates...** in the tray menu. It always
asks first. Say yes and it downloads the release, checks it against GitHub's SHA-256 checksum,
exits, and `SqueakerUpdater.exe` swaps the files and starts it again. The previous version is
kept in a `backup` folder next to it. Pre-releases (`v1.2.3-test`) are never offered, and
development builds (version `0.0.0-dev`, like the ones `publish.cmd` makes) never update.

## How it hides the cursor

The same way PowerToys does: it swaps every system cursor for a see-through one with
`SetSystemCursor`, and puts them back by reloading your cursor scheme
(`SystemParametersInfo(SPI_SETCURSORS)`). The keyboard and mouse hooks run on a thread of their
own that does nothing else, so typing never waits on the UI.

## Tray menu

Click the icon for Settings. Right-click for Settings, Pause (the icon goes grey), Start with
Windows, Open settings folder, Check for updates and Exit. Hover over it for the version.

## Files

Everything lives in `%AppData%\Squeaker`:

| File | What |
| --- | --- |
| `settings.json` | Options. Safe to hand-edit; restart Squeaker afterwards. |
| `squeaker.log` | Starts, pauses, when it steps aside and for what, and anything that went wrong. |

## Building

Needs the .NET 10 SDK. In VS Code press F5, or:

```
dotnet run --project src/Squeaker
dotnet test
```

`publish.cmd` makes a self-contained release build.

GitHub Actions builds and tests every push to `main` and every pull request, and keeps the zip
as a build artifact. Push a tag like `v1.2.3` to turn it into a release (`v1.2.3-test` makes a
pre-release).

The icon and logos are drawn by `branding/make_branding.py` (needs Pillow):
`python branding/make_branding.py`.

## Known limits

- Don't run it alongside PowerToys' CursorHide or the old zuulMouse; they'd fight over the cursor.
- Windows' own *Hide pointer while typing* (Mouse Properties > Pointer Options) overrides some
  options in older apps like Notepad. If it's on, Settings says so and can turn it off.
- Squeaker runs as a normal user, and Windows doesn't let it see input going to apps running as
  administrator. Typing in one won't hide the cursor, and clicking in one won't bring it back;
  moving the mouse always does (Squeaker checks where the cursor is every 100ms as well).
