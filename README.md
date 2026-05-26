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

## Use

Run `AltHMinimize.exe`. It appears in the system tray with menu options to enable or disable `Alt+H`, add or remove itself from Windows startup, and exit.
