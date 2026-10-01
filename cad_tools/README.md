# CÔNG CỤ AUTOLISP: TRÍCH XUẤT TỌA ĐỘ TIM CỐNG (V2.0)

Cung cấp công cụ hỗ trợ cho bộ Revit Addin **DV_TOOL_HTKT** nhằm trích xuất tọa độ VN2000 (X1, Y1, X2, Y2), chiều dài và góc xoay từ Bình đồ AutoCAD sang file `TOA_DO_CONG.csv` để nạp vào tool Revit.

---

## 1. Danh sách file
* **`XUAT_TIM_CONG.lsp`**: Mã nguồn AutoLISP phiên bản V2.0 (chống crash ActiveX khi gặp Block, tự động lọc sạch text lý trình, chống khóa file Excel).
* **`HUONG_DAN_PICK_TIM_CONG.png`**: Ảnh minh họa vị trí pick điểm đầu vào Thượng lưu ($P_1$) và đầu ra Hạ lưu ($P_2$).

---

## 2. Hướng dẫn sử dụng trong AutoCAD
1. Mở bản vẽ bình đồ thoát nước trong AutoCAD.
2. Gõ lệnh: `APPLOAD` $\rightarrow$ Chọn nạp file `XUAT_TIM_CONG.lsp`.
3. Gõ lệnh: **`XTC`** (hoặc `XUAT_TIM_CONG`, `TIMCONG`).
4. Thao tác lần lượt:
   * **Điểm 1**: Pick điểm đầu cống Thượng lưu ($P_1$).
   * **Điểm 2**: Pick điểm đầu cống Hạ lưu ($P_2$) (có dây chun định hướng).
   * **Điểm 3**: Click vào dòng chữ hoặc mũi tên chỉ dẫn lý trình (hoặc ấn `ENTER` để đặt tên mặc định).
5. Sau khi pick hết tuyến, nhấn phím **`ESC`** để kết thúc lệnh.
6. File kết quả `TOA_DO_CONG.csv` sẽ tự động tạo ngay tại thư mục chứa file bản vẽ CAD.
