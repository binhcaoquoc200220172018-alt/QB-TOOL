# Standing Rules

- For anything Revit-related, use the `765t-flow` server.
- The server's operating rules arrive with the connection, in the MCP `instructions` field at handshake — read them once and keep them. And for any Revit task you are not already sure how to carry out — which tools exist, in which order, under which gates — call `tool_get_guidance` first, with the task as `Query` and the `instanceId` from `flow_list_instances`; it answers the curated plan and the Revit operations rules for that task. Do not draw, model or mutate through the server before that grounding is in hand.

## QUY TẮC CỐT LÕI BẮT BUỘC VỀ ADD-IN ĐV_TOOL_HTKT (NGƯỜI DÙNG ĐÃ CHỐT)

1. **BẢO TỒN NGUYÊN VẸN THUẬT TOÁN ĐÃ CHUẨN XÁC 100%:**
   - Các thuật toán rải cống hộp (đúc sẵn & đổ tại chỗ), cống kỹ thuật (CKT), sân cống, sân gia cố, cửa xả, hố ga, hộp nối và toàn bộ phụ kiện liên quan đã được kiểm chứng và chạy HOÀN TOÀN CHÍNH XÁC.
   - **TUYỆT ĐỐI KHÔNG ĐƯỢC TỰ Ý THAY ĐỔI, TỐI ƯU LẠI HOẶC SỬA BẤT KỲ THUẬT TOÁN RẢI NÀO ĐÃ CÓ.** Mọi thay đổi vào logic này sẽ làm sai lệch mô hình thực tế.

2. **CHỈ BỔ SUNG TÍNH NĂNG MỚI (ADD-ON ONLY):**
   - Add-in chỉ được phép mở rộng, cập nhật thêm tính năng mới (ví dụ: phát triển thêm module rải cống tròn).
   - Mã nguồn mới phải được thiết kế độc lập, đóng gói module riêng biệt, tuyệt đối không được làm ảnh hưởng, xáo trộn hay can thiệp vào luồng xử lý của cống hộp và cống kỹ thuật.

3. **QUY TRÌNH DUYỆT BẮT BUỘC — CHỈ CHỈNH SỬA CODE KHI NGƯỜI DÙNG NÓI "CHỐT":**
   - Khi có yêu cầu cập nhật, nâng cấp hay thêm tính năng mới, AI chỉ được trao đổi, phân tích, đề xuất giải pháp hoặc lập kế hoạch.
   - **CHỈ KHI NÀO NGƯỜI DÙNG RA LỆNH VÀ NÓI RÕ RÀNG TỪ "CHỐT", AI MỚI ĐƯỢC PHÉP CHỈNH SỬA MÃ NGUỒN ADD-IN VÀ THÊM TÍNH NĂNG VÀO DỰ ÁN.**
   - Không có từ "chốt" thì TUYỆT ĐỐI KHÔNG chạm vào bất kỳ file code nào của add-in.
