# Client mod loading (vanilla = scripts in `Data/Scripts`)

## What the game actually runs (vanilla)

For a typical local / Workshop mod, **Space Engineers compiles C# from `Data/Scripts/...`**.  
That is what drives [**`GridManagerSession`**](e:/Documentation/Github/SE_Grid_Manager/WorkshopMod/SEGridManagerClient/Data/Scripts/SEGridManagerClient/GridManagerSession.cs), [**`GridManagerUiSession`**](e:/Documentation/Github/SE_Grid_Manager/WorkshopMod/SEGridManagerClient/Data/Scripts/SEGridManagerClient/GridManagerUiSession.cs) (hotkeys, secure messages, **mission/notification** fallback), and — when [Rich HUD Master (Workshop 1965654081)](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081) is loaded — [**`GridManagerRichHudSession`**](e:/Documentation/Github/SE_Grid_Manager/WorkshopMod/SEGridManagerClient/Data/Scripts/SEGridManagerClient/GridManagerRichHudSession.cs) plus the third-party RHF API under `Data/Scripts/SEGridManagerClient/RichHudFramework/` (must be **nested** there — see below). **Do not** rely on a loose `SEGridManagerClient.dll` in the mod root for vanilla — it is often **not** loaded, so a MyGui-only DLL will **not** run `HandleInput` or show screens.

**Shipped mod layout:** under `Data/Scripts/` have **only one top-level folder** — `SEGridManagerClient/` — and place RHF sources at **`SEGridManagerClient/RichHudFramework/`** (Client + Shared). The game compiles each *direct* child of `Data/Scripts` as a **separate** assembly; a sibling `Data/Scripts/RichHudFramework/` would **not** see your mod code ([Mod Scripting](https://spaceengineers.wiki.gg/wiki/Modding/Reference/ModScripting)). Also ship **`Data/BlockCategories/BlockCategories.sbc`**, `metadata.mod`. You should **not** ship a second client entry that doubles secure-message registration.

**Load order:** enable **[Rich HUD Master (1965654081)](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081)** *before* (above) this mod if you use the RHF terminal UI. Without RHM, the mod still works via mission screens + `/gmg` (see `GridManagerUiSession`).

**Optional DLL project:** `SEGridManagerClient/SEGridManagerClient.csproj` can still be built for development or for environments that **do** load mod assemblies (e.g. some community tools). The `.csproj` does **not** copy the DLL to the mod root unless you set **`CopySeClientDllToModRoot=true`** when building, to avoid mixing a non-loading DLL with the script mod by mistake.

```text
msbuild SEGridManagerClient.csproj /p:CopySeClientDllToModRoot=true
```

## If “nothing happens” (no hotkey, no panel)

1. **Confirm** `Data\Scripts\SEGridManagerClient\` contains your `.cs` files **and** `RichHudFramework\` (nested). There must be **no** second top-level folder under `Data\Scripts` for RHF.
2. **Remove a stray `SEGridManagerClient.dll`** from the mod folder if you copied an old build — scripts are the source of truth for vanilla.  
3. **Listen-server host** (`MultiplayerActive && IsServer`): the mod does not send; use a dedicated/remote client to the Torch server.  
4. **Dedicated** client: `GridManagerSession` returns early; no UI by design.  
5. **Logs:** `%AppData%\SpaceEngineers\Logs\` for mod compile or runtime errors.

## `MOD_ERROR` / script compilation (C# 6)

The in-game compiler is **C# 6** (no `out var`, etc.). If you add code, keep syntax compatible. If you see errors about **`Data/Scripts`**, you may have a partial old tree — use the full set of files from this repository.

## PluginLoader / external loaders

If you use a **loader that injects plugins or mod DLLs** and you intentionally run a **DLL-based** client, you must follow that tool’s rules and avoid **duplicate** session/handler code in `Data/Scripts` (see that loader’s documentation).
