# Controller Tuner

Windows app that sits between your physical controller and a ViGEm virtual Xbox 360 pad,
with GameSir-style tuning. Games see only the tuned virtual pad.

## Features
- ViGEmBus status light (missing / outdated / blocked / OK) and virtual pad slot detection
- Per-stick inner/outer dead zone, anti dead zone, response curve, sensitivity, invert
- Triggers: activation point, trigger stop (max), sensitivity, hair trigger (digital)
- Button remap / disable, per-button turbo with adjustable speed
- Macros: build sequences by typing steps or recording from your controller; trigger on press, while held, or as a toggle
- Rumble pass-through from the game to your real pad, with strength slider
- Profiles: duplicate, rename, delete; Back + LB / RB cycles profiles (hidden from the game)
- Auto-switch profile when a game's process is focused (e.g. `cod`)
- Bypass checkbox for raw pass-through

## Requirements
- Windows 10/11
- [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases) installed
- Optional: HidHide, so games see only the virtual pad

## Build
GitHub: push to `main` and download `ControllerTuner-win-x64` from the Actions run.
Push a tag like `v0.1.0` to get the exe attached to a Release.

Local: `dotnet run --project src/ControllerTuner` (needs the .NET 8 SDK).

Profiles are saved in `%AppData%\ControllerTuner\profiles.json`.
