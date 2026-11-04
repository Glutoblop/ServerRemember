# Server Remember

![version](https://img.shields.io/badge/version-0.1.0-blue) ![valheim](https://img.shields.io/badge/Valheim-1.0.21-orange) ![client](https://img.shields.io/badge/client--side-only-green)

A BepInEx plugin for Valheim.

Stop retyping server passwords. Server Remember saves the last password that successfully got you into each server, and adds a small key button to the right of the password prompt that fills it in for you.

## Features
- Saves a password only after the server accepts it, so typos are never remembered.
- Per server: dedicated servers by address and port, Steam/PlayFab hosts by their id.
- The key button is always there; it's greyed out until you have a saved password.
- Falls back to the last password that worked anywhere if this server has none saved.
- Client-side only: works on vanilla servers. Nothing extra is sent to the server.

## How passwords are stored
Passwords are **encrypted, not hashed**: a hash can't be turned back into the password, so it couldn't be filled in for you.

- **Windows:** encrypted with Windows DPAPI tied to your Windows user account. Only you, logged into this PC, can decrypt them.
- **Linux/macOS:** encrypted with AES using a key derived from your user and machine name. That stops casual reading of the file, but anyone with access to your account could decrypt it.

They live in `BepInEx/config/jordan.valheim.serverremember.passwords`. Delete the file to forget every password.

## Configuration (`BepInEx/config/jordan.valheim.serverremember.cfg`)
| Section | Key | Default | What it does |
|---|---|---|---|
| General | Enabled | true | Save passwords and show the button. |
| Layout | ButtonGap | 6 | Gap between the password box and the key button. |

## Install

### Easiest: Valheim Mod Installer
Download the [**Valheim Mod Installer**](https://github.com/Glutoblop/ValheimPlugins) and run it (`Start-Installer.bat` on Windows, `start-installer.sh` on Linux/macOS), point it at your Valheim folder, tick **Server Remember** and click **Install / Update**. It installs BepInEx for you if needed.

### Manual
Download from the [latest release](https://github.com/Glutoblop/ServerRemember/releases/latest) ([`ServerRemember.zip`](https://github.com/Glutoblop/ServerRemember/releases/latest/download/ServerRemember.zip): unzip into your Valheim folder), or:

1. Install [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (5.4.2360 or newer) into your Valheim folder.
2. Download [`dist/ServerRemember.dll`](https://github.com/Glutoblop/ServerRemember/raw/main/dist/ServerRemember.dll).
3. Copy it to `<Valheim>/BepInEx/plugins/ServerRemember/ServerRemember.dll`.
4. Launch the game once; settings are written to `BepInEx/config/jordan.valheim.serverremember.cfg`.

## Compatibility

| | |
|---|---|
| Plugin version | **0.1.0** |
| Last tested on Valheim | **1.0.21** (network version 41, Steam build 26249040) on 2026-11-03 |
| BepInExPack_Valheim | 5.4.2360 |
| Multiplayer | Client-side only: works on vanilla servers, other players don't need it |

Machine-readable versions live in [`plugin.json`](plugin.json). The installer reads it straight from `main` to tell users when an update is available, or when the mod hasn't been tested against their game version.

## Building

Requires the .NET SDK (8+) and a Valheim install with BepInEx.

```
dotnet build -c Release -p:GameDir="C:\Path\To\Valheim"
```

The build copies the DLL into `<GameDir>/BepInEx/plugins/ServerRemember` for testing, and into `dist/` (which is committed, and is what the installer downloads).

## Releasing an update

Run `release\Release.bat` (or `powershell -File release\release.ps1 [-Bump patch|minor|major] [-Changes "text"]`). It checks the repo is pushed, bumps the version in `plugin.json` and `src/Plugin.cs`, rebuilds, commits, pushes and publishes the GitHub release with the zip. Use `-BuildOnly` to just build the package.
