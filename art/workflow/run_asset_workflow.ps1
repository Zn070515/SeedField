param(
    [Parameter(Mandatory = $true)]
    [string]$Brief,
    [int]$Size = 512,
    [int]$ReviewAngles = 4,
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    [switch]$SkipSheet,
    [switch]$Register
)

$ErrorActionPreference = "Stop"
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$briefPath = (Resolve-Path $Brief).Path
$briefData = Get-Content $briefPath -Raw | ConvertFrom-Json
$assetId = $briefData.assetId
if (-not $assetId) {
    throw "Brief is missing assetId: $briefPath"
}

$output = Join-Path $PSScriptRoot "out\$assetId"
New-Item -ItemType Directory -Force $output | Out-Null

Write-Host "==> Render $assetId (topdown 90, $Size px)" -ForegroundColor Cyan
& $Blender --background --factory-startup `
    --python (Join-Path $root "art/blender/render_brief_asset.py") -- `
    --brief $briefPath --output $output --size $Size --review-angles $ReviewAngles
if ($LASTEXITCODE -ne 0) {
    throw "Blender render failed with exit code $LASTEXITCODE"
}

if (-not $SkipSheet) {
    Write-Host "==> Review sheet" -ForegroundColor Cyan
    python (Join-Path $root "art/workflow/make_review_sheet.py") `
        --input $output --asset $assetId --output (Join-Path $output "review_sheet.png")
    if ($LASTEXITCODE -ne 0) {
        throw "Review sheet failed with exit code $LASTEXITCODE"
    }
}

Write-Host ""
Write-Host "Asset:      $assetId" -ForegroundColor Green
Write-Host "Main PNG:   $(Join-Path $output "$assetId`_topdown_000.png")"
Write-Host "Review:     $(Join-Path $output "review_sheet.png")"
Write-Host "Meta:       $(Join-Path $output "meta.json")"

if ($Register) {
    $runtime = Join-Path $root "game/SeedField.Game/Art/style_topdown"
    New-Item -ItemType Directory -Force $runtime | Out-Null
    Copy-Item (Join-Path $output "$assetId`_topdown_000.png") $runtime -Force
    Write-Host "Copied to:  $runtime" -ForegroundColor Yellow
    Write-Host "Next: add $assetId to tools/build_art_manifest.py registries, then run:" -ForegroundColor Yellow
    Write-Host "  python tools/build_art_manifest.py"
    Write-Host "  dotnet test tests/SeedField.Repository.Tests --filter ArtAssetTests"
}
else {
    Write-Host "Review-only. Re-run with -Register after the look is approved." -ForegroundColor DarkGray
}
