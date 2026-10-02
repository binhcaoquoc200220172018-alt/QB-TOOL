# CÔNG CỤ AUTOLISP: TRÍCH XUẤT TỌA ĐỘ TIM CỐNG (PHIÊN BẢN V3.0)

Cung cấp công cụ hỗ trợ cho bộ Revit Add-in **DV_TOOL_HTKT** nhằm trích xuất tọa độ VN2000 ($X_1, Y_1, Z_1, X_2, Y_2, Z_2$), chiều dài, độ dốc, góc xoay và các tham số hình học **CHUẨN 21 CỘT** từ Bình đồ AutoCAD sang file `DU_LIEU_CONG_NAP_TOOL.csv` để nạp trực tiếp vào tool Revit.

---

## 1. Điểm mới của phiên bản V3.0
* **Chuẩn hóa 21 CỘT**: Đồng bộ 100% với cấu trúc dữ liệu mới nhất của Revit Add-in (bao gồm $B\_HT1, B\_HT2, L\_Ngam\_San, Khe\_Ho\_HN$).
* **Định dạng số thực chuẩn (Không có dấu phẩy hàng nghìn)**: Tọa độ được xuất ra dưới dạng số thực `0.000` (ví dụ `580860.018` và `1477321.694`), dấu phẩy chỉ dùng để phân cách cột trong CSV, tránh hoàn toàn lỗi nhầm lẫn dấu phẩy thập phân.
* **Tự động nhận diện cống thông minh**: Tự động bóc tách loại cống (`CONG_HOP` / `CONG_TRON`), số cửa (đơn/đôi), và khẩu độ khi click chọn text lý trình.
* **Hỗ trợ lệnh tiện ích**: Thêm lệnh `XTC_OPEN` (mở ngay file CSV bằng Excel) và `XTC_RESET` (khởi tạo lại file mới từ STT 1).

---

## 2. Danh sách file trong thư mục
* **`XUAT_TIM_CONG.lsp`**: Mã nguồn AutoLISP phiên bản V3.0.
* **`HUONG_DAN_PICK_TIM_CONG.png`**: Ảnh minh họa vị trí pick điểm đầu vào Thượng lưu ($P_1$) và đầu ra Hạ lưu ($P_2$).

---

## 3. Hướng dẫn sử dụng trong AutoCAD
1. Mở bản vẽ bình đồ thoát nước trong AutoCAD.
2. Gõ lệnh: `APPLOAD` $\rightarrow$ Chọn nạp file `XUAT_TIM_CONG.lsp`.
3. Các lệnh chính:
   * **`XTC`** (hoặc `XUAT_TIM_CONG`): Pick 2 điểm tim cống (Khuyên dùng):
     * **Điểm 1**: Pick điểm đầu cống Thượng lưu ($P_1$).
     * **Điểm 2**: Pick điểm đầu cống Hạ lưu ($P_2$) (có dây chun định hướng).
     * **Điểm 3**: Click vào TEXT/MTEXT lý trình trên CAD (hoặc ấn `ENTER` để lấy mặc định).
     * Lặp lại cho đến hết tuyến, sau đó nhấn **`ESC`** để hoàn tất.
   * **`XTCL`**: Click chọn trực tiếp vào đoạn đường Line / Polyline tim cống có sẵn.
   * **`XTC_OPEN`**: Mở ngay file kết quả CSV bằng Excel để kiểm tra số liệu.
   * **`XTC_RESET`**: Xóa và khởi tạo lại file CSV từ STT 1 khi bắt đầu tuyến mới.
4. File dữ liệu `DU_LIEU_CONG_NAP_TOOL.csv` được tạo ngay tại thư mục chứa bản vẽ, có thể nạp trực tiếp vào Revit Add-in **DV_TOOL_HTKT**.
