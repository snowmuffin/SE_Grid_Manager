# SE Grid Manager — Client mod (Workshop / local)

This folder is a **Space Engineers scripted mod** (`Data/Scripts/SEGridManagerClient/*.cs` and bundled **Rich HUD Framework** under `Data/Scripts/SEGridManagerClient/RichHudFramework/`) that talks to the **Torch Gridmanager** server plugin using the same **ModAPI secure message IDs** as `TorchPlugin/Plugin.cs` (`42424`–`42426`). **Layout:** there must be **only one** top-level folder under `Data/Scripts` (this mod name); RHF is nested inside it so the game compiles one assembly.

**Rich HUD Master (required for RHF terminal UI):** subscribe to the Workshop mod **[1965654081](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081)** and place it **above** this mod in the world’s mod list. With RHM loaded, use the **Rich HUD** terminal (category **SE Grid Manager**) for grid list, block summary, and the same request/delete flow as chat `/gmg`. If RHM is not loaded, the mod **falls back** to mission screens + **Ctrl+Shift+G** and `/gmg` (see `GridManagerUiSession`).

**Vanilla client:** the game **compiles and runs** the scripts — that is what provides **Ctrl+Shift+G** / **Alt+Shift+G**, secure handlers, and the UI paths above. A loose **`SEGridManagerClient.dll`** in the mod root is **not** required for vanilla and is often **not loaded** by the game; do not rely on it for the Workshop package (see **LOADING.md**).

**Optional `SEGridManagerClient` Visual Studio project:** builds a **MyGui** DLL for experiments or custom loaders. It does **not** copy to the mod root unless you build with `CopySeClientDllToModRoot=true`. **Do not** ship that DLL next to a full script mod without knowing what you are doing (double session risk).

**Important (Workshop / description):** This is a **dedicated- or remote-client** companion. On a **listen-server (you are host)**, the client part does not send messages. The server must run the **Gridmanager** plugin for real data.

## Required folder layout

```
SEGridManagerClient/         ← mod root
  Data/
    BlockCategories/
      BlockCategories.sbc     ← minimal SBC (definition load)
    Scripts/
      SEGridManagerClient/
        GridManagerProtocol.cs
        GridManagerSession.cs
        GridManagerUiSession.cs
        GridManagerRichHudSession.cs
        GridManagerClientUiState.cs
        GridManagerClientUiCommands.cs
        RichHudFramework/
          Client/ …
          Shared/ …
  LOADING.md
  metadata.mod
  modinfo.sbmi.template
  thumb.jpg
  README.md
  SEGridManagerClient/        ← optional: VS project (not required to play)
    SEGridManagerClient.csproj
```

**Steam upload** helpers: `WorkshopMod/SteamUpload/`.

## Install (local)

1. Copy the whole **`SEGridManagerClient`** directory into **`%AppData%\SpaceEngineers\Mods\`**.
2. If you still have an old **`SEGridManagerClient.dll`** in that folder from a previous test, **delete it** so only the scripted mod runs.
3. Enable the mod on your world. Use a **remote client** to a server with the plugin for full behavior.

## In-game (script UI)

- **With [Rich HUD Master (1965654081)](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081):** open the **Rich HUD** settings menu and select **SE Grid Manager** (grid list, blocks, action buttons) — same protocol as below.
- **Ctrl+Shift+G** or **Alt+Shift+G** — request **get-grids**; without RHM, replies use a **mission text panel**; help with **Ctrl+Shift+M** / **Alt+Shift+M** and **chat** `/gmg` … (see in-game help panel).
- **Fallback** UI is **`ShowMissionScreen` / `ShowNotification`** (not `MyGuiScreenBase` — script whitelist). See [Mod script whitelist](https://spaceengineers.wiki.gg/wiki/Modding/Reference/ModScripting).

## Server requirement

**Torch Gridmanager** plugin (or compatible) with the same message contract as `TorchPlugin/Plugin.cs`.

## Handler / listen-server policy

- **Listen-server host** (`MultiplayerActive && IsServer`): no client sends (Torch owns that process).
- **Remote client** / **single-player (typical)**: handlers register; sends work when a server can answer.

## See also

- [`LOADING.md`](LOADING.md) — why vanilla uses scripts, optional DLL, C#6 compile notes.
