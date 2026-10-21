$pkgDir = 'C:\Users\ADMIN\Desktop\CAI_DAT_TOOL_HTKT_VE_NHA'
if (Test-Path $pkgDir) { Remove-Item $pkgDir -Recurse -Force }
New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null

$sourceAddins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2025'
Copy-Item (Join-Path $sourceAddins 'DV_TOOL_HTKT.addin') $pkgDir -Force

$destDllFolder = Join-Path $pkgDir 'DV_TOOL_HTKT'
New-Item -ItemType Directory -Path $destDllFolder -Force | Out-Null

Get-ChildItem (Join-Path $sourceAddins 'DV_TOOL_HTKT') | Where-Object { 
    $_.Extension -ne '.old' -and !$_.Name.Contains('_old_') 
} | ForEach-Object {
    Copy-Item $_.FullName $destDllFolder -Recurse -Force
}

$batLines = @(
    '@echo off',
    'chcp 65001 >nul',
    'echo ========================================================',
    'echo   CAI DAT DV TOOL HA TANG KY THUAT CHO REVIT 2025',
    'echo ========================================================',
    'echo.',
    'set "TARGET_DIR=%APPDATA%\Autodesk\Revit\Addins\2025"',
    'if not exist "%TARGET_DIR%" (',
    '    echo [THONG BAO] Chua tim thay thu muc Revit 2025, dang tu dong tao...',
    '    mkdir "%TARGET_DIR%"',
    ')',
    'echo Dang copy file cau hinh add-in...',
    'copy /Y "%~dp0DV_TOOL_HTKT.addin" "%TARGET_DIR%\" >nul',
    'echo Dang copy thu muc chuong trinh DV_TOOL_HTKT...',
    'if not exist "%TARGET_DIR%\DV_TOOL_HTKT" mkdir "%TARGET_DIR%\DV_TOOL_HTKT"',
    'xcopy /E /Y /I /Q "%~dp0DV_TOOL_HTKT\*" "%TARGET_DIR%\DV_TOOL_HTKT\" >nul',
    'echo.',
    'echo ========================================================',
    'echo [THANH CONG] Da cai dat xong Add-in DV_TOOL_HTKT!',
    'echo Anh chi can mo Revit 2025 len la thay Tool tren thanh Ribbon.',
    'echo (Neu Revit hoi "Security - Unsigned Add-In", chon "Always Load").',
    'echo ========================================================',
    'pause'
)

[System.IO.File]::WriteAllLines((Join-Path $pkgDir '1_CLICK_CAI_DAT.bat'), $batLines)

$zipPath = 'C:\Users\ADMIN\Desktop\CAI_DAT_TOOL_HTKT_VE_NHA.zip'
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$pkgDir\*" -DestinationPath $zipPath -Force

Write-Output ("PACKAGED: " + $zipPath + " -> " + (Get-Item $zipPath).Length + " bytes")
