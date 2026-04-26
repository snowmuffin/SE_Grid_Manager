# Publishes / updates the SEGridManagerClient folder on the Space Engineers Steam Workshop via SteamCMD.
# This script lives OUTSIDE the mod root so subscribers do not download publish tools (contentfolder = ..\SEGridManagerClient only).
# Usage: set STEAM_USER (optional), run from this folder or pass -WorkshopId, -SteamUser, -SteamCmd

param(
    [string] $WorkshopId,
    [string] $ChangeNote = "",
    [string] $Title = "SE Grid Manager Client",
    [string] $Description = "Client mod for dedicated/remote play only. The server must run the Gridmanager (Torch) plugin. Subscribing or downloading this Workshop item does not enable features by itself — you need a matching server. Not for listen-server host use. GitHub: SE_Grid_Manager.",
    [string] $SteamUser = $env:STEAM_USER,
    [string] $SteamCmd = $env:STEAMCMD
)

$ErrorActionPreference = "Stop"

# Mod root: published Workshop content (sibling: WorkshopMod/SEGridManagerClient)
$modRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\SEGridManagerClient")).Path
$thumb = Join-Path $modRoot "thumb.jpg"

$idFile = Join-Path $PSScriptRoot "workshop_id.txt"
if (-not $WorkshopId -and (Test-Path $idFile)) {
    $line = (Get-Content -LiteralPath $idFile -ErrorAction SilentlyContinue | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1)
    if ($line) { $WorkshopId = $line.Trim() }
}
$modinfo = Join-Path $modRoot "modinfo.sbmi"
$idFromModinfo = $null
$modinfoHint = ""
if (-not (Test-Path -LiteralPath $modinfo)) {
    $modinfoHint = " modinfo.sbmi is not in the mod root. After an in-game Workshop upload, copy modinfo.sbmi from that mod folder, or from your Steam Workshop subscription folder, into: $modRoot"
} else {
    $raw = Get-Content -LiteralPath $modinfo -Raw -ErrorAction SilentlyContinue
    if ($raw -match '<Id>\s*([^<]+?)\s*</Id>') {
        $cand = $matches[1].Trim()
        if ($cand -match '^\d{5,20}$') {
            $idFromModinfo = $cand
        } elseif ($cand -match 'YOUR_STEAM|placeholder|INSERT') {
            $modinfoHint = " modinfo.sbmi still has the template text in Id. Replace with your real numeric Workshop file id, or use workshop_id.txt in SteamUpload."
        } else {
            $modinfoHint = " modinfo.sbmi Id is not only digits: '$cand'. Use a numeric id, or use workshop_id.txt / -WorkshopId."
        }
    } else {
        $modinfoHint = " modinfo.sbmi has no Id element in the expected place."
    }
}
if (-not $WorkshopId -and $idFromModinfo) { $WorkshopId = $idFromModinfo }

if (-not $WorkshopId) {
    $msg = @"
No numeric Workshop published file id found. Mod root: $modRoot

$modinfoHint
- Put modinfo.sbmi here with a numeric Id (from game after first upload or Steam), not modinfo.sbmi.template text.
- Or create: WorkshopMod\SteamUpload\workshop_id.txt (one line, digits only).
- Or: .\publish_steam_workshop.ps1 -WorkshopId 1234567890

Checked: $(Join-Path $PSScriptRoot "workshop_id.txt"), $modinfo
"@
    throw $msg
}

if (-not $ChangeNote) {
    $meta = Join-Path $modRoot "metadata.mod"
    if (Test-Path $meta) {
        if ((Get-Content -LiteralPath $meta -Raw) -match '<ModVersion>([^<]+)</ModVersion>') {
            $ChangeNote = "Update (metadata.mod " + $matches[1] + ")"
        }
    }
    if (-not $ChangeNote) { $ChangeNote = "workshop update" }
}

if (-not (Test-Path -LiteralPath $thumb)) {
    throw "Missing thumb.jpg in mod root: $thumb`nAdd a workshop thumbnail, or the Steam upload may be rejected. See mod README."
}
$previewMax = 1MB
$thumbSize = (Get-Item -LiteralPath $thumb).Length
if ($thumbSize -gt $previewMax) {
    throw "thumb.jpg is too large for Steam Workshop preview: $thumbSize bytes (use under 1 MB). Re-encode the JPEG or use a smaller image."
}

function Get-SteamCmdPath {
    $pathFile = Join-Path $PSScriptRoot "steamcmd_path.txt"
    if (Test-Path -LiteralPath $pathFile) {
        $line = Get-Content -LiteralPath $pathFile -ErrorAction SilentlyContinue |
        Where-Object { $_ -and ($_ -notmatch '^\s*#') } |
        ForEach-Object { $_.Trim() } |
        Select-Object -First 1
        if ($line -and (Test-Path -LiteralPath $line)) { return $line }
    }
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Steam\steamcmd\steamcmd.exe"),
        (Join-Path $env:ProgramFiles "Steam\steamcmd\steamcmd.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\SteamCMD\steamcmd.exe"),
        (Join-Path $env:USERPROFILE "Steam\steamcmd\steamcmd.exe"),
        "C:\SteamCMD\steamcmd.exe",
        "C:\steamcmd\steamcmd.exe",
        "D:\SteamCMD\steamcmd.exe",
        "D:\steamcmd\steamcmd.exe",
        "E:\SteamCMD\steamcmd.exe",
        (Join-Path $env:ProgramFiles "SteamCmd\steamcmd.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "SteamCmd\steamcmd.exe")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return (Resolve-Path -LiteralPath $c).Path }
    }
    $w = Get-Command steamcmd.exe -ErrorAction SilentlyContinue
    if ($w -and (Test-Path -LiteralPath $w.Source)) { return $w.Source }
    $where = & where.exe steamcmd 2>$null
    if ($where) {
        foreach ($p in $where) { if (Test-Path -LiteralPath $p) { return $p } }
    }
    return $null
}

if (-not $SteamCmd) { $SteamCmd = Get-SteamCmdPath }
if (-not $SteamCmd -or -not (Test-Path -LiteralPath $SteamCmd)) {
    throw @"
SteamCMD not found. Install: https://developer.valvesoftware.com/wiki/SteamCMD
Then set env STEAMCMD, or put the full path in SteamUpload\steamcmd_path.txt (see steamcmd_path.txt.example).
"@
}

function Escape-VdfPath([string] $p) {
    return ($p -replace "\\", "/")
}

$modEnc = Escape-VdfPath $modRoot
$thumbEnc = Escape-VdfPath $thumb
$descOneLine = $Description -replace "\r\n|\n", "\\n" -replace '"', "'"
$titleSafe = $Title -replace '"', "'"
$changeSafe = $ChangeNote -replace '"', "'"

$vdf = @"
"workshopitem"
{
    "appid" "244850"
    "publishedfileid" "$WorkshopId"
    "contentfolder" "$modEnc"
    "previewfile" "$thumbEnc"
    "visibility" "0"
    "title" "$titleSafe"
    "description" "$descOneLine"
    "changenote" "$changeSafe"
}
"@

$vdfOut = Join-Path $env:TEMP ("segrid_workshop_build_{0}.vdf" -f [Guid]::NewGuid().ToString("N"))
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($vdfOut, $vdf, $utf8)

Write-Host "Mod root (uploaded):  $modRoot"
Write-Host "VDF:                 $vdfOut"
Write-Host "Workshop id:         $WorkshopId"
Write-Host "SteamCMD:            $SteamCmd"
Write-Host ""

if (-not $SteamUser) {
    $SteamUser = Read-Host "Steam account name (login for SteamCMD)"
}

$argList = @(
    "+login", $SteamUser,
    "+workshop_build_item", $vdfOut,
    "+quit"
)

$cmdDir = Split-Path -Parent -Path $SteamCmd
$proc = Start-Process -FilePath $SteamCmd -ArgumentList $argList -WorkingDirectory $cmdDir -NoNewWindow -PassThru -Wait
if ($proc.ExitCode -ne 0) {
    $hint = ""
    if ($proc.ExitCode -eq 7) {
        $hint = " 'Limit exceeded' on upload is often: preview (thumb.jpg) over 1 MB, or Steam throttling (wait 15+ min). This script enforces preview size before upload. See SteamUpload\README.md."
    }
    throw "SteamCMD exited with code $($proc.ExitCode). Check login / Guard / item id. $hint"
}
Write-Host "Done. Verify the item in Steam Community -> Workshop (Space Engineers)."
