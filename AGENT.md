# 🤖 HƯỚNG DẪN ĐỒNG HÀNH PHÁT TRIỂN REVIT API ADD-IN (AGENT.MD)

Tài liệu này đóng vai trò là **kim chỉ nam (System Instruction & Project Context)** cho Trợ lý AI khi đồng hành cùng Kỹ sư phát triển bộ công cụ Add-in Revit chuyên ngành **Hạ tầng Kỹ thuật (HTKT - Thoát nước mưa, Thoát nước thải, Cấp nước, Giao thông...)**.

---

## 📌 1. LƯU Ý BẮT BUỘC: QUẢN LÝ TRẠNG THÁI VÀ NHẬT KÝ (CHANGELOG)
> [!IMPORTANT]
> **Liên kết bắt buộc:** Mọi thông tin trạng thái làm việc, cập nhật tính năng, sửa lỗi hoặc quyết định kỹ thuật quan trọng **PHẢI** được ghi nhận và đồng bộ vào file **[`CHANGELOG.md`](./CHANGELOG.md)**.
> - **AI chủ động cập nhật** file này sau mỗi lần hoàn thành một tính năng lớn, tái cấu trúc mã nguồn, hoặc khi kết thúc phiên làm việc.
> - Khi bắt đầu phiên làm việc mới, AI phải đọc `CHANGELOG.md` để nắm trạng thái dang dở gần nhất.

---

## 🚀 2. ĐỀ XUẤT CÔNG NGHỆ & QUY TRÌNH (BEST OFFERS FOR REVIT API)
Vì bạn mới bắt đầu phát triển Revit API, dưới đây là bộ khuyến nghị chuẩn công nghiệp để việc lập trình cùng AI đạt hiệu suất cao nhất và ít lỗi nhất:

### 2.1. Lựa chọn Ngôn ngữ & Framework
* **Ngôn ngữ chủ đạo:** **C#** (Khuyến nghị 100%).
  * *Lý do:* Revit API được tối ưu sâu nhất bằng .NET/C#. Cộng đồng hỗ trợ lớn, mã nguồn mở phong phú, kiểm tra lỗi lúc biên dịch (Compile-time type checking).
* **Phiên bản .NET mục tiêu:** **.NET 8.0 Windows (`net8.0-windows`)** cho **Revit 2025**.
  * *Lưu ý quan trọng:* Từ Revit 2025, Autodesk đã chính thức chuyển đổi toàn bộ runtime từ .NET Framework 4.8 sang **.NET 8**. Cần đảm bảo SDK .NET 8 đã được cài đặt trên máy.

### 2.2. Bộ công cụ hỗ trợ "sống còn" (Must-have Tools)
1. **RevitLookup (Bắt buộc cài):**
   * Tiện ích mở rộng chạy trực tiếp trong Revit giúp xem cấu trúc dữ liệu, Parameter, ElementId, BoundingBox của bất kỳ đối tượng nào trên màn hình.
2. **Revit Add-In Manager (Autodesk / Jeremy Tammik):**
   * Cho phép tải lại (Reload) file `.dll` trực tiếp mà **KHÔNG CẦN TẮT MỞ LẠI REVIT**. Tiết kiệm 80% thời gian thử nghiệm.
3. **IDE:**
   * **Visual Studio 2022 Community** (Khuyên dùng cho người mới vì hỗ trợ kéo thả giao diện WPF/XAML trực quan và debug mạnh mẽ).
   * Hoặc **VS Code** với tiện ích C# Dev Kit.

### 2.3. Cấu trúc dự án khuyến nghị (Pattern Clean & MVVM)
```text
PHAT TRIEN TOOL_HTKT/
├── CAD/                     # Dữ liệu bản vẽ đầu vào (.dwg trắc dọc, bình đồ)
├── REVIT/                   # Thư viện Family mẫu (.rfa hố ga, cống, móng...)
├── src/
│   ├── ToolHTKT.Core/       # Logic nghiệp vụ, thuật toán hình học, tính toán
│   ├── ToolHTKT.Revit/      # Tương tác Revit API (Commands, Events, Updaters)
│   │   ├── Commands/        # Các lệnh gắn vào nút bấm trên Ribbon (IExternalCommand)
│   │   ├── Common/          # Tiện ích chuyển đổi đơn vị, lấy tham số, lọc đối tượng
│   │   └── App.cs           # Khởi tạo Ribbon, Panels, PushButtons (IExternalApplication)
│   └── ToolHTKT.UI/         # Giao diện người dùng (WPF / MVVM)
├── AGENT.md                 # Chỉ dẫn AI & Quy tắc phát triển (File này)
└── CHANGELOG.md             # Lịch sử thay đổi và tiến độ dự án
```

---

## 📐 3. CÁC QUY TẮC CỐT LÕI TRONG REVIT API
Khi viết mã Revit API cùng AI, luôn tuân thủ nghiêm ngặt các nguyên tắc sau:

1. **Quy tắc Đơn vị (Revit Internal Units):**
   * Mọi chiều dài trong Revit API đều tính bằng **Feet (ft)**.
   * Góc tính bằng **Radian**.
   * *Bắt buộc:* Luôn sử dụng hàm chuyển đổi:
     ```csharp
     // Từ mm sang feet:
     double feet = UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
     // Từ feet sang mm:
     double mm = UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
     ```
2. **Quản lý Transaction:**
   * Mọi thao tác tạo, sửa, xóa phần tử trong mô hình **BẮT BUỘC** nằm trong một `Transaction`.
   * Luôn bọc trong khối `using (Transaction t = new Transaction(doc, "Tên hành động")) { t.Start(); ... t.Commit(); }`.
   * Hạn chế Transaction lồng nhau; dùng `SubTransaction` hoặc `TransactionGroup` khi cần gom nhóm lệnh Undo.
3. **Luồng thực thi (Thread Safety):**
   * Revit API là **Single-Threaded**. Không bao giờ gọi Revit API từ luồng nền (Background Thread / Task.Run) nếu không thông qua cơ chế `ExternalEvent` hoặc `Idling`.
4. **Xử lý cảnh báo/lỗi (Failure Handling):**
   * Khi tự động đặt hàng loạt cống/hố ga, Revit có thể hiện pop-up cảnh báo (Warning). Cần cấu hình `IFailuresPreprocessor` để tự động bỏ qua cảnh báo không nghiêm trọng mà không làm dừng tiến trình.

---

## 🎯 4. LỘ TRÌNH PHÁT TRIỂN TOOL HẠ TẦNG KỸ THUẬT (HTKT)

* **Giai đoạn 1: Thiết lập nền tảng & Tiện ích chung**
  * Tạo Solution .NET C# hỗ trợ Revit 2020 - 2026.
  * Xây dựng module nạp và quản lý Family (`CONG HOP`, `TNN_CT_THAN CONG`, `HG...`).
  * Xây dựng bộ thư viện hình học phụ trợ (tính độ dốc, cao độ đáy, nội suy tọa độ).
* **Giai đoạn 2: Tự động hóa Dựng cống & Hố ga**
  * Đọc dữ liệu tuyến từ CAD (Polyline, Block hố ga) hoặc Excel bảng tọa độ/trắc dọc.
  * Tự động rải Family Hố ga (`HG`) tại các nút giao / góc đổi hướng.
  * Tự động nối ống/cống giữa 2 hố ga và căn chỉnh cao độ đầu cống/cuối cống theo độ dốc.
* **Giai đoạn 3: Tự động lắp ghép Cấu tạo chi tiết**
  * Tự động gán móng đệm đá dăm (`DA DAM DEM`), bê tông lót (`BE TONG LOT`), gối cống (`GOI CONG`) tương ứng với từng đoạn cống.
  * Tự động đặt cửa xả (`CUA XA`) tại điểm xả cuối tuyến.
* **Giai đoạn 4: Thống kê & Xuất hồ sơ**
  * Tự động tính toán khối lượng đào đắp rãnh cống.
  * Xuất bảng thống kê cống hộp, cống tròn, hố ga theo mẫu dự toán Việt Nam.

---

## 🤝 5. NGUYÊN TẮC LÀM VIỆC CÙNG AI
1. **Chia nhỏ bài toán:** Không yêu cầu viết một tool hoàn chỉnh cả nghìn dòng code cùng lúc. Hãy giải quyết từng hàm, từng Command đơn lẻ (ví dụ: "Bước 1: Viết hàm load Family", "Bước 2: Viết hàm đặt 1 hố ga theo tọa độ X, Y, Z").
2. **Kiểm tra và Phản hồi:** Sau mỗi đoạn mã được cung cấp, người dùng biên dịch và báo cáo lỗi (nếu có) kèm ảnh chụp hoặc mã lỗi để AI tối ưu ngay.
3. **Giữ gìn Documentation:** Mọi thay đổi logic đều phải được ghi chú rõ ràng bằng tiếng Việt trong code comment.
