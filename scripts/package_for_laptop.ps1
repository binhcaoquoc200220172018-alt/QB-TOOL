# ==============================================================================
# SCRIPT ĐÓNG GÓI TRỌN BỘ ADD-IN ĐV_TOOL_HTKT ĐỂ CÀI ĐẶT TRÊN MÁY TÍNH / LAPTOP Ở NHÀ
# ==============================================================================

$ErrorActionPreference = "Stop"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "   BẮT ĐẦU ĐÓNG GÓI ADD-IN ĐV_TOOL_HTKT (CỐNG NGANG, CKT & HỐ GA)    " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# 1. Biên dịch toàn bộ Solution ở chế độ Release
Write-Host "[1/6] Đang biên dịch Solution ToolHTKT.sln ở chế độ Release..." -ForegroundColor Yellow
$solutionPath = Join-Path $PSScriptRoot "..\ToolHTKT.sln"
dotnet build $solutionPath -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Biên dịch Solution thất bại! Vui lòng kiểm tra lỗi code trước khi đóng gói."
}
Write-Host " -> Biên dịch thành công 100%!" -ForegroundColor Green

# 2. Khởi tạo thư mục đóng gói trên Desktop
$desktopPath = [System.Environment]::GetFolderPath("Desktop")
$pkgDir = Join-Path $desktopPath "CAI_DAT_TOOL_HTKT_VE_NHA"
$zipPath = Join-Path $desktopPath "CAI_DAT_TOOL_HTKT_VE_NHA.zip"

Write-Host "[2/6] Đang dọn dẹp và tạo thư mục đóng gói: $pkgDir..." -ForegroundColor Yellow
if (Test-Path $pkgDir) {
    Remove-Item $pkgDir -Recurse -Force
}
New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null

# 3. Đóng gói Tool 1: Rải Cống Ngang & Cống Kỹ Thuật (DV_TOOL_HTKT)
Write-Host "[3/6] Đang đóng gói Tool Rải Cống Ngang & Cống Kỹ Thuật (DV_TOOL_HTKT)..." -ForegroundColor Yellow
$culvertBin = Join-Path $PSScriptRoot "..\src\InfraBIM.CulvertTool\bin\x64\Release\net8.0-windows"
$culvertAddin = Join-Path $PSScriptRoot "..\src\InfraBIM.CulvertTool\DV_TOOL_HTKT.addin"
$destCulvertFolder = Join-Path $pkgDir "DV_TOOL_HTKT"

Copy-Item $culvertAddin $pkgDir -Force
New-Item -ItemType Directory -Path $destCulvertFolder -Force | Out-Null

Get-ChildItem -Path $culvertBin | Where-Object { 
    $_.Extension -ne ".old" -and !$_.Name.Contains("_old_") -and $_.Name -ne "DV_TOOL_HTKT.addin"
} | ForEach-Object {
    Copy-Item $_.FullName $destCulvertFolder -Recurse -Force
}

# Sao chép cấu hình LastSessionConfig nếu có
$appDataCulvert = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\UserConfig"
if (Test-Path $appDataCulvert) {
    $destUserConfig = Join-Path $destCulvertFolder "UserConfig"
    New-Item -ItemType Directory -Path $destUserConfig -Force | Out-Null
    Copy-Item "$appDataCulvert\*" $destUserConfig -Recurse -Force -ErrorAction SilentlyContinue
}

# 4. Đóng gói Tool 2: Rải Hố Ga (DV_TOOL_HO_GA)
Write-Host "[4/6] Đang đóng gói Tool Rải Hố Ga (DV_TOOL_HO_GA)..." -ForegroundColor Yellow
$manholeBin = Join-Path $PSScriptRoot "..\src\InfraBIM.ManholeTool\bin\x64\Release\net8.0-windows"
$manholeAddin = Join-Path $PSScriptRoot "..\src\InfraBIM.ManholeTool\DV_TOOL_HO_GA.addin"
$destManholeFolder = Join-Path $pkgDir "DV_TOOL_HO_GA"

Copy-Item $manholeAddin $pkgDir -Force
New-Item -ItemType Directory -Path $destManholeFolder -Force | Out-Null

Get-ChildItem -Path $manholeBin | Where-Object { 
    $_.Extension -ne ".old" -and !$_.Name.Contains("_old_") -and $_.Name -ne "DV_TOOL_HO_GA.addin"
} | ForEach-Object {
    Copy-Item $_.FullName $destManholeFolder -Recurse -Force
}

# 5. Đóng gói Dữ Liệu Excel Mẫu
Write-Host "[5/6] Đang sao chép file Excel dữ liệu mẫu..." -ForegroundColor Yellow
$destSamples = Join-Path $pkgDir "Du_Lieu_Mau"
New-Item -ItemType Directory -Path $destSamples -Force | Out-Null

$sampleFiles = @(
    (Join-Path $PSScriptRoot "..\Mau_Du_Lieu_Cong_Ngang.xlsx"),
    (Join-Path $PSScriptRoot "..\Bang_Du_Lieu_Cong_Ngang_Mau.xlsx"),
    (Join-Path $PSScriptRoot "..\Mau_Du_Lieu_Ho_Ga.xlsx")
)
foreach ($f in $sampleFiles) {
    if (Test-Path $f) {
        Copy-Item $f $destSamples -Force
    }
}

# 6. Tạo file Cài đặt 1-Click (1_CLICK_CAI_DAT.bat)
$batInstall = @(
    '@echo off',
    'chcp 65001 >nul',
    'title CAI DAT TRON BO ADD-IN DV_TOOL_HTKT - REVIT 2025/2026',
    'echo ======================================================================',
    'echo       CÀI ĐẶT TỰ ĐỘNG TRỌN BỘ ADD-IN ĐV_TOOL_HTKT VÀO REVIT',
    'echo   Bao gồm: 1. Rải Cống Ngang ^& Cống Kỹ Thuật  -  2. Rải Hố Ga',
    'echo ======================================================================',
    'echo.',
    'set "TARGET_2025=%APPDATA%\Autodesk\Revit\Addins\2025"',
    'set "TARGET_2026=%APPDATA%\Autodesk\Revit\Addins\2026"',
    'echo [*] Đang kiểm tra thư mục Add-in Revit trên máy của bạn...',
    'echo.',
    '',
    'REM --- CÀI ĐẶT CHO REVIT 2025 (Mặc định) ---',
    'if not exist "%TARGET_2025%" (',
    '    echo [*] Thư mục Revit 2025 chưa có, đang tự động tạo: %TARGET_2025%',
    '    mkdir "%TARGET_2025%" 2>nul',
    ')',
    'echo [1/2] Đang sao chép Tool Rải Cống Ngang ^& Cống Kỹ Thuật vào Revit 2025...',
    'copy /y "%~dp0DV_TOOL_HTKT.addin" "%TARGET_2025%\" >nul',
    'if exist "%TARGET_2025%\DV_TOOL_HTKT" rmdir /s /q "%TARGET_2025%\DV_TOOL_HTKT" 2>nul',
    'xcopy /e /i /y /q "%~dp0DV_TOOL_HTKT" "%TARGET_2025%\DV_TOOL_HTKT\" >nul',
    '',
    'echo [2/2] Đang sao chép Tool Rải Hố Ga vào Revit 2025...',
    'copy /y "%~dp0DV_TOOL_HO_GA.addin" "%TARGET_2025%\" >nul',
    'if exist "%TARGET_2025%\DV_TOOL_HO_GA" rmdir /s /q "%TARGET_2025%\DV_TOOL_HO_GA" 2>nul',
    'xcopy /e /i /y /q "%~dp0DV_TOOL_HO_GA" "%TARGET_2025%\DV_TOOL_HO_GA\" >nul',
    '',
    'if %errorlevel% equ 0 (',
    '    echo.',
    '    echo [THÀNH CÔNG] Đã cài đặt xong trọn bộ Add-in vào Revit 2025!',
    ') else (',
    '    echo.',
    '    echo [LƯU Ý] Quá trình sao chép có thể gặp trục trặc nếu Revit đang mở. Vui lòng tắt Revit rồi chạy lại file này.',
    ')',
    '',
    'REM --- CÀI ĐẶT BỔ SUNG CHO REVIT 2026 NẾU CÓ ---',
    'if exist "%TARGET_2026%" (',
    '    echo.',
    '    echo [*] Phát hiện có thư mục Revit 2026. Đang cài đặt bổ sung cho Revit 2026...',
    '    copy /y "%~dp0DV_TOOL_HTKT.addin" "%TARGET_2026%\" >nul',
    '    if exist "%TARGET_2026%\DV_TOOL_HTKT" rmdir /s /q "%TARGET_2026%\DV_TOOL_HTKT" 2>nul',
    '    xcopy /e /i /y /q "%~dp0DV_TOOL_HTKT" "%TARGET_2026%\DV_TOOL_HTKT\" >nul',
    '    copy /y "%~dp0DV_TOOL_HO_GA.addin" "%TARGET_2026%\" >nul',
    '    if exist "%TARGET_2026%\DV_TOOL_HO_GA" rmdir /s /q "%TARGET_2026%\DV_TOOL_HO_GA" 2>nul',
    '    xcopy /e /i /y /q "%~dp0DV_TOOL_HO_GA" "%TARGET_2026%\DV_TOOL_HO_GA\" >nul',
    '    echo [THÀNH CÔNG] Đã cài đặt thêm cho Revit 2026!',
    ')',
    '',
    'echo.',
    'echo ======================================================================',
    'echo [HƯỚNG DẪN MỞ VÀ SỬ DỤNG TRÊN REVIT]:',
    'echo 1. Khởi động Revit 2025 (hoặc Revit 2026).',
    'echo 2. Nếu Revit hiện hộp thoại cảnh báo: "Security - Unsigned Add-In",',
    'echo    anh hãy bấm chọn nút: "Always Load" (Luôn luôn tải).',
    'echo 3. Mở file Revit dự án, nhìn lên thanh Ribbon sẽ thấy Tab: "ĐV_TOOL_HTKT"',
    'echo    - Nhóm "TNN - Cống Ngang": Nút "Rải Cống Ngang" (hỗ trợ Cống tròn, Cống hộp, CKT).',
    'echo    - Nhóm "TNN - Hố Ga":      Nút "Rải Hố Ga" (hỗ trợ rải hố ga tuyến trái/phải).',
    'echo 4. File Excel dữ liệu mẫu nằm trong thư mục "Du_Lieu_Mau".',
    'echo ======================================================================',
    'echo.',
    'pause'
)
[System.IO.File]::WriteAllLines((Join-Path $pkgDir "1_CLICK_CAI_DAT.bat"), $batInstall, [System.Text.Encoding]::UTF8)

# 7. Tạo file Gỡ Cài Đặt (GO_CAI_DAT.bat)
$batUninstall = @(
    '@echo off',
    'chcp 65001 >nul',
    'title GO CAI DAT TRON BO ADD-IN DV_TOOL_HTKT',
    'echo ======================================================================',
    'echo             GỠ CÀI ĐẶT ADD-IN ĐV_TOOL_HTKT KHỎI REVIT',
    'echo ======================================================================',
    'echo.',
    'set /p confirm="Anh có chắc chắn muốn gỡ bỏ Add-in ĐV_TOOL_HTKT khỏi Revit không? (Y/N): "',
    'if /i not "%confirm%"=="Y" (',
    '    echo Đã hủy thao tác gỡ cài đặt.',
    '    pause',
    '    exit /b',
    ')',
    '',
    'REM Go khoi Revit 2025',
    'if exist "%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT.addin" del /f /q "%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT.addin"',
    'if exist "%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HO_GA.addin" del /f /q "%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HO_GA.addin"',
    'if exist "%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT" rmdir /s /q "%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT"',
    'if exist "%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HO_GA" rmdir /s /q "%APPDATA%\Autodesk\Revit\Addins\2025\DV_TOOL_HO_GA"',
    '',
    'REM Go khoi Revit 2026',
    'if exist "%APPDATA%\Autodesk\Revit\Addins\2026\DV_TOOL_HTKT.addin" del /f /q "%APPDATA%\Autodesk\Revit\Addins\2026\DV_TOOL_HTKT.addin"',
    'if exist "%APPDATA%\Autodesk\Revit\Addins\2026\DV_TOOL_HO_GA.addin" del /f /q "%APPDATA%\Autodesk\Revit\Addins\2026\DV_TOOL_HO_GA.addin"',
    'if exist "%APPDATA%\Autodesk\Revit\Addins\2026\DV_TOOL_HTKT" rmdir /s /q "%APPDATA%\Autodesk\Revit\Addins\2026\DV_TOOL_HTKT"',
    'if exist "%APPDATA%\Autodesk\Revit\Addins\2026\DV_TOOL_HO_GA" rmdir /s /q "%APPDATA%\Autodesk\Revit\Addins\2026\DV_TOOL_HO_GA"',
    '',
    'echo.',
    'echo [HOÀN TẤT] Đã gỡ bỏ sạch sẽ Add-in khỏi Revit!',
    'pause'
)
[System.IO.File]::WriteAllLines((Join-Path $pkgDir "GO_CAI_DAT.bat"), $batUninstall, [System.Text.Encoding]::UTF8)

# 8. Tạo file Hướng dẫn sử dụng & cài đặt chi tiết (HUONG_DAN_CAI_DAT.txt)
$guideContent = @"
========================================================================================
            HƯỚNG DẪN CÀI ĐẶT & SỬ DỤNG BỘ ADD-IN ĐV_TOOL_HTKT CHO REVIT 2025/2026
========================================================================================

I. GIỚI THIỆU BỘ CÔNG CỤ:
   Bộ Add-in chuyên dụng cho Kỹ sư Hạ tầng Kỹ thuật (Thoát nước mưa, Thoát nước thải,
   Giao thông, Cống ngang đường, Cống kỹ thuật đúc sẵn & đổ tại chỗ, Hố ga thu nước).
   
   Bao gồm 2 Module tích hợp trên cùng 1 Tab Ribbon "ĐV_TOOL_HTKT":
   1. "Rải Cống Ngang":
      - Tự động dựng mô hình cống tròn, cống hộp đúc sẵn, cống đổ tại chỗ.
      - Chuyên biệt chế độ Cống Kỹ Thuật (CKT) có hố kỹ thuật & hộp nối cống dọc.
      - Tùy biến tham số hình học theo từng dải lý trình, gán vật liệu và BIM Metadata chuẩn.
   2. "Rải Hố Ga":
      - Dựng tự động hố ga theo 2 tuyến (HO GA TRAI, HO GA PHAI).
      - Xoay 3D 360 độ kiểm tra cao độ Đỉnh, Đáy, chiều cao thân H trước khi dựng.
      - Quét tham số Family tự nhiên, gán thông tin phi hình học & chia sẻ thông số con.

----------------------------------------------------------------------------------------
II. CÁCH CÀI ĐẶT TRÊN MÁY TÍNH / LAPTOP Ở NHÀ:

   BƯỚC 1: Sao chép file "CAI_DAT_TOOL_HTKT_VE_NHA.zip" vào máy tính của anh (qua USB,
           Google Drive, Zalo...).
   
   BƯỚC 2: Giải nén file zip ra một thư mục bất kỳ (ví dụ: Desktop hoặc ổ D:\).
   
   BƯỚC 3: Nhấp đúp chuột vào file "1_CLICK_CAI_DAT.bat" để cài đặt tự động.
           - File sẽ tự động nhận diện và copy toàn bộ thư viện vào thư mục Addins của Revit.
           - KHÔNG CẦN quyền Administrator, chạy cực nhanh trong 1 giây.
   
   BƯỚC 4: Mở Revit 2025 (hoặc Revit 2026) lên.
           - Khi Revit xuất hiện bảng hỏi bảo mật "Security - Unsigned Add-In",
             anh hãy bấm: "Always Load" (Luôn luôn nạp).
           - Mở một dự án bất kỳ, anh sẽ thấy Tab "ĐV_TOOL_HTKT" xuất hiện trên Ribbon!

----------------------------------------------------------------------------------------
III. CẤU TRÚC FILE TRONG BỘ CÀI:
   ├── 1_CLICK_CAI_DAT.bat      : File cài đặt tự động 1 chạm vào Revit
   ├── GO_CAI_DAT.bat           : File gỡ bỏ Add-in sạch sẽ khỏi Revit khi cần
   ├── HUONG_DAN_CAI_DAT.txt    : Tài liệu hướng dẫn này
   ├── DV_TOOL_HTKT.addin       : File manifest đăng ký Cống Ngang với Revit
   ├── DV_TOOL_HO_GA.addin      : File manifest đăng ký Hố Ga với Revit
   ├── DV_TOOL_HTKT/            : Thư mục chứa mã nguồn biên dịch Cống Ngang (.dll, resources)
   ├── DV_TOOL_HO_GA/           : Thư mục chứa mã nguồn biên dịch Hố Ga (.dll, resources)
   └── Du_Lieu_Mau/             : Các file Excel mẫu chuẩn trắc dọc để nạp vào Tool thử nghiệm
       ├── Mau_Du_Lieu_Cong_Ngang.xlsx
       ├── Bang_Du_Lieu_Cong_Ngang_Mau.xlsx
       └── Mau_Du_Lieu_Ho_Ga.xlsx

----------------------------------------------------------------------------------------
IV. LƯU Ý KỸ THUẬT QUAN TRỌNG:
   - Add-in chạy trên nền .NET 8 (chuẩn của Autodesk Revit 2025 và 2026).
   - Nếu anh mở Revit mà không thấy Tool, hãy kiểm tra chắc chắn đã bấm "Always Load".
   - Nếu muốn cập nhật phiên bản mới, chỉ cần chạy lại file "1_CLICK_CAI_DAT.bat"
     (lưu ý tắt Revit trước khi cài để không bị khóa file .dll).

========================================================================================
"@
[System.IO.File]::WriteAllText((Join-Path $pkgDir "HUONG_DAN_CAI_DAT.txt"), $guideContent, [System.Text.Encoding]::UTF8)

# 9. Nén toàn bộ thư mục thành file ZIP
Write-Host "[6/6] Đang nén thư mục thành file ZIP: $zipPath..." -ForegroundColor Yellow
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$pkgDir\*" -DestinationPath $zipPath -Force

$zipItem = Get-Item $zipPath
$zipSizeMb = [Math]::Round($zipItem.Length / 1MB, 2)
Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "   ĐÓNG GÓI HOÀN TẤT THÀNH CÔNG!" -ForegroundColor Green
Write-Host "   - File nén cài đặt: $zipPath" -ForegroundColor Green
Write-Host "   - Dung lượng: $zipSizeMb MB ($($zipItem.Length) bytes)" -ForegroundColor Green
Write-Host "   - Thư mục giải nén: $pkgDir" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
