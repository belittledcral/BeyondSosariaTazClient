param(
    [string]$OutDir    = "$PSScriptRoot\dist",
    [string]$ZipName   = "TazUO-BeyondSosaria.zip",
    [string]$UODataDir = ""   # path to ...UO-Sosaria-Launcher-x.x.x\data\client\
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

# Helper: set existing property or add it if absent
function Set-Prop($Obj, $Name, $Value) {
    if ($Obj.PSObject.Properties[$Name]) { $Obj.$Name = $Value }
    else { $Obj | Add-Member -NotePropertyName $Name -NotePropertyValue $Value }
}

# Keep: server IP/port, client version, encryption, language, fps, other prefs
# Clear: credentials, absolute paths, personal window geometry
Set-Prop $raw username              ''
Set-Prop $raw password              ''
Set-Prop $raw ultimaonlinedirectory ''
Set-Prop $raw profilespath          ''
Set-Prop $raw plugins               @('RazorEnhanced.dll')
Set-Prop $raw window_position       $null
Set-Prop $raw window_size           $null
Set-Prop $raw is_win_maximized      $true
Set-Prop $raw saveaccount           $false
Set-Prop $raw autologin             $false

# Beyond Sosaria server & client settings
Set-Prop $raw ip                    'play.beyondsosaria.com'
Set-Prop $raw port                  2593
Set-Prop $raw clientversion         '7.0.95.0'
Set-Prop $raw encryption            0
Set-Prop $raw lang                  'ENU'
Set-Prop $raw shard_type            0
Set-Prop $raw reconnect             $true
Set-Prop $raw reconnect_time        600000
Set-Prop $raw login_music           $true
Set-Prop $raw fixed_time_step       $true
Set-Prop $raw fps                   60
Set-Prop $raw last_server_name      'Beyond Sosaria'
Set-Prop $raw ultimaonlinedirectory ''

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
    Write-Host "WARNING: No Razor Enhanced release ZIP found - skipping" -ForegroundColor Yellow
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
    Write-Host "WARNING: No ClassicAssist release ZIP found - skipping" -ForegroundColor Yellow
}

# ── 4d. Bundle Beyond Sosaria UO data files ───────────────────────────────────
# Auto-discover the BS launcher data directory if not supplied as a parameter
if (-not $UODataDir) {
    $bsRoot = "C:\Users\$env:USERNAME\Beyond Sosaria"
    $launcher = Get-ChildItem $bsRoot -Directory -Filter "UO-Sosaria-Launcher-*" `
                    -ErrorAction SilentlyContinue |
                Sort-Object Name -Descending | Select-Object -First 1
    if ($launcher) { $UODataDir = "$($launcher.FullName)\data\client" }
}

if ($UODataDir -and (Test-Path $UODataDir)) {
    Write-Host "Bundling UO data from: $UODataDir" -ForegroundColor Cyan

    # Extensions and directories to skip (TazUO binaries / personal config)
    $skipExt  = @('.exe','.dll','.pdb','.so','.dylib','.nettrace','.speedscope.json')
    $skipDirs = @('x64','lib64','osx','osx-arm','win-arm','vulkan','iplib',
                  'Data\Plugins','Data\Profiles','Data/Plugins','Data/Profiles')

    $copied = 0
    Get-ChildItem $UODataDir -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($UODataDir.TrimEnd('\').Length + 1)

        # Skip excluded extensions
        if ($skipExt -contains $_.Extension.ToLower()) { return }

        # Skip excluded directories
        foreach ($sd in $skipDirs) {
            if ($rel.StartsWith($sd, [System.StringComparison]::OrdinalIgnoreCase)) { return }
        }

        $dest = Join-Path $OutDir $rel
        $destDir = Split-Path $dest -Parent
        if (-not (Test-Path $destDir)) { New-Item $destDir -ItemType Directory -Force | Out-Null }
        Copy-Item $_.FullName -Destination $dest -Force
        $copied++
    }

    Write-Host "Bundled $copied UO data files" -ForegroundColor Cyan
} else {
    Write-Host "WARNING: UO data directory not found - package will require manual UO path setup" -ForegroundColor Yellow
    Write-Host "  Supply -UODataDir 'path\to\UO-Sosaria-Launcher-x.x.x\data\client'" -ForegroundColor Yellow
}

# ── 5. Zip ────────────────────────────────────────────────────────────────────
Compress-Archive -Path "$OutDir\*" -DestinationPath $zip -Force

$sizeMB = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "ZIP ($sizeMB MB): $zip" -ForegroundColor Cyan

# ── 6. Self-extracting EXE (requires 7-Zip) ───────────────────────────────────
$exeName = $ZipName -replace '\.zip$', '.exe'
$exePath = "$OutDir\$exeName"
$7zip    = "${env:ProgramFiles}\7-Zip\7z.exe"
$7zSfx   = "${env:ProgramFiles}\7-Zip\7z.sfx"

if ((Test-Path $7zip) -and (Test-Path $7zSfx)) {
    Write-Host "Creating self-extracting EXE..." -ForegroundColor Cyan

    $tmpPayload = "$env:TEMP\bs-payload.7z"
    $tmpCfg     = "$env:TEMP\bs-sfx.cfg"
    $zipLeaf    = [System.IO.Path]::GetFileName($zip)

    # 7z archive of staged files, excluding the zip we just made
    Write-Host "  Compressing payload (this may take a while)..." -ForegroundColor Cyan
    & $7zip a -t7z -mx=5 -mmt=4 $tmpPayload "$OutDir\*" "-x!$zipLeaf"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: 7z failed (exit $LASTEXITCODE) - skipping EXE" -ForegroundColor Red
        return
    }

    # SFX config — shown by 7z.sfx before extraction
    $sfxCfgLines = @(
        ';!@Install@!UTF-8!'
        'Title="Beyond Sosaria"'
        'BeginPrompt="Install the Beyond Sosaria client?"'
        'Directory="C:\BeyondSosaria"'
        'RunProgram="TazUO.exe"'
        ';!@InstallEnd@!'
    )
    Set-Content $tmpCfg -Value $sfxCfgLines -Encoding UTF8

    # Stream-concatenate SFX stub + config + payload → single EXE (avoids loading GB into RAM)
    $out = [System.IO.File]::OpenWrite($exePath)
    try {
        foreach ($src in @($7zSfx, $tmpCfg, $tmpPayload)) {
            $in = [System.IO.File]::OpenRead($src)
            $in.CopyTo($out)
            $in.Close()
        }
    } finally {
        $out.Close()
    }

    Remove-Item $tmpPayload, $tmpCfg -ErrorAction SilentlyContinue

    $exeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
    Write-Host "EXE  ($exeMB MB): $exePath" -ForegroundColor Green
} else {
    Write-Host "7-Zip not found at '${env:ProgramFiles}\7-Zip\' - skipping EXE (ZIP only)" -ForegroundColor Yellow
}

# ── 7. Clean staging files from dist\ ─────────────────────────────────────────
Get-ChildItem $OutDir | Where-Object { $_.Name -notin @($ZipName, $exeName) } |
    Remove-Item -Recurse -Force

Write-Host ""
Write-Host "Package ready:" -ForegroundColor Green
Write-Host "  ZIP: $zip"
if (Test-Path $exePath) { Write-Host "  EXE: $exePath" }
Write-Host ""
Write-Host "Beyond Sosaria - player setup:" -ForegroundColor Yellow
Write-Host "  1. Run $exeName, choose an install folder, click Extract"
Write-Host "  2. TazUO launches automatically after extraction"
Write-Host "  3. Enter your Beyond Sosaria username and password"
Write-Host "     Server is pre-filled: $($raw.ip):$($raw.port)"
Write-Host "  (UO data is bundled - no separate UO installation needed)"
