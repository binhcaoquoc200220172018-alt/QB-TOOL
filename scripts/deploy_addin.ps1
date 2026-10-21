$addinDir = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2025\DV_TOOL_HTKT'
$cand1 = 'src\InfraBIM.CulvertTool\bin\Release\net8.0-windows'
$cand2 = 'src\InfraBIM.CulvertTool\bin\x64\Release\net8.0-windows'

$sourceDir = $cand1
if (Test-Path (Join-Path $cand2 'DV_TOOL_HTKT.dll')) {
    if ((Get-Item (Join-Path $cand2 'DV_TOOL_HTKT.dll')).LastWriteTime -gt (Get-Item (Join-Path $cand1 'DV_TOOL_HTKT.dll')).LastWriteTime) {
        $sourceDir = $cand2
    }
}

if (!(Test-Path $addinDir)) {
    New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
}

# Clean old backup dlls if unlocked
Get-ChildItem -Path $addinDir -Filter '*.old' -ErrorAction SilentlyContinue | ForEach-Object {
    try { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue } catch {}
}

# Rename locked DV_TOOL_HTKT.dll so a new one can be placed
$targetDll = Join-Path $addinDir 'DV_TOOL_HTKT.dll'
if (Test-Path $targetDll) {
    try {
        $tempOld = Join-Path $addinDir ('DV_TOOL_HTKT.' + [System.Guid]::NewGuid().ToString('N') + '.old')
        Move-Item -Path $targetDll -Destination $tempOld -Force
    } catch {
        Write-Warning "Could not rename DV_TOOL_HTKT.dll: $_"
    }
}

# Copy newly built files
Copy-Item (Join-Path $sourceDir 'DV_TOOL_HTKT.dll') $targetDll -Force
Copy-Item (Join-Path $sourceDir 'DV_TOOL_HTKT.pdb') (Join-Path $addinDir 'DV_TOOL_HTKT.pdb') -Force

$deployed = Get-Item $targetDll
Write-Output ("DEPLOYED: " + $deployed.FullName + " -> " + $deployed.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') + " (" + $deployed.Length + " bytes)")
