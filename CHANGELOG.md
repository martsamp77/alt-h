# Changelog

All notable changes to this project will be documented in this file.

This project uses Semantic Versioning: `MAJOR.MINOR.PATCH`.

## [Unreleased]

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
