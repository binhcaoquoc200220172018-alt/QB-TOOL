# 📝 NHẬT KÝ THAY ĐỔI & TRẠNG THÁI DỰ ÁN (CHANGELOG.MD)

Tài liệu này lưu trữ toàn bộ lịch sử thay đổi, trạng thái làm việc gần nhất và danh sách công việc tiếp theo của dự án **Tool Add-in Hạ Tầng Kỹ Thuật Revit (ToolHTKT)**.

> [!NOTE]
> File này được liên kết trực tiếp từ [`AGENT.md`](./AGENT.md) và được cập nhật liên tục bởi AI và Kỹ sư phát triển.

---

## 📅 [2026-09-30] - Nâng cấp V2.6.1: Tinh chỉnh Chuẩn Xác 100% Nhóm "Other (Khác)" Theo Đúng Mục "Other" Trong Revit (Đã bàn giao)

- **Tình trạng:** **ĐÃ HOÀN TẤT, BIÊN DỊCH VÀ TRIỂN KHAI TRỰC TIẾP VÀO REVIT 2025 THÀNH CÔNG 100% (`0 Error(s)`).**
  - File DLL đã cập nhật: `C:\Users\ADMIN\AppData\Roaming\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`
  - Thời gian cập nhật: 2026-09-30 13:39:59 (175,616 bytes)
- **Chi tiết tinh chỉnh chuẩn xác:**
  - **Sửa triệt để bộ lọc "Tham số Khác (Other)":**
    - Trước đây, bất kỳ tham số nào không phải `Dimensions` đều bị xếp tạm vào `Other` (dẫn tới các nhóm `06g. Thể tích...`, `06h. Thể tích...`, `06i. Khối lượng...`, `Data`, `Text`, `Identity Data` bị hiển thị lẫn vào).
    - Sau khi sửa: Tool chỉ nhận diện **DUY NHẤT** các tham biến nằm đúng trong **mục `Other` của bảng thuộc tính Revit Properties** (ví dụ cụ thể trong Family của Kỹ sư là: `ẨN PHẢI`, `ẨN TRÁI`, `kg/m`...).
    - Các nhóm tham số thống kê khối lượng, thể tích (`06...`), Identity Data, Text... hoàn toàn bị loại bỏ khỏi bộ lọc `Other`, giúp bảng Tab 02 sạch sẽ, đúng chuẩn xác 100% cấu trúc Family của Kỹ sư.

---

## 📅 [2026-09-30] - Nâng cấp V2.6: Tối ưu Tự động chọn Type theo Khẩu độ Excel & Mở toàn bộ Tham số Dimensions / Other tại Tab 02 (Đã bàn giao)

- **Tình trạng:** **ĐÃ HOÀN TẤT, BIÊN DỊCH VÀ TRIỂN KHAI TRỰC TIẾP VÀO REVIT 2025 THÀNH CÔNG 100% (`0 Error(s)`).**
  - File DLL đã cập nhật: `C:\Users\ADMIN\AppData\Roaming\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`
  - Thời gian cập nhật: 2026-09-30 13:33:35 (175,616 bytes)
- **Chi tiết các nội dung nâng cấp theo yêu cầu của Kỹ sư:**
  1. **Tab 02 (Parameter Hình học) - Hiển thị đầy đủ Dimensions & Other:**
     - Mặc định bật hiển thị đồng thời cả 2 nhóm tham số `Dimensions (Kích thước)` và `Other (Khác)`.
     - Giữ nguyên cơ chế thuần tùy chỉnh thủ công: Toàn bộ danh sách tham biến của Family được xổ ra bảng để Kỹ sư chủ động lựa chọn, kiểm tra và gán biến bằng tay theo ý muốn.
  2. **Tự động chọn đúng Family Type theo Khẩu độ & Chủng loại trong Excel (Multi-Type Engine):**
     - Nâng cấp `ResolveFamilyForCulvert` trong `CulvertCoreEngine`:
       - Nhận diện chuẩn xác tiền tố Family thực tế của Kỹ sư (`TNN_CT_...` cho Cống Tròn, `TNN_CH_...` cho Cống Hộp, `TNM_HT_...` cho Hố Thu, `TNM_HG_...` cho Hố Ga).
       - Khớp chính xác Type theo khẩu độ ghi trong Excel (ví dụ `D800`, `D1000`, `D1200`, `D1500`, `D1800`, `1.5x1.5`...).
       - Tự động đồng bộ Type của cả `Thân cống` lẫn `Bê tông lót đốt cống` (`TNN_CT_BE TONG LOT_Dxxxx`) theo đúng khẩu độ của từng đốt cống trên tuyến.
       - Nhận diện đa dạng ký hiệu cống tròn đôi (`Cống tròn đôi`, `CT đôi`, `SoCua >= 2`) để tách 2 nhánh lệch tim $D_{\text{tim}}$ tự động.

---

## 📅 [2026-09-30] - Nâng cấp Tối ưu V2.5: Tinh gọn Bê Tông Lót (Chỉ rải BTL Thân Cống, ngắt quãng trước Hố Thu) (Đã bàn giao)

- **Tình trạng:** **ĐÃ HOÀN TẤT, BIÊN DỊCH VÀ TRIỂN KHAI TRỰC TIẾP VÀO REVIT 2025 THÀNH CÔNG 100% (`0 Error(s)`).**
  - File DLL đã cập nhật: `C:\Users\ADMIN\AppData\Roaming\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`
  - Thời gian cập nhật: 2026-09-30 13:11:15 (174,080 bytes)
- **Chi tiết tinh giản cấu hình Bê Tông Lót (BTL) theo đúng ý Kỹ sư:**
  1. **Loại bỏ hoàn toàn BTL sân cống & BTL hố thu khỏi Tool:**
     - Vì Family Sân cống và Family Hố thu của Kỹ sư đã vẽ liền BTL và có thể tùy biến trực tiếp trong Family, Tool loại bỏ các dòng cấu kiện này để giao diện gọn gàng, không phát sinh dư thừa hình học.
  2. **Duy nhất giữ lại `Bê tông lót đốt cống` tại Tab 01:**
     - Mặc định **`[✔] BẬT`** (Chênh cao $Z = -0.10\text{m}$ với cống hộp, $-0.30\text{m}$ với cống tròn).
     - Tool tự động lấy Family BTL thân cống riêng của Kỹ sư để rải theo từng đốt cống.
  3. **Thuật toán ngắt quãng trước vách hố thu (Smart Interruption):**
     - Dải BTL thân cống dừng chính xác ngay trước mép ngoài hố thu ($\frac{B_{\text{box}}}{2}$), tuyệt đối không đâm xuyên vào lòng hố ga/hộp nối.
  4. **Đồng bộ hóa Tab 03 (Preview 2D/3D) & Tab 04 (Vật liệu):**
     - Bản vẽ 2D chỉ vẽ dải BTL dưới các đốt cống (không vẽ BTL rời ở sân hay hố thu).
     - Tab 04 chỉ quản lý vật liệu `Bê tông lót đốt cống` cùng các cấu kiện chính.

---

## 📅 [2026-09-30] - Nâng cấp Đột phá V2.4: Bổ sung Tab Xem Trước (Preview 2D/3D), Đổi tên Chênh cao Z & Tự động nhận diện Family theo Excel (Đã bàn giao)

- **Tình trạng:** **ĐÃ HOÀN TẤT, BIÊN DỊCH VÀ TRIỂN KHAI TRỰC TIẾP VÀO REVIT 2025 THÀNH CÔNG 100% (`0 Error(s)`).**
  - File DLL đã cập nhật: `C:\Users\ADMIN\AppData\Roaming\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`
  - Thời gian cập nhật: 2026-09-30 11:44:54 (195,584 bytes)
- **Chi tiết 3 nội dung trọng tâm nâng cấp V2.4:**
  1. **Bổ sung Tab hoàn toàn mới: `03 XEM TRƯỚC HÌNH HỌC (PREVIEW 2D/3D)`:**
     - **Vị trí chuẩn xác:** Đặt ngay sau Tab 02 Parameter Hình học $\to$ Quy trình 6 Tab hoàn chỉnh: `01 KHAI BÁO & VỊ TRÍ` $\to$ `02 PARAMETER HÌNH HỌC` $\to$ `03 XEM TRƯỚC HÌNH HỌC (PREVIEW 2D/3D)` $\to$ `04 VẬT LIỆU CẤU KIỆN` $\to$ `05 GÁN THÔNG TIN BIM` $\to$ `06 KẾT QUẢ KIỂM TRA & THỰC HIỆN`.
     - **Cột trái - Bộ lọc & Danh sách cống Excel:**
       - Cho phép lọc nhanh theo chủng loại (*Tất cả, Cống Hộp, Cống Tròn, Cống Đôi*).
       - Ô tìm kiếm linh hoạt theo Lý trình (*Km...*) hoặc STT cống.
       - Danh sách DataGrid cống rõ ràng: Nhấp chọn bất kỳ cống nào thì bản vẽ mô phỏng bên phải lập tức chuyển sang cống đó tức thì.
     - **Cột phải - Khung vẽ đồ họa Vector tương tác (Interactive Canvas Viewer):**
       - **Mặt cắt dọc cống (Longitudinal Profile):** Thể hiện chi tiết sân cống Thượng lưu ($Z_1$), Hạ lưu ($Z_2$), tim cống dốc, từng đốt cống chuẩn ($L_{\text{std}}$) và đốt bù ($L_{\text{bù}}$), hố thu/hộp nối, lớp bê tông lót (BTL) và đệm cát bên dưới.
       - **Đường gióng kích thước chuẩn CAD (Dimension Lines):** Kích thước từng đốt cống, tổng chiều dài $L$, ngàm sân cống, mốc cao độ đáy cống ($\nabla Z_1, \nabla Z_2$), mũi tên ký hiệu độ dốc $i\%$.
       - **Mặt bằng cống (Plan View):** Thể hiện tim cống, góc xoay $\alpha$, bề rộng cống. Đặc biệt với cống tròn đôi: Thể hiện rõ 2 nhánh cống tròn song song kèm đường đo khoảng cách giữa 2 tim cống $D_{\text{tim}}$!
       - **Phối cảnh 3D (Axonometric 3D):** Hiển thị khối 3D trực quan gồm thân cống, sân cống và các lớp lót đệm.
       - **Công cụ điều khiển:** Phóng to (`🔍+`), Thu nhỏ (`🔍-`), Vừa khít (`🎯 Fit View`), hỗ trợ lăn chuột (Mouse Wheel) và kéo chuột (Drag Pan) mượt mà.
     - **Thẻ tổng kết kiểm tra hình học nhanh:** Báo ngay tổng chiều dài, số đốt chuẩn, chiều dài đốt bù (kèm cảnh báo hợp lệ $\ge L_{\text{min}}$ hoặc cảnh báo đỏ nếu $< L_{\text{min}}$), cao độ đầu - cuối.
  2. **Đổi tên cột `ΔZ (m)` thành `Chênh cao Z (m)`:**
     - Đổi tên cột trong bảng Cụm Family lắp ghép (Tab 01) thành **`Chênh cao Z (m)`** với độ rộng 120px.
     - Bổ sung Tooltip giải thích trực quan: *"Chênh cao đặt so với đáy cống (m): Thân cống & Sân cống = 0.00m; Cấu kiện nằm dưới đáy (Bê tông lót, Đệm cát) nhập giá trị âm (vd: -0.10m, -0.20m)"*.
  3. **Tự động nhận diện Family theo loại cống từ Excel (Smart Auto-Mapping):**
     - Nâng cấp bộ máy `CulvertCoreEngine`: Khi rải cống hàng loạt từ Excel có nhiều loại cống hỗn hợp (vừa có cống hộp đơn, tròn đơn, tròn đôi...), tool tự động đối chiếu `LoaiCong` và `KhauDo` của từng dòng với kho Family trong dự án Revit để tự chọn đúng loại Family phù hợp, không bị áp nhầm cống hộp sang cống tròn!

---

## 📅 [2026-09-29] - Nâng cấp V2.3: Điều chỉnh Chiều cao Tab 01 & Tùy biến Vật liệu / Bảng màu Toàn diện Tab 03 (Đã bàn giao)

- **Tình trạng:** **ĐÃ BIÊN DỊCH VÀ TRIỂN KHAI TRỰC TIẾP VÀO REVIT 2025 THÀNH CÔNG 100% (`0 Error(s)`).**
  - File DLL đã cập nhật: `C:\Users\ADMIN\AppData\Roaming\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`
  - Thời gian cập nhật: 2026-09-29 17:18:22 (151,040 bytes)
- **Chi tiết các hạng mục nâng cấp V2.3 theo yêu cầu của Kỹ sư:**
  1. **Điều chỉnh chiều cao khu vực khoanh đỏ ở Tab 01 (Khung 1, Khung 2, Khung 3):**
     - Nâng chiều cao hiển thị của Bảng Cụm Family lắp ghép (Khung 2) từ `165px` lên `240px`.
     - Giúp hiển thị trọn vẹn cả 7 cấu kiện lắp ghép mẫu (*Đốt cống chuẩn, Đốt bù, Sân cống TL, Sân cống HL, Hộp nối/Hố thu, Bê tông lót, Đệm cát*) mà không bị co hẹp hoặc che khuất thanh cuộn.
     - Khung 1 (Nguồn dữ liệu Excel) và Khung 3 (Thiết lập rải đốt & hình học) tự động giãn đều chiều cao tương ứng, tạo bố cục tổng thể thông thoáng, chuyên nghiệp.
  2. **Tùy biến toàn diện Tab 03 (`03 VẬT LIỆU CẤU KIỆN`):**
     - **Tự tạo & Tùy chỉnh danh mục cấu kiện:**
       - Bổ sung nút `[➕ Thêm cấu kiện]` và `[➖ Xóa]` cho phép thêm bớt cấu kiện tùy thích theo thực tế dự án.
       - Cho phép sửa trực tiếp cột `Loại cấu kiện` và `Tên vật liệu Revit` (gõ tự do hoặc chọn từ danh sách vật liệu có sẵn trong Revit).
     - **Tự do chỉnh màu sắc cho từng cấu kiện theo ý muốn:**
       - Tích hợp nút `[🎨]` ngay trên từng dòng trong DataGrid: Nhấp vào sẽ mở trực tiếp bảng pha màu chuẩn **Windows Color Picker** (`System.Windows.Forms.ColorDialog`), cho phép chọn bất kỳ màu nào từ bảng triệu màu (Full Spectrum / Palette), tạo màu tùy ý hoặc nhập tọa độ màu.
       - **Thẻ Tùy chỉnh chi tiết cấu kiện (Khu vực 2):**
         - Hiển thị trực tiếp ô xem trước màu lớn (`ColorBrush`), nhãn cấu kiện đang chọn.
         - Nút bấm nổi bật `[🎨 MỞ BẢNG CHỌN MÀU (COLOR PICKER)...]`.
         - Bổ sung 4 ô nhập giá trị trực tiếp: **Đỏ (R: 0-255)**, **Lục (G: 0-255)**, **Lam (B: 0-255)** và **Mã Hex (`#RRGGBB`)** — tự động đồng bộ 2 chiều tức thì.
         - Thanh trượt điều chỉnh **Độ trong suốt (`Transparency` 0 - 100%)**.
         - Nút `[➕ Tạo / Gán vật liệu này vào Revit ngay]`: Khởi tạo ngay đối tượng Revit Material vào dự án.
         - Giữ nguyên tiện ích Bảng màu nhanh 8 ô hạ tầng và nút `[⚡ Áp dụng mẫu vật liệu TCVN chuẩn]`.

---

## 📅 [2026-09-29] - Nâng cấp Toàn diện V2.2: Đồng bộ ScrollBar & Bổ sung Tab Gán Vật Liệu Cấu Kiện (Đã bàn giao)

- **Tình trạng:** **ĐÃ HOÀN THÀNH VÀ TRIỂN KHAI TRỰC TIẾP VÀO REVIT 2025 THÀNH CÔNG 100% (`0 Error(s)`).**
  - File DLL đã cập nhật: `C:\Users\ADMIN\AppData\Roaming\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`
  - Đầy đủ file Manifest, Resources icon và thư viện phụ thuộc.
- **Chi tiết các hạng mục nâng cấp V2.2:**
  1. **Đồng bộ ScrollBar 2 chiều (ngang & dọc) cho toàn bộ các khung bảng:**
     - Thiết lập `ScrollViewer.HorizontalScrollBarVisibility="Auto"`, `ScrollViewer.VerticalScrollBarVisibility="Auto"`, `ScrollViewer.CanContentScroll="True"` và độ rộng tối thiểu (`MinWidth`) độc lập cho từng cột:
       - **Tab 01 - Khung 2:** *Bảng Cụm Family lắp ghép (tùy biến cộng/trừ)*.
       - **Tab 01 - Khung 4:** *Bảng dữ liệu trích xuất từ Excel 19 cột*.
       - **Tab 02 - Bảng bên trái:** *Danh mục tham số quét theo Family* (như mẫu Tab 02).
       - **Tab 02 - Bảng bên phải:** *Bảng gán giá trị tham số hàng loạt*.
       - **Tab 03 (Mới):** *Bảng danh mục cấu kiện & thiết lập vật liệu, tên, màu sắc*.
       - **Tab 04 - Khung 2:** *Bảng tham số BIM tùy biến*.
       - **Tab 04 - Khung 3:** *Bảng xem trước thông tin BIM sẽ gán*.
       - **Tab 05:** *Bảng kiểm tra chi tiết tọa độ, chiều dài, độ dốc*.
     - **NGOẠI TRỪ THEO ĐÚNG CHỈ ĐẠO:**
       - ❌ Tab 01 Khung 1 (*Nguồn dữ liệu Excel*).
       - ❌ Tab 01 Khung 3 (*Thiết lập rải đốt & hình học*).
  2. **Bổ sung Tab hoàn toàn mới: `03 VẬT LIỆU CẤU KIỆN`:**
     - **Quy trình 5 Tab chuyên nghiệp:** `01 KHAI BÁO & VỊ TRÍ` $\to$ `02 PARAMETER HÌNH HỌC` $\to$ `03 VẬT LIỆU CẤU KIỆN` $\to$ `04 GÁN THÔNG TIN BIM` $\to$ `05 KẾT QUẢ KIỂM TRA & THỰC HIỆN`.
     - **Bảng danh mục cấu kiện & vật liệu (Khu vực 1):**
       - Tự động đồng bộ danh sách các cấu kiện đang kích hoạt từ Tab 01 (Đốt cống chuẩn, Đốt bù, Sân cống TL, Sân cống HL, Hố thu, Bê tông lót, Đệm cát, Gối cống...).
       - Cho phép chọn vật liệu sẵn có trong Revit **hoặc gõ trực tiếp tên vật liệu mới muốn tạo** (Editable ComboBox).
       - Hiển thị trực quan ô màu sắc cấu kiện (`ColorBrush`), mã HEX, RGB, và độ trong suốt `Transparency` (0 - 100%).
       - Cho phép chỉ định tham số vật liệu Family cần gán (`Material`, `Structural Material`, `Vật liệu`...).
       - Hiển thị nhãn trạng thái trực quan: `Đã có trong Revit` hoặc `Sẽ tạo mới`.
     - **Tiện ích Mẫu TCVN & Bảng màu nhanh (Khu vực 2):**
       - Nút 1 chạm `[⚡ Áp dụng mẫu vật liệu TCVN chuẩn]`: Gán tự động vật liệu chuẩn ngành hạ tầng (Đốt cống: `BTCT_M300_Cống đúc sẵn` màu `#BDC3C7`, Sân cống: `BT_M250_Đổ tại chỗ` màu `#95A5A6`, Hố ga: `BT_M250_Hố ga` màu `#7F8C8D`, Bê tông lót: `BT_M100_Lót móng` màu `#4A5568`, Đệm cát: `Cát đầm chặt K95` màu `#ECC94B`, Đá dăm: `Đá dăm 1x2 đệm` màu `#566573`).
       - Bảng màu nhanh chuyên ngành (8 ô màu): Nhấp vào ô màu nào là cấu kiện đang chọn sẽ đổi màu ngay lập tức.
       - Nút `[➕ Tạo vật liệu này vào Revit ngay]`: Khởi tạo trước đối tượng `Material` vào dự án Revit để kiểm tra trên cây vật liệu.
       - Nút `[🔄 Quét vật liệu Revit]`: Tải lại danh sách vật liệu mới nhất từ file Revit đang mở.
     - **Cơ chế gán tự động khi chạy mô hình:** Dịch vụ `BimMaterialService` tự động kiểm tra, khởi tạo `Material` trong Revit (với tên, màu sắc Shading RGB, độ trong suốt) và gán `MaterialId` trực tiếp vào từng cấu kiện cống, sân cống, hố ga, bê tông lót.

---

## 📅 [2026-09-29] - Nâng cấp Kiến trúc & Tối ưu Giao diện V2.1 (Đã hoàn thiện & Triển khai)

- **Tình trạng:** **ĐÃ HOÀN THÀNH VÀ TRIỂN KHAI TRỰC TIẾP VÀO REVIT 2025 THÀNH CÔNG 100% (`0 Error(s)`).**
  - File DLL đã nạp: `C:\Users\ADMIN\AppData\Roaming\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\DV_TOOL_HTKT.dll`
  - Đầy đủ file Manifest, Resources icon và thư viện phụ thuộc.
- **Cấu hình kỹ thuật:**
  - Nền tảng: **Autodesk Revit 2025 (.NET 8.0 Windows `net8.0-windows`)**.
  - Ngôn ngữ: **C# 12**, UI: **WPF / XAML Dark Theme chuyên nghiệp** (Navy `#0A1120`, `#132035` + Cyan `#38BDF8` + Mint Green `#86EFAC`, `#16A34A`).
  - Thư viện: **RevitAPI 2025**, **RevitAPIUI 2025**, **ClosedXML 0.104.2**.
- **Chi tiết các hạng mục đã hoàn thiện:**
  1. **Highlight nút mẫu cống & Tự động ẩn/hiện $D_{\text{tim}}$:**
     - 4 nút `[📦 Hộp Đơn]`, `[📦 Hộp Đôi]`, `[🔵 Tròn Đơn]`, `[🔵 Tròn Đôi]` được chuyển sang dạng **Toggle Button có Highlight phát sáng viền và nền Xanh Cyan (`#0284C7` viền `#38BDF8`)** kèm chữ trắng in đậm khi được chọn.
     - **Chỉ khi chọn `[🔵 Tròn Đôi]`:** Ô nhập khoảng cách giữa 2 tim cống mới xuất hiện trong Khung 3 với nhãn chuẩn: **`Khoảng cách giữa 2 tim cống D_tim (m):`**. Khi chọn Hộp đơn, Hộp đôi, Tròn đơn thì ô này tự động ẩn hoàn toàn.
     - Xóa bỏ hoàn toàn checkbox thừa `Áp dụng cống đôi (2 nhánh lệch tim)` ở Khung 3.
  2. **Chuẩn hóa nhãn thuật ngữ trực quan:**
     - Đổi tên `B_box` thành **`Bề rộng hố thu_B (m):`** (mặc định 1.50m) kèm Tooltip hướng dẫn ý nghĩa kỹ thuật: bề rộng phủ bì ngoài hố thu/hộp nối để trừ hao chiều dài rải cống, tránh đâm thủng hố ga.
     - Đổi tên `Khe hở` thành **`Khe hở mối nối cống (m):`** (mặc định 0.05m = 5cm) kèm Tooltip kỹ thuật.
  3. **Tab 02 hỗ trợ song song nhóm `Dimensions` và `Other`:**
     - Bổ sung 2 checkbox bộ lọc:
       - `☑ Tham số Kích thước (Dimensions)`
       - `☑ Tham số Khác (Other)`
     - Dịch vụ quét `FamilyParameterScannerService` tự động phân loại và hiển thị đầy đủ các tham biến của Family thuộc cả 2 nhóm.
  4. **Chuẩn hóa font chữ duy nhất `Segoe UI` & Chống co ép cột bảng (DataGrid ScrollBars):**
     - Đồng bộ 100% font chữ toàn bộ Add-in sang font chuẩn hệ thống **`Segoe UI`** (tiêu đề, groupbox, nút bấm, ô nhập và nội dung bảng).
     - Bật thanh cuộn 2 chiều (`ScrollViewer.HorizontalScrollBarVisibility="Auto"` và `ScrollViewer.VerticalScrollBarVisibility="Auto"`) cho toàn bộ DataGrids ở cả 4 Tab.
     - Thiết lập độ rộng tối thiểu (`MinWidth`) độc lập cho tất cả 19 cột ở Khung 4 Tab 01, Tab 02, Tab 03, Tab 04 $\implies$ Khắc phục triệt để tình trạng co ép cột làm mất chữ ở ảnh 5. Dữ liệu hiển thị tròn vành rõ chữ, cuộn mượt mà.

---

## 📅 [2026-09-29] - Nâng cấp Kiến trúc V2 (Dynamic Assembly, Cống Tròn Đôi 2 Nhánh, Dynamic BIM Parameters)
- Hoàn thành Dynamic Component Assembly (Cộng/Trừ cấu kiện).
- Hoàn thành thuật toán cống tròn đôi 2 nhánh độc lập (.T / .P).
- Hoàn thành bộ quản lý tham số BIM động theo tiêu chuẩn dự án (Tab 03).
- Chuẩn hóa thuật ngữ Excel: `KC_HN1`, `KC_HN2`, `KC_Tim`.

---

## 📅 [2026-09-28] - Khởi tạo dự án & Thiết lập phiên bản V1
- Hoàn thành Add-in ban đầu cho Revit 2025 (.NET 8).
- Thuật toán rải cống đơn TH1/TH2.
- Giao diện Dark Theme ban đầu & file batch cập nhật `cap_nhat_giao_dien.bat`.
