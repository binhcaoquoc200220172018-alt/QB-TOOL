# 📝 NHẬT KÝ THAY ĐỔI & TIẾN ĐỘ DỰ ÁN (CHANGELOG)

Tài liệu này ghi lại toàn bộ lịch sử phát triển, cập nhật tính năng, tối ưu hóa mã nguồn và trạng thái làm việc của bộ Add-in Revit Hạ tầng Kỹ thuật (Tool Rải Hố Ga).

---

## [2026-10-26] - Nâng cấp v1.3.0: Chuẩn Hóa Giao Diện Đơn Giản, Chuyên Nghiệp - Đồng Bộ 100% Màu Sắc & Font Segoe UI Theo Tool Cống Ngang

### 🎯 Trạng thái hiện tại
- **Phiên bản:** 1.3.0 (Đồng bộ nhận diện thương hiệu HTKT, font Segoe UI, bảng màu Navy Dark chuẩn)
- **Môi trường:** Revit 2025, .NET 8.0 Windows (`net8.0-windows`), C# 12
- **Trạng thái Build:** ✅ **Thành công 100% (0 Lỗi, 0 Cảnh báo)** trên cả 2 Solution (`ToolManhole.sln` và `ToolHTKT.sln`).
- **Trạng thái Triển khai:** ✅ Đã cập nhật bản build mới nhất vào `%AppData%\Autodesk\Revit\Addins\2025\DV_TOOL_HO_GA`.

---

### 🌟 Các Điểm Cải Tiến & Chuẩn Hóa Toàn Diện

#### 1. Đồng Bộ 100% Màu Sắc & Font Chữ Theo Tool Cống Ngang (`PHAT TRIEN TOOL_HTKT`)
- **Font chữ:** Toàn bộ hệ thống giao diện chuẩn hóa font **`Segoe UI`** với kích thước chuẩn 12 - 13.5px, chống mỏi mắt và hiển thị sắc nét trên màn hình độ phân giải cao.
- **Bảng màu Navy Dark chuyên nghiệp:**
  + Nền Window & TabControl: `NavyBg` (`#0A1120`), `NavyDark` (`#0B132B`).
  + Khung GroupBox & Thẻ: `NavyCard` (`#132035`), `NavyCardInner` (`#182A44`).
  + Đường viền: `NavyBorder` (`#223959`), điểm nhấn focus `CyanPrimary` (`#38BDF8`).
  + Điểm nhấn thương hiệu: Huy hiệu `ĐV` nền `#1E3A5F`, chữ `ĐV_TOOL_HTKT` xanh lơ `#38BDF8` kèm tiêu đề phụ xanh lá mạ `#86EFAC`.
  + Nút thực thi chính: Nổi bật với màu xanh lá tươi sáng `#16A34A` và viền `#86EFAC` (`BtnExecuteStyle`).

#### 2. Tab 01: Nâng Cấp Bảng Review Số Liệu Excel 11 Cột & Bộ Lọc Nhanh
- **DataGrid Review Excel 11 cột chuẩn kỹ thuật:** Chọn, STT, Bên Tuyến, Tên Hố Ga, Tọa Độ X, Tọa Độ Y, CĐ Đỉnh A, CĐ Đáy D, Chiều Cao Thân $H = A - D$ (tô đỏ cảnh báo nếu $A \le D$), Góc PV (°), Trạng Thái.
- **Thanh cuộn 2 chiều mượt mà:** Bật `ScrollViewer.HorizontalScrollBarVisibility="Auto"` và `VerticalScrollBarVisibility="Auto"`.
- **Bộ lọc xem nhanh:** Bổ sung các nút bấm `[Tất cả]`, `[Sheet Trái]`, `[Sheet Phải]` để kỹ sư rà soát từng tuyến hoặc toàn bộ tuyến ngay tại Tab 1.

#### 3. Tab 02: Quét Tự Nhiên Tham Số Family Qua `doc.EditFamily` (Kế Thừa Từ Cống Ngang)
- Tích hợp kỹ thuật đọc tham số từ `FamilyManager`:
  + Giữ nguyên 100% thứ tự khai báo tự nhiên của Family trong Revit.
  + **Lọc bỏ toàn bộ tham số đã gán công thức `Formula`** để tránh xung đột khi gán giá trị.
  + Phân nhóm rõ ràng: **Dimensions (Kích thước)** và **Yes/No (Bật tắt cấu kiện)**.
- Khung gán tham số theo **Dải STT** linh hoạt (`1-5, 8, 10-15`) kèm bảng tổng hợp Overrides rõ ràng.

#### 4. Tab 04 (Vật Liệu) & Tab 05 (BIM Metadata) Đồng Bộ Phong Cách Cống Ngang
- **Tab 04:** Tự động phát hiện tham số `Material`, ánh xạ nhanh với vật liệu dự án Revit và nút `Tự Động Ghép Vật Liệu (Auto Match)`.
- **Tab 05:** Quản lý thông tin công trình, hỗ trợ Smart Tags (`{TEN_HO_GA}`, `{STT}`, `{CD_DINH}`, `{CD_DAY}`) và tùy chọn `[x] Đồng bộ thông tin xuống tất cả cấu kiện con (SubComponents)` bằng `GetSubComponentIds()`.

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
