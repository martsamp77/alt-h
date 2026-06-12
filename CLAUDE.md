# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Repository structure, coding style, manual-testing checklist, and security conventions are in @AGENTS.md — follow them.

## Branch & release workflow

- Work on the `develop` branch; merge to `main` via pull request. A merge to `main` cuts a release.
- Releases are manual: run `scripts\package-release.ps1 -Version "<version>"` (run `dotnet tool restore` first to get WiX 7), then upload the artifacts from `artifacts\release\v<version>\` with `gh release create`. Artifacts: standalone `.exe`, `.msi`, `install.ps1`, `SHA256SUMS.txt`.
- Keep the version in `AltHMinimize.csproj`, `CHANGELOG.md`, and the release tag in sync.

## Gotchas

- The .NET 10 SDK is a per-user install at `%LOCALAPPDATA%\Microsoft\dotnet` and may not be on PATH in non-interactive shells. If `dotnet` isn't found, use `& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"` (and set `DOTNET_ROOT` to that directory).
- The low-level mouse hook callback must not do real work: it suppresses synchronously, then posts `WM_APP_MIDDLE`/`WM_APP_SIDE` to the message-only window so the action runs from `WndProc` — staying under Windows' `LowLevelHooksTimeout` (~300 ms). Don't move logic into the callback.
- `SetWindowsHookExW` in `NativeMethods.cs` is intentionally a classic `[DllImport]` (not `LibraryImport`) because the source generator can't marshal the hook callback delegate. Don't "modernize" it.
- The app runs `asInvoker`; elevated windows are unaffected by the hotkey, hook, and synthesized input. This is by design — never add elevation.
- No automated tests exist; verification is `dotnet build` plus the manual checklist in AGENTS.md.
