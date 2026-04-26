# SE Grid Manager

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Overview

SE Grid Manager is a *Space Engineers* plugin stack for **server-side grid management** (Torch and dedicated). Players interact through **ModAPI secure messaging**; the former **ClientPlugin** (WPF in-game UI) has been **removed** from this repository. A **scripted Workshop / local mod** skeleton lives under `WorkshopMod/SEGridManagerClient` and is intended to carry the in-game client experience forward.

## Features

- **Client experience**: Local or Workshop **scripted mod** (`WorkshopMod/SEGridManagerClient`) using the same message IDs as the Torch plugin (see that folder’s README).
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

### 4. Client scripted mod (`WorkshopMod/SEGridManagerClient`)

- **Local / Workshop** mod layout: `Data/Scripts/SEGridManagerClient/` (see [Mod Scripting](https://spaceengineers.wiki.gg/wiki/Modding/Reference/ModScripting)).
- Copy the `SEGridManagerClient` folder into `%AppData%\SpaceEngineers\Mods\` and enable it on the client. Details: `WorkshopMod/SEGridManagerClient/README.md`.

## Requirements

### Development environment

- **Visual Studio 2019 or later** (or VS Code with the C# extension).
- **.NET Framework 4.8** (or 4.8.1 if your SDK provides reference assemblies for it).
- **Space Engineers `Bin64`** (optional; useful if you extend the scripted mod with additional game references).
- **Space Engineers Dedicated Server** (`DedicatedServer64`) for dedicated plugin references.
- **Torch Server** (optional) for Torch plugin references.

### Game dependencies

- **Space Engineers** (current branch used by your server).
- **Harmony 2.3.3** (NuGet).
- **Newtonsoft.Json** (as shipped with the game / Torch stack).

## Installation

### Initial setup

This solution expects paths in `Directory.Build.props` to point at your installs.

#### 1. Clone the repository

```bash
git clone https://github.com/snowmuffin/SE_Grid_Manager.git
cd SE_Grid_Manager
```

#### 2. Configure `Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <Bin64>C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64</Bin64>
    <Dedicated64>C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineersDedicatedServer\DedicatedServer64</Dedicated64>
    <Torch>C:\TorchServer</Torch>
  </PropertyGroup>
</Project>
```

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

Copy `WorkshopMod\SEGridManagerClient` (the folder that contains `Data`) to `%AppData%\SpaceEngineers\Mods\`, then enable the mod in the game. The server must run the Gridmanager Torch (or compatible) plugin.

### Torch server plugin

1. Build **TorchPlugin**.
2. Deploy to Torch `Plugins\` (e.g. `Gridmanager.dll`, `manifest.xml` per your Torch layout).

### Dedicated server plugin

1. Build **DedicatedPlugin**.
2. Copy the plugin DLL to the dedicated server’s plugins folder.

## Usage

### Client (scripted mod)

Enable **SEGridManagerClient** in the world’s mod list. Requests and replies use secure message IDs aligned with `TorchPlugin/Plugin.cs` (see `WorkshopMod/SEGridManagerClient`). In-game UI beyond logging is still to be implemented there.

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
├── WorkshopMod/SEGridManagerClient/   # Scripted client mod (ModAPI)
├── Directory.Build.props
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
   Ensure the **server plugin** is loaded and the **client mod** is enabled; check firewalls and that message IDs have not diverged between mod and plugin.

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
