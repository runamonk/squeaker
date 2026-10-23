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
- **Hide when idle** (optional): hides after the mouse hasn't moved for 1–60 seconds.
- **Comes back** when you move the mouse further than a couple of pixels, click or scroll; or,
  optionally, a few seconds after you stop typing.
- **Excluded apps:** never hides while one of them is in front.
- **Never gets stuck hidden.** A small watchdog copy of Squeaker waits for it to exit, however it
  goes (a crash, End task), and puts the cursors back.

## How it hides the cursor

The same way PowerToys does: it swaps every system cursor for a see-through one with
`SetSystemCursor`, and puts them back by reloading your cursor scheme
(`SystemParametersInfo(SPI_SETCURSORS)`). The keyboard and mouse hooks run on a thread of their
own that does nothing else, so typing never waits on the UI.

## Tray menu

Click the icon for Settings. Right-click for Settings, Pause (the icon goes grey), Start with
Windows, Open settings folder and Exit.

## Files

Everything lives in `%AppData%\Squeaker`:

| File | What |
| --- | --- |
| `settings.json` | Options. Safe to hand-edit; restart Squeaker afterwards. |
| `squeaker.log` | Starts, and anything that went wrong. |

## Building

Needs the .NET 10 SDK. In VS Code press F5, or:

```
dotnet run --project src/Squeaker
dotnet test
```

`publish.cmd` makes a self-contained release build.

The icon and logos are drawn by `branding/make_branding.py` (needs Pillow):
`python branding/make_branding.py`.

## Known limits

- Don't run it alongside PowerToys' CursorHide or the old zuulMouse; they'd fight over the cursor.
- Windows' own *Hide pointer while typing* (Mouse Properties > Pointer Options) overrides some
  options in older apps like Notepad. If it's on, Settings says so and can turn it off.
- Squeaker runs as a normal user, so typing in an app running as administrator may not hide the
  cursor.
