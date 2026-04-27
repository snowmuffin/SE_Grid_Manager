# SE Grid Manager

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Overview

SE Grid Manager is a *Space Engineers* plugin stack for **server-side grid management** (Torch and dedicated). Players interact through **ModAPI secure messaging**; the former **ClientPlugin** (WPF in-game UI) has been **removed** from this repository. The **Workshop / local client mod** lives under [`WorkshopMod/SEGridManagerClient`](WorkshopMod/SEGridManagerClient): **`Data/Scripts`** (in-game compiled; **Rich HUD Framework** terminal when [Rich HUD Master (1965654081)](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081) is enabled, otherwise mission / notification + hotkeys). An optional **`SEGridManagerClient` C# project** can build a MyGui **DLL** for special setups — **vanilla does not depend on** that DLL; see **LOADING.md** there.

## Features

- **Client experience**: Local or Workshop **scripted mod** (`Data/Scripts/SEGridManagerClient/` including nested `RichHudFramework/`) and same message IDs as the Torch plugin (see `WorkshopMod/SEGridManagerClient/README.md`). For the **Rich HUD** terminal UI, players also subscribe to **[Rich HUD Master (1965654081)](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081)** and load it before this mod.
- **Server-side grid management**: Torch and dedicated server builds.
- **Block-level operations**: View and delete flows via server APIs and messaging (contract in `TorchPlugin/Plugin.cs`).
- **Multi-platform support**: Dedicated and Torch deployments.
- **HTTP API integration** (Torch): REST endpoints for external tools.
- **Real-time communication**: ModAPI secure messages between client mod and server plugin.
- **Owner-based filtering**: Server enforces ownership rules for sensitive operations.

## Architecture

The project is organized into these parts:

### 1. TorchPlugin

- Server-side plugin for Torch.
- HTTP listener and REST API.
- Grid data, permissions, and ModAPI message handlers for client requests.

### 2. DedicatedPlugin

- Lightweight server plugin for non-Torch dedicated hosts.
- Core grid management without Torch-specific features.

### 3. Shared

- Common code for plugins.
- Configuration, logging, Harmony helpers.

### 4. Client mod (`WorkshopMod/SEGridManagerClient`)

- **Local / Workshop** mod: `Data/Scripts/SEGridManagerClient/*.cs` + `Data/BlockCategories/BlockCategories.sbc` + `metadata.mod`. Copy the folder to `%AppData%\SpaceEngineers\Mods\` — **remove any stale `SEGridManagerClient.dll`** in that folder if hotkeys do nothing. Optional VS project `SEGridManagerClient/SEGridManagerClient.csproj` builds a MyGui DLL (not used by default). See [`LOADING.md`](WorkshopMod/SEGridManagerClient/LOADING.md).

## Requirements

### Development environment

- **Visual Studio 2019 or later** (or VS Code with the C# extension).
- **.NET Framework 4.8** (or 4.8.1 if your SDK provides reference assemblies for it).
- **Space Engineers `Bin64`** (required in `Directory.Build.props` if you build the optional **SEGridManagerClient** DLL project).
- **Space Engineers Dedicated Server** (`DedicatedServer64`) for dedicated plugin references.
- **Torch Server** (optional) for Torch plugin references.

### Game dependencies

- **Space Engineers** (current branch used by your server).
- **[Rich HUD Master (Workshop 1965654081)](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081)** — **for the client mod’s Rich HUD terminal** (optional: without it, the script mod still offers mission + `/gmg` fallback).
- **Harmony 2.3.3** (NuGet).
- **Newtonsoft.Json** (as shipped with the game / Torch stack).

## Installation

### Initial setup

Copy `Directory.Build.props.example` to `Directory.Build.props` and set paths to your local game and Torch installs. The `Directory.Build.props` file is not committed to avoid machine-specific paths.

#### 1. Clone the repository

```bash
git clone https://github.com/snowmuffin/SE_Grid_Manager.git
cd SE_Grid_Manager
```

#### 2. Configure `Directory.Build.props`

In the repository root, copy the template and adjust paths as needed.

```bash
cp Directory.Build.props.example Directory.Build.props
# On Windows: copy Directory.Build.props.example Directory.Build.props
```

Edit `Directory.Build.props` so `Bin64`, `Dedicated64`, and `Torch` match your installs. The same content is in `Directory.Build.props.example` as a reference.

#### 3. Manual assembly setup

##### TorchPlugin

Verify Torch assemblies under `$(Torch)` (typical names include):

- `Torch.dll`
- `Torch.API.dll`
- `Torch.Server.exe`

##### DedicatedPlugin

Verify dedicated server assemblies under `$(Dedicated64)` as referenced by the project.

#### 4. Build the solution

```bash
msbuild Gridmanager.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

Pre-build runs `verify_props.bat` to validate paths.

### Workshop client mod (local)

Copy `WorkshopMod\SEGridManagerClient` to `%AppData%\SpaceEngineers\Mods\` and enable the mod. **No build step** is required for the scripted mod. If a previous test left **`SEGridManagerClient.dll`** in that folder, delete it. The server must run the Gridmanager Torch (or compatible) plugin. See `WorkshopMod/SEGridManagerClient/LOADING.md`.

### Torch server plugin

1. Build **TorchPlugin**.
2. Deploy to Torch `Plugins\` (e.g. `Gridmanager.dll`, `manifest.xml` per your Torch layout).

### Dedicated server plugin

1. Build **DedicatedPlugin**.
2. Copy the plugin DLL to the dedicated server’s plugins folder.

## Usage

### Client (scripted mod)

Enable **SEGridManagerClient** in the world’s mod list; the mod folder must contain **`Data/Scripts/SEGridManagerClient/`** with the `.cs` files. **Ctrl+Shift+G** or **Alt+Shift+G** requests grid data (mission / notifications per `GridManagerUiSession`). Protocol matches `TorchPlugin/Plugin.cs`.

### Server configuration (Torch)

Typical options include:

- **Enable HTTP Listener**: Turn the REST API on or off.
- **HTTP Port**: Listener port (commonly `8080`).
- **Web Host Address**: Base address for callbacks / notifications.

## Development notes

### Repository layout

```
SE_Grid_Manager/
├── TorchPlugin/
├── DedicatedPlugin/
├── Shared/
├── WorkshopMod/SEGridManagerClient/   # Client mod: Data/Scripts + optional MyGui csproj
├── WorkshopMod/SteamUpload/           # SteamCMD publish scripts (not part of the mod files)
├── Directory.Build.props.example     # copy → Directory.Build.props (local, gitignored)
├── Gridmanager.sln
├── verify_props.bat
└── setup.py
```

### Build events

- **Pre-build**: `verify_props.bat` checks that configured paths exist.
- **Post-build**: Projects may copy outputs to local game/Torch folders (see each `.csproj`).

## Troubleshooting

1. **Missing references**  
   Fix paths in `Directory.Build.props` and confirm game/Torch installs match the targeted build.

2. **Plugin not loading**  
   Confirm DLL and `manifest.xml` locations; read Torch / dedicated logs.

3. **Client–server messaging**  
   Ensure the **server plugin** is loaded and the **client mod** is enabled (`Data/Scripts` present). If the hotkey does nothing, remove any stray **`SEGridManagerClient.dll`** in the mod folder and read [`WorkshopMod/SEGridManagerClient/LOADING.md`](WorkshopMod/SEGridManagerClient/LOADING.md). Check firewalls and message IDs.

### Logging

- Game client: `%AppData%\SpaceEngineers\Logs\`
- Dedicated / Torch: your server’s log directory

## Contributing

1. Fork the repository.  
2. Create a branch for your change.  
3. Submit a pull request with a clear description.

## License

This project is licensed under the MIT License; see [LICENSE](LICENSE).

---

Questions and issues: [GitHub Issues](https://github.com/snowmuffin/SE_Grid_Manager/issues).
