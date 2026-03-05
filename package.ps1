param(
    [string]$OutDir = "$PSScriptRoot\dist",
    [string]$ZipName = "TazUO-custom.zip"
)

$src  = "$PSScriptRoot\bin\Release\net10.0\win-x64"
$zip  = "$OutDir\$ZipName"

# ── 1. Verify build exists ────────────────────────────────────────────────────
if (-not (Test-Path "$src\TazUO.exe")) {
    Write-Host "TazUO.exe not found. Run 'dotnet build -c Release' first." -ForegroundColor Red
    exit 1
}

# ── 2. Clean output ───────────────────────────────────────────────────────────
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item $OutDir -ItemType Directory | Out-Null

# ── 3. Copy everything except dev-only files ──────────────────────────────────
$devOnly = @("*.nettrace", "*.speedscope.json", "profile.bat", "TazUO.pdb", "settings.json")

Get-ChildItem $src -File | Where-Object {
    $n = $_.Name
    -not ($devOnly | Where-Object { $n -like $_ })
} | ForEach-Object { Copy-Item $_.FullName -Destination $OutDir }

Write-Host "Copied $(( Get-ChildItem $OutDir -File ).Count) runtime files" -ForegroundColor Cyan

# Copy subdirectories (Data/, Fonts/, x64/, lib64/, osx/, osx-arm/, iplib/, etc.)
Get-ChildItem $src -Directory | ForEach-Object {
    Copy-Item $_.FullName -Destination $OutDir -Recurse -Force
}

# ── 4. Build a clean settings.json ────────────────────────────────────────────
$raw = Get-Content "$src\settings.json" -Raw | ConvertFrom-Json

# Keep: server IP/port, client version, encryption, language, fps, other prefs
# Clear: credentials, absolute paths, personal window geometry
$raw.username              = ""
$raw.password              = ""
$raw.ultimaonlinedirectory = ""      # server owner sets this to his UO data folder
$raw.profilespath          = ""
$raw.plugins               = @("RazorEnhanced.dll")
$raw.window_position       = $null
$raw.window_size           = $null
$raw.is_win_maximized      = $true
$raw.saveaccount           = $false  # don't auto-save his credentials on first run
$raw.autologin             = $false

$raw | ConvertTo-Json -Depth 10 | Set-Content "$OutDir\settings.json" -Encoding UTF8

Write-Host "Created sanitized settings.json (server: $($raw.ip):$($raw.port))" -ForegroundColor Cyan

# ── 4b. Download Razor Enhanced plugin ───────────────────────────────────────
Write-Host "Downloading Razor Enhanced..." -ForegroundColor Cyan
$reApi   = "https://api.github.com/repos/RazorEnhanced/RazorEnhanced/releases/latest"
$reInfo  = Invoke-RestMethod -Uri $reApi -Headers @{ "User-Agent" = "TazUO-Packager" }
$reAsset = $reInfo.assets | Where-Object { $_.name -like "RazorEnhanced*.zip" } | Select-Object -First 1

if ($reAsset) {
    $tmpZip  = "$env:TEMP\RazorEnhanced.zip"
    $tmpDir  = "$env:TEMP\RazorEnhanced-extract"
    Invoke-WebRequest -Uri $reAsset.browser_download_url -OutFile $tmpZip
    Expand-Archive -Path $tmpZip -DestinationPath $tmpDir -Force

    $reDll = Get-ChildItem $tmpDir -Filter "RazorEnhanced.dll" -Recurse | Select-Object -First 1
    if ($reDll) {
        $pluginsDir = "$OutDir\Data\Plugins"
        New-Item $pluginsDir -ItemType Directory -Force | Out-Null
        Copy-Item $reDll.FullName -Destination "$pluginsDir\RazorEnhanced.dll"
        Write-Host "Razor Enhanced $($reInfo.tag_name) bundled" -ForegroundColor Cyan
    } else {
        Write-Host "WARNING: RazorEnhanced.dll not found in release ZIP" -ForegroundColor Yellow
    }
    Remove-Item $tmpZip, $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "WARNING: No Razor Enhanced release ZIP found — skipping" -ForegroundColor Yellow
}

# ── 4c. Download ClassicAssist plugin ─────────────────────────────────────────
Write-Host "Downloading ClassicAssist..." -ForegroundColor Cyan
$caApi   = "https://api.github.com/repos/ClassicAssist/ClassicAssist/releases/latest"
$caInfo  = Invoke-RestMethod -Uri $caApi -Headers @{ "User-Agent" = "TazUO-Packager" }
$caAsset = $caInfo.assets | Where-Object { $_.name -like "*ClassicAssist*.zip" } | Select-Object -First 1

if ($caAsset) {
    $tmpZip  = "$env:TEMP\ClassicAssist.zip"
    $tmpDir  = "$env:TEMP\ClassicAssist-extract"
    Invoke-WebRequest -Uri $caAsset.browser_download_url -OutFile $tmpZip
    Expand-Archive -Path $tmpZip -DestinationPath $tmpDir -Force

    $caDll = Get-ChildItem $tmpDir -Filter "ClassicAssist.dll" -Recurse | Select-Object -First 1
    if ($caDll) {
        $pluginsDir = "$OutDir\Data\Plugins"
        New-Item $pluginsDir -ItemType Directory -Force | Out-Null
        Copy-Item $caDll.FullName -Destination "$pluginsDir\ClassicAssist.dll"
        Write-Host "ClassicAssist $($caInfo.tag_name) bundled" -ForegroundColor Cyan
    } else {
        Write-Host "WARNING: ClassicAssist.dll not found in release ZIP" -ForegroundColor Yellow
    }
    Remove-Item $tmpZip, $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "WARNING: No ClassicAssist release ZIP found — skipping" -ForegroundColor Yellow
}

# ── 5. Zip ────────────────────────────────────────────────────────────────────
Compress-Archive -Path "$OutDir\*" -DestinationPath $zip -Force

$sizeMB = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host "Package ready ($sizeMB MB):" -ForegroundColor Green
Write-Host "  $zip"
Write-Host ""
Write-Host "Server owner setup:" -ForegroundColor Yellow
Write-Host "  1. Extract to any folder"
Write-Host "  2. Run TazUO.exe"
Write-Host "  3. At the login screen, enter:"
Write-Host "     - UO Data Directory  ->  path to his UO client files (map0.mul etc)"
Write-Host "     - Username / Password"
Write-Host "     - Server IP/Port are pre-filled as: $($raw.ip):$($raw.port)"
