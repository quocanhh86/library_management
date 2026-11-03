# BẢNG TỔNG HỢP TRẠNG THÁI CÁC MODULE & CHỨC NĂNG DỰ ÁN QUẢN LÝ THƯ VIỆN UTC

> Đối chiếu trực tiếp với đặc tả yêu cầu **Phân tích hệ thống Quản lý thư viện.docx** trên nền tảng **ASP.NET Core MVC (.NET 10.0) + Entity Framework Core + SQL Server (`library_management`)**.

---

## 📊 Tóm Tắt Tiến Độ Tổng Thể

| STT | Phân Hệ Nghiệp Vụ (Theo Spec `.docx`) | Trạng Thái | Tỷ Lệ Hoàn Thành | Ghi Chú Kỹ Thuật |
| :-: | :--- | :---: | :---: | :--- |
| **0** | **Hạ tầng Common, Bảo mật `.env`, DB Schema & Seed Data** | ✅ **Hoàn tất** | **100%** | 17 Entities, `UnitOfWork`, `ServiceResult`, `.env` tập trung, Gmail SMTP, Cloudinary. |
| **1** | **Xác thực, Phân quyền & Bảo mật OTP 2FA (Mục 1, 2.f, 3)** | ✅ **Hoàn tất** | **100%** | Cookie Auth (`Admin`, `Librarian`, `Reader`), OTP 6 số qua Gmail SMTP, chống Brute-force. |
| **2** | **Cổng Tra cứu Sách Công khai & Đa ảnh OPAC (Mục 2.a, 3)** | ✅ **Hoàn tất** | **100%** | Tìm kiếm/lọc sách AJAX, gợi ý nhanh, xem chi tiết sách, bộ sưu tập đa ảnh & đọc thử trang sách. |
| **3** | **Quản lý Danh mục Sách & Bản sao Vật lý (Mục 2.a, 4.1)** | ✅ **Hoàn tất** | **100%** | CRUD Đầu sách, Upload đa ảnh Cloudinary, Bản sao/Barcode, Tác giả, Thể loại, NXB, Kệ sách, Thanh lý/Báo hỏng/Xóa 2 bước. |
| **4** | **Quản lý Độc giả & Thẻ thư viện (Mục 2.b, 4.2)** | ✅ **Hoàn tất** | **100%** | CRUD Độc giả, Cấp thẻ, Gia hạn thẻ, Khóa/Mở khóa thẻ, Quản lý Loại độc giả (`ReaderTypes`). |
| **5** | **Nghiệp vụ Lưu thông: Mượn – Trả – Gia hạn – Đặt trước (Mục 2.c, 4.3)** | ✅ **Hoàn tất** | **100%** | Lập phiếu mượn (kiểm tra đủ 4 điều kiện), Nhận trả sách từng cuốn, Gia hạn mượn, Đặt trước sách & giữ bản sao (`Copy Trap`). |
| **6** | **Quản lý Phạt: Quá hạn, Hư hỏng, Làm mất (Mục 2.d, 4.4)** | ✅ **Hoàn tất** | **100%** | Tự động tính phạt trễ hạn/hỏng/mất khi trả sách, Thu tiền phạt (`CASH`/`TRANSFER`), Miễn phạt (`Admin`). |
| **7** | **Quản trị Hệ thống: Nhân sự & Quy định Mượn trả (Mục 2.f)** | ✅ **Hoàn tất** | **100%** | CRUD Cán bộ/Thủ thư (`/System/Staff`), Cấu hình tham số phạt & giữ sách (`/System/Settings`). |
| **8** | **Thống kê, Báo cáo & Lịch sử Phiếu mượn (Mục 2.e)** | 🟡 **Đã có UI (Đang dùng Mock)** | **60%** | Đã có giao diện `/Admin/Dashboard`, `/Reports`, `/Circulation/History` và đủ dữ liệu DB; đang dùng `MockAdminPortalService`, chờ nối sang truy vấn EF Core thật. |
| **9** | **Trang Cá nhân Độc giả Tự Phục Vụ (Mục 3 - Độc giả)** | 🟡 **Chờ hoàn thiện** | **50%** | Độc giả đã Đăng ký/Đăng nhập/Tra cứu/Xem chi tiết sách & bản sao; cần nối thêm màn hình cá nhân xem sách đang mượn, lịch sử mượn, tiền phạt của riêng độc giả. |

---

## 🧩 Chi Tiết Từng Module Tương Ứng Với Chức Năng, Route, Controller, Service & View

### Module 1: Xác Thực, Phân Quyền & Bảo Mật OTP 2FA (`Account`) — ✅ ĐÃ XONG 100%

| Chức năng chi tiết | Route / URL | Controller & Action | Service & Entity | Razor View (`.cshtml`) |
| :--- | :--- | :--- | :--- | :--- |
| Đăng ký thẻ bạn đọc trực tuyến + gửi OTP | `GET/POST /Account/Register` | `AccountController.Register` | `AuthService`, `OtpService`, `EmailService` (`Reader`, `OtpEntry`) | `Views/Account/Register.cshtml` |
| Đăng nhập Bạn đọc (Username/Email + Password -> OTP 2FA) | `GET/POST /Account/Login` | `AccountController.Login` | `AuthService.AuthenticateReaderAsync` (`Reader`) | `Views/Account/Login.cshtml` |
| Đăng nhập Cán bộ / Thủ thư / Admin (Email -> OTP 2FA) | `GET/POST /Account/StaffLogin` | `AccountController.StaffLogin` | `AuthService.InitiateStaffPasswordlessLoginAsync` (`Staff`) | `Views/Account/StaffLogin.cshtml` |
| Xác thực mã OTP 6 chữ số (Đăng ký, Đăng nhập, Quên MK) | `GET/POST /Account/VerifyOtp` | `AccountController.VerifyOtp` | `OtpService.VerifyOtpAsync`, `EmailService` (`OtpEntry`) | `Views/Account/VerifyOtp.cshtml` |
| Quên mật khẩu & Đặt lại mật khẩu bằng OTP | `GET/POST /Account/ForgotPassword`<br>`GET/POST /Account/ResetPassword` | `AccountController.ForgotPassword`<br>`AccountController.ResetPassword` | `AuthService.InitiateForgotPasswordAsync`<br>`AuthService.ResetPasswordAsync` | `Views/Account/ForgotPassword.cshtml`<br>`Views/Account/ResetPassword.cshtml` |
| Đăng xuất & Từ chối truy cập (403) | `POST /Account/Logout`<br>`GET /Account/AccessDenied` | `AccountController.Logout`<br>`AccountController.AccessDenied` | Cookie Authentication + Session Clear | `Views/Account/AccessDenied.cshtml` |

---

### Module 2: Cổng Tra Cứu Sách Bạn Đọc & Đa Hình Ảnh OPAC (`Public Catalog`) — ✅ ĐÃ XONG 100%

| Chức năng chi tiết | Route / URL | Controller & Action | Service & Entity | Razor View (`.cshtml`) |
| :--- | :--- | :--- | :--- | :--- |
| Trang chủ Thư viện (Sách mới nhập, Chủ đề, Nội quy, Giờ mở cửa) | `GET /` | `HomeController.Index`<br>`HomeController.Rules` | `BookService.GetNewArrivalsAsync`<br>`LibraryInfoService` (`LibrarySetting`, `ReaderType`) | `Views/Home/Index.cshtml`<br>`Views/Home/Rules.cshtml` |
| Tìm kiếm & Lọc sách động (AJAX không tải lại trang theo Từ khóa, Thể loại, Tác giả, NXB, Năm XB, Còn bản sao) | `GET /Books`<br>`GET /Books/Suggest` | `BooksController.Index`<br>`BooksController.Suggest` | `BookService.GetPublicCatalogAsync`<br>`BookService.SuggestAsync` (`Book`, `BookCopy`, `BookImage`) | `Views/Books/Index.cshtml`<br>`Views/Books/_BookResults.cshtml` |
| Xem chi tiết đầu sách, Bộ sưu tập đa ảnh (Bìa, Mục lục, Trang đọc thử + Modal phóng to lật trang) & Danh sách bản sao theo kệ | `GET /Books/Detail/{id}` | `BooksController.Detail` | `BookService.GetPublicBookDetailAsync` (`Book`, `BookImage`, `BookCopy`, `Shelf`) | `Views/Books/Detail.cshtml` |

---

### Module 3: Quản Lý Danh Mục Sách, Đa Ảnh Cloudinary & Bản Sao Mã Vạch (`Admin Catalog`) — ✅ ĐÃ XONG 100%

| Chức năng chi tiết | Route / URL | Controller & Action | Service & Entity | Razor View (`.cshtml`) |
| :--- | :--- | :--- | :--- | :--- |
| Danh sách & Bộ lọc Đầu sách (kèm ảnh bìa thu nhỏ & số lượng ảnh) | `GET /Admin/Books` | `AdminBooksController.Index` | `BookService.GetAdminBooksPagedAsync` (`Book`, `BookImage`, `BookCopy`) | `Views/AdminBooks/Index.cshtml` |
| Nhập đầu sách mới kèm lô bản sao ban đầu + Upload ảnh bìa & nhiều ảnh trang sách lên Cloudinary | `GET/POST /Admin/Books/Create` | `AdminBooksController.Create` | `BookService.CreateBookWithCopiesAsync`, `CloudinaryService` (`Book`, `BookAuthor`, `BookImage`, `BookCopy`) | `Views/AdminBooks/Form.cshtml` |
| Cập nhật đầu sách, thêm/xóa ảnh trang sách, đổi ảnh bìa chính | `GET/POST /Admin/Books/Edit/{id}` | `AdminBooksController.Edit` | `BookService.UpdateBookAsync`, `CloudinaryService` (`Book`, `BookImage`) | `Views/AdminBooks/Form.cshtml` |
| Xóa mềm (`DELETED`), Khôi phục & Xóa vĩnh viễn (`HardDelete`) đầu sách | `POST /Admin/Books/Delete/{id}`<br>`POST /Admin/Books/Restore/{id}`<br>`POST /Admin/Books/HardDelete/{id}` | `AdminBooksController` | `BookService.DeleteBookAsync`<br>`BookService.RestoreBookAsync`<br>`BookService.HardDeleteBookAsync` | `Views/AdminBooks/Index.cshtml` |
| Quản lý Bản sao vật lý / Mã vạch (`Barcode`), Nhập thêm lô bản sao, Đổi kệ, Thanh lý / Báo hỏng / Báo mất | `GET /Admin/BookCopies`<br>`POST /Admin/BookCopies/CreateBatch`<br>`POST /Admin/BookCopies/Dispose` | `AdminBookCopiesController` | `BookService.GetBookCopiesPagedAsync`<br>`BookService.CreateBookCopiesBatchAsync`<br>`BookService.DisposeOrMarkCopyAsync` (`BookCopy`) | `Views/AdminBookCopies/Index.cshtml` |
| Quản lý Danh mục Thể loại (`Categories`), Tác giả (`Authors`), Nhà xuất bản (`Publishers`), Kệ sách (`Shelves`) | `GET/POST /Admin/Categories`<br>`GET/POST /Admin/Authors`<br>`GET/POST /Admin/Publishers`<br>`GET/POST /Admin/Shelves` | `AdminMasterDataController` | `CatalogMasterService` (`Category`, `Author`, `Publisher`, `Shelf`) | `Views/AdminMasterData/Categories.cshtml`<br>`Views/AdminMasterData/Authors.cshtml`<br>`Views/AdminMasterData/Publishers.cshtml`<br>`Views/AdminMasterData/Shelves.cshtml` |

---

### Module 4: Quản Lý Độc Giả & Thẻ Thư Viện (`Admin Readers`) — ✅ ĐÃ XONG 100%

| Chức năng chi tiết | Route / URL | Controller & Action | Service & Entity | Razor View (`.cshtml`) |
| :--- | :--- | :--- | :--- | :--- |
| Danh sách độc giả, lọc theo Loại độc giả, Trạng thái thẻ (`ACTIVE`, `LOCKED`, `EXPIRED`) | `GET /Admin/Readers` | `AdminReadersController.Index` | `SystemAdminService.GetReadersPagedAsync` (`Reader`, `ReaderType`) | `Views/AdminReaders/Index.cshtml` |
| Cấp thẻ độc giả mới, Cập nhật hồ sơ, Gia hạn thẻ thư viện | `POST /Admin/Readers/Create`<br>`POST /Admin/Readers/Update`<br>`POST /Admin/Readers/ExtendCard/{id}` | `AdminReadersController` | `SystemAdminService.CreateReaderAccountAsync`<br>`UpdateReaderAccountAsync`<br>`RenewReaderCardAsync` | `Views/AdminReaders/Index.cshtml` |
| Khóa thẻ / Mở khóa thẻ độc giả vi phạm, Đặt lại mật khẩu độc giả | `POST /Admin/Readers/ToggleCardLock/{id}`<br>`POST /Admin/Readers/ResetPassword` | `AdminReadersController` | `SystemAdminService.ToggleReaderCardLockAsync`<br>`ResetReaderPasswordAsync` | `Views/AdminReaders/Index.cshtml` |

---

### Module 5: Nghiệp Vụ Lưu Thông: Mượn – Trả – Gia Hạn – Đặt Trước (`Circulation`) — ✅ ĐÃ XONG 100%

| Chức năng chi tiết | Route / URL | Controller & Action | Service & Entity | Razor View (`.cshtml`) |
| :--- | :--- | :--- | :--- | :--- |
| Quầy Lập phiếu mượn sách (Kiểm tra 4 điều kiện: Thẻ còn hạn, Chưa vượt hạn mức `MaxBorrowBooks`, Không nợ phạt, Bản sao sẵn sàng) | `GET/POST /Circulation/Borrow` | `CirculationController.Borrow` | `CirculationService.GetDeskAsync`<br>`CirculationService.CreateBorrowSlipAsync` (`BorrowSlip`, `BorrowDetail`, `BookCopy`) | `Views/Circulation/Borrow.cshtml` |
| Quầy Nhận trả sách từng cuốn (Ghi nhận tình trạng `GOOD`, `DAMAGED`, `LOST`; tự động tính phạt nếu quá hạn/hỏng/mất) | `GET/POST /Circulation/Return` | `CirculationController.Return` | `CirculationService.ReturnAsync` (`BorrowDetail`, `BookCopy`, `Fine`, `Reservation`) | `Views/Circulation/Return.cshtml` |
| Gia hạn mượn sách (Kiểm tra số lần gia hạn tối đa `MaxRenewals`, sách chưa quá hạn và không có người đặt trước chờ) | `POST /Circulation/Renew` | `CirculationController.Renew` | `CirculationService.RenewAsync` (`BorrowDetail`, `ReaderType`, `Reservation`) | `Views/Circulation/Return.cshtml` |
| Quản lý Đặt trước sách (`Reservations` — Đặt theo đầu sách, tự động giữ bản sao `READY` trong 48h khi có sách trả về) | `GET /Reservations`<br>`POST /Reservations/Create`<br>`POST /Reservations/Cancel` | `CirculationController.Reservations`<br>`CreateReservation`<br>`CancelReservation` | `CirculationService.GetReservationsPageAsync`<br>`CreateReservationAsync`<br>`CancelReservationAsync` (`Reservation`) | `Views/Circulation/Reservations.cshtml` |

---

### Module 6: Quản Lý Phạt (`Fines`) — ✅ ĐÃ XONG 100%

| Chức năng chi tiết | Route / URL | Controller & Action | Service & Entity | Razor View (`.cshtml`) |
| :--- | :--- | :--- | :--- | :--- |
| Danh sách phiếu phạt (`OVERDUE`, `DAMAGED`, `LOST`), lọc theo trạng thái `UNPAID`, `PAID`, `WAIVED` | `GET /Fines` | `CirculationController.Fines` | `CirculationService.GetFinesPageAsync` (`Fine`, `BorrowDetail`) | `Views/Circulation/Fines.cshtml` |
| Thu tiền phạt (`CASH` / `TRANSFER`) & Miễn phạt (dành cho `Admin`) | `POST /Fines/Pay`<br>`POST /Fines/Waive` | `CirculationController.PayFine`<br>`CirculationController.WaiveFine` | `CirculationService.PayFineAsync`<br>`CirculationService.WaiveFineAsync` (`Fine`) | `Views/Circulation/Fines.cshtml` |

---

### Module 7: Quản Trị Hệ Thống (`System Admin`) — ✅ ĐÃ XONG 100%

| Chức năng chi tiết | Route / URL | Controller & Action | Service & Entity | Razor View (`.cshtml`) |
| :--- | :--- | :--- | :--- | :--- |
| Quản lý tài khoản Cán bộ / Thủ thư / Admin (`Create`, `Update`, `Unlock`, `SoftDelete`, `Restore`, `HardDelete`) | `GET /System/Staff`<br>`POST /System/Staff/*` | `SystemAdminController.Staff` | `SystemAdminService` (`Staff`) | `Views/SystemAdmin/Staff.cshtml` |
| Cấu hình Loại độc giả (`MaxBorrowBooks`, `LoanDays`, `MaxRenewals`, `RenewDays`, `CardValidMonths`) | `GET /Admin/ReaderTypes`<br>`POST /Admin/ReaderTypes/Save` | `SystemAdminController.ReaderTypes` | `SystemAdminService` (`ReaderType`) | `Views/SystemAdmin/ReaderTypes.cshtml` |
| Cấu hình Quy định Mượn trả & Mức phạt toàn hệ thống (`OverdueFinePerDay`, `DamagedFinePercent`, `LostFinePercent`, `MaxUnpaidFine`, `ReservationHoldHours`) | `GET /System/Settings`<br>`POST /System/Settings/Save` | `SystemAdminController.Settings`<br>`SystemAdminController.SaveSettings` | `SystemAdminService.GetLibrarySettingAsync`<br>`SaveLibrarySettingAsync` (`LibrarySetting`) | `Views/SystemAdmin/Settings.cshtml` |

---

### Module 8 & 9: Các Hạng Mục Đã Có Giao Diện / DB, Chờ Nối Truy Vấn Thật Hoặc Mở Rộng Cuối (40% Còn Lại Của Nhóm Báo Cáo & Cổng Cá Nhân Độc Giả)

1. **Dashboard vận hành (`/Admin/Dashboard`), Báo cáo thống kê (`/Reports`) & Lịch sử phiếu mượn (`/Circulation/History`):**
   - **Hiện trạng:** Đã có sẵn Controller (`AdminController`) và Razor Views (`Views/Admin/Dashboard.cshtml`, `Views/Admin/Reports.cshtml`, `Views/Admin/Index.cshtml`), nhưng đang注入 `MockAdminPortalService`.
   - **Việc cần làm khi triển khai tiếp:** Thay `MockAdminPortalService` bằng truy vấn EF Core thật trên `BorrowSlips`, `BorrowDetails`, `BookCopies`, `Readers`, `Fines` (Top sách mượn nhiều, Độc giả tích cực, Sách quá hạn chưa trả, Thống kê theo tháng/quý/năm).
2. **Trang Cá nhân Độc giả Tự phục vụ (Sách đang mượn, Lịch sử mượn, Tiền phạt cá nhân, Nút bấm Đặt trước trực tiếp trên trang `/Books/Detail/{id}`):**
   - **Hiện trạng:** Độc giả đã đăng nhập và xem được danh mục sách/chi tiết sách; nghiệp vụ đặt trước đã có sẵn trong `CirculationService.CreateReservationAsync`.
   - **Việc cần làm khi triển khai tiếp:** Thêm `MyBorrows` / `MyReservations` / `MyFines` cho vai trò `Reader` và nút bấm "Đặt trước sách này" ngay trên `Views/Books/Detail.cshtml` khi `AvailableCopies == 0`.
