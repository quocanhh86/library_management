# CẨM NANG & KỊCH BẢN DEMO BẢO VỆ ĐI THI (EXAM MASTER DATASET V3)

> **Hệ thống Quản lý Thư viện Số — Trường Đại học Giao thông Vận tải (UTC)**
> Kiến trúc: **ASP.NET Core MVC (.NET 10.0)** + **Entity Framework Core** + **SQL Server (`library_management`)**

---

## 1. LỆNH KHỞI TẠO / RESET NHANH TRƯỚC KHI VÀO PHÒNG THI

Nếu trong quá trình tập dượt bạn đã thêm/sửa/xóa nhiều dữ liệu và muốn **đưa toàn bộ 16 bảng CSDL + 140 hình ảnh sách + 46 file upload mẫu về trạng thái chuẩn 100% trước khi giám khảo chấm**, chỉ cần chạy **1 lệnh duy nhất** tại thư mục gốc dự án:

```powershell
powershell -ExecutionPolicy Bypass -File .\du_lieu_demo_di_thi\reset_data_chuan_di_thi.ps1
```

Hoặc chạy trực tiếp từ CLI của project:
```powershell
dotnet run --project web_mvc_management -- --reseed --seed-only
```

Sau đó khởi chạy ứng dụng để demo:
```powershell
dotnet run --project web_mvc_management
```

---

## 2. CƠ CHẾ ĐĂNG NHẬP & DANH SÁCH TÀI KHOẢN CHUẨN

> **Mẹo cực nhanh khi đi thi (nếu mạng phòng thi chập chờn khi nhận Gmail OTP)**:
> Mở sẵn file [`du_lieu_demo_di_thi/3_truy_van_sql_demo_ssms.sql`](./3_truy_van_sql_demo_ssms.sql) trên SSMS và chạy **Câu truy vấn số 2 (`SELECT TOP 5 ... FROM dbo.otps ORDER BY CreatedAt DESC`)** để lấy ngay mã OTP 6 số vừa phát sinh trong 1 giây!

### 2.1. Hai luồng đăng nhập tách biệt theo vai trò
1. **Cổng Cán bộ / Thủ thư / Admin (`/Account/StaffLogin`)**:
   - Cơ chế: **Passwordless Email OTP** (Chỉ cần nhập địa chỉ Email cán bộ $\rightarrow$ Nhận & nhập mã OTP 6 số).
2. **Cổng Bạn đọc (`/Account/Login`)**:
   - Cơ chế: **Xác thực 2 lớp (2FA)** — Bước 1 nhập `Username` hoặc `Email` + `Mật khẩu` (mặc định trong `.env` qua `SeedSettings__DefaultPassword` hoặc **`Admin@123456`**) $\rightarrow$ Bước 2 nhập mã OTP 6 số.

### 2.2. Danh sách Tài khoản Cán bộ / Quản trị (`dbo.staff` — Đăng nhập tại `/Account/StaffLogin`)
| Vai trò | Tên đăng nhập (`Username`) | Email đăng nhập | Trạng thái | Mục đích Demo Đi Thi |
| :--- | :--- | :--- | :--- | :--- |
| **Admin** | `quocanh_admin` | `quocanhn352@gmail.com` | `ACTIVE` | **Tài khoản Admin chính (nhận OTP Gmail thật)**: Toàn quyền quản trị, cấu hình hệ thống, quản lý nhân sự, miễn phạt, xóa vĩnh viễn (`XOA`) |
| **Admin** | `admin` | `admin@library.utc.edu.vn` | `ACTIVE` | Quản trị viên hệ thống mặc định (lấy OTP nhanh qua SSMS) |
| **Librarian** | `attendance_librarian` | `attendancestudent86@gmail.com` | `ACTIVE` | **Tài khoản Thủ thư chính (nhận OTP Gmail thật)**: Lập phiếu mượn, nhận trả sách, thu tiền phạt, quản lý đặt trước |
| **Librarian** | `librarian01` | `thuthu01@library.utc.edu.vn` | `ACTIVE` | Thủ thư Ca Sáng — Demo nghiệp vụ quầy lưu thông |
| **Librarian** | `librarian02` | `thuthu02@library.utc.edu.vn` | `ACTIVE` | Thủ thư Ca Chiều — Demo phân quyền `Librarian` (bị chặn khi truy cập `/System/Settings` hoặc `/System/Staff`) |
| **Librarian** | `librarian_locked` | `thuthu.khoa@library.utc.edu.vn` | `ACTIVE` (Đang khóa Brute-Force) | Demo **Chống Brute-Force**: Đã sai 5 lần (`failed_login_attempts = 5`, `locked_until > Now`) & Demo nút **Mở khóa (`Unlock`)** tại `/System/Staff` |
| **Librarian** | `staff_deleted` | `nhanvien.nghi@library.utc.edu.vn` | `DELETED` | Nằm sẵn trong **Thùng rác Nhân sự (`/System/Staff?status=DELETED`)** để demo **Khôi phục (`Restore`)** |

### 2.3. Danh sách Tài khoản Độc giả (`dbo.readers` — Đăng nhập tại `/Account/Login`)
| Mã thẻ (`CardNo`) | Username | Họ và tên | Email | Loại thẻ | Trạng thái | Kịch bản Demo Đi Thi |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`SV20268888`** | `quocanh_reader` | Nguyễn Quốc Anh (Độc giả) | `quocanhn352@gmail.com` | Sinh viên (`STUDENT`) | `ACTIVE` | **Tài khoản chính Demo Cổng Bạn Đọc (nhận OTP Gmail thật)**: Có 3 sách đang mượn (`0/2`, `1/2`, `2/2` gia hạn), 5 lịch sử đã trả, 4 phiếu phạt (`UNPAID 6.000đ`, `PAID TRANSFER 8.000đ`, `PAID CASH 15.000đ`, `WAIVED 2.000đ`) & đủ **4 trạng thái đặt trước (`READY`, `PENDING`, `FULFILLED`, `CANCELLED`)** |
| **`SV20268889`** | `attendance_student` | Sinh Viên Attendance | `attendancestudent86@gmail.com` | Sinh viên (`STUDENT`) | `ACTIVE` | Tài khoản Bạn đọc dự phòng nhận OTP Gmail thật (đầy đủ 100% kịch bản Cổng Bạn Đọc như `SV20268888`) |
| **`GV20268881`** | `anhnq_reader` | TS. Nguyễn Quốc Anh (FPT) | `anhnqth01267@fpt.edu.vn` | Giảng viên (`LECTURER`) | `ACTIVE` | Tài khoản Giảng viên nhận OTP thật (hạn mức 10 cuốn / 30 ngày / 3 lần gia hạn) |
| **`SV20260001`** | `student01` | Nguyễn Hoàng Long | `nguyenhoanglong@student.utc.edu.vn` | Sinh viên (`STUDENT`) | `ACTIVE` | Đang mượn 1 cuốn trong hạn (**`BC-202610-0003`**), nợ phạt `0đ`, có 1 sách đặt trước **`READY` (`BC-202610-0008`)** đang giữ tại quầy $\rightarrow$ **Dùng để demo Lập phiếu mượn mới hoặc Nhận sách đặt trước tại quầy!** |
| **`SV20260002`** | `student02` | Lê Thu Hà | `lethuha@student.utc.edu.vn` | Sinh viên (`STUDENT`) | `ACTIVE` | **Đang giữ 1 cuốn QUÁ HẠN 5 NGÀY chưa trả (`BC-202610-0006`)** + 1 cuốn sách Hot (`BC-202610-0024`) $\rightarrow$ **Dùng để demo Chặn mượn mới do quá hạn & Quét trả sách tự động sinh phiếu phạt `10.000đ`!** |
| **`SV20260003`** | `student03` | Trần Minh Khôi | `tranminhkhoi@student.utc.edu.vn` | Sinh viên (`STUDENT`) | `ACTIVE` | **Đang nợ 2 phiếu phạt `UNPAID` tổng `72.000đ` (`12.000đ` + `60.000đ` > Trần `50.000đ`)** & đang chờ đặt trước `PENDING` sách Hot #1 $\rightarrow$ **Dùng để demo Chặn mượn do nợ phạt, Thu phạt (`Pay`) & Miễn phạt (`Waive`)!** |
| **`SV20260004`** | `student04` | Phạm Thảo Tiên | `phamthaotien@student.utc.edu.vn` | Sinh viên (`STUDENT`) | `ACTIVE` | **Đang mượn chạm trần `5/5` cuốn** (`BC-202610-0025`, `0032`, `0047`, `0048`, `0062`) $\rightarrow$ **Dùng để demo Chặn mượn cuốn thứ 6 do chạm trần `MaxBorrowBooks = 5`!** |
| **`GV20260001`** | `lecturer01` | TS. Phạm Quốc Bảo | `baopq@utc.edu.vn` | Giảng viên (`LECTURER`) | `ACTIVE` | Đang mượn 2/10 cuốn (`BC-202610-0015`, `BC-202610-0058`) & có 1 đặt trước `PENDING` sách Hot #2 (`978-6047625066`) |
| **`KH20260001`** | `guest01` | Đỗ Văn Nam (Khách ngoài) | `dovannam@gmail.com` | Khách ngoài (`GUEST`) | `ACTIVE` | Đang mượn 1/2 cuốn (`BC-202610-0017`) — Demo phân loại Khách ngoài (hạn mức 2 cuốn / 7 ngày) |
| **`SV20260098`** | `student_expired` | Hoàng Gia Huy | `hoanggiahuy@student.utc.edu.vn` | Sinh viên (`STUDENT`) | Hết hạn thẻ | **Thẻ đã hết hạn (`CardExpiryDate < Today`)** $\rightarrow$ Demo Chặn mượn sách & bấm **Gia hạn thẻ (`ExtendCard`)** tại `/Admin/Readers` |
| **`SV20260099`** | `student_locked` | Vũ Minh Đức | `vuminhduc@student.utc.edu.vn` | Sinh viên (`STUDENT`) | `LOCKED` | **Thẻ đang bị khóa (`CardStatus = LOCKED`)** $\rightarrow$ Demo Chặn mượn sách & bấm **Mở khóa thẻ** tại `/Admin/Readers` |
| **`SV20260100`** | `student_deleted` | Ngô Thanh Tùng | `ngothanhtung@student.utc.edu.vn` | Sinh viên (`STUDENT`) | `DELETED` | Nằm sẵn trong **Thùng rác Độc giả (`/Admin/Readers?status=DELETED`)** để demo **Khôi phục / Xóa vĩnh viễn** |

---

## 3. CẤU TRÚC THƯ MỤC TÀI NGUYÊN ĐI THI (`du_lieu_demo_di_thi/`)

```text
du_lieu_demo_di_thi/
├── HUONG_DAN_DEMO_DI_THI.md                                    # File cẩm nang kịch bản chấm thi này
├── reset_data_chuan_di_thi.ps1                                 # Script 1-Click reset toàn bộ DB & hình ảnh
├── 3_truy_van_sql_demo_ssms.sql                                # Script SQL mở trên SSMS để show dữ liệu 16 bảng & lấy OTP nhanh
├── 1_anh_sach_de_upload/                                       # 6 bộ ảnh sách (.png & .jpg chuẩn Magic Bytes) để demo Thêm sách / Upload ảnh
│   ├── Bo_01_ASPNET_Core_10_In_Action/
│   ├── Bo_02_Deep_Learning_Computer_Vision_Giao_Thong/
│   ├── Bo_03_Thiet_Ke_Duong_Sat_Toc_Do_Cao_BIM/
│   ├── Bo_04_Quan_Tri_Chuoi_Cung_Ung_So_Cang_Bien/
│   ├── Bo_05_An_Toan_Mang_Va_Phong_Chong_Tan_Cong_Web/
│   └── Bo_06_Trac_Dia_Anh_So_UAV_Va_GIS_3D/
└── 2_file_test_bao_mat_upload/                                 # 4 file giả mạo để demo Chống Upload Mã Độc (Magic Bytes & XSS)
    ├── 01_malware_exe_gia_danh_anh.png                         # File thực thi MZ (.exe) đổi đuôi thành .png
    ├── 02_text_doi_duoi_anh.jpg                                # Text/Script đổi đuôi thành .jpg (sai chữ ký FF D8 FF)
    ├── 03_anh_polyglot_chua_xss.png                            # Ảnh PNG thật nhưng nhúng <script>alert('XSS')</script>
    └── 04_webshell.aspx.png                                    # Tấn công Double Extension (.aspx.png)
```

Mỗi thư mục trong `1_anh_sach_de_upload/` đều có sẵn **7 file**:
- `01_bia_truoc.png` — Ảnh bìa chính (`COVER`)
- `02_bia_sau.png` — Ảnh bìa sau (`BACK_COVER`)
- `03_muc_luc.png` — Ảnh trang mục lục (`TABLE_OF_CONTENTS`)
- `04_doc_thu_chuong_1.png` — Trang đọc thử Chương 1 (`PREVIEW_PAGE`)
- `05_doc_thu_chuong_2.png` — Trang đọc thử Chương 2 kèm sơ đồ kiến trúc (`PREVIEW_PAGE`)
- `06_bia_truoc_ban_jpg.jpg` — Bản `.jpg` chuẩn JPEG Magic Bytes (`FF D8 FF`)
- `THONG_TIN_COPY_NHANH.txt` — Chứa sẵn **Nhan đề, ISBN, Năm XB, Giá bìa, Mô tả, Danh sách mã vạch gợi ý** để bạn copy-paste trong 5 giây khi giám khảo yêu cầu *"Em hãy thêm mới 1 đầu sách và upload ảnh lên xem"*.

---

## 4. BẢNG TRA CỨU NHANH MÃ VẠCH & ĐẦU SÁCH THEO MỤC ĐÍCH DEMO

| Mục đích Demo khi Giám khảo hỏi | Mã vạch (`Barcode`) / ISBN cần nhập | Đầu sách & Trạng thái hiện tại |
| :--- | :--- | :--- |
| **1. Quét mã vạch để Lập phiếu mượn mới (`/Circulation/Borrow`)** | **`BC-202610-0001`**, **`BC-202610-0002`**, **`BC-202610-0004`**, **`BC-202610-0007`**, **`BC-202610-0009`** | Các bản sao đang `AVAILABLE` (Sẵn sàng cho mượn ngay) |
| **2. Nhận sách Đặt trước đang giữ tại quầy (`READY` $\rightarrow$ `FULFILLED`)** | Độc giả **`SV20260001`** + Mã vạch **`BC-202610-0008`** | *Refactoring: Improving the Design of Existing Code* (Đang `RESERVED` riêng cho `SV20260001`) |
| **3. Trả sách QUÁ HẠN 5 NGÀY để tự động sinh phiếu phạt `10.000đ` (`/Circulation/Return`)** | **`BC-202610-0006`** (Chọn tình trạng: `GOOD` / `DAMAGED` / `LOST`) | *Clean Architecture* — Độc giả `SV20260002` (Lê Thu Hà) đang giữ quá hạn 5 ngày |
| **4. Trả sách kích hoạt Bẫy Giữ Sách (`Copy Trap`: `PENDING` $\rightarrow$ `READY`)** | **`BC-202610-0024`** hoặc **`BC-202610-0025`** (hoặc **`BC-202610-0047`**, **`BC-202610-0062`**) | Khi trả về ở tình trạng `GOOD`, bản sao tự động chuyển sang `RESERVED` và phiếu đặt trước `PENDING` tự động nhảy sang `READY`! |
| **5. Trả sách đúng hạn / làm hỏng / làm mất tại quầy** | **`BC-202610-0003`** (của `SV20260001`) hoặc **`BC-202610-0015`** (của `GV20260001`) | Đang `BORROWED` trong hạn — có thể trả `GOOD` (0đ phạt), `DAMAGED` (phạt 50% giá bìa) hoặc `LOST` (phạt 150% giá bìa) |
| **6. Đầu sách HOT (`0 bản rảnh`) để Bạn đọc bấm "Đặt trước" trực tiếp trên Web** | ISBN **`978-1484289105`** (*Lập trình Web ASP.NET Core MVC 10*) hoặc **`978-6047625066`** (*Khóa luận Thư viện số*) | Có `AvailableCopies = 0` (100% bản sao đang `BORROWED`) và tài khoản `.env` chưa đặt trước $\rightarrow$ Bấm **Đặt trước** thành công ngay! |
| **7. Đầu sách `ACTIVE` sạch (chưa có lịch sử mượn) để demo Xóa mềm $\rightarrow$ Xóa cứng** | ISBN **`978-6047625059`** (*Luận án Tiến sĩ Logistics*) hoặc **`978-6048039018`** (*Tạp chí CNTT & AI*) | Chưa từng phát sinh giao dịch mượn trả $\rightarrow$ Xóa mềm rồi Xóa vĩnh viễn (`XOA`) thành công 100% |
| **8. Đầu sách nằm sẵn trong Thùng rác (`/Admin/Books?status=DELETED`)** | ISBN **`978-6040009901`** (*Turbo Pascal 7.0*) & **`978-6040009918`** (*Đầu máy Hơi nước*) | Đang ở trạng thái `DELETED` $\rightarrow$ Bấm **Khôi phục** hoặc **Xóa vĩnh viễn (`XOA`)** được ngay |

---

## 5. KỊCH BẢN DEMO CHI TIẾT THEO TỪNG CHỨC NĂNG CHẤM THI

### Kịch bản 1: Trang chủ (`/`) & Tra cứu OPAC (`/Books`, `/Books/Detail/{id}`)
1. Mở trang chủ `/`:
   - Kiểm tra khối **Thống kê trực quan**: **26 đầu sách** đang lưu hành (`ACTIVE`), **9 chuyên ngành** (bao gồm CNTT, CSDL, AI, Cầu đường, Cơ khí Ô tô, Trắc địa – GIS, Kinh tế Logistics, Khoa học cơ bản, Văn học), **75 bản sao** hoạt động.
   - Chuyển đổi 4 tab tại mục **Sách mới cập nhật**:
     - `Tất cả tài liệu` (hiển thị đủ 6 cuốn)
     - `Giáo trình` (hiển thị đủ 6 cuốn)
     - `Luận văn & Đồ án` (hiển thị đủ 6 cuốn)
     - `Tạp chí khoa học` (hiển thị đủ 6 cuốn)
2. Bấm vào bất kỳ đầu sách nào để mở `/Books/Detail/{id}`:
   - Mỗi cuốn sách đều có **Bộ sưu tập 5 hình ảnh trực quan** (`140 ảnh SVG` sắc nét trong `wwwroot/uploads/books/seed/`): Bìa trước (`COVER`), Bìa sau kèm mã vạch ISBN (`BACK_COVER`), Mục lục chi tiết (`TABLE_OF_CONTENTS`), và 2 Trang đọc thử (`PREVIEW_PAGE` có cả sơ đồ khối kiến trúc).
   - Bảng danh sách **Bản sao vật lý (`BookCopies`)** hiển thị rõ trạng thái từng mã vạch (`AVAILABLE`, `BORROWED`, `RESERVED`, `DAMAGED`, `LOST`, `DISPOSED`) và đúng vị trí kệ chuyên ngành.

### Kịch bản 2: Lưu thông — Lập Phiếu Mượn & 4 Chốt Chặn Nghiệp Vụ (`/Circulation/Borrow`)
Đăng nhập tại `/Account/StaffLogin` bằng `quocanhn352@gmail.com` (`quocanh_admin`) hoặc `admin@library.utc.edu.vn`:
1. **Demo Lập phiếu mượn thành công**:
   - Chọn độc giả **`SV20260001`** (Nguyễn Hoàng Long — đang mượn 1/5 cuốn) hoặc **`KH20260001`** (Đỗ Văn Nam — đang mượn 1/2 cuốn).
   - Nhập mã vạch đang `AVAILABLE` (ví dụ: **`BC-202610-0001`**, **`BC-202610-0002`**, **`BC-202610-0004`**) $\rightarrow$ Bấm **Lập phiếu mượn**.
   - Hoặc nhập **`BC-202610-0008`** cho **`SV20260001`** để demo **nhận sách đặt trước đang giữ tại quầy (`READY` $\rightarrow$ `FULFILLED`)**.
2. **Demo Chốt chặn 1 — Chặn độc giả đang có sách quá hạn chưa trả**:
   - Chọn độc giả **`SV20260002`** (Lê Thu Hà — đang giữ cuốn `BC-202610-0006` quá hạn 5 ngày) $\rightarrow$ Hệ thống cảnh báo và chặn mượn mới.
3. **Demo Chốt chặn 2 — Chặn độc giả nợ phạt vượt trần (`50.000đ`)**:
   - Chọn độc giả **`SV20260003`** (Trần Minh Khôi — đang nợ phạt `72.000đ` > `50.000đ`) $\rightarrow$ Hệ thống từ chối lập phiếu mượn.
4. **Demo Chốt chặn 3 — Chặn độc giả mượn chạm trần số cuốn tối đa (`5/5 cuốn`)**:
   - Chọn độc giả **`SV20260004`** (Phạm Thảo Tiên — đang mượn đủ 5/5 cuốn chưa trả) $\rightarrow$ Hệ thống chặn không cho mượn thêm cuốn thứ 6.
5. **Demo Chốt chặn 4 — Chặn thẻ hết hạn hoặc thẻ bị khóa**:
   - Chọn **`SV20260098`** (Hoàng Gia Huy — Thẻ đã hết hạn) hoặc **`SV20260099`** (Vũ Minh Đức — Thẻ `LOCKED`).

### Kịch bản 3: Nhận Trả Sách, Tự Động Tính Phạt & Bẫy Giữ Sách (`/Circulation/Return`)
1. **Trả sách quá hạn (Tự động sinh phiếu phạt `OVERDUE`)**:
   - Nhập mã vạch **`BC-202610-0006`** (Độc giả: Lê Thu Hà `SV20260002` — đang quá hạn 5 ngày).
   - Chọn tình trạng trả **`GOOD` (Tốt)** $\rightarrow$ Bấm **Xác nhận trả sách**.
   - Hệ thống tự động đóng dòng mượn, chuyển bản sao `BC-202610-0006` về `AVAILABLE`, và **tự động sinh khoản phạt quá hạn `10.000đ` (`5 ngày x 2.000đ/ngày`)**.
2. **Trả sách kích hoạt Bẫy Giữ Sách (`Copy Trap`: `PENDING` $\rightarrow$ `READY`)**:
   - Nhập mã vạch **`BC-202610-0024`** (thuộc Đầu sách Hot #1 *ASP.NET Core MVC 10*, đang có độc giả `SV20260003` đặt trước `PENDING`).
   - Chọn tình trạng trả **`GOOD`** $\rightarrow$ Bản sao **không** về `AVAILABLE` mà tự động bị bắt vào trạng thái **`RESERVED`**, đồng thời phiếu đặt trước của `SV20260003` tự động chuyển từ **`PENDING` sang `READY`** (giữ chỗ 48 giờ tại quầy)!
3. **Trả sách bị hư hỏng (`DAMAGED`) hoặc mất sách (`LOST`)**:
   - Nhập mã vạch đang mượn **`BC-202610-0003`** (của `SV20260001`), chọn tình trạng trả **`DAMAGED`** (tự động phạt 50% giá bìa) hoặc **`LOST`** (tự động phạt 150% giá bìa) $\rightarrow$ Bản sao tự động đổi sang `DAMAGED`/`LOST`.

### Kịch bản 4: Quản Lý Phạt — Thu Tiền Phạt & Miễn Giảm (`/Fines`)
- Mở màn hình `/Fines` và lọc trạng thái `UNPAID`:
  - **`SV20260003`** (Trần Minh Khôi): `12.000đ` (`OVERDUE` — `BC-202610-0009`) và `60.000đ` (`DAMAGED` — `BC-202610-0010`).
  - **`SV20268888`** (Nguyễn Quốc Anh): `6.000đ` (`OVERDUE` — `BC-202610-0012`).
- Demo bấm **Thu tiền phạt (`CASH` / `TRANSFER`)** hoặc **Miễn phạt (`Waive` — chỉ `Admin`, bắt buộc nhập lý do)**.

### Kịch bản 5: Đặt Trước Sách Hot (`/Reservations` & `/Reader/Reservations`)
Hệ thống chỉ cho phép đặt trước khi **Đầu sách có `AvailableCopies == 0`**. Đã cấu hình sẵn **3 Đầu sách Hot (0 bản rảnh ở cả 3 nhóm Giáo trình, Luận văn, Tạp chí)**:
1. **Đầu sách Hot #1 (Giáo trình)**: **`978-1484289105`** — *Lập trình Web doanh nghiệp với ASP.NET Core MVC 10 & Entity Framework Core* (`BC-202610-0024`, `BC-202610-0025` đang `BORROWED`).
2. **Đầu sách Hot #2 (Luận văn)**: **`978-6047625066`** — *Khóa luận Tốt nghiệp: Xây dựng hệ thống quản lý thư viện số thông minh tích hợp chống gian lận đa lớp* (`BC-202610-0047`, `BC-202610-0048` đang `BORROWED`).
3. **Đầu sách Hot #3 (Tạp chí)**: **`978-6040359032`** — *Kỷ yếu Hội thảo Quốc gia về Trắc địa Bản đồ và Viễn thám thông minh (GIS-UTC 2025)* (`BC-202610-0062` đang `BORROWED`, `BC-202610-0063` đã `DISPOSED`).

### Kịch bản 6: Cổng Bạn Đọc Tự Phục Vụ (`/Reader/Profile`, `/Reader/BorrowHistory`, `/Reader/Fines`, `/Reader/Reservations`)
Đăng nhập tại `/Account/Login` bằng tài khoản **`quocanh_reader`** (`quocanhn352@gmail.com`):
1. **Hồ sơ & Sách đang mượn (`/Reader/Profile` & `/Reader/BorrowHistory`)**:
   - Đang mượn 3 cuốn với 3 trạng thái gia hạn khác nhau:
     - *Clean Code* (`BC-202610-0065`, còn 10 ngày, `0/2` lần gia hạn) $\rightarrow$ Bấm **Gia hạn trực tuyến** thành công ngay (+7 ngày)!
     - *Quy hoạch và Tổ chức Giao thông Đô thị thông minh (ITS)* (`BC-202610-0066`, sắp đến hạn còn 2 ngày, `1/2` lần gia hạn) $\rightarrow$ Bấm **Gia hạn trực tuyến** lần cuối thành công!
     - *Cấu trúc dữ liệu và Giải thuật nâng cao* (`BC-202610-0067`, đã gia hạn `2/2` lần) $\rightarrow$ Nút gia hạn tự động khóa kèm chú thích *"Đã hết lượt gia hạn (2/2)"*.
2. **Tra cứu Phạt cá nhân (`/Reader/Fines`)**:
   - Hiển thị đầy đủ 4 phiếu phạt ở cả 3 trạng thái: `UNPAID` (`6.000đ`), `PAID` (`8.000đ` chuyển khoản & `15.000đ` tiền mặt), và `WAIVED` (`2.000đ` miễn phạt nghỉ lễ).
3. **Quản lý Đặt trước cá nhân (`/Reader/Reservations`)**:
   - Hiển thị đầy đủ cả **4 trạng thái đặt trước**: `READY` (đang giữ bản sao `BC-202610-0068` tại kệ `KE-VH-01`), `PENDING` (có thể bấm **Hủy đặt trước** ngay), `FULFILLED` (đã nhận sách), và `CANCELLED` (đã hủy).
   - Mở trang chi tiết sách Hot #1 (`978-1484289105` — *ASP.NET Core MVC 10*) và bấm **"Đặt trước khi có sách"** trực tiếp!

### Kịch bản 7: Xóa Mềm, Khôi Phục, Xóa Vĩnh Viễn & Bảo Mật Upload Magic Bytes
1. **Thùng rác & Khôi phục / Xóa cứng 3 bước**:
   - Tất cả 9 màn hình quản trị danh mục đều đã có sẵn bản ghi trong tab **Thùng rác (`?status=DELETED`)**:
     - `/Admin/Books?status=DELETED` (2 đầu sách cổ điển)
     - `/Admin/BookCopies?status=DELETED` (1 bản sao `BC-202610-0064`)
     - `/Admin/Categories?status=DELETED` (1 thể loại cũ)
     - `/Admin/Publishers?status=DELETED` (1 NXB cũ)
     - `/Admin/Authors?status=DELETED` (1 tác giả lưu trữ)
     - `/Admin/Shelves?status=DELETED` (1 kệ kho `KE-CU-99`)
     - `/Admin/ReaderTypes?status=DELETED` (1 loại thẻ `ALUMNI_OLD`)
     - `/Admin/Readers?status=DELETED` (1 độc giả `SV20260100`)
     - `/System/Staff?status=DELETED` (1 nhân sự `staff_deleted`)
2. **Bảo mật Upload Ảnh & Kiểm tra Magic Bytes (`du_lieu_demo_di_thi/2_file_test_bao_mat_upload/`)**:
   - Vào `/Admin/Books/Create` hoặc `/Admin/Books/Edit/{id}`:
   - Thử upload lần lượt 4 file giả mạo trong `2_file_test_bao_mat_upload/` (`01_malware_exe_gia_danh_anh.png`, `02_text_doi_duoi_anh.jpg`, `03_anh_polyglot_chua_xss.png`, `04_webshell.aspx.png`) $\rightarrow$ Hệ thống kiểm tra chữ ký nhị phân (Magic Bytes), quét nội dung script nhúng và chặn đứng toàn bộ!
   - Sau đó chọn bộ ảnh thật trong `1_anh_sach_de_upload/Bo_01_ASPNET_Core_10_In_Action/` kèm thông tin trong `THONG_TIN_COPY_NHANH.txt` $\rightarrow$ Upload và tạo mới thành công!
