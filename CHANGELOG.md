# Changelog

All notable changes to this project will be documented in this file.

This project uses Semantic Versioning: `MAJOR.MINOR.PATCH`.

## [Unreleased]

### Added

- Added a per-app exclusion list: mouse remapping turns off automatically while an excluded app (e.g. Blender, a game) is focused. Manage it from the new "Excluded Apps" tray submenu via "Add Current App".
- Added independent actions for the Back and Forward side buttons: each can be bound to Minimize Window, Close Window, Close Tab (`Ctrl+W`), or Play/Pause Media. The previous single side-button setting migrates automatically.
- Added a "Pause All" tray item that temporarily disables the hotkey and all mouse actions at once.
- Added a one-time first-run notification explaining the default actions, including that middle-click→`Ctrl+W` is on by default.
- Added automated tests and CI; pushing a `v*` tag now builds release assets and creates a draft GitHub release.

### Fixed

- Launching a second instance now exits silently instead of installing a duplicate mouse hook (which doubled the `Ctrl+W` sent per middle click).

## [0.3.0] - 2026-06-03

### Added

- Added an optional mouse action that minimizes the focused window from a configurable thumb/side button (Back `XBUTTON1` or Forward `XBUTTON2`), defaulting to the Forward button.
- Added an optional mouse action that remaps the scroll-wheel click (middle button) to `Ctrl+W` globally, for one-click tab/document closing.
- Added tray menu controls for the two mouse actions, including a "Side Button Minimizes" submenu (Off / Back / Forward).
- Added persistence of the mouse-action preferences under `HKCU\Software\AltHMinimize`.

## [0.2.0] - 2026-05-26

### Added

- Added a custom Windows-native tray and executable icon.
- Added a per-user PowerShell installer for `irm` installs.
- Added MSI packaging for conventional Windows installs.

## [0.1.0] - 2026-05-26

### Added

- Added the initial Alt-H Minimize Windows tray utility.
- Added global `Alt+H` hotkey support for minimizing the focused non-shell window.
- Added tray menu controls for enabling and disabling the hotkey.
- Added current-user Windows startup registration controls.
- Added MIT license and repository ignore rules.
