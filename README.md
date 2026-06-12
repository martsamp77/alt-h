# Alt-H Minimize

A small, low-overhead Windows tray utility for window and mouse-button shortcuts. It:

- Registers `Alt+H` as a global hotkey that minimizes the currently focused window.
- Can minimize the focused window from a **thumb/side mouse button** (Back or Forward).
- Can remap the **scroll-wheel click (middle button)** to `Ctrl+W` to close the active tab/document.

It is built as a lightweight alternative to heavyweight vendor mouse software (e.g. Logitech
Options+) for these specific actions. The mouse buttons are read through a global low-level
mouse hook using each button's default Windows mapping, so vendor software is not required —
and should not be running, since it would intercept the buttons first.

## Build

```powershell
dotnet build
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The published executable is written to:

```text
bin\Release\net10.0-windows\win-x64\publish\AltHMinimize.exe
```

## Install

Install the latest release for the current user:

```powershell
irm https://raw.githubusercontent.com/martsamp77/alt-h/main/install.ps1 | iex
```

The installer writes `AltHMinimize.exe` to:

```text
%LOCALAPPDATA%\Programs\AltHMinimize
```

It enables startup by default. To install without startup or without launching immediately, run:

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/martsamp77/alt-h/main/install.ps1))) -NoStartup -NoRun
```

To uninstall:

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/martsamp77/alt-h/main/install.ps1))) -Uninstall
```

You can also download the MSI from the GitHub Releases page.

### SmartScreen warning

Release artifacts are not code-signed, so Windows SmartScreen may warn when you run a downloaded
executable. Click **More info → Run anyway** to proceed. To verify a download, compare its hash
against `SHA256SUMS.txt` from the same release:

```powershell
Get-FileHash .\AltHMinimize-v0.3.0-win-x64.exe -Algorithm SHA256
```

## Use

Run `AltHMinimize.exe`. It appears in the system tray with menu options to:

- **Alt+H Enabled** — toggle the global `Alt+H` minimize hotkey.
- **Middle-Click Closes Tab (Ctrl+W)** — toggle remapping the scroll-wheel click to `Ctrl+W` (on by default).
- **Side Button Minimizes** — choose which thumb/side button minimizes the focused window: `Off`, `Back button (XBUTTON1)`, or `Forward button (XBUTTON2)` (Forward by default).
- **Start with Windows** — add or remove the per-user startup entry.
- **Exit**.

The mouse-action choices are saved under `HKCU\Software\AltHMinimize` and restored on the next launch. The low-level mouse hook is only installed while at least one mouse action is enabled.

### Notes and limitations

- **Middle-click is remapped globally.** While enabled, the scroll-wheel click sends `Ctrl+W` in every application, replacing normal middle-click (autoscroll, open-link-in-new-tab). Turn it off in the tray if you want native middle-click back.
- **Quit vendor mouse software.** Logitech Options+ (and similar) intercept these buttons before Windows sees them. The mode-shift button directly behind the scroll wheel is *not* usable here — it sends no standard input Windows can read, only Back/Forward/middle do.
- **Elevated windows.** The app runs as the current user (`asInvoker`). Its hook and synthesized `Ctrl+W` will not affect a window running at a higher integrity level (e.g. an app launched as administrator) while that window is focused.
- **Security software.** A global hook plus synthesized input can occasionally trip heuristic antivirus or game anti-cheat. This is expected for input-remapping tools.

## Project Notes

- The app uses a custom Windows-native icon for the executable and system tray.
- Releases follow Semantic Versioning: `MAJOR.MINOR.PATCH`.
- Release assets are built with `scripts\package-release.ps1`. Pushing a `v*` tag builds them on
  CI and creates a draft GitHub release for review.

## License

MIT. See [LICENSE](LICENSE).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
