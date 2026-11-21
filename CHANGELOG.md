# 📝 NHẬT KÝ THAY ĐỔI & TIẾN ĐỘ DỰ ÁN (CHANGELOG)

Tài liệu này ghi lại toàn bộ lịch sử phát triển, cập nhật tính năng, tối ưu hóa mã nguồn và trạng thái làm việc của bộ Add-in Revit Hạ tầng Kỹ thuật (Tool Rải Hố Ga & Tool Rải Cống Ngang).

---

## 🌟 [2026-11-20] - Hoàn Thiện Rải Cống Tròn, Mối Nối Hiện Trạng Cách Vô 0.4m, Cut Geometry Tự Động & Đóng Gói Bộ Cài Cống Ngang + Hố Ga

### 🎯 Yêu cầu & Quyết định kỹ thuật (Người dùng đã "CHỐT"):
1. **Thuật toán rải thân cống tròn (đơn & đôi):**
   - Thân cống tròn rải chuẩn xác 100% theo đúng 2 điểm tọa độ $P_1 \to P_2$ từ Excel, chạy xuyên suốt không bị cắt ngắn hay thụt vào.
   - Thêm nút đảo đầu âm/dương cống tròn: cho phép đảo ngược đầu âm (Adaptive Point 1) và đầu dương (Adaptive Point 2) của các đốt cống tròn.
2. **Mối nối cống tròn hiện trạng (`TNN_CT_CX_MOI NOI_LOAI 1 & 2`):**
   - Điểm 2 Adaptive đặt tại điểm Excel ($P_2 = P_{\text{Excel}} + \vec{z}$).
   - Điểm 1 Adaptive cách vô trong đúng khoảng cách khai báo ($P_1 = P_2 + \vec{u}_{\text{in}} \times \text{OffsetMoiNoi} + \vec{z}$).
   - Đo khoảng cách từ mép cống đến Điểm 1 chuẩn xác 400mm (0.4m), tham số chiều dài $L = 0.40\text{m}$.
   - Bê tông lót & Đá dăm đệm rải tới tiếp xúc đúng Điểm 1 của mối nối thì dừng lại, ôm khít mặt trong mối nối.
3. **Cắt hình học (Cut Geometry) tự động toàn diện:**
   - Cắt tự động giữa tường đầu cửa xả (`TNN_CX_TUONG DAU`) với thân cống tròn gần nhất qua `InstanceVoidCutUtils` & `SolidSolidCutUtils`.
   - Cắt tự động giữa BTL và toàn bộ các đốt cống tròn tiếp xúc.
   - Cắt tự động giữa đai mối nối hiện trạng và các đốt cống tròn tiếp xúc.
4. **Đồng bộ Review 3D trực quan:**
   - 3D review cập nhật hiển thị chính xác thân cống từ $P_1$ đến $P_2$, mối nối thụt vào đúng vị trí và BTL ngắt tại mối nối.
5. **Đóng gói trọn bộ mang về nhà cài đặt:**
   - Đóng gói toàn bộ cả 2 công cụ: Tool Rải Cống Ngang (`DV_TOOL_HTKT`) và Tool Rải Hố Ga (`DV_TOOL_HO_GA`).
   - Tạo file cài đặt tự động 1-click `CAI_DAT_TU_DONG.bat` và file hướng dẫn `HUONG_DAN_CAI_DAT.txt`.
   - Xuất file nén `CAI_DAT_TOOL_HTKT_VA_HO_GA_REVIT_2025.zip` ra Desktop.

---

## 🌟 [2026-11-20] - Sắp Xếp Nhóm Dimensions Lên Đầu Tiên, Tiếp Theo Là Other & Bảo Toàn 100% Thứ Tự Tự Nhiên Của Family Revit (CT_D Đứng Đầu)

### 🎯 Yêu cầu & Quyết định kỹ thuật (Người dùng đã "CHỐT"):
1. **Sắp xếp thứ tự nhóm Parameter tại Tab 02 (Parameter Hình học):**
   - **Ưu tiên nhóm Dimensions lên đầu tiên:** Toàn bộ các tham số thuộc nhóm `Dimensions (Kích thước)` luôn được đưa lên các dòng đầu tiên của bảng tham số (Khung 1 & Khung 2).
   - **Nhóm Other đặt ngay phía sau:** Các tham số thuộc nhóm `Other (Khác)` (bao gồm các tham số kích thước phụ, dữ liệu hoặc Yes/No ẩn hiện) được xếp ngay sau nhóm Dimensions.
   - **Bảo tồn 100% thứ tự tương đối tự nhiên trong Family:** Sử dụng thuật toán sắp xếp ổn định (`Stable Sort` qua LINQ `OrderBy`) kết hợp trích xuất chính xác từ Revit. Tuyệt đối không xáo trộn thứ tự bên trong từng nhóm, giữ nguyên 100% vị trí ban đầu như kỹ sư đã thiết kế trong Family (`CT_D` đứng đầu tiên tại STT 1, tiếp đến `CT_T`, `CT_Joint_Thickness`, `L`...).
   - **Nhận diện nhóm Revit Group chuẩn xác:** Sửa triệt để logic nhận diện nhóm từ Revit API `Definition.GetGroupTypeId()`, ưu tiên nhận diện `Dimensions` và `Other` trực tiếp từ Family gốc thay vì tự động ép kiểu dựa trên StorageType.

2. **Bảo tồn 100% vị trí biến tự nhiên & Tránh đá biến giữa các cấu kiện:**
   - Đọc trực tiếp từ Family Definition (`famDoc.FamilyManager.GetParameters()`) và lưu bộ nhớ đệm `_familyParamCache` để giao diện phản hồi tức thì (0 ms).
   - Danh mục Fallback chuẩn hóa cho các cấu kiện cống tròn:
     * `TNN_CT_THAN CONG` (14 tham số chuẩn): `CT_D` (STT 1), `CT_T` (STT 2), `CT_Joint_Thickness` (STT 3), `L` (STT 4), `CT_D_Outer_1` (STT 5), `CT_D_Inter_0` (STT 6), `CT_X1` (STT 7), `CT_X2` (STT 8), `CT_X3` (STT 9), `CT_X4` (STT 10), `CT_A` (STT 11), `CT_R` (STT 12), `x` (STT 13), `A1` (STT 14).
     * `TNN_CT_BE TONG LOT` (8 tham số chuẩn): `B_BTL`, `H1_BTL`, `B VAC`, `H VAC`, `L1`, `L2`, `H`, `H_BTL`.
     * `TNN_CT_DA DAM DEM` (5 tham số chuẩn): `B_DDD`, `H_DDD`, `L1`, `L2`, `H`.
     * `TNN_CT_CX_MOI NOI_LOAI 1 & 2` (5 tham số chuẩn): `D`, `T`, `B`, `H`, `L`.

3. **Biên dịch & Đóng gói triển khai:**
   - Biên dịch `ToolHTKT.sln -c Release` thành công 100% (**0 Error**, 178 Warnings).
   - Đã chép DLL mới nhất vào `%AppData%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`.
   - Đã cập nhật file nén cài đặt hoàn chỉnh trên Desktop: [CAI_DAT_DV_TOOL_HTKT_REVIT_2025.zip](file:///C:/Users/ADMIN/Desktop/CAI_DAT_DV_TOOL_HTKT_REVIT_2025.zip) (dung lượng 3.04 MB).

---

## 🌟 [2026-11-20] - Bảo Tồn 100% Vị Trí Biến Tự Nhiên Của Family, Sửa Triệt Để Lỗi Rỗng Parameter Tab 02 & Đồng Bộ Khung 1 - Khung 2

### 🎯 Yêu cầu & Quyết định kỹ thuật (Người dùng đã "CHỐT"):
1. **Bảo tồn 100% thứ tự vị trí biến tự nhiên của Family trong Revit (Tuyệt đối không đảo lộn vị trí biến):**
   - **Đọc thứ tự gốc từ Family Definition:** Kích hoạt lại việc trích xuất tham số từ `famDoc.FamilyManager.GetParameters()` thông qua `doc.EditFamily(sym.Family)` và lưu vào bộ nhớ đệm `_familyParamCache`. Thứ tự tham số trong Revit Family Types dialog được bảo toàn 100% nguyên vẹn.
   - **Tối ưu tốc độ giao diện:** Nhờ cơ chế cache `_familyParamCache`, `doc.EditFamily` chỉ đọc một lần duy nhất khi nạp, các lần chuyển tab hay chuyển cấu kiện sau đó trả về tức thì (0 ms), không gây đơ hoặc giật lag giao diện.
   - **Chuẩn hóa danh mục thứ tự Fallback:**
     * `TNN_CT_THAN CONG` (14 tham số chuẩn): `CT_D`, `CT_T`, `CT_Joint_Thickness`, `L`, `CT_D_Outer_1`, `CT_D_Inter_0`, `CT_X1`, `CT_X2`, `CT_X3`, `CT_X4`, `CT_A`, `CT_R`, `x`, `A1`.
     * `TNN_CT_BE TONG LOT` (8 tham số chuẩn): `B_BTL`, `H1_BTL`, `B VAC`, `H VAC`, `L1`, `L2`, `H`, `H_BTL`.
     * `TNN_CT_DA DAM DEM` (5 tham số chuẩn): `B_DDD`, `H_DDD`, `L1`, `L2`, `H`.
     * `TNN_CT_CX_MOI NOI_LOAI 1 & 2` (5 tham số chuẩn): `D`, `T`, `B`, `H`, `L` (Tách biệt hoàn toàn khỏi Cửa Xả, không bị gán nhầm 33 thông số của Cửa Xả).

2. **Khắc phục triệt để lỗi không hiển thị Parameter ở Khung 1 của Tab 02:**
   - **Cache Document an toàn:** Khắc phục tình trạng `_uiApp.ActiveUIDocument` trả về `null` tạm thời khi tương tác WPF modal dialog bằng cách lưu `_cachedDoc` trong `MainViewModel`.
   - **Tối ưu hóa bộ lọc `FilterParameterMappings`:** Khi cấu kiện được chọn, bảng tham số Khung 1 lọc theo `FamilyName`. Nếu cùng 1 Family được dùng cho nhiều cấu kiện (ví dụ `Cống tròn đơn` và `Cống tròn đôi`), ưu tiên khớp `CategoryName` và `ProfileName`, nếu chưa có sẽ tự động quét tức thời qua `EnsureComponentParametersScanned` để bảng luôn hiển thị đầy đủ tham số, không bao giờ bị rỗng.
   - **Đồng bộ hoàn hảo Khung 1 và Khung 2:** Đảm bảo toàn bộ 14 tham số của thân cống tròn đôi hiển thị song song và khớp chuẩn 100% giữa Khung 1 (Áp dụng hàng loạt) và Khung 2 (Tùy biến theo từng lý trình).

3. **Biên dịch & Đóng gói:**
   - Đã biên dịch `ToolHTKT.sln -c Release` thành công 100% (0 Error, 178 Warnings).
   - Đã cập nhật DLL mới nhất vào `%AppData%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`.
   - Đã đóng gói bộ cài mới nhất ra Desktop: `C:\Users\ADMIN\Desktop\CAI_DAT_DV_TOOL_HTKT_REVIT_2025.zip` (dung lượng ~3.04 MB).

---

## 🌟 [2026-11-18] - Fix Khóa Xoay Mặt Đứng (Chỉ Zoom & Pan), Cố Định Tag Cao Độ Chuẩn Xác & Nhận Diện 100% Mô Hình Revit Thực Tế Khi Mở Lại File

### 🎯 Yêu cầu & Quyết định kỹ thuật (Người dùng đã "CHỐT"):
1. **Khóa cứng xoay 3D (Orbit) khi ở Mặt Đứng (Front View - `[🏠 ĐỨNG]`):**
   - Khi ở góc nhìn Mặt Đứng, thao tác chuột **chỉ cho phép Zoom In / Zoom Out (cuộn chuột) và Pan (kéo rê chuột trái / chuột giữa / chuột phải)** để di chuyển khung nhìn kỹ thuật 2D phẳng.
   - **Tuyệt đối không cho xoay (No 3D Orbit)** khi đang ở Mặt Đứng, triệt tiêu hoàn toàn tình trạng vô tình chạm chuột làm xoay lệch góc nhìn.
   - Khi muốn xoay 3D trở lại, người dùng chỉ cần bấm nút **`[🏠 3D ISO]`**.
   - Cập nhật dòng HUD trực quan: `"📐 Mặt đứng chiếu ngang (Front View) - Khóa xoay | Cuộn: Zoom | Kéo chuột: Pan"`.

2. **Cố định vị trí Tag Cao Độ, không bị lơ lửng, định vị chuẩn xác 100% trên bề mặt cấu kiện:**
   - **Triệt tiêu sai số thị sai (Parallax Zero):** Toàn bộ điểm neo 3D của các Tag cao độ được đặt cố định trên mặt phẳng tim cống $Z = 0.0$ (thay vì lơ lửng ở $Z$ lệch ngoài), giúp các mốc cao độ bám dính chắc chắn, không bị trôi hay trượt khi Zoom/Pan.
   - **Chuẩn hóa cao độ $Y$ neo thực tế:**
     * Đáy cống Thượng lưu $Z_1$: Neo đúng đáy lòng cống tại $X = 0, Y = -L_{\text{Ngầm}}, Z = 0$.
     * Đáy cống Hạ lưu $Z_2$: Neo đúng đáy lòng cống tại $X = L_{\text{tổng}}, Y = -Drop(L) - L_{\text{Ngầm}}, Z = 0$.
     * Điểm 1 Sân gia cố: Neo chính xác mặt trên sân cống/sân gia cố tại $X = -3.46\text{m}$ (TL) và $X = L + 3.46\text{m}$ (HL).
     * Điểm 2 Sân gia cố: Neo chính xác đỉnh dầm chân khay tại $X = -6.46\text{m}$ (TL) và $X = L + 6.46\text{m}$ (HL) theo cao độ $Z_{\text{SGC}}$ thực tế.

3. **Tự động nhận diện và trích xuất hình học mô hình thực tế từ Revit khi mở lại file:**
   - **Ưu tiên hàng đầu (Revit Document Extraction):** Trong `ExtractRealFamilyGeometryForPreview`, Add-in ưu tiên trích xuất trực tiếp 100% Solids/Meshes của các cấu kiện thực tế đã mô hình hóa trong file Revit (tìm kiếm theo `Mark: CN-{STT}` và `Comments: Cống {STT}` cũng như vùng tọa độ), thay vì chỉ chạy transaction ảo.
   - Khi mở lại file Revit đã rải cống trước đó, Add-in tự động nhận diện và hiển thị chính xác toàn bộ các đốt cống, hộp nối, cửa xả, sân gia cố thực tế đã rải trong 3D Preview.
   - Khi chưa bấm chạy mô hình, Add-in tự động fallback sang cơ chế dựng ảo để người dùng xem trước hình dạng Family.

4. **Trạng thái kiểm tra & Triển khai:**
   - **Kiểm tra trực tiếp qua Revit MCP (`765t-flow`):** Đã kết nối kiểm tra 67 cấu kiện của cống `CN-1` (Cống Km 1+000) trên Document `HTKT_XREF BINH DO CHUAN`, toàn bộ cấu kiện hợp lệ (`READ_SUCCEEDED`, không có lỗi phát sinh).
   - Đã biên dịch Solution `ToolHTKT.sln` đạt **0 Error**.
   - Đã chép DLL mới nhất (518,144 bytes) vào `%AppData%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`.

---

## 🌟 [2026-11-18] - Tối Ưu Hóa Trực Quan 3D Review: Tag Dim Cao Độ Độc Quyền Mặt Đứng (Front View) & Chống Đè Chữ 100%

### 🎯 Yêu cầu & Quyết định kỹ thuật (Người dùng đã "CHỐT"):
1. **Chỉ hiển thị Tag Dim cao độ khi chọn Mặt Đứng (Front View - `[🏠 ĐỨNG]`):**
   - Khi ở góc nhìn 3D ISO, Mặt Bằng (Top), Nhìn Phải (Right), hoặc xoay tự do (Orbit 360°): Toàn bộ thước đo, đường gióng và nhãn cao độ được **tự động ẩn hoàn toàn**. Mô hình 3D thông thoáng 100%, không bị rối mắt hay che khuất hình học thực tế.
   - Khi người dùng bấm nút **`[🏠 ĐỨNG]`** (Front View): Add-in tự động chuyển sang chế độ trắc dọc kỹ thuật phẳng ($yaw = 0^\circ, pitch = 0^\circ$) và hiển thị đầy đủ các Tag Dim cao độ sắc nét dạng 2D Canvas Overlay.
2. **Loại bỏ hoàn toàn lỗi đè chữ & chập nhãn (100% Collision-Free Staggered Layout):**
   - **Phân tầng độ cao so le (Staggered tiers):**
     * **Điểm 1 Sân gia cố:** Gióng lên **TẦNG CAO** (cột gióng 80px) nhãn Sky Blue neo tại mép tiếp giáp sân cống.
     * **Điểm 2 Sân gia cố:** Gióng ở **TẦNG THẤP** (cột gióng 45px) nhãn Amber neo tại dầm chân khay.
     * **Đáy cống $Z_1, Z_2$:** Gióng hướng vào **LÒNG CỐNG** (align trái ở HL, align phải ở TL), tách biệt hoàn toàn khỏi Sân gia cố.
     * **Độ dốc Sân gia cố $\Delta Z$:** Đặt thẻ compact màu Emerald **PHÍA DƯỚI BẢN ĐÁY** ($rodHeightPx = -35px$), công thức dốc chuẩn $i = \frac{|\Delta Z|}{L_{\text{SGC}}} \times 100\%$ (khớp CAD).
   - **Loại bỏ khối 3D Solid Poles:** Không vẽ cột khối 3D trong không gian để triệt tiêu hiện tượng các thanh cột nổi lơ lửng khi xoay 3D.
3. **Trạng thái triển khai:**
   - Đã cập nhật file [Culvert3DViewportControl.cs](file:///c:/Users/ADMIN/Desktop/PHAT%20TRIEN%20TOOL_HTKT/src/InfraBIM.CulvertTool/Views/Culvert3DViewportControl.cs).
   - Đã biên dịch Solution `ToolHTKT.sln` đạt **0 Error**.
   - Đã xuất bản và nạp DLL vào thư mục Add-in Revit 2025: `%AppData%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`.

---

## 🌟 [2026-11-18] - Nâng Cấp 3D Viewport: Điều Khiển Chuẩn Autodesk Revit & Tag Dim Cao Độ Trực Quan

### 🎯 Yêu cầu & Quyết định kỹ thuật (Người dùng đã "CHỐT"):
1. **Thao tác chuột chuẩn 100% Autodesk Revit trong khung nhìn 3D Review:**
   - **Pan (Di chuyển khung nhìn):** Giữ **Nút chuột giữa (Wheel)** và kéo rê.
   - **Orbit (Xoay 3D 360°):** Giữ **Shift + Nút chuột giữa (Shift + Wheel)** và kéo xoay quanh tâm cống.
   - **Zoom In / Out:** Cuộn bánh xe chuột (Mouse Wheel).
   - **Zoom to Fit (Xem toàn bộ):** Nhấp đúp **Nút chuột giữa (Double-click Wheel)**.
   - Hỗ trợ thêm kéo chuột trái để xoay 3D (dành cho người dùng có chuột giữa cứng).
2. **Hệ thống Tag / Dim cao độ trực quan trong không gian 3D (Cống ngang):**
   - **Tại Sân Gia Cố (Thượng lưu & Hạ lưu):**
     + Tag Điểm 1 (Mép tiếp giáp Sân cống): Hiển thị cao độ $Z_{\text{SGC1}} = Z_1$ (TL) / $Z_2$ (HL).
     + Tag Điểm 2 (Mép dầm chân khay): Hiển thị cao độ $Z_{\text{SGC2}} = Z_{\text{SGC TL}}$ / $Z_{\text{SGC HL}}$ kèm độ hạ.
     + Thước đo độ dốc $\Delta Z$: Hiển thị độ chênh cao $\Delta Z = -0.17\text{ m}$ và độ dốc $i\%$ khớp bản vẽ CAD.
   - **Tại Sân Cống & Đáy Cống:**
     + Tag mốc cao độ đáy trong tại 2 đầu cửa vào/ra ($Z_1$ Thượng lưu, $Z_2$ Hạ lưu).
     + Tag cao độ bản đáy sân cống.
   - **Nút Bật / Tắt `[📏 DIM CAO ĐỘ]`:** Tích hợp trực tiếp trên cả HUD của 3D Viewport và thanh công cụ cửa sổ 3D Preview để kỹ sư tùy ý bật/tắt.
3. **Các file mã nguồn đã cập nhật & triển khai:**
   - [Culvert3DViewportControl.cs](file:///c:/Users/ADMIN/Desktop/PHAT%20TRIEN%20TOOL_HTKT/src/InfraBIM.CulvertTool/Views/Culvert3DViewportControl.cs)
   - [Culvert3DPreviewWindow.xaml](file:///c:/Users/ADMIN/Desktop/PHAT%20TRIEN%20TOOL_HTKT/src/InfraBIM.CulvertTool/Views/Culvert3DPreviewWindow.xaml)
   - [Culvert3DPreviewWindow.xaml.cs](file:///c:/Users/ADMIN/Desktop/PHAT%20TRIEN%20TOOL_HTKT/src/InfraBIM.CulvertTool/Views/Culvert3DPreviewWindow.xaml.cs)
   - Đã biên dịch Release thành công (**0 Lỗi**) và chép DLL mới nhất (517,120 bytes) vào `%AppData%\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`.

---

## 🌟 [2026-11-18] - Cập Nhật Tính Năng: Tùy Biến Cao Độ Điểm 1 & Điểm 2 Sân Gia Cố (Dốc Nghiêng Theo CAD)

### 🎯 Yêu cầu & Quyết định kỹ thuật (Người dùng đã "CHỐT"):
- **Bài toán thực tế:** Theo hồ sơ thiết kế chi tiết (CAD), bản Sân gia cố tiếp giáp với tường đầu / sân cống tại **Điểm 1** và dốc xuôi dần ra mép ngoài (dầm khóa / rãnh gia cố) tại **Điểm 2** với độ chênh cao $\Delta Z = 0.17$ m (ví dụ: Điểm 1 = 2.07m hoặc 2.21m; Điểm 2 = 1.90m hoặc 2.04m).
- **Giải pháp triển khai (Add-on Only, bảo tồn 100% thuật toán gốc):**
  1. Bổ sung 2 cột dữ liệu độc lập: `Z SGC TL` (Cao độ Điểm 2 Thượng lưu) và `Z SGC HL` (Cao độ Điểm 2 Hạ lưu).
  2. Cao độ Điểm 1 của Sân gia cố tự động kế thừa chính xác theo cao độ đỉnh tiếp giáp tường đầu ($Z_1$ Thượng lưu, $Z_2$ Hạ lưu).
  3. Cao độ Điểm 2 được đọc trực tiếp từ 2 cột mới trong Excel. Nếu để trống hoặc bằng 0, add-in tự động giữ nguyên cao độ phẳng bằng Điểm 1 (tương thích ngược 100%).
  4. Đã cập nhật toàn diện:
     - Model dữ liệu `CulvertRowData.cs` & `CulvertPreviewModels.cs`.
     - Bộ đọc Excel `ExcelReaderService.cs` (hỗ trợ công thức liên kết an toàn không lỗi).
     - Thuật toán dựng hình Adaptive Component `CulvertCoreEngine.cs` (`PlaceApronAssembly`).
     - Giao diện người dùng Tab 01 DataGrid `CulvertMainWindow.xaml`.
     - Bộ mô phỏng 3D Viewport `Culvert3DViewportControl.cs` & 2D Canvas `CulvertPreviewCanvas.cs`.
  5. Đã cập nhật file dữ liệu mẫu `DU_LIEU_CONG_NAP_TOOL.xlsx` tại thư mục kiểm tra `TEST 1`.
  6. Đã kiểm thử trực tiếp và liên kết thành công với Revit 2025 qua MCP `765t-flow`: Cả 2 cấu kiện `TNN_CX_SAN GIA CO` và `TNN_CX_SGC_BE TONG LOT` đều nghiêng chính xác theo tọa độ điểm, không phát sinh bất kỳ lỗi hình học nào.

---

## 🔒 [2026-11-18] - Xác Lập Nguyên Tắc Bất Di Bất Dịch & Đóng Băng Thuật Toán Đã Chuẩn Xác

### 🎯 Nguyên tắc cốt lõi đã chốt cùng Kỹ sư:
1. **Đóng băng 100% thuật toán rải hiện tại:**
   - Thuật toán rải cống hộp (đúc sẵn, đổ tại chỗ), cống kỹ thuật (CKT), sân cống, sân gia cố, cửa xả, hố ga, các phụ kiện... đã chạy hoàn toàn chính xác theo đúng thực tế thi công.
   - **Tuyệt đối KHÔNG ĐƯỢC THAY ĐỔI/CHỈNH SỬA các thuật toán rải này.**
2. **Nguyên tắc phát triển tương lai (Add-on Only):**
   - Bộ Add-in từ nay về sau **CHỈ ĐƯỢC CẬP NHẬT THÊM** (nhiệm vụ tiếp theo là bổ sung tính năng rải cống tròn).
   - Module mới phải độc lập, không được gây ảnh hưởng hay làm sai lệch logic cống hộp / CKT đã chuẩn.
3. **Quy trình phê duyệt bắt buộc — Chỉ sửa code khi có từ "CHỐT":**
   - Khi Kỹ sư yêu cầu cập nhật hoặc thêm tính năng, AI chỉ trao đổi, phân tích và lên phương án.
   - **CHỈ KHI KỸ SƯ NÓI "CHỐT", AI MỚI ĐƯỢC PHÉP CHỈNH SỬA MÃ NGUỒN ADD-IN.**

---

## [2026-10-26] - Nâng cấp v1.2.0: Hoàn Thiện Tab 3 Theo "MẪU 1: TOÀN TUYẾN 3D & FOCUS CẬN CẢNH"

### 🎯 Trạng thái hiện tại
- **Phiên bản:** 1.2.0 (Triển khai chính thức Mẫu 1 - Toàn Tuyến 3D Thông Minh + Focus Cận Cảnh)
- **Môi trường:** Revit 2025, .NET 8.0 Windows (`net8.0-windows`), C# 12
- **Trạng thái Build:** ✅ **Thành công 100% (0 Lỗi, 0 Cảnh báo)** trên cả 2 Solution (`ToolManhole.sln` và `ToolHTKT.sln`).
- **Trạng thái Triển khai:** ✅ Đã cập nhật bản build mới nhất vào `%AppData%\Autodesk\Revit\Addins\2025\DV_TOOL_HO_GA`.
- **Live Preview Artifact:** ✅ Đã cập nhật file mô phỏng tương tác 3D Web tại `preview_manhole_ui.html`.

---

### 🌟 Các Tính Năng Đột Phá Đã Triển Khai (Mẫu 1)

#### 1. Chế Độ Mặc Định: Hiển Thị "TOÀN TUYẾN 3D" (Full Drainage Pipeline Alignment)
- **Dựng hình đồng thời toàn bộ chuỗi hố ga trên tuyến:** Add-in tự động đọc toàn bộ danh sách hố ga của sheet đang chọn (`HO GA TRAI` hoặc `HO GA PHAI`), đưa về hệ tọa độ tương đối $(X_{rel}, Y_{elev}, Z_{rel})$ không bị giật số thực và dựng hình toàn bộ tuyến cống.
- **Vẽ chuỗi đường ống cống ngầm 3D liên tục:** Kết nối từ đáy cống của hố ga thứ $i$ sang hố ga thứ $i+1$, phản ánh trực quan độ dốc dọc và hướng dốc thoát nước của toàn tuyến.
- **Tương tác click chọn hố ga trực tiếp trong không gian 3D (3D Raycast Hit-Testing):** Người dùng có thể click chuột trái trực tiếp vào bất kỳ hố ga nào trên không gian 3D, hệ thống sẽ tự động bắt trúng hố ga đó, làm sáng viền Cyan Neon rực rỡ và cập nhật bảng thông số bên trái.
- **Cảnh báo lỗi màu sắc:** Hố ga bị lỗi cao độ (Đỉnh $\le$ Đáy hoặc quá nông) lập tức chuyển sang màu đỏ rực rỡ để kỹ sư nhận diện ngay trên toàn tuyến.

#### 2. Chế Độ Focus Cận Cảnh 1 Hố Ga & Thước Đo Cao Độ 3D (3D Datum Rulers)
- Bấm nút **`[🔍 Focus Cận Cảnh]`** hoặc Double-click vào hố ga trong không gian 3D:
  - Camera tự động căn chỉnh mượt mà vào trung tâm hố ga được chọn.
  - **Thước đo cao độ 3D thông minh 3 mốc chuẩn:**
    + **Vạch `CĐ ĐỈNH (A)` (Xanh lá cây `#4ADE80`):** Mốc cao độ đỉnh hố thu / mặt bó vỉa hè.
    + **Vạch `CĐ ĐÁY CỐNG NGANG (C)` (Xanh dương `#0EA5E9`):** Mốc cao độ đặt ống cống ngang thoát nước.
    + **Vạch `CĐ ĐÁY HỐ THU (D)` (Vàng hổ phách `#F59E0B`):** Mốc cao độ đáy hố ga / hố lắng cặn.
    + **Thước gióng đo kích thước:** Đo khoảng cách chiều cao thân hố ga $H = A - D$ và chiều sâu lắng cặn $C - D$.

#### 3. Mô Phỏng Chuẩn Xác Hình Học Hố Thu Nước `HO THU NUOC DVB_LOAI 1.rfa`
- Dựa trên khảo sát trực tiếp file Family thực tế tại `TEST TOOL_RAI HO GA\FAMILY_REVIT\HO THU NUOC DVB_LOAI 1.rfa`, bộ tạo mesh 3D tham số hóa (`ManholeMeshExtractor.cs`) được nâng cấp thể hiện đúng các bộ phận:
  - **Bản bê tông lót đáy ga:** Rộng nhô ra mỗi bên $100\text{mm} - 125\text{mm}$, dày $150\text{mm}$.
  - **Thân hố thu BTCT:** Khối hộp chính chữ nhật $1.4\text{m} \times 1.4\text{m}$.
  - **Cổ thu nước hàm ếch / bó vỉa hè:** Miệng thu nước vỉa hè gom nước mặt đường.
  - **Song chắn rác nắp gang:** Hoàn thiện màu gang than kim loại đen đặt tại cao độ đỉnh.
  - **Ống cống nối ngang:** Kết nối tại cao độ cống $C$ thoát nước ra ngoài.

#### 4. Đọc Linh Hoạt Dữ Liệu Excel Cả 2 Định Dạng
- Hỗ trợ chuẩn file Excel 7 cột (`STT`, `TEN HO GA`, `TOA DO X`, `TOA DO Y`, `CD DINH`, `CD DAY`, `GOC PV`).
- Tự động nhận diện các cột thực tế của dự án đường giao thông:
  - `CAO ĐỘ ĐỈNH HỐ THU - A`
  - `CAO ĐỘ ĐÁY CỐNG NGANG - C`
  - `CAO ĐỘ ĐÁY HỐ THU - D`
  - `LÝ TRÌNH` (Ví dụ: `Km0+120.00`).

---

## [2026-10-26] - Nâng cấp v1.1.0: Thêm Tab 3 "Kiểm Tra Mô Hình" & Tích hợp Live Preview

### 🎯 Trạng thái hiện tại
- **Phiên bản:** 1.1.0 (Bổ sung tính năng Kiểm tra mô hình, Đọc Family 3D & Live Preview)
- **Môi trường:** Revit 2025, .NET 8.0 Windows (`net8.0-windows`), C# 12
- **Trạng thái Build:** ✅ **Thành công 100% (0 Lỗi, 0 Cảnh báo)** trên cả 2 Solution (`ToolManhole.sln` và `ToolHTKT.sln`).
- **Trạng thái Triển khai:** ✅ Đã cập nhật bản build mới nhất vào `%AppData%\Autodesk\Revit\Addins\2025\DV_TOOL_HO_GA`.

---

### 🌟 Tính Năng Mới & Cải Tiến Chi Tiết

#### 1. Thêm Mới Tab 3: "Kiểm Tra Mô Hình (Preview & Đọc Family)" (Nằm ngay sau Tab 2)
Theo đúng yêu cầu của kỹ sư, Tab 3 được thiết kế chuyên biệt để kiểm tra tính đúng đắn của dữ liệu và hình học Family trước khi bấm rải:
- **Bộ lọc kiểm tra theo từng Sheet Excel:**
  - Hỗ trợ chuyển đổi nhanh giữa 2 Sheet: `HO GA TRAI` và `HO GA PHAI`.
  - Bảng danh sách chọn từng hố ga (STT, Tên Hố Ga, CĐ Đỉnh, CĐ Đáy, H Thân, Tình Trạng).
- **Card thông số kiểm tra cao độ chi tiết:**
  - Hiển thị nổi bật 3 khối thông số cốt lõi:
    + **CĐ ĐỈNH (m):** Đánh dấu màu xanh lá (`#4ADE80`).
    + **CĐ ĐÁY (m):** Đánh dấu màu vàng hổ phách (`#F59E0B`).
    + **H THÂN (m):** Tự động tính = `CD DINH - CD DAY`. Đổi màu đỏ ngay lập tức nếu phát hiện cao độ đỉnh $\le$ cao độ đáy.
  - Tọa độ thực địa VN2000 $X, Y$, góc xoay phương vị `GOC PV` (°).
  - Tự động tính khoảng cách và độ dốc đáy thoát nước đến hố ga kế tiếp dọc tuyến để rà soát lỗi ngược dốc.
- **Trích xuất 100% hình học thực tế từ Revit Family (`ManholeMeshExtractor.cs`):**
  - Tích hợp nút bấm **"Đọc Hình Học Family"**: Sử dụng kỹ thuật *Virtual Rollback Transaction* an toàn.
  - Đọc trực tiếp các khối 3D Solid của FamilySymbol đã chọn trong dự án Revit và chuyển thành `MeshGeometry3D` hiển thị lên Viewport 3D mà không làm thay đổi hay để lại rác trong mô hình.
- **Viewport 3D tương tác thực tế (`Manhole3DViewportControl.cs`):**
  - Không gian 3D mượt mà: Chuột trái xoay 360° (Orbit), Chuột phải kéo di chuyển (Pan), Lăn chuột thu phóng (Zoom).
  - Thanh công cụ điều khiển góc nhìn nhanh: `3D Iso`, `Mặt Đứng Front`, `Mặt Bằng Top`, `Căn Vừa (Fit View)`.
  - **Thước đo cao độ 3D (Elevation Datum Ruler):** Cột thước đo trực quan cắm cạnh hố ga trong không gian 3D, đánh dấu rõ ràng vị trí mốc `CĐ ĐỈNH` và `CĐ ĐÁY` tương ứng với giá trị Excel.
  - Tự động fallback sang mô hình 3D tham số hóa (Thân bê tông, Cổ thu, Nắp gang, Lót đáy) khi chưa đọc Family hoặc khi kiểm tra nhanh.

#### 2. Điều Chỉnh Thứ Tự 5 Tab Giao Diện Hoàn Chỉnh
1. **Tab 1:** Khai Báo Excel & Nạp Family
2. **Tab 2:** Tab Parameter (Biến Hình Học Theo STT)
3. **Tab 3:** **Kiểm Tra Mô Hình (Preview & Đọc Family)** *(Mới thêm)*
4. **Tab 4:** Tab Gán Vật Liệu
5. **Tab 5:** Gán Thông Tin Phi Hình Học & BIM Metadata + Nút Thực Thi **`RẢI HỐ GA`**

#### 3. Tích Hợp Live Preview Trực Quan & Công Cụ Tùy Chỉnh Nhanh
- Tạo file batch cập nhật siêu tốc: [`cap_nhat_giao_dien.bat`](./cap_nhat_giao_dien.bat).
- Hướng dẫn chi tiết kỹ sư sử dụng **XAML Live Preview**, **XAML Hot Reload** trong Visual Studio 2022 và **Generative UI Live Preview** trong Antigravity để chỉnh sửa giao diện trực tiếp không cần tắt Revit.

---

### 📌 Hướng Dẫn Kỹ Sư Sử Dụng Tab Mới Trên Revit 2025
1. Mở Revit 2025 -> Tab **"ĐV_TOOL_HTKT"** -> Bấm **"Rải Hố Ga"**.
2. Tại **Tab 1**: Nạp file Excel mẫu `Mau_Du_Lieu_Ho_Ga.xlsx` và chọn Family Hố Ga.
3. Chuyển sang **Tab 3 ("3. Kiểm Tra Mô Hình")**:
   - Chọn nhánh `HO GA TRAI` hoặc `HO GA PHAI`.
   - Click chọn từng hố ga trên bảng danh sách bên trái.
   - Quan sát 3 ô Card: **CĐ ĐỈNH**, **CĐ ĐÁY** và **H THÂN** để kiểm tra tính đúng đắn.
   - Bấm chuột trái vào khung 3D bên phải để xoay 360° kiểm tra hình dáng hố ga và vị trí nắp gang / đáy hố ga.
   - Bấm nút **"Đọc Hình Học Family"** nếu muốn soi chi tiết khối hình học thực tế của Family trong Revit.
4. Sau khi kiểm tra cao độ chuẩn xác, chuyển sang Tab 5 và bấm **`RẢI HỐ GA`**.
