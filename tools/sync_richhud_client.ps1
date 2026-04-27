# Downloads RichHudFramework.Client release zips (MIT) into ThirdParty for manual merge.
# Does not install Rich HUD Master — subscribers still need Workshop mod 1965654081.
# Usage: run from repo root:  .\tools\sync_richhud_client.ps1  [-Version 1.3.0.0]

param(
    [string] $Version = "1.3.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$destRoot = Join-Path $root "WorkshopMod\SEGridManagerClient\ThirdParty\RichHudFramework"
$clientDir = Join-Path $destRoot "Client"
$baseUrl = "https://github.com/ZachHembree/RichHudFramework.Client/releases/download/$Version"

New-Item -ItemType Directory -Path $clientDir -Force | Out-Null

$zips = @("Term.zip", "Font.zip")
foreach ($z in $zips) {
    $url = "$baseUrl/$z"
    $out = Join-Path $clientDir $z
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing
    $extract = Join-Path $clientDir ([System.IO.Path]::GetFileNameWithoutExtension($z))
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    New-Item -ItemType Directory -Path $extract -Force | Out-Null
    Expand-Archive -LiteralPath $out -DestinationPath $extract -Force
    Write-Host "Extracted to $extract"
}

Write-Host ""
Write-Host "Done. Merge contents into your Data/Scripts layout per the zip README and ThirdParty/RichHudFramework/README.md"
Write-Host "Rich HUD Master (Workshop 1965654081) is still required at runtime."
