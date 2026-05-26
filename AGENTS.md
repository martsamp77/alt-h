# Repository Guidelines

## Project Structure & Module Organization

This repository contains a small Windows tray utility built with .NET and Windows Forms.

- `AltHMinimize.csproj` defines a WinExe targeting `net10.0-windows` with WinForms, nullable references, implicit usings, and unsafe blocks enabled.
- `Program.cs` contains the app entry point, tray context, global hotkey window, and Win32 interop helpers.
- `app.manifest` sets execution privileges to `asInvoker`.
- `README.md` documents build and usage basics.
- `bin/` and `obj/` are generated outputs. Do not edit them directly.

There is no separate `src/`, `tests/`, or assets directory. If the app grows, move reusable logic into focused classes before adding broad structure.

## Build, Test, and Development Commands

```powershell
dotnet build
```

Builds the app in the default Debug configuration.

```powershell
dotnet run
```

Runs the tray app locally. Use an interactive Windows desktop session.

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Creates `bin\Release\net10.0-windows\win-x64\publish\AltHMinimize.exe`.

## Coding Style & Naming Conventions

Use standard C# conventions: four-space indentation, file-scoped namespaces, `PascalCase` for types and methods, `camelCase` for locals and parameters, and `_camelCase` for private fields. Keep nullable annotations meaningful.

Keep interop code isolated in helper types such as `NativeMethods`. Prefer named constants for Win32 values, and keep tray UI text short.

## Testing Guidelines

No automated test project exists yet. For current changes, run `dotnet build` and manually verify:

- `Alt+H` minimizes the focused non-shell window.
- The tray menu can enable/disable the hotkey.
- Startup registration toggles the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value.
- Exit unregisters the hotkey and removes the tray icon.

If tests are added, create `AltHMinimize.Tests` and keep pure logic testable without a desktop session.

## Commit & Pull Request Guidelines

This checkout has no Git history, so no repository-specific convention can be inferred. Use short, imperative subjects such as `Add startup toggle error handling`.

Pull requests should include a summary, manual verification steps, affected Windows versions when relevant, and screenshots only for tray UI or notification changes.

## Security & Configuration Tips

Keep the app running as the current user. Do not request elevation in `app.manifest` unless required. Scope startup registry writes to `CurrentUser`, preserve executable path quoting, and report failures through tray notifications.
