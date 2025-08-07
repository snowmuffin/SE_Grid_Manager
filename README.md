# SE Grid Manager

A Space Engineers plugin that provides comprehensive grid management capabilities for both client and server environments. This plugin allows players to view, manage, and delete blocks from their grids through an in-game interface, with support for Torch server integration.

## Features

- **Client-side Grid Management**: In-game UI for viewing and managing player grids
- **Block-level Operations**: View and delete individual blocks from grids
- **Multi-platform Support**: Works with standalone Space Engineers, dedicated servers, and Torch servers
- **HTTP API Integration**: RESTful API endpoints for external integrations
- **Real-time Communication**: Client-server messaging for real-time grid updates
- **Owner-based Filtering**: Only shows blocks owned by the requesting player

## Architecture

The project consists of three main components:

### 1. ClientPlugin
- Provides in-game UI for players
- Handles keyboard shortcuts (Ctrl+G to open grid list)
- Communicates with server plugins via secure messaging
- Displays grid lists and detailed block information

### 2. TorchPlugin
- Server-side plugin for Torch server environments
- HTTP listener with REST API endpoints
- Manages grid data and player permissions
- Handles client requests for grid and block information

### 3. DedicatedPlugin
- Basic server-side plugin for dedicated server environments
- Lightweight implementation for non-Torch setups
- Provides core grid management functionality

### 4. Shared
- Common code shared between all plugins
- Configuration management
- Logging utilities
- Harmony patching helpers

## Requirements

### Development Environment
- **Visual Studio 2019 or later** (or VS Code with C# extension)
- **.NET Framework 4.8.1**
- **Space Engineers Game Files** (for client plugin references)
- **Space Engineers Dedicated Server** (for server plugin references)
- **Torch Server** (optional, for Torch plugin development)

### Game Dependencies
- **Space Engineers** (latest version)
- **Harmony 2.3.3** (included via NuGet)
- **Newtonsoft.Json** (included with Space Engineers)

## Initial Setup

Since this project lacks automated dependency resolution, you'll need to manually configure the assembly references:

### 1. Clone the Repository
```bash
git clone https://github.com/snowmuffin/SE_Grid_Manager.git
cd SE_Grid_Manager
```

### 2. Configure Directory.Build.props
Edit `Directory.Build.props` and update the paths to match your installations:

```xml
<Project>
  <PropertyGroup>
    <!-- Path to Space Engineers game installation -->
    <Bin64>C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64</Bin64>
    
    <!-- Path to Space Engineers Dedicated Server -->
    <Dedicated64>C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineersDedicatedServer\DedicatedServer64</Dedicated64>
    
    <!-- Path to Torch Server (optional) -->
    <Torch>C:\TorchServer</Torch>
  </PropertyGroup>
</Project>
```

### 3. Manual Assembly Setup (Required)

Since the automated setup is not yet implemented, you need to manually ensure all required assemblies are available:

#### For ClientPlugin:
Verify these Space Engineers assemblies exist in your `$(Bin64)` path:
- `Sandbox.Game.dll`
- `Sandbox.Common.dll`
- `Sandbox.Graphics.dll`
- `VRage.dll`
- `VRage.Game.dll`
- `VRage.Input.dll`
- `VRage.Library.dll`
- `VRage.Math.dll`
- `Newtonsoft.Json.dll`
- All other VRage and system assemblies referenced in the .csproj

#### For TorchPlugin:
Verify these Torch assemblies exist in your `$(Torch)` path:
- `Torch.dll`
- `Torch.API.dll`
- `Torch.Server.exe`
- All dedicated server assemblies in `$(Torch)\DedicatedServer64\`

#### For DedicatedPlugin:
Verify the dedicated server assemblies exist in your `$(Dedicated64)` path.

### 4. Build the Solution
```bash
# Using Visual Studio
# Open Gridmanager.sln and build the solution

# Using command line (if MSBuild is available)
msbuild Gridmanager.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

## Installation

### Client Plugin Installation
1. Build the `ClientPlugin` project
2. Copy `Gridmanager.dll` from `ClientPlugin\bin\Debug\` to your Space Engineers Plugins folder:
   - `%AppData%\SpaceEngineers\Plugins\Local\`

### Torch Server Plugin Installation
1. Build the `TorchPlugin` project
2. Copy the following files to your Torch `Plugins\` folder:
   - `Gridmanager.dll`
   - `manifest.xml`

### Dedicated Server Plugin Installation
1. Build the `DedicatedPlugin` project
2. Copy `Gridmanager.dll` to your dedicated server's Plugins folder

## Usage

### Client Controls
- **Ctrl+G**: Open the grid list interface
- Navigate through your grids and view detailed block information
- Click "Delete Block" to remove individual blocks (requires server permission)

### Server Configuration (Torch)
The Torch plugin provides these configuration options:
- **Enable HTTP Listener**: Enable/disable the REST API
- **HTTP Port**: Port for the HTTP listener (default: 8080)
- **Web Host Address**: Host address for notifications

### API Endpoints (Torch Plugin)
- `GET /ping`: Health check endpoint
- `POST /Update-Grid`: Update grid information for a player
- `POST /get-blocks`: Retrieve block information for a specific grid

## Development Notes

### Project Structure
```
SE_Grid_Manager/
├── ClientPlugin/          # Client-side plugin
├── TorchPlugin/           # Torch server plugin  
├── DedicatedPlugin/       # Dedicated server plugin
├── Shared/                # Common shared code
├── Directory.Build.props  # Global build properties
├── Gridmanager.sln       # Visual Studio solution
├── setup.py              # Python setup script (for future use)
└── verify_props.bat      # Build verification script
```

### Build Events
The project uses pre-build and post-build events:
- **Pre-build**: Verifies that all assembly paths in `Directory.Build.props` exist
- **Post-build**: Deploys compiled plugins to appropriate game directories

### Future Enhancements
- Automated dependency resolution via `setup.py`
- Package manager integration
- Simplified installation process
- Extended API functionality

## Troubleshooting

### Common Issues

1. **Build Errors - Missing References**
   - Ensure all paths in `Directory.Build.props` are correct
   - Verify game installations are up to date
   - Check that all required assemblies exist

2. **Plugin Not Loading**
   - Verify the plugin DLL is in the correct folder
   - Check game logs for error messages
   - Ensure .NET Framework 4.8.1 is installed

3. **Client-Server Communication Issues**
   - Verify both client and server plugins are installed
   - Check network connectivity and firewall settings
   - Review server logs for message handling errors

### Logging
The plugin uses comprehensive logging. Check these locations for log files:
- Client: `%AppData%\SpaceEngineers\Logs\`
- Server: Your server's log directory
- Torch: Torch logs directory

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Ensure all builds pass
5. Submit a pull request

## License

This project is licensed under the terms specified in the LICENSE file.

## Credits

- Built for Space Engineers by Keen Software House
- Uses Harmony for runtime patching
- Torch server integration support
