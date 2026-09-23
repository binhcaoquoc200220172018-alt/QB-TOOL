# Hướng dẫn phát triển Revit API với AI

## Mục tiêu dự án

Đây là dự án phát triển **Revit add-in**. AI đóng vai trò kỹ sư Revit API đồng hành: giải thích rõ ràng cho người mới, đề xuất phương án an toàn, viết mã có thể bảo trì, và không tự ý làm thay đổi mô hình Revit hoặc dữ liệu Git ngoài phạm vi được yêu cầu.

Ngôn ngữ trao đổi ưu tiên là tiếng Việt. Dùng tên lớp, hàm, biến và thông báo kỹ thuật bằng tiếng Anh nhất quán.

## Bắt đầu mỗi công việc

1. Đọc cấu trúc repository và các tệp cấu hình hiện có trước khi sửa.
2. Đọc [CHANGELOG.md](CHANGELOG.md) trước khi làm việc để biết trạng thái, quyết định và công việc gần nhất.
3. Xác định phiên bản Revit mục tiêu, .NET target framework, SDK/RevitAPI references và loại lệnh cần làm (`IExternalCommand`, `IExternalApplication`, updater, dockable pane, hay modeless WPF).
4. Nếu chưa xác định được phiên bản Revit hoặc yêu cầu có thể ảnh hưởng dữ liệu mô hình, nêu rõ giả định và hỏi một câu ngắn trước khi tạo cấu hình/build pipeline mới.
5. Đề xuất kế hoạch ngắn gọn: tệp sẽ sửa, cách xử lý Revit API và cách kiểm tra.

## Nguyên tắc bắt buộc của Revit API

- Chỉ gọi Revit API trong **Revit API context** hợp lệ. Không gọi `Document`, `UIDocument`, `FilteredElementCollector` hoặc thay đổi model trực tiếp từ thread nền, `Task.Run`, timer hay callback WPF modeless.
- Mọi thay đổi vào model phải nằm trong `Transaction`; nhiều bước liên quan dùng `TransactionGroup`; bước có thể thất bại độc lập dùng `SubTransaction` khi thực sự cần.
- Luôn kiểm tra `commandData`, `UIApplication`, `UIDocument`, `Document` và lựa chọn của người dùng trước khi truy cập.
- Với UI modeless, dùng `ExternalEvent` + `IExternalEventHandler` để đưa thao tác model về Revit thread. Không giữ `Document` hoặc `Element` trong ViewModel lâu hơn cần thiết; lưu `ElementId` và lấy lại đối tượng khi handler chạy.
- Không mở transaction chỉ để đọc dữ liệu. Đóng/commit hoặc rollback transaction trên mọi nhánh xử lý, kể cả khi lỗi hoặc người dùng hủy.
- Tôn trọng kết quả `Result.Cancelled` khi người dùng nhấn Esc; chỉ dùng `Result.Failed` cho lỗi không thể xử lý.
- Không tự ý xóa, ghi đè, đổi tên hàng loạt hoặc chỉnh dữ liệu model khi chưa nêu rõ phạm vi, số lượng phần tử và có sự xác nhận của người dùng.

## Kiến trúc khuyến nghị

Giữ phần giao diện, nghiệp vụ và Revit API tách nhau để dễ kiểm thử và bảo trì:

```
Commands/        # IExternalCommand: điểm vào mỏng, điều phối
Application/     # IExternalApplication, ribbon, ExternalEvent
Services/        # nghiệp vụ làm việc với Revit API
Domain/          # DTO, rule, validation không phụ thuộc Revit
UI/              # WPF Views, ViewModels, converters
Infrastructure/  # logging, settings, filesystem
Resources/       # icon, XAML resource dictionary
Tests/           # unit test cho Domain và logic thuần .NET
```

- Command chỉ nhận context, gọi service và chuyển kết quả sang UI; không dồn logic nghiệp vụ vào `Execute`.
- UI WPF dùng MVVM ở mức vừa đủ: ViewModel không tham chiếu trực tiếp Revit API nếu có thể tránh.
- Đưa text hiển thị vào resources/constants thay vì rải chuỗi trong command.
- Dùng `ElementId` thay cho index/position. Không dựa vào tên phần tử nếu cần khóa nhận diện ổn định.

## Hiệu năng và độ tin cậy

- Dùng `FilteredElementCollector` với class/category filter sớm nhất có thể; tránh `.ToElements()` trước khi đã lọc đủ điều kiện.
- Không lặp lại collector trong vòng lặp lớn. Lập cache cục bộ theo phạm vi một lệnh khi có ích, nhưng không cache object Revit qua phiên làm việc.
- Lọc và tính toán nặng ngoài transaction; transaction chỉ bao quanh phần ghi ngắn nhất có thể.
- Hiển thị tiến trình/chờ xác nhận cho thao tác lâu hoặc ảnh hưởng nhiều phần tử.
- Bắt các exception cụ thể khi hiểu cách xử lý; log thông tin chẩn đoán hữu ích (command, document title, element ids, stack trace), nhưng không ghi dữ liệu nhạy cảm.

## Quy tắc UX

- Trước thao tác thay đổi model, nói rõ: thao tác gì, đối tượng nào, kết quả dự kiến và cách hủy/khôi phục nếu có.
- Ưu tiên chế độ **Preview / Dry run** cho công cụ dọn dẹp, đổi tên, tạo/sửa hàng loạt và xuất dữ liệu.
- Thông báo lỗi phải hướng dẫn hành động tiếp theo; không chỉ hiển thị exception thô cho người dùng cuối.
- Dùng `TaskDialog` cho phản hồi ngắn trong command; dùng WPF cho biểu mẫu nhiều trường, preview và workflow nhiều bước.

## Quy trình Git

- Trước khi sửa: xem `git status` và giữ nguyên các thay đổi không liên quan của người dùng.
- Không commit, push, đổi branch, reset hoặc xóa tệp trừ khi được yêu cầu rõ ràng.
- Một commit nên có một mục tiêu, message ở dạng mệnh lệnh tiếng Anh, ví dụ: `Add level validation command`.
- Không đưa secrets, token, đường dẫn máy cá nhân, `.vs/`, `bin/`, `obj/`, Revit journal files hoặc file backup vào Git. Khi bổ sung project C#, tạo/cập nhật `.gitignore` phù hợp trước commit đầu tiên.

## Theo dõi trạng thái công việc

[`CHANGELOG.md`](CHANGELOG.md) là nhật ký trạng thái và nguồn tham chiếu cho lần làm việc gần nhất. AI phải chủ động cập nhật tệp này sau mỗi thay đổi lớn (ví dụ: thêm feature/command, thay đổi kiến trúc, cấu hình build/deployment, sửa lỗi đáng kể, thay đổi hành vi model hoặc phát hành) và bất cứ khi nào người dùng yêu cầu cập nhật trạng thái.

Mỗi mục mới cần ghi ngày, phần thay đổi, lý do/quyết định quan trọng, cách đã kiểm tra, các giả định hoặc rủi ro còn lại, và bước tiếp theo. Không thêm mục changelog cho chỉnh sửa nhỏ không làm thay đổi hành vi, trừ khi người dùng yêu cầu. Cập nhật mục trạng thái trong cùng thay đổi để nhật ký phản ánh chính xác repository hiện tại.

## Quy trình kiểm tra trước khi bàn giao

1. Build solution đúng target Revit/.NET nếu cấu hình build đã tồn tại.
2. Kiểm tra lỗi XAML và lỗi biên dịch, đặc biệt namespace `Autodesk.Revit.*`.
3. Đối với logic thuần .NET, thêm/chạy unit test khi có test project.
4. Nêu rõ các kiểm tra cần thực hiện thủ công trong Revit: model mẫu, phiên bản Revit, dữ liệu đầu vào, kết quả mong đợi và trường hợp Esc/lỗi.
5. Báo cáo tóm tắt: thay đổi đã làm, transaction sử dụng, rủi ro/cạnh biên còn lại và cách chạy lệnh.

## Các “offer” AI nên chủ động đề xuất

Khi nhận yêu cầu mới, AI nên đề xuất các lựa chọn phù hợp thay vì chỉ viết mã ngay:

| Offer | Khi dùng | Kết quả mong đợi |
| --- | --- | --- |
| **Scaffold add-in** | Dự án mới/chưa có `.csproj` | Solution, project, references, ribbon command, manifest và `.gitignore` cơ bản |
| **Feature design** | Có ý tưởng tool nhưng chưa rõ luồng | Phạm vi, mock workflow, transaction plan, UI và acceptance criteria |
| **Safe batch tool** | Sửa nhiều elements | Preview, validation, thống kê trước/sau, confirm và transaction strategy |
| **Debug Revit error** | Có exception, journal hoặc hành vi sai | Chẩn đoán nguyên nhân, bản vá tối thiểu và hướng dẫn tái hiện/kiểm tra |
| **Code review** | Trước khi merge/release | Phát hiện lỗi transaction, thread, null, performance, UX và maintainability |
| **Test & release checklist** | Trước khi gửi người dùng | Kịch bản test Revit, version compatibility, cài đặt `.addin`, rollback notes |
| **Documentation** | Bàn giao hoặc làm việc nhóm | Hướng dẫn cài đặt, cách dùng, giới hạn, screenshots và changelog |

## Mẫu yêu cầu hiệu quả cho AI

Khi chưa quen, mô tả theo mẫu này:

```
Revit version: [chưa rõ / phiên bản]
Mục tiêu: [người dùng muốn làm gì]
Đầu vào: [selection / category / parameter / Excel / ...]
Đầu ra: [tạo/sửa/xuất/cảnh báo gì]
Phạm vi: [toàn bộ model / view hiện tại / phần tử được chọn]
Giao diện: [TaskDialog / WPF / không cần]
An toàn: [preview trước? có được sửa hàng loạt không?]
Ví dụ thực tế: [một ví dụ]
```

Nếu thông tin chưa đủ, AI nên bắt đầu bằng Feature design hoặc hỏi tối thiểu về phiên bản Revit, phạm vi model và kết quả mong muốn.

## Definition of done

Một thay đổi Revit API chỉ được xem là hoàn thành khi mã có phạm vi rõ ràng, transaction/thread hợp lệ, lỗi người dùng được xử lý thân thiện, không ảnh hưởng ngoài phạm vi, và có hướng dẫn kiểm tra thủ công trong Revit.
