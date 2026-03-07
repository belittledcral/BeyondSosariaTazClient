param(
    [string]$OutDir  = "$PSScriptRoot\dist",
    [string]$ZipName = "TazUO-BeyondSosaria.zip",
    [string]$Tag     = ""      # override auto date tag if needed
)

# ── 1. Resolve tag ────────────────────────────────────────────────────────────
if (-not $Tag) {
    $Tag = "v" + (Get-Date -Format "yyyy.MM.dd")
}

$zip = "$OutDir\$ZipName"
$exe = $zip -replace '\.zip$', '.exe'

# ── 2. Verify dist artifacts exist ────────────────────────────────────────────
if (-not (Test-Path $zip)) {
    Write-Host "ERROR: $zip not found. Run package.ps1 first." -ForegroundColor Red
    exit 1
}

# ── 3. Create GitHub release ──────────────────────────────────────────────────
Write-Host "Creating release $Tag ..." -ForegroundColor Cyan
gh release create $Tag `
    --title "Beyond Sosaria Client $Tag" `
    --notes "Beyond Sosaria TazUO client release $Tag" `
    --latest

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: gh release create failed (exit $LASTEXITCODE)" -ForegroundColor Red
    exit 1
}

# ── 4. Upload assets ──────────────────────────────────────────────────────────
$assets = @($zip)
if (Test-Path $exe) { $assets += $exe }

foreach ($asset in $assets) {
    $name = [System.IO.Path]::GetFileName($asset)
    Write-Host "Uploading $name ..." -ForegroundColor Cyan
    gh release upload $Tag $asset --clobber
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: upload failed for $name" -ForegroundColor Red
        exit 1
    }
}

# ── 5. Print result ───────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Release $Tag published." -ForegroundColor Green
gh release view $Tag --web
