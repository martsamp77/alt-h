# Alt-H Minimize

A small Windows tray utility that registers `Alt+H` as a global hotkey and minimizes the currently focused window.

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

You can also download the MSI from the GitHub Releases page. Release artifacts are not code-signed yet, so Windows SmartScreen may show a warning.

## Use

Run `AltHMinimize.exe`. It appears in the system tray with menu options to enable or disable `Alt+H`, add or remove itself from Windows startup, and exit.

## Project Notes

- The app uses a custom Windows-native icon for the executable and system tray.
- Releases follow Semantic Versioning: `MAJOR.MINOR.PATCH`.
- Release assets are built with `scripts\package-release.ps1`.

## License

MIT. See [LICENSE](LICENSE).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
