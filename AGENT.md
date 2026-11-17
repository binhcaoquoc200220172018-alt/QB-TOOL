# 🤖 HƯỚNG DẪN ĐỒNG HÀNH PHÁT TRIỂN REVIT API ADD-IN (AGENT.MD)

Tài liệu này đóng vai trò là **Kim chỉ nam (System Instruction, Technical Stack & AI Collaboration Framework)** cho Trợ lý AI khi đồng hành cùng Kỹ sư phát triển hệ thống Add-in Revit chuyên ngành **Hạ tầng Kỹ thuật (HTKT - Thoát nước mưa, Thoát nước thải, Cấp nước, Cống ngang, Cống kỹ thuật, Hố ga)**.

---

## 📌 1. QUẢN LÝ TRẠNG THÁI & LIÊN KẾT NHẬT KÝ (CHANGELOG.MD)
> [!IMPORTANT]
> **Liên kết bắt buộc:** Mọi thông tin trạng thái làm việc, cập nhật tính năng, sửa đổi mã nguồn hoặc quyết định kỹ thuật quan trọng **PHẢI** được ghi nhận và đồng bộ vào file **[`CHANGELOG.md`](./CHANGELOG.md)**.
> - **AI chủ động cập nhật:** AI có trách nhiệm tự động cập nhật `CHANGELOG.md` mỗi khi hoàn thành một chức năng, tái cấu trúc mã nguồn, khắc phục lỗi hoặc khi người dùng yêu cầu.
> - **Khởi đầu phiên làm việc:** Mỗi khi mở phiên trao đổi mới, AI phải đọc `CHANGELOG.md` đầu tiên để nắm bắt chính xác tiến độ và trạng thái dang dở của dự án.

---

## 🚀 2. ĐỀ XUẤT TỐT NHẤT (BEST OFFERS) KHI PHÁT TRIỂN REVIT API VỚI AI
Dành cho kỹ sư mới bắt đầu lập trình Revit API, dưới đây là bộ khuyến nghị chuẩn công nghiệp để việc phát triển đạt hiệu quả tối đa, tối ưu tốc độ và hạn chế phát sinh lỗi:

### 2.1. Lựa chọn Ngôn ngữ & Nền tảng Runtime
* **Ngôn ngữ chủ đạo:** **C#** (Khuyến nghị 100%).
  * *Lý do:* Revit API được viết và tối ưu hóa sâu nhất cho .NET C#. Hỗ trợ kiểm tra lỗi lúc biên dịch (compile-time type safety), cộng đồng giải pháp lớn nhất.
* **Phiên bản Framework:** **.NET 8.0 Windows (`net8.0-windows`)** cho **Revit 2025+**.
  * *Lưu ý quan trọng:* Từ Revit 2025, Autodesk đã chuyển toàn bộ lõi Revit API từ .NET Framework 4.8 sang **.NET 8**. Không dùng các package cũ chỉ hỗ trợ .NET Framework.
* **Thư viện xử lý Excel:** **ClosedXML** (phiên bản `0.104.2+`).
  * Đọc/ghi trực tiếp file `.xlsx` tốc độ cao, không cần cài đặt Microsoft Excel trên máy tính, không sợ xung đột tiến trình COM (`EXCEL.EXE`).

### 2.2. Bộ ba Công cụ "Sống còn" Cần Cài Đặt Ngay
1. **RevitLookup (Bắt buộc phải có):**
   * Tiện ích mở rộng chạy trực tiếp trong Revit, cho phép "soi" cấu trúc dữ liệu, Parameter ID, BuiltInParameter, StorageType, BoundingBox của bất kỳ cấu kiện nào trên màn hình.
2. **Revit Add-In Manager (Jeremy Tammik / Autodesk):**
   * Cho phép tải lại (Reload / Modify) file `.dll` trực tiếp mà **KHÔNG CẦN TẮT MỞ LẠI REVIT**. Tiết kiệm hàng chục giờ kiểm thử mỗi tuần.
3. **Visual Studio 2022 Community (hoặc VS Code C# Dev Kit):**
   * Hỗ trợ thiết kế trực quan giao diện WPF/XAML, IntelliSense gợi ý mã nguồn và trình gỡ lỗi (Debugger) mạnh mẽ.

### 2.3. Mô hình Kiến trúc Chuẩn (MVVM & Modeless Window Thread-Safety)
Revit API là môi trường **Single-Threaded** (đơn luồng). Khi thiết kế giao diện WPF không khóa màn hình Revit (Modeless Window):
* **Tuyệt đối KHÔNG gọi Revit API trực tiếp từ sự kiện UI Click** nếu không đang trong luồng chính của Revit.
* **Bắt buộc sử dụng cơ chế `IExternalEventHandler` / `ExternalEvent`:**
  * WPF Window chỉ nhận tương tác từ người dùng và chuẩn bị dữ liệu trong ViewModel.
  * Khi bấm nút (ví dụ: *Rải Hố Ga*), kích hoạt `externalEvent.Raise()`.
  * Mã lệnh Revit API sẽ thực thi an toàn trong hàm `Execute(UIApplication app)` của Handler.

```
┌─────────────────────────────────┐
│     WPF UI Window (Modeless)    │
│  (DataBinding, Validations)     │
└──────────────┬──────────────────┘
               │ 1. Người dùng bấm nút / Đổi cấu hình
               ▼
┌─────────────────────────────────┐
│           ViewModel             │
│   (Chuẩn bị dữ liệu / Model)    │
└──────────────┬──────────────────┘
               │ 2. externalEvent.Raise()
               ▼
┌─────────────────────────────────┐
│    IExternalEventHandler        │
│   (Chạy trong Revit Main Thread)│
└──────────────┬──────────────────┘
               │ 3. Mở Transaction & Gọi Core Engine
               ▼
┌─────────────────────────────────┐
│        Revit Core Engine        │
│ (NewFamilyInstance, Parameters) │
└─────────────────────────────────┘
```

### 2.4. Nguyên Tắc Phát Triển & Trình Duyệt Giao Diện (Visual Live Preview Trong Antigravity)
> [!IMPORTANT]
> **Quy trình tương tác giao diện bắt buộc giữa AI và Kỹ sư:**
> 1. **Luôn hiển thị Live Preview trực tiếp trong chat:** Mỗi khi đề xuất thiết kế, điều chỉnh bố cục, bổ sung tính năng hoặc cập nhật giao diện Add-in, AI **BẮT BUỘC** phải nhúng trực tiếp giao diện tương tác (Live Preview Widget) bằng thẻ `<agent-embed src="file:///.../preview_manhole_ui.html"></agent-embed>` ngay trong khung chat của Antigravity.
> 2. **Kỹ sư thao tác trực tiếp trước khi duyệt:** Kỹ sư sẽ thao tác trực tiếp trên giao diện nhúng này (chuyển tab, click chọn hố ga, xoay 3D, đổi góc nhìn, kiểm tra các ô thông số cao độ) để hình dung trọn vẹn và đánh giá thực tế.
> 3. **Chỉ sửa code Add-in khi Kỹ sư nói "CHỐT":** Tuyệt đối không vội vàng sửa file `.cs` / `.xaml` khi Kỹ sư chưa chốt phương án. Chỉ khi Kỹ sư phản hồi *"chốt"* hoặc đồng ý phương án, AI mới được phép tiến hành sửa mã nguồn, biên dịch kiểm thử và đồng bộ vào Revit.

---

## 📐 3. CÁC QUY TẮC VÀNG TRONG LẬP TRÌNH REVIT API

### 3.1. Đơn vị đo nội bộ (Internal Units)
* **Chiều dài:** Revit API luôn luôn tính bằng **Feet (ft)** (1 meter ≈ 3.28084 feet).
* **Góc:** Luôn luôn tính bằng **Radian** (180° = π rad).
* **Bắt buộc sử dụng hàm chuẩn của Revit:**
  ```csharp
  // Chuyển từ Mét sang Feet nội bộ
  double feet = UnitUtils.ConvertToInternalUnits(chieuDaiMet, UnitTypeId.Meters);
  // Chuyển từ Feet nội bộ sang Mét
  double met = UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);
  // Đổi góc từ Độ sang Radian
  double rad = gocDo * Math.PI / 180.0;
  ```

### 3.2. Quản lý Giao dịch (Transaction & TransactionGroup)
* Mọi hành động thêm, sửa, xoá phần tử trong Revit bắt buộc phải đặt trong `Transaction`.
* Để gom nhiều bước (tạo cấu kiện, gán thông số, gán vật liệu) thành 1 bước Undo duy nhất trong Revit, sử dụng `TransactionGroup`:
  ```csharp
  using (var tg = new TransactionGroup(doc, "Rải Hố Ga Tự Động"))
  {
      tg.Start();
      using (var t = new Transaction(doc, "Đặt Family Hố Ga"))
      {
          t.Start();
          // Thao tác Revit API...
          t.Commit();
      }
      tg.Assimilate(); // Gom toàn bộ hành động thành 1 Undo duy nhất
  }
  ```

### 3.3. Tự động xử lý Cảnh báo (Suppress All Warnings)
Khi tự động rải hàng loạt phần tử, Revit thường hiện hộp thoại Warning (như trùng lặp vị trí, lệch mặt cắt...) làm gián đoạn tiến trình. Cần gắn bộ tiền xử lý `IFailuresPreprocessor`:
```csharp
public class SuppressAllWarningsFailuresPreprocessor : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        var msgs = failuresAccessor.GetFailureMessages();
        foreach (var msg in msgs)
        {
            if (msg.GetSeverity() == FailureSeverity.Warning)
            {
                failuresAccessor.DeleteWarning(msg);
            }
        }
        return FailureProcessingResult.Continue;
    }
}
```

### 3.4. Xử lý Family Lồng nhau (Nested Shared Families)
Các Family hạ tầng phức tạp (Hố ga có nắp gang, thân, cổ thu, ống nối) thường được ghép từ các Family con:
* Để truy cập các bộ phận con bên trong một `FamilyInstance`, sử dụng:
  ```csharp
  ICollection<ElementId> subIds = instance.GetSubComponentIds();
  foreach (ElementId subId in subIds)
  {
      Element subElem = doc.GetElement(subId);
      // Gán Parameter hoặc vật liệu cho cấu kiện con
  }
  ```
* Luôn kiểm tra tính sẵn sàng trước khi gán tham số:
  ```csharp
  Parameter p = elem.LookupParameter("Tên Tham Số");
  if (p != null && !p.IsReadOnly)
  {
      p.Set(value);
  }
  ```

---

## 🎨 4. PHONG CÁCH GIAO DIỆN (UI/UX DESIGN SYSTEM)
Đồng bộ 100% với hệ thống giao diện công cụ Hạ tầng Kỹ thuật đã phát triển:
* **Màu nền chủ đạo:** Dark Navy (`#0A1120`), Khung Card (`#132035`), Khung con (`#182A44`), Đường viền (`#223959`).
* **Màu điểm nhấn (Accent):**
  * Xanh lơ (Cyan): `#38BDF8` (Tiêu đề, Tab đang chọn, Focus, Border).
  * Xanh lá mạ (Mint Green): `#4ADE80` / `#16A34A` (Nút bấm thực thi chính, Trạng thái thành công).
* **Màu cảnh báo / Lỗi:** Đỏ cam (`#EF4444`).
* **Font chữ:** `Segoe UI`, kích thước chuẩn 12 - 13px, độ tương phản cao với chữ trắng (`#FFFFFF`) và xanh nhạt (`#BAE6FD`).

---

## 🤝 5. QUY TẮC PHỐI HỢP VỚI AI
1. **Chia nhỏ và Xác nhận theo Module:**
   * Module 1: Xử lý Đọc/Ghi dữ liệu Excel & Tạo file mẫu.
   * Module 2: Nạp Family, Phân tích tham số (Kích thước, Vật liệu, Văn bản).
   * Module 3: Xây dựng Giao diện người dùng WPF (4 Tabs).
   * Module 4: Xây dựng Core Engine Revit API (Chuyển đổi tọa độ, Đặt Family, Xoay góc, Gán thông số & Subcomponents).
2. **Ghi chú bằng Tiếng Việt chuẩn:**
   * Mọi mã nguồn C# đều phải có XML Documentation (`/// <summary>`) bằng tiếng Việt rõ ràng, dễ hiểu.
3. **Bảo toàn và Tương thích:**
   * Không phá vỡ các thư viện dùng chung (`CoordinateService`, `ExcelReaderService`).
   * Đảm bảo biên dịch thành công 0 lỗi với `dotnet build`.
