```markdown
# SE Grid Manager

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Overview

SE Grid Manager is a powerful plugin for *Space Engineers* that facilitates comprehensive grid management for both clients and server environments. The plugin enables players to effectively view, manage, and delete blocks from their grids via an intuitive in-game interface, while also supporting seamless integration with Torch server environments.

## Features

- **Client-side Grid Management**: An in-game user interface (UI) allows players to manage their grids effortlessly.
- **Block-level Operations**: Provides the ability to view and delete individual blocks from grids, enhancing player control and flexibility.
- **Multi-platform Support**: Compatible with standalone *Space Engineers*, dedicated servers, and Torch servers, offering versatility in deployment.
- **HTTP API Integration**: Exposes RESTful API endpoints for external integrations, making it possible to interact programmatically with the grid management system.
- **Real-time Communication**: Implements client-server messaging to provide timely updates on grid status and changes.
- **Owner-based Filtering**: Displays only blocks owned by the requesting player, ensuring privacy and security.

## Architecture

The SE Grid Manager is organized into four primary components, each serving a specific purpose:

### 1. ClientPlugin

- Provides the in-game UI for players to interact with their grids.
- Implements keyboard shortcuts (e.g., `Ctrl+G`) to quickly access the grid management interface.
- Facilitates secure communication with server plugins to retrieve and manipulate grid data.
- Displays detailed block information, enhancing player awareness of their grid configurations.

### 2. TorchPlugin

- Functions as a server-side plugin tailored for Torch server environments.
- Listens for HTTP requests and provides REST API endpoints for external applications.
- Manages grid data and player permissions, ensuring that operations are performed securely and accurately.
- Processes client requests for grid and block information, serving as the backbone for server-client interactions.

### 3. DedicatedPlugin

- A lightweight server-side plugin designed for dedicated server environments.
- Offers core grid management functionalities without the overhead of Torch-specific features.
- Ensures basic grid management capabilities are accessible in non-Torch setups.

### 4. Shared

- Contains common code that is utilized across all plugins.
- Manages configuration settings and logging utilities.
- Includes Harmony patching helpers to facilitate code modifications and enhancements.

## Requirements

### Development Environment

To develop and build the SE Grid Manager, ensure you have the following installed:

- **Visual Studio 2019 or later** (or Visual Studio Code with the C# extension)
- **.NET Framework 4.8.1**
- **Space Engineers Game Files** (for client plugin references)
- **Space Engineers Dedicated Server** (for server plugin references)
- **Torch Server** (optional for Torch plugin development)

### Game Dependencies

- **Space Engineers** (latest version)
- **Harmony 2.3.3** (included via NuGet)
- **Newtonsoft.Json** (included with *Space Engineers*)

## Installation

### Initial Setup

Since this project lacks automated dependency resolution, you'll need to manually configure assembly references.

#### 1. Clone the Repository

```bash
git clone https://github.com/snowmuffin/SE_Grid_Manager.git
cd SE_Grid_Manager
```

#### 2. Configure Directory.Build.props

Edit `Directory.Build.props` to update the paths to match your installations:

```xml
<Project>
  <PropertyGroup>
    <Bin64>C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Bin64</Bin64>
    <Dedicated64>C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineersDedicatedServer\DedicatedServer64</Dedicated64>
    <Torch>C:\TorchServer</Torch>
  </PropertyGroup>
</Project>
```

#### 3. Manual Assembly Setup

Ensure the required assemblies are available:

##### For ClientPlugin:

Verify that the following assemblies exist in your `$(Bin64)` path:

- `Sandbox.Game.dll`
- `Sandbox.Common.dll`
- `Sandbox.Graphics.dll`
- `VRage.dll`
- `VRage.Game.dll`
- `VRage.Input.dll`
- `VRage.Library.dll`
- `VRage.Math.dll`
- `Newtonsoft.Json.dll`

##### For TorchPlugin:

Verify that the Torch assemblies exist in your `$(Torch)` path:

- `Torch.dll`
- `Torch.API.dll`
- `Torch.Server.exe`

##### For DedicatedPlugin:

Verify that the dedicated server assemblies exist in your `$(Dedicated64)` path.

#### 4. Build the Solution

```bash
# Using Visual Studio
# Open Gridmanager.sln and build the solution

# Using command line (if MSBuild is available)
msbuild Gridmanager.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

### Client Plugin Installation

1. Build the `ClientPlugin` project.
2. Copy `Gridmanager.dll` from `ClientPlugin\bin\Debug\` to your Space Engineers Plugins folder:
   - `%AppData%\SpaceEngineers\Plugins\Local\`

### Torch Server Plugin Installation

1. Build the `TorchPlugin` project.
2. Copy the following files to your Torch `Plugins\` folder:
   - `Gridmanager.dll`
   - `manifest.xml`

### Dedicated Server Plugin Installation

1. Build the `DedicatedPlugin` project.
2. Copy `Gridmanager.dll` to your dedicated server's Plugins folder.

## Usage

### Client Controls

- **Ctrl+G**: Opens the grid list interface.
- Navigate through your grids to view detailed block information.
- Click "Delete Block" to remove individual blocks (requires server permission).

### Server Configuration (Torch)

The Torch plugin provides several configuration options:

- **Enable HTTP Listener**: Toggle to enable or disable the REST API.
- **HTTP Port**: Specify the port for the HTTP listener (default: 8080).
- **Web Host Address**: Configure the address for external access.

## Contributing

We welcome contributions to enhance the SE Grid Manager. To contribute:

1. Fork the repository.
2. Create a new branch for your feature or bug fix.
3. Make your changes and add tests if applicable.
4. Submit a pull request detailing your changes.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

For more detailed information or support, please refer to the project's [GitHub Issues](https://github.com/snowmuffin/SE_Grid_Manager/issues) or reach out to the community.
```

This README provides a comprehensive overview of the SE Grid Manager project, enhancing the existing content while maintaining its core structure and features. It includes essential sections such as installation, usage, contributing, and licensing, ensuring clarity and professionalism throughout the document.