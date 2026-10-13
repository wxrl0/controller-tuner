# Controller Tuner

Windows app that sits between your physical controller and a ViGEm virtual Xbox 360 pad.
It applies per-profile dead zones, response curves, sensitivity, trigger tuning and
stick swap/invert, and shows whether the ViGEmBus driver is available.

## Requirements
- Windows 10/11
- [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases) installed
- Optional: HidHide, so games see only the virtual pad

## Build
GitHub: push to `main` and download `ControllerTuner-win-x64` from the Actions run.
Push a tag like `v0.1.0` to get the exe attached to a Release.

Local: `dotnet run --project src/ControllerTuner` (needs the .NET 8 SDK).

Profiles are saved in `%AppData%\ControllerTuner\profiles.json`.
