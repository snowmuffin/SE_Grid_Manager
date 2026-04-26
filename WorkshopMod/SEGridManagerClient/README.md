# SE Grid Manager — Client mod (Workshop / local)

This folder is a **Space Engineers scripted mod** that replaces the removed **ClientPlugin**. It talks to the **Torch Gridmanager** server plugin using the same **ModAPI secure message IDs** as `TorchPlugin/Plugin.cs` (`42424`–`42426`).

**Important (Workshop / description):** This is a **dedicated- or remote-client** companion to a **server** that runs the **Gridmanager** plugin. **Only downloading or enabling this mod does nothing** until you play on a world where that **server** is present and configured. It is **not** a “install and it works in every world” mod. On a **listen-server (you are host)**, the client part of this mod is intentionally inactive so it does not clash with Torch on the same machine (see below).

## Required folder layout (official convention)

Scripted mods must place C# under **`Data/Scripts/<namespace-or-assembly-folder>/`**. The inner folder name should match your script namespace / project convention (here: `SEGridManagerClient`).

```
SEGridManagerClient/          ← mod root (this folder name is your mod folder name)
  Data/
    Scripts/
      SEGridManagerClient/    ← one folder under Scripts; contains *.cs
        GridManagerProtocol.cs
        GridManagerSession.cs
        GridManagerUiSession.cs
  README.md
  modinfo.sbmi.template       ← optional: copy to modinfo.sbmi after Workshop publish
  modinfo.sbmi                ← after first Workshop upload; used by Steam upload script
  thumb.jpg
```

**Steam upload scripts** (not inside the mod folder; not uploaded to subscribers): `WorkshopMod/SteamUpload/` — see that folder’s `README.md`.

See: [Mod Scripting (Space Engineers Wiki)](https://spaceengineers.wiki.gg/wiki/Modding/Reference/ModScripting) and [Creating and uploading mods](https://spaceengineers.wiki.gg/wiki/Modding/Tutorials/Creating_And_Uploading_Mods).

## Install (local)

1. Copy the entire **`SEGridManagerClient`** directory (the folder that contains `Data`) into **`%AppData%\SpaceEngineers\Mods\`**.
2. Start the game → **Mods** → enable **SE Grid Manager Client** (or whatever title appears from the folder).
3. Create or load a world and ensure this mod is checked for that save.

You do **not** need `modinfo.sbmi` for local-only testing; the game creates or uses it when you publish updates to Workshop ([wiki](https://spaceengineers.wiki.gg/wiki/Modding/Tutorials/Creating_And_Uploading_Mods)).

## Workshop publish (short)

1. Add a **`thumb.jpg`** Workshop preview — keep it **under ~1 MB** (Steam/SteamCMD often fail with "Limit exceeded" if larger).
2. Use the in-game mod UI to **upload** the mod folder; after first success, keep the generated **`modinfo.sbmi`** in the mod root for updates.
3. If you lost `modinfo.sbmi`, use the wiki “Manually creating the modinfo.sbmi” section; a starting template is in **`modinfo.sbmi.template`** (rename and fill real Workshop item IDs).

### Command-line (SteamCMD) updates

Scripts live in **`../SteamUpload/`** (sibling of this folder) so the **workshop package is only** `SEGridManagerClient\` (no `publish_*.ps1` inside the mod for subscribers). Run **`WorkshopMod\SteamUpload\publish_steam_workshop.ps1`**. See **`../SteamUpload/README.md`**.

## Important: when this mod registers message handlers (Torch host)

The Torch server plugin **already registers** the same message handlers on the **game process** that runs inside Torch. This mod **must not** also register them on that same process, or you risk **duplicate** handler registration.

`GridManagerSession.ShouldRegisterClientHandlers` and `Init` implement this as follows:

- **Dedicated**: `Init` returns early (`IsDedicated`) — no client handler registration.
- **Listen-server host** (`MultiplayerActive && IsServer`): **no** registration — the Torch plugin already owns the handlers on this process.
- **Remote client** (`MultiplayerActive && !IsServer`): **yes** — registration (normal multiplayer client path).
- **Single-player** (`!MultiplayerActive`): **yes** — registration is allowed for local testing and UI work; there is no Torch process here, so there is no duplicate with the server plugin. Any replies still depend on what the local game does with those messages, not a remote Torch host.

`RequestGetGrids` / `RequestGetBlocks` / `RequestBlockDelete` skip sending when `IsServer && MultiplayerActive` (listen host). In single-player that condition is false, so the send helpers still run; **a supported production path is a remote client connected to a Torch server with the Gridmanager plugin**. For first-class support on listen-server or single-player, plan a separate channel (different message IDs, HTTP, etc.) and change the plugin and mod together.

## Server requirement

The world / server must run the **Torch `Gridmanager` plugin** (same message contract).

## In-game panel and commands

Scripted mods use the official **`ShowMissionScreen` / `ShowNotification`** pattern (not custom `MyGuiScreenBase` windows), so the “panel” is a mission-style text screen you open from the mod.

- **Default chords (less overlap with F-keys and single-mod game binds):** use **double modifiers** from `IMyInput` (`IsAnyCtrlKeyPressed` + `IsAnyShiftKeyPressed`, or Alt+Shift, **without Ctrl** for the Alt line). All still need **no chat and no game cursor** on the mod input path (THDigi-style).
  - **Grid list (get-grids):** **Ctrl+Shift+G** or **Alt+Shift+G** — same as `/gmg getgrids`. Reply for `get-grids` always uses a **mission text panel** for readability.
  - **Help + 30s menu:** **Ctrl+Shift+M** or **Alt+Shift+M** — mission help; then for **30s**, **plain 1/2/3** with **no** Ctrl/Alt/Shift (release modifiers first).
  - **Anytime, no 30s wait:** **Ctrl+Shift+1/2/3** or **Alt+Shift+1/2/3** (numpad 1–3) → get grids / get blocks / delete, same as the timed 1/2/3.
- If **G**, **M**, or digits still clash with *your* other mods, edit `MyKeys.G` / `MyKeys.M` in `GridManagerUiSession.cs` to unused letters, or use **chat only** (`/gmg` …).
- **Chat** (prefix `/gmg` or `!gmg` — the line is not broadcast to other players as chat):
  - `getgrids` — request grid list for your session Steam id.
  - `grid <EntityId>` — set target grid id for the next `getblocks` or `delete`.
  - `block <name>` — set target block (use quotes for spaces, e.g. `block "Sliding Door"`).
  - `getblocks` — require `grid` set; request blocks on that grid.
  - `delete` — require `grid` and `block`.

Short JSON replies show as a **notification**; longer ones open a **mission screen** with the body (truncated for size).

**Listen-server host:** the mod does not send the same mod messages on the **host** client (to avoid clashing with the Torch plugin). Use a **dedicated/remote** client to exercise these tools.

## Next steps (implementation)

- Parse JSON in the UI (optional) instead of showing raw strings from the server.
- Richer form fields (grid id / block name) would need another UI approach or chat parsing rules (see server `TorchPlugin/Plugin.cs` for payloads).
