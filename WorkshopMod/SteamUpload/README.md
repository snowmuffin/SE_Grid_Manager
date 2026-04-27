# SteamCMD workshop upload (SE Grid Manager Client)

**This folder is not part of the mod** you copy into `%AppData%\SpaceEngineers\Mods\` — it only holds **upload scripts**. The actual mod body is the sibling folder **`SEGridManagerClient\`**; SteamCMD’s `contentfolder` points there so subscribers do not download PowerShell, VDF examples, or `workshop_id.txt`.

After a first in-game (or past SteamCMD) upload you have a `publishedfileid`. Use **`publish_steam_workshop.ps1`** to push updates.

## Prerequisites

1. **SteamCMD** — [Valve: SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD); default path can go in `steamcmd_path.txt` (one line) or set **`STEAMCMD`**.
2. **Account** for the item; **`STEAM_USER`** or prompt; Steam Guard in the same console.
3. **ID** — `workshop_id.txt` (one line, digits) or `modinfo.sbmi` inside **`SEGridManagerClient\`**. The number is also the folder name under Steam:  
   `...\Steam\steamapps\workshop\content\244850\<ID>\` (244850 = Space Engineers app id).
4. **`SEGridManagerClient\thumb.jpg`** — Workshop preview: **under 1 MB** (Steam often rejects larger with `Limit exceeded`). The publish script errors early if the file is too big.

The default **scripted** mod does **not** need a client DLL. If you add an optional `SEGridManagerClient.dll` for special setups, the publish script also accepts a mod with **metadata.mod** and no DLL (see `publish_steam_workshop.ps1` guard).

## Workshop “Required items” (Strongly recommended)

In the Steam Workshop item for this mod, add a **Required item** (or dependency) on **[Rich HUD Master](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081)** (`1965654081`) so players get the RHF runtime. The mod still **degrades gracefully** without RHM (mission + `/gmg` only), but the intended experience is RHM + this mod in that load order.

## Run

From this directory (`WorkshopMod\SteamUpload`):

```text
.\publish_steam_workshop.ps1
```

or:

```text
publish_steam_workshop.cmd
```

## If subscribers see `MOD_ERROR` / `RichHudFramework` could not be found

**Cause A (missing files):** The upload must include **`Data\Scripts\SEGridManagerClient\RichHudFramework\`** (many `.cs` files), not only the top-level `.cs` files in `SEGridManagerClient\`.

**Cause B (wrong layout):** Space Engineers compiles **each folder directly under `Data\Scripts`** as a **separate assembly**. `RichHudFramework` must **not** be a **sibling** of `SEGridManagerClient` — RHF sources must be nested as **`Data\Scripts\SEGridManagerClient\RichHudFramework\`** so they compile with the rest of the mod. See [Mod Scripting](https://spaceengineers.wiki.gg/wiki/Modding/Reference/ModScripting) (“only one folder directly in Scripts”). **`publish_steam_workshop.ps1` checks** for the nested path before SteamCMD.

## See also

- `steam_workshop.vdf.example` — manual VDF if you do not use the script.
- `workshop_id.txt.example` — copy to `workshop_id.txt` if you do not use `modinfo.sbmi` for the id.
