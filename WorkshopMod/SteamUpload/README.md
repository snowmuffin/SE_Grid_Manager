# SteamCMD workshop upload (SE Grid Manager Client)

**This folder is not part of the mod** you copy into `%AppData%\SpaceEngineers\Mods\` — it only holds **upload scripts**. The actual mod body is the sibling folder **`SEGridManagerClient\`**; SteamCMD’s `contentfolder` points there so subscribers do not download PowerShell, VDF examples, or `workshop_id.txt`.

After a first in-game (or past SteamCMD) upload you have a `publishedfileid`. Use **`publish_steam_workshop.ps1`** to push updates.

## Prerequisites

1. **SteamCMD** — [Valve: SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD); default path can go in `steamcmd_path.txt` (one line) or set **`STEAMCMD`**.
2. **Account** for the item; **`STEAM_USER`** or prompt; Steam Guard in the same console.
3. **ID** — `workshop_id.txt` (one line, digits) or `modinfo.sbmi` inside **`SEGridManagerClient\`**. The number is also the folder name under Steam:  
   `...\Steam\steamapps\workshop\content\244850\<ID>\` (244850 = Space Engineers app id).
4. **`SEGridManagerClient\thumb.jpg`** — Workshop preview: **under 1 MB** (Steam often rejects larger with `Limit exceeded`). The publish script errors early if the file is too big.

## Run

From this directory (`WorkshopMod\SteamUpload`):

```text
.\publish_steam_workshop.ps1
```

or:

```text
publish_steam_workshop.cmd
```

## See also

- `steam_workshop.vdf.example` — manual VDF if you do not use the script.
- `workshop_id.txt.example` — copy to `workshop_id.txt` if you do not use `modinfo.sbmi` for the id.
