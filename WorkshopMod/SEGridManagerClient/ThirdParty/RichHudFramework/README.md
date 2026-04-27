# Rich HUD Framework (bundling notes for SE Grid Manager Client)

## Pinned versions (SE Grid Manager Client in this repo)

| Component | Version / ID | Notes |
|-----------|----------------|------|
| [Rich HUD Master](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081) (Workshop) | **1965654081** | Required at runtime for the Rich HUD terminal and RHF registration. Match with your game’s Workshop subscription. |
| [RichHudFramework.Client](https://github.com/ZachHembree/RichHudFramework.Client) (vendored source) | **1.3.0.0** | Matches `RichHudClient.versionID` `(1, 3, 0, 0)` in the Client API. Re-sync with `.\tools\sync_richhud_client.ps1 -Version 1.3.0.0` when you intentionally upgrade. |

**Compile layout in the mod:** `Data/Scripts/SEGridManagerClient/RichHudFramework/Client` and `.../Shared` (from **Term.zip**). Space Engineers builds **one assembly per folder directly under `Data/Scripts`** — RHF must be **inside** `SEGridManagerClient`, not a sibling folder. **Font.zip** is still downloaded by the sync script into `ThirdParty/.../Font` for reference; font assets are normally supplied at runtime by **Rich HUD Master** (see upstream Client release notes).

## What “the same way as [Rich HUD Master](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081)” means

The stack is **two parts**:

| Part | Role | How you ship it |
|------|------|-----------------|
| **Master** | Runtime: billboard / retained UI host, input, terminal, etc. | **Workshop mod** [1965654081](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081) (large; ~tens of MB). Not realistically reimplemented inside SE Grid Manager. |
| **Client** | Your mod’s UI code: registers pages with the Master, buttons, text, etc. | **Source files in *your* mod** (this is the part you *include* and ship). |

So **“모드에 포함”** in the same sense as other RHF mods = **vend the Client module** (MIT) under this folder and wire `GridManager*` logic to the Rich HUD API. Users still need **one** [Rich HUD Master](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081) subscription so the **Master** runtime is present (unless you fork and ship the *entire* Master inside one package — duplicate, heavy, and hard to maintain).

## License

`LICENSE` in this directory is a copy of the **MIT** terms from the upstream [RichHudFramework.Master](https://github.com/ZachHembree/RichHudFramework.Master) repository. The **Client** module is also MIT — see [RichHudFramework.Client](https://github.com/ZachHembree/RichHudFramework.Client). Keep the copyright notice in any substantial copy of their code.

## Upstream

- [Rich HUD Master (Workshop)](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081) — **required** at load time for RHF UIs.  
- [RichHudFramework.Client (GitHub)](https://github.com/ZachHembree/RichHudFramework.Client) — releases (e.g. `Term.zip`, `Font.zip`) and [API reference](https://zachhembree.github.io/RichHudFramework.Client/).

## Vendoring Client into this mod

1. **Pin a version** of **Rich HUD Master** and **Client** that match (see Client release notes for breaking changes).  
2. Run `tools/sync_richhud_client.ps1` from the repo root (or download the same zips by hand) into `ThirdParty/RichHudFramework/Client/`.  
3. Follow the Client readme inside the release to merge **Term** `Client` + `Shared` into **`Data/Scripts/SEGridManagerClient/RichHudFramework/`** (not a sibling of `SEGridManagerClient` under `Data/Scripts` — the game compiles one assembly per top-level scripts folder).  
4. Implement a **new** session or plugin class that uses the RHF public API to show grid list / block list / delete — reusing `GridManagerSession`, `GridManagerProtocol`, and `GridManagerParse` from this mod.  
5. On the **Workshop** item for SE Grid Manager Client, set **Required items** to **Rich HUD Master** (Steam UI when publishing; see Workshop upload README).

## Relationship to current “script-only” UI

The existing `GridManagerUiSession` path uses `ShowMissionScreen` and chat because **Keen’s script whitelist blocks `MyGui`**. RHF does **not** use that path; it talks to the **Master** mod. The **Client** code may still need to follow RHF’s own constraints (see upstream docs). Keep a **fallback** (mission + `/gmg`) for worlds where RHM is not loaded, if you want a single Workshop item to degrade gracefully.

## This folder

`Client/` is produced by the sync script and may be **gitignored** to avoid a huge tree in git; for reproducible releases you can **commit** a pinned Client version instead.
