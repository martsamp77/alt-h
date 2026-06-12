# Repository Guidelines

## Project Structure & Module Organization

This repository contains a small Windows tray utility built with .NET and Windows Forms.

- `AltHMinimize.csproj` defines a WinExe targeting `net10.0-windows` with WinForms, nullable references, implicit usings, and unsafe blocks enabled.
- `Program.cs` contains the app entry point, tray context (`TrayApplicationContext`), and the global hotkey window (`HotKeyWindow`).
- `NativeMethods.cs` isolates the Win32 P/Invoke surface (hotkey, window, low-level mouse hook, `SendInput`) and the interop structs.
- `MouseHook.cs` owns the low-level mouse hook (`WH_MOUSE_LL`), its suppression logic, the message-only dispatcher, and the `SideButton` enum.
- `AppSettings.cs` reads and writes the mouse-action preferences under `HKCU\Software\AltHMinimize`.
- `app.manifest` sets execution privileges to `asInvoker`.
- `README.md` documents build and usage basics.
- `bin/` and `obj/` are generated outputs. Do not edit them directly.

Logic is split across focused top-level files (no `src/` or `tests/` directory yet); `Assets/` holds the embedded icon. Keep interop in `NativeMethods`, and continue adding focused classes before introducing broader folder structure.

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

Run `dotnet test AltHMinimize.slnx` first; `AltHMinimize.Tests` covers the pure decision logic
(hook suppression, window filtering, settings parsing/migration). Interop and tray behavior
still need manual verification:

- `Alt+H` minimizes the focused non-shell window.
- The tray menu can enable/disable the hotkey.
- Middle-click sends `Ctrl+W` when enabled, and the original middle-click is suppressed; disabling it restores native middle-click.
- The selected side button (Back `XBUTTON1` / Forward `XBUTTON2`) minimizes the focused window and is suppressed; `Off` restores native navigation.
- Mouse-action choices persist under `HKCU\Software\AltHMinimize` across restarts, and the mouse hook installs only while an action is enabled.
- Startup registration toggles the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value.
- Exit unregisters the hotkey, removes the mouse hook, and removes the tray icon.

Keep pure logic testable without a desktop session: decision functions live in `HookDecision`,
`WindowFilter`, and `AppSettings` parse helpers — extend those rather than embedding logic in
interop callbacks.

## Commit & Pull Request Guidelines

This checkout has no Git history, so no repository-specific convention can be inferred. Use short, imperative subjects such as `Add startup toggle error handling`.

Pull requests should include a summary, manual verification steps, affected Windows versions when relevant, and screenshots only for tray UI or notification changes.

## Security & Configuration Tips

Keep the app running as the current user. Do not request elevation in `app.manifest` unless required. Scope startup registry writes to `CurrentUser`, preserve executable path quoting, and report failures through tray notifications.
