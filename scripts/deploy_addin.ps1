$addinRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2025'
$addinDir = Join-Path $addinRoot 'DV_TOOL_HTKT'
$cand1 = 'src\InfraBIM.CulvertTool\bin\Release\net8.0-windows'
$cand2 = 'src\InfraBIM.CulvertTool\bin\x64\Release\net8.0-windows'

$sourceDir = $cand1
if (Test-Path (Join-Path $cand2 'DV_TOOL_HTKT.dll')) {
    if ((Get-Item (Join-Path $cand2 'DV_TOOL_HTKT.dll')).LastWriteTime -gt (Get-Item (Join-Path $cand1 'DV_TOOL_HTKT.dll')).LastWriteTime) {
        $sourceDir = $cand2
    }
}

if (!(Test-Path $addinRoot)) {
    New-Item -ItemType Directory -Path $addinRoot -Force | Out-Null
}
if (!(Test-Path $addinDir)) {
    New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
}

# 1. Copy file .addin vao thu muc Addins 2025
$manifestSource = 'src\InfraBIM.CulvertTool\DV_TOOL_HTKT.addin'
if (Test-Path $manifestSource) {
    Copy-Item $manifestSource (Join-Path $addinRoot 'DV_TOOL_HTKT.addin') -Force
}

# 2. Don dep cac file .old
Get-ChildItem -Path $addinDir -Filter '*.old' -ErrorAction SilentlyContinue | ForEach-Object {
    try { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue } catch {}
}

# 3. Rename file DLL cu neu dang bi khoa (Revit dang mo)
$targetDll = Join-Path $addinDir 'DV_TOOL_HTKT.dll'
if (Test-Path $targetDll) {
    try {
        $tempOld = Join-Path $addinDir ('DV_TOOL_HTKT.' + [System.Guid]::NewGuid().ToString('N') + '.old')
        Move-Item -Path $targetDll -Destination $tempOld -Force
    } catch {
        Write-Warning "Could not rename DV_TOOL_HTKT.dll: $_"
    }
}

# 4. Copy toan bo file build va dependencies tu sourceDir
if (Test-Path $sourceDir) {
    Get-ChildItem -Path $sourceDir | Where-Object { 
        $_.Extension -ne '.old' -and !$_.Name.Contains('_old_') 
    } | ForEach-Object {
        Copy-Item $_.FullName $addinDir -Recurse -Force
    }
}

if (Test-Path $targetDll) {
    $deployed = Get-Item $targetDll
    Write-Output ("DEPLOYED: " + $deployed.FullName + " -> " + $deployed.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') + " (" + $deployed.Length + " bytes)")
} else {
    Write-Warning "DV_TOOL_HTKT.dll was not found in $sourceDir. Please build the project first."
}
