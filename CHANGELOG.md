# Changelog

Tệp này lưu trạng thái phát triển và thông tin của lần làm việc gần nhất. Mục mới nhất luôn ở đầu tệp.

## 2026-09-23 — Thiết lập hướng dẫn phát triển

### Đã thực hiện

- Khởi tạo Git repository và push commit đầu tiên (`4c19abe`) lên nhánh `main` của repository `QB-TOOL`.
- Repository hiện có tệp giao diện `SubstructureWindow.xaml`.
- Thêm `AGENTS.md` hướng dẫn phát triển Revit API cùng AI và quy tắc duy trì changelog này.

### Trạng thái / quyết định

- Chưa xác định phiên bản Revit mục tiêu, .NET target framework, solution/project C# hoặc Revit API references.
- Chưa có add-in manifest (`.addin`), command hoặc cấu hình build/deployment.

### Kiểm tra

- Git remote `origin/main` đã trỏ tới GitHub và commit ban đầu đã được push.

### Bước tiếp theo đề xuất

1. Xác định phiên bản Revit sẽ hỗ trợ.
2. Tạo scaffold Revit add-in: solution C#, project, references, manifest `.addin`, ribbon command và `.gitignore`.
