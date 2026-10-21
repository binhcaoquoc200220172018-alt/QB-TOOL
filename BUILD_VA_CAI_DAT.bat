@echo off
echo ========================================================
echo    TU DONG BUILD VA CAI DAT ADD-IN VAO REVIT 2025
echo ========================================================
echo.

echo [1/2] Dang bien dich ma nguon (dotnet build Release)...
dotnet build "%~dp0src\InfraBIM.CulvertTool\InfraBIM.CulvertTool.csproj" -c Release
if %errorlevel% neq 0 (
    echo.
    echo [LOI] Qua trinh bien dich that bai! Vui long kiem tra ma loi o tren.
    pause
    exit /b %errorlevel%
)

echo.
echo [2/2] Dang trien khai Add-in vao thu muc Revit Addins 2025...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\deploy_addin.ps1"

echo.
echo ========================================================
echo [HOAN TAT] Add-in da duoc cai dat thanh cong vao Revit 2025!
echo Anh chi can mo Revit 2025 len la thay Tool tren thanh Ribbon.
echo (Neu Revit hoi Security Warning, hay chon "Always Load").
echo ========================================================
pause
