@echo off
chcp 65001 >nul
echo ========================================================
echo   CẬP NHẬT TỰ ĐỘNG PHIÊN BẢN MỚI CHO ĐV_TOOL_HTKT (REVIT 2025)
echo ========================================================
echo.

:: Kiểm tra nếu Revit đang chạy thì nhắc tắt
tasklist /FI "IMAGENAME eq Revit.exe" 2>NUL | find /I /N "Revit.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [THÔNG BÁO] Autodesk Revit đang chạy và đang khóa file DLL.
    echo Vui lòng đóng Revit rồi nhấn phím bất kỳ để cập nhật file...
    pause >nul
)

:: Thực hiện copy DLL và Resource mới nhất
set TARGET_DIR=%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%TARGET_DIR%\Resources" mkdir "%TARGET_DIR%\Resources"

copy /Y "src\InfraBIM.CulvertTool\bin\Debug\net8.0-windows\*.dll" "%TARGET_DIR%\" >nul
copy /Y "src\InfraBIM.CulvertTool\bin\Debug\net8.0-windows\*.pdb" "%TARGET_DIR%\" >nul
copy /Y "src\InfraBIM.CulvertTool\Resources\*.*" "%TARGET_DIR%\Resources\" >nul
copy /Y "src\InfraBIM.CulvertTool\DV_TOOL_HTKT.addin" "%APPDATA%\Autodesk\Revit\Addins\2025\" >nul

if "%ERRORLEVEL%"=="0" (
    echo.
    echo [THÀNH CÔNG] Đã cập nhật xong bản build mới nhất của ĐV_TOOL_HTKT!
    echo Các tính năng mới: Cụm Family linh hoạt (cộng/trừ), Cống tròn đôi 2 nhánh, Dynamic BIM Parameters.
    echo Bây giờ bạn có thể mở lại Revit 2025 để sử dụng.
) else (
    echo.
    echo [LỖI] Chưa thể ghi đè file do Revit vẫn đang mở. Hãy tắt hẳn Revit rồi chạy lại file này nhé!
)

echo.
pause
