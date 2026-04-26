# SE Grid Manager — Client mod (Workshop / local)

This folder is a **Space Engineers scripted mod** that replaces the removed **ClientPlugin**. It talks to the **Torch Gridmanager** server plugin using the same **ModAPI secure message IDs** as `TorchPlugin/Plugin.cs` (`42424`–`42426`).

## Required folder layout (official convention)

Scripted mods must place C# under **`Data/Scripts/<namespace-or-assembly-folder>/`**. The inner folder name should match your script namespace / project convention (here: `SEGridManagerClient`).

```
SEGridManagerClient/          ← mod root (this folder name is your mod folder name)
  Data/
    Scripts/
      SEGridManagerClient/    ← one folder under Scripts; contains *.cs
        GridManagerProtocol.cs
        GridManagerSession.cs
  README.md
  modinfo.sbmi.template       ← optional: copy to modinfo.sbmi after Workshop publish
```

See: [Mod Scripting (Space Engineers Wiki)](https://spaceengineers.wiki.gg/wiki/Modding/Reference/ModScripting) and [Creating and uploading mods](https://spaceengineers.wiki.gg/wiki/Modding/Tutorials/Creating_And_Uploading_Mods).

## Install (local)

1. Copy the entire **`SEGridManagerClient`** directory (the folder that contains `Data`) into **`%AppData%\SpaceEngineers\Mods\`**.
2. Start the game → **Mods** → enable **SE Grid Manager Client** (or whatever title appears from the folder).
3. Create or load a world and ensure this mod is checked for that save.

You do **not** need `modinfo.sbmi` for local-only testing; the game creates or uses it when you publish updates to Workshop ([wiki](https://spaceengineers.wiki.gg/wiki/Modding/Tutorials/Creating_And_Uploading_Mods)).

## Workshop publish (short)

1. Add a **`thumb.jpg`** thumbnail (Workshop requirement; size per current game UI).
2. Use the in-game mod UI to **upload** the mod folder; after first success, keep the generated **`modinfo.sbmi`** in the mod root for updates.
3. If you lost `modinfo.sbmi`, use the wiki “Manually creating the modinfo.sbmi” section; a starting template is in **`modinfo.sbmi.template`** (rename and fill real Workshop item IDs).

## Important: double registration on the Torch host

The Torch server plugin **already registers** the same message handlers on the **game process** that runs inside Torch.

This mod **must not register the same handlers on that same process**, or you risk duplicate handler registration.

The sample registers handlers only when it believes it is a **remote client** (`MultiplayerActive && !IsServer`). **Listen-server hosts** and **dedicated** paths skip registration or never load client scripts as a player would—see `GridManagerSession.ShouldRegisterClientHandlers` and `Init`. If you need **listen-server / SP** UI, plan a separate channel (different message IDs or HTTP-only) and adjust the plugin + mod together.

## Server requirement

The world / server must run the **Torch `Gridmanager` plugin** (same message contract).

## Next steps (implementation)

- Add in-game UI (ModAPI screens) on top of `GridManagerSession`.
- Parse JSON responses in the `OnGetGridsMessage` / `OnGetBlocksMessage` handlers (see server `TorchPlugin/Plugin.cs` for payloads).
