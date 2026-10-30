# Authentication and Authorization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Xây dựng hệ thống Authentication và Authorization toàn diện cho ứng dụng Quản lý Thư viện (ASP.NET Core MVC SSR .NET 10.0), hỗ trợ Reader Đăng ký/Đăng nhập qua OTP 2FA, Staff Đăng nhập Passwordless qua Gmail OTP, tích hợp cơ chế chống Brute-Force Rate Limiting (Account Lockout 15 phút, Cooldown gửi mã 60s, IP Rate Limiter, OTP Burn hủy mã sau 5 lần sai) và áp dụng triệt để 22 case an ninh quốc tế (OWASP ASVS, NIST SP 800-63B).

**Architecture:** 
- Mô hình ASP.NET Core MVC Server-Side Rendering với Cookie Authentication (`CookieAuthenticationDefaults`), phân quyền Role-based (`Admin`, `Librarian`, `Reader`).
- Bảng CSDL lưu trữ `otps` bền vững trên Microsoft SQL Server (SEC-08), quản lý mục đích (`purpose`), số lần thử sai (`failed_attempts`), thời gian hết hạn (`expires_at`).
- Bổ sung các trường bảo vệ brute-force trên bảng `readers` và `staff`: `failed_login_attempts`, `last_login_attempt`, `locked_until`, `security_stamp` (SEC-05, SEC-11, SEC-21).
- Bổ sung trường `role` trên bảng `staff` (`Admin`, `Librarian`) để phân quyền nội bộ chính xác.
- Tầng phòng thủ chiều sâu (Defense-in-Depth): IP Rate Limiting middleware (SEC-17), Email Service với Cooldown 60s, OTP Burning Engine (SEC-04), Timing Attack Defense với Dummy Hash (SEC-15), Generic Response chống dò tài khoản (SEC-09), và `OnValidatePrincipal` đối soát Security Stamp để thu hồi phiên toàn cục (SEC-05, SEC-21).

**Tech Stack:** ASP.NET Core MVC 10.0, Entity Framework Core 10.0, SQL Server, PBKDF2 HMAC-SHA256, MailKit / SmtpClient, Cookie Authentication, Bootstrap 5, jQuery AJAX, xUnit & FluentAssertions.

---

## Danh Sách 22 Case Bảo Mật & Ánh Xạ Vào Kiến Trúc Dự Án

| Case | Tiêu chuẩn | Giải pháp thiết kế trong hệ thống Quản lý Thư viện |
| :---: | :--- | :--- |
| **SEC-01** | OWASP ASVS 4.1.1, NIST 800-63B §7.1 | **Cross-Domain Replay Defense**: Tách biệt Purpose (`READER_LOGIN` vs `STAFF_LOGIN` vs `REGISTER`). Token/OTP của Reader không thể dùng cho Staff và ngược lại. |
| **SEC-02** | RFC 6819 §5.2.2.3, OWASP A07:2021 | **Cookie Sliding Expiration**: Cookie Auth có thời hạn 8 giờ, trượt theo phiên hoạt động, thu hồi khi logout. |
| **SEC-03** | OWASP ASVS 3.4.1, RFC 6265bis | **HttpOnly & Secure Cookie**: Không lưu token ở LocalStorage/SessionStorage, chống rò rỉ qua XSS. |
| **SEC-04** | NIST 800-63B §5.1.3, ASVS 2.3.2 | **OTP Brute-Force & Burning Engine**: Giới hạn tối đa 5 lần thử sai; lần thứ 5 tự động đốt hủy (DELETE) mã OTP khỏi CSDL. Xóa mã ngay khi verify thành công (chống replay). |
| **SEC-05** | NIST 800-63B §7.1, ASVS 3.3.1 | **Session Invalidation via Security Stamp**: Cột `security_stamp` kiểm soát phiên; khi đổi mật khẩu hoặc bị khóa, stamp thay đổi làm vô hiệu hóa phiên trên tất cả trình duyệt. |
| **SEC-06** | OWASP A01:2021, ASVS 4.1.1 | **Cross-Domain Role Separation**: Controller nhân viên yêu cầu `[Authorize(Roles = "Admin,Librarian")]`, Reader không thể truy cập trái phép. |
| **SEC-07** | OWASP A04:2021, ASVS 5.1.4 | **Unbound Target State Injection Defense**: Khi verify OTP đăng ký thành công, server dùng Data Protection API ký vé đăng ký gắn chặt email/SĐT đích, không nhận email unverified từ client. |
| **SEC-08** | OWASP ASVS 1.4.1, Cloud-Native | **Persistent Database OTP**: Lưu OTP trong bảng SQL Server `otps`, không lưu ở RAM, hỗ trợ restart server hoặc scale multi-instance mà không mất phiên. |
| **SEC-09** | NIST 800-63B §5.1.1.2, ASVS 2.1.7 | **Account Enumeration Defense**: Generic response ở màn hình Staff Login và Quên mật khẩu ("Nếu thông tin chính xác, mã xác thực đã được gửi...") không tiết lộ sự tồn tại của tài khoản. |
| **SEC-10** | OAuth 2.0 BCP, ASVS 2.7.1 | **Gmail / Staff Identity Verification**: Chỉ chấp nhận các Gmail thuộc danh sách nhân viên đã được kích hoạt (`status = 'ACTIVE'`). |
| **SEC-11** | NIST 800-63B §5.1.1.2, ASVS 2.2.1 | **Account Lockout Engine**: Nhập sai mật khẩu hoặc OTP 5 lần liên tiếp -> Khóa tài khoản tạm thời 15 phút (`locked_until`). Đăng nhập đúng tự động reset `failed_login_attempts = 0`. |
| **SEC-12** | OWASP ASVS 2.3.1, CWE-285 | **OTP Purpose / Namespace Isolation**: Cột `purpose` phân lập: `REGISTER`, `READER_LOGIN`, `STAFF_LOGIN`, `RESET_PASSWORD`. |
| **SEC-13** | RFC 8725 §2.1, ASVS 3.5.2 | **Cryptographic Hardening**: Thuật toán PBKDF2 HMAC-SHA256 với Salt ngẫu nhiên 128-bit và 100.000 vòng lặp. |
| **SEC-14** | Concurrency Race Condition, ASVS 3.3.3 | **Concurrency Race Condition Defense on OTP**: Database transaction và kiểm tra trạng thái OTP nguyên tử chống 2 request song song cùng verify 1 OTP. |
| **SEC-15** | OWASP A02:2021, ASVS 2.1.8 | **Side-Channel Timing Attack Defense**: Khi username/email không tồn tại, server vẫn chạy hàm băm giả lập `DUMMY_PBKDF2_HASH` để độ trễ luôn đồng nhất ~100-200ms. |
| **SEC-16** | RFC 6749 §10.12, ASVS 4.2.1 | **CSRF & Nonce Protection**: `[ValidateAntiForgeryToken]` trên tất cả POST actions (Login, Register, VerifyOtp, Logout). |
| **SEC-17** | NIST 800-63B §5.1.1.1, ASVS 13.1.1 | **IP Rate Limiting & Cooldown Engine**: Cooldown 60s giữa 2 lần gửi OTP; IP Rate Limit (5 gửi OTP/phút, 30 login/phút) chống DoS và Mail Toll Fraud. |
| **SEC-18** | OWASP A08:2021, ASVS 5.1.4 | **Strict DTO/ViewModel Mass Assignment Defense**: Tách biệt 100% giữa Entity CSDL và ViewModel (`ReaderRegisterViewModel`, `VerifyOtpViewModel`,...). |
| **SEC-19** | OWASP API1:2023, ASVS 4.1.3 | **BOLA / IDOR Defense via Security Context**: 100% trích xuất định danh qua `IUserContext.CurrentUserId`, không nhận `userId` từ query string hay form ẩn. |
| **SEC-20** | OWASP A03:2021, ASVS 12.1.1 | **Safe Avatar / File Handling**: (Sẵn sàng cho upload ảnh thẻ với Magic Bytes & GUID). |
| **SEC-21** | OWASP ASVS 2.8.1, NIST §7.1 | **Re-Authentication & Multi-Session Invalidation on Password Change**: Bắt buộc nhập mật khẩu cũ, cấm trùng mật khẩu cũ, cập nhật `security_stamp` để hủy toàn bộ phiên đang đăng nhập khác. |
| **SEC-22** | ACID Concurrency, CWE-362 | **Database Unique Constraints**: Unique index trên `username`, `email`, `phone` ở tầng SQL Server. |

---

## Kế Hoạch Triển Khai Từng Bước (Bite-Sized Tasks)

### Task 1: Cập Nhật Schema CSDL (Thêm Trường Brute-Force, Staff Role & Bảng `otps`)

**Files:**
- Modify: `web_mvc_management/Entity/Staff.cs`
- Modify: `web_mvc_management/Entity/Reader.cs`
- Create: `web_mvc_management/Entity/OtpEntry.cs`
- Modify: `web_mvc_management/Data/ApplicationDbContext.cs`
- Create: `web_mvc_management/Data/Migrations/20261031_AddAuthAndSecurityFields.sql`

**Step 1: Viết script SQL cập nhật schema SQL Server**
Tạo script thêm cột `role`, `failed_login_attempts`, `last_login_attempt`, `locked_until`, `security_stamp` vào `staff` và `readers`, cùng bảng `otps`:
```sql
-- Cập nhật bảng staff
ALTER TABLE staff ADD role VARCHAR(20) NOT NULL CONSTRAINT df_staff_role DEFAULT 'Librarian';
ALTER TABLE staff ADD failed_login_attempts INT NOT NULL CONSTRAINT df_staff_failed_login DEFAULT 0;
ALTER TABLE staff ADD last_login_attempt DATETIMEOFFSET NULL;
ALTER TABLE staff ADD locked_until DATETIMEOFFSET NULL;
ALTER TABLE staff ADD security_stamp VARCHAR(36) NOT NULL CONSTRAINT df_staff_security_stamp DEFAULT NEWID();

-- Cập nhật bảng readers
ALTER TABLE readers ADD failed_login_attempts INT NOT NULL CONSTRAINT df_readers_failed_login DEFAULT 0;
ALTER TABLE readers ADD last_login_attempt DATETIMEOFFSET NULL;
ALTER TABLE readers ADD locked_until DATETIMEOFFSET NULL;
ALTER TABLE readers ADD security_stamp VARCHAR(36) NOT NULL CONSTRAINT df_readers_security_stamp DEFAULT NEWID();

-- Tạo bảng otps bền vững (SEC-08, SEC-12)
CREATE TABLE otps (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    email VARCHAR(255) NOT NULL,
    otp_code VARCHAR(10) NOT NULL,
    purpose VARCHAR(30) NOT NULL,
    failed_attempts INT NOT NULL DEFAULT 0,
    is_used BIT NOT NULL DEFAULT 0,
    expires_at DATETIMEOFFSET NOT NULL,
    created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE INDEX idx_otps_lookup ON otps (email, purpose, is_used, expires_at);
```

**Step 2: Chạy script cập nhật CSDL**
Run: `powershell -Command "..."` để thực thi script trên SQL Server `localhost,1433`.

**Step 3: Cập nhật Entity `Staff.cs`, `Reader.cs`, tạo `OtpEntry.cs`**
- `Staff.cs`: Thêm các property `Role`, `FailedLoginAttempts`, `LastLoginAttempt`, `LockedUntil`, `SecurityStamp`.
- `Reader.cs`: Thêm các property `FailedLoginAttempts`, `LastLoginAttempt`, `LockedUntil`, `SecurityStamp`.
- `OtpEntry.cs`: Tạo entity ánh xạ bảng `otps`.
- `ApplicationDbContext.cs`: Thêm `DbSet<OtpEntry> Otps`, cấu hình Fluent API cho các trường mới.

**Step 4: Kiểm tra build dự án**
Run: `dotnet build web_mvc_management/web_mvc_management.csproj`
Expected: Build succeeded 0 Warning(s) 0 Error(s).

**Step 5: Commit Task 1**
Run: `git commit -m "feat(db): add security fields, staff role and persistent otps table"`

---

### Task 2: Thiết Lập Test Project & Tiện Ích Timing Attack Defense (SEC-15)

**Files:**
- Create: `tests/web_mvc_management.Tests/web_mvc_management.Tests.csproj`
- Create: `tests/web_mvc_management.Tests/Util/PasswordHasherTests.cs`
- Modify: `web_mvc_management/Util/PasswordHasher.cs`

**Step 1: Tạo test project xUnit**
Run: `dotnet new xunit -n web_mvc_management.Tests -o tests/web_mvc_management.Tests`
Run: `dotnet add tests/web_mvc_management.Tests reference web_mvc_management/web_mvc_management.csproj`
Run: `dotnet sln add tests/web_mvc_management.Tests/web_mvc_management.Tests.csproj`

**Step 2: Viết failing test kiểm tra Dummy Hash Timing Defense**
File `tests/web_mvc_management.Tests/Util/PasswordHasherTests.cs`:
```csharp
[Fact]
public void VerifyPassword_WhenUserNotFound_ExecutesDummyHashToPreventTimingAttack()
{
    // Test that VerifyDummyPassword executes without throwing and takes roughly similar time
    var stopwatch = Stopwatch.StartNew();
    PasswordHasher.VerifyDummyPassword("AnyPassword123!");
    stopwatch.Stop();
    Assert.True(stopwatch.ElapsedMilliseconds > 0);
}
```

**Step 3: Cập nhật `PasswordHasher.cs`**
Thêm `DUMMY_PBKDF2_HASH` và phương thức `VerifyDummyPassword`:
```csharp
private static readonly string DummyHash = HashPassword("DummySecurityCheckPassword@123");

public static bool VerifyDummyPassword(string password)
{
    return VerifyPassword(password, DummyHash);
}
```

**Step 4: Chạy test kiểm tra**
Run: `dotnet test tests/web_mvc_management.Tests`
Expected: 1 passed.

**Step 5: Commit Task 2**
Run: `git commit -m "test: add test project and timing attack defense in PasswordHasher"`

---

### Task 3: Xây Dựng OTP Engine & Burning Engine (SEC-04, SEC-08, SEC-12, SEC-14)

**Files:**
- Create: `web_mvc_management/Services/IOtpService.cs`
- Create: `web_mvc_management/Services/OtpService.cs`
- Create: `tests/web_mvc_management.Tests/Services/OtpServiceTests.cs`
- Modify: `web_mvc_management/Extensions/ServiceCollectionExtensions.cs`

**Step 1: Viết test cho OtpService (Verify, Brute-Force & Burn at attempt 5)**
Kiểm tra:
- Sinh OTP 6 số ngẫu nhiên an toàn (chỉ chứa chữ số).
- Xác thực đúng -> Đánh dấu `is_used = true` và trả về Success.
- Xác thực sai lần 1-4 -> Tăng `failed_attempts`, trả về Failure kèm số lần còn lại.
- Xác thực sai lần thứ 5 -> **Đốt hủy mã (Burn OTP)**: Xóa hoặc đánh dấu bị hủy, trả về Burned.
- Chặn OTP quá hạn (`expires_at < now`).
- Chặn sai Purpose (SEC-12).

**Step 2: Chạy test để xác nhận fails**
Run: `dotnet test --filter OtpServiceTests`

**Step 3: Cài đặt `IOtpService` và `OtpService`**
```csharp
public interface IOtpService
{
    Task<string> GenerateOtpAsync(string email, string purpose, TimeSpan? validity = null, CancellationToken cancellationToken = default);
    Task<OtpVerificationResult> VerifyOtpAsync(string email, string purpose, string otpCode, CancellationToken cancellationToken = default);
}
```
Cài đặt với:
- `RandomNumberGenerator` sinh 6 chữ số an toàn.
- Kiểm tra Purpose và Expiry.
- Tăng `failed_attempts`. Khi `>= 5` -> Xóa bản ghi OTP và trả về kết quả `Burned`.
- Đăng ký Scoped trong `ServiceCollectionExtensions.cs`.

**Step 4: Chạy test kiểm tra pass**
Run: `dotnet test --filter OtpServiceTests`
Expected: All tests pass.

**Step 5: Commit Task 3**
Run: `git commit -m "feat(security): implement OtpService with persistent SQL storage and OTP burning engine"`

---

### Task 4: Xây Dựng Email Service Kèm Cooldown 60s (SEC-17)

**Files:**
- Create: `web_mvc_management/Services/IEmailService.cs`
- Create: `web_mvc_management/Services/EmailService.cs`
- Create: `tests/web_mvc_management.Tests/Services/EmailServiceTests.cs`
- Modify: `web_mvc_management/Extensions/ServiceCollectionExtensions.cs`

**Step 1: Viết test cho Cooldown gửi email**
- Gửi lần 1 -> Thành công.
- Gửi lần 2 trong vòng 60 giây -> Trả về lỗi "Vui lòng chờ X giây trước khi yêu cầu mã mới".

**Step 2: Cài đặt `EmailService`**
- Hỗ trợ gửi template HTML OTP đẹp mắt, chuyên nghiệp mang thương hiệu Thư viện UTC.
- Bộ nhớ đệm kiểm tra Cooldown 60s giữa 2 lần gửi mã tới cùng một email.
- Ghi log rõ ràng trong console Development để thuận tiện test mà không bắt buộc cấu hình SMTP máy chủ ngoài.

**Step 3: Chạy test và xác nhận pass**
Run: `dotnet test --filter EmailServiceTests`

**Step 4: Commit Task 4**
Run: `git commit -m "feat(notification): implement EmailService with 60s cooldown against toll fraud"`

---

### Task 5: Xây Dựng IP Rate Limiting & Account Lockout Engine (SEC-11, SEC-17)

**Files:**
- Create: `web_mvc_management/Services/IIpRateLimiterService.cs`
- Create: `web_mvc_management/Services/IpRateLimiterService.cs`
- Create: `tests/web_mvc_management.Tests/Services/IpRateLimiterTests.cs`
- Modify: `web_mvc_management/Extensions/ServiceCollectionExtensions.cs`

**Step 1: Viết test cho IP Rate Limiter**
- Sliding Window theo IP: Quá 5 lần gửi OTP/phút -> Chặn (HTTP 429).
- Quá 30 lần login/phút -> Chặn (HTTP 429).
- Trích xuất Real-IP an toàn qua `X-Forwarded-For` / `X-Real-IP`.

**Step 2: Cài đặt `IpRateLimiterService`**
- Quản lý bộ đếm request theo IP bằng `ConcurrentDictionary` hoặc memory cache sliding window.
- Đăng ký Singleton trong DI.

**Step 3: Chạy test và xác nhận pass**
Run: `dotnet test --filter IpRateLimiterTests`

**Step 4: Commit Task 5**
Run: `git commit -m "feat(security): implement IpRateLimiterService and brute-force tracking"`

---

### Task 6: Cấu Hình Cookie Auth & Session Invalidation Qua Security Stamp (SEC-05, SEC-21)

**Files:**
- Modify: `web_mvc_management/Extensions/ServiceCollectionExtensions.cs`
- Create: `web_mvc_management/Infrastructure/SecurityStampValidator.cs`
- Create: `tests/web_mvc_management.Tests/Infrastructure/SecurityStampTests.cs`

**Step 1: Viết test xác thực Security Stamp**
- Khi Security Stamp trên Cookie khớp với DB -> Principal hợp lệ.
- Khi Security Stamp trong DB thay đổi (do đổi mật khẩu hoặc khóa) -> `RejectPrincipal()` và sign out.

**Step 2: Cấu hình `AddCookieAuthentication` với `OnValidatePrincipal`**
- Đăng ký `CookieAuthenticationEvents.OnValidatePrincipal` để kiểm tra tài khoản còn `ACTIVE` và `security_stamp` chưa bị đổi.
- Cấu hình Cookie: `HttpOnly = true`, `Secure = true`, `SameSite = SameSiteMode.Lax`.

**Step 3: Chạy test và kiểm tra**
Run: `dotnet test`

**Step 4: Commit Task 6**
Run: `git commit -m "feat(auth): configure cookie authentication with OnValidatePrincipal security stamp check"`

---

### Task 7: Luồng Reader Đăng Ký & Xác Thực OTP (SEC-04, SEC-07, SEC-18, SEC-22)

**Files:**
- Create: `web_mvc_management/Models/ViewModels/Auth/ReaderRegisterViewModel.cs`
- Create: `web_mvc_management/Models/ViewModels/Auth/VerifyOtpViewModel.cs`
- Create: `web_mvc_management/Services/IAuthService.cs`
- Create: `web_mvc_management/Services/AuthService.cs`
- Create: `web_mvc_management/Controllers/AccountController.cs`
- Create: `tests/web_mvc_management.Tests/Services/ReaderRegisterTests.cs`

**Step 1: Viết test cho luồng Reader Register**
- Submit form đăng ký -> Validate dữ liệu, trùng username/email/phone.
- Sinh OTP lưu vào CSDL bảng `otps` (Purpose: `REGISTER`), gửi email OTP.
- Nhập sai OTP 5 lần -> Burn OTP.
- Nhập đúng OTP -> Tạo Reader, cấp số thẻ (`card_no`), ngày cấp, ngày hết hạn theo `ReaderType`.

**Step 2: Cài đặt ViewModel & Service Logic**
- `ReaderRegisterViewModel` với DataAnnotations chặt chẽ (họ tên, email, sđt, loại độc giả, mật khẩu độ phức tạp cao).
- Tách biệt hoàn toàn khỏi Entity (SEC-18).
- Phương thức `InitiateRegisterReaderAsync` và `CompleteRegisterReaderAsync`.

**Step 3: Viết Action trong `AccountController`**
- `GET /Account/Register`
- `POST /Account/Register` (kèm `[ValidateAntiForgeryToken]`)
- `GET /Account/VerifyRegisterOtp`
- `POST /Account/VerifyRegisterOtp`

**Step 4: Chạy test và xác nhận pass**
Run: `dotnet test --filter ReaderRegisterTests`

**Step 5: Commit Task 7**
Run: `git commit -m "feat(reader): implement reader registration with 2FA OTP verification and card issuance"`

---

### Task 8: Luồng Reader Đăng Nhập Kèm 2FA OTP & Account Lockout (SEC-01, SEC-11, SEC-15)

**Files:**
- Create: `web_mvc_management/Models/ViewModels/Auth/ReaderLoginViewModel.cs`
- Modify: `web_mvc_management/Services/IAuthService.cs`
- Modify: `web_mvc_management/Services/AuthService.cs`
- Modify: `web_mvc_management/Controllers/AccountController.cs`
- Create: `tests/web_mvc_management.Tests/Services/ReaderLoginTests.cs`

**Step 1: Viết test cho Reader Login 2FA**
- Nhập sai mật khẩu: Tăng `failed_login_attempts`. Đạt 5 lần -> Gán `locked_until = now + 15m`.
- Tài khoản không tồn tại -> Chạy `VerifyDummyPassword` (SEC-15 Timing Defense).
- Tài khoản đang bị khóa (`locked_until > now`) -> Chặn đăng nhập, thông báo thời gian còn lại.
- Nhập đúng mật khẩu -> Sinh OTP 6 số (Purpose: `READER_LOGIN`), gửi email OTP, chuyển sang bước nhập OTP.
- Verify OTP đúng -> Reset `failed_login_attempts = 0`, cấp Cookie Auth Role `Reader`.

**Step 2: Cài đặt `ReaderLoginViewModel` & Service Logic**
- Xử lý đăng nhập 2 bước an toàn.
- Cấp `ClaimsPrincipal` đầy đủ claims (`NameIdentifier`, `Name`, `FullName`, `Email`, `Role = "Reader"`, `SecurityStamp`).

**Step 3: Viết Action trong `AccountController`**
- `GET /Account/Login` (Tab Độc giả)
- `POST /Account/Login`
- `GET /Account/VerifyLoginOtp`
- `POST /Account/VerifyLoginOtp`

**Step 4: Chạy test và xác nhận pass**
Run: `dotnet test --filter ReaderLoginTests`

**Step 5: Commit Task 8**
Run: `git commit -m "feat(reader): implement reader 2FA login with brute-force lockout and timing attack defense"`

---

### Task 9: Luồng Staff Đăng Nhập Passwordless Qua Gmail OTP (SEC-01, SEC-06, SEC-09, SEC-11)

**Files:**
- Create: `web_mvc_management/Models/ViewModels/Auth/StaffLoginViewModel.cs`
- Modify: `web_mvc_management/Services/IAuthService.cs`
- Modify: `web_mvc_management/Services/AuthService.cs`
- Modify: `web_mvc_management/Controllers/AccountController.cs`
- Create: `tests/web_mvc_management.Tests/Services/StaffLoginTests.cs`

**Step 1: Viết test cho Staff Gmail OTP Login**
- Nhập Gmail / Email cơ quan:
  - Nếu email không tồn tại trong `staff` -> Vẫn trả về Generic Response "Mã xác thực đã được gửi nếu email hợp lệ" (SEC-09) và chạy dummy timing (SEC-15).
  - Nếu tài khoản staff bị khóa (`locked_until > now`) -> Thông báo khóa.
  - Nếu email hợp lệ -> Sinh OTP 6 số (Purpose: `STAFF_LOGIN`), gửi qua Gmail.
- Xác thực OTP:
  - Sai 5 lần -> Burn OTP (SEC-04) + Khóa tài khoản Staff 15 phút (SEC-11).
  - Đúng -> Cấp Cookie Auth với Role = `Staff.Role` (`Admin` hoặc `Librarian`).
- Đăng xuất -> Hủy Cookie Auth.

**Step 2: Cài đặt `StaffLoginViewModel` & Service Logic**
- Phương thức `InitiateStaffLoginAsync` và `CompleteStaffLoginAsync`.
- Kiểm tra vai trò chính xác: gán Claim `ClaimTypes.Role` tương ứng `Admin` hoặc `Librarian`.

**Step 3: Viết Action trong `AccountController`**
- `GET /Account/StaffLogin`
- `POST /Account/StaffLogin`
- `GET /Account/VerifyStaffOtp`
- `POST /Account/VerifyStaffOtp`
- `POST /Account/Logout`

**Step 4: Chạy test và xác nhận pass**
Run: `dotnet test --filter StaffLoginTests`

**Step 5: Commit Task 9**
Run: `git commit -m "feat(staff): implement passwordless Gmail OTP login for Admin and Librarian"`

---

### Task 10: Xây Dựng Giao Diện Razor Views & jQuery AJAX OTP Countdown

**Files:**
- Create: `web_mvc_management/Views/Account/Login.cshtml`
- Create: `web_mvc_management/Views/Account/StaffLogin.cshtml`
- Create: `web_mvc_management/Views/Account/Register.cshtml`
- Create: `web_mvc_management/Views/Account/VerifyOtp.cshtml`
- Create: `web_mvc_management/Views/Account/AccessDenied.cshtml`
- Modify: `web_mvc_management/wwwroot/js/site.js`
- Modify: `web_mvc_management/Views/Shared/_Layout.cshtml`

**Step 1: Thiết kế giao diện Đăng nhập & Đăng ký đẹp mắt, chuẩn UI/UX**
- Màn hình `Login.cshtml`: Giao diện hiện đại, có tab chuyển đổi giữa "Độc giả" và nút liên kết "Đăng nhập Cán bộ / Thủ thư".
- Màn hình `Register.cshtml`: Form đăng ký thẻ thư viện với dropdown chọn `ReaderType` nạp từ CSDL, client-side validation.
- Màn hình `StaffLogin.cshtml`: Chuyên nghiệp, dành riêng cho Cán bộ thư viện (Admin / Librarian) đăng nhập qua Gmail OTP.
- Màn hình `VerifyOtp.cshtml`: 6 ô nhập mã OTP tự động focus sang ô tiếp theo khi nhập, nút gửi lại mã với bộ đếm ngược 60 giây (Countdown Timer).
- Màn hình `AccessDenied.cshtml`: Thông báo không đủ quyền hạn kèm nút quay lại an toàn.

**Step 2: Viết script đếm ngược Cooldown và jQuery AJAX resend OTP**
- Trong `site.js`: Bổ sung hàm đếm ngược 60s cho nút "Gửi lại mã OTP" và AJAX gọi endpoint resend OTP.

**Step 3: Kiểm tra hiển thị thực tế trên trình duyệt**
Run: `dotnet run --project web_mvc_management/web_mvc_management.csproj`
Kiểm tra các route: `/Account/Login`, `/Account/Register`, `/Account/StaffLogin`.

**Step 4: Commit Task 10**
Run: `git commit -m "feat(ui): create responsive Razor Views for Login, Register, StaffLogin, and OTP verification"`

---

### Task 11: Kiểm Thử Toàn Diện (End-to-End & Security Verification)

**Files:**
- Create: `tests/web_mvc_management.Tests/Security/SecurityVerificationTests.cs`
- Modify: `README.md`

**Step 1: Viết bộ test tổng hợp kiểm chứng 22 case bảo mật**
- Test SEC-01: Chặn Reader dùng OTP đăng nhập vào Staff endpoint.
- Test SEC-04: OTP bị xóa sạch ngay sau lần nhập sai thứ 5 (Burned).
- Test SEC-09: Generic response không lộ tài khoản tồn tại hay không.
- Test SEC-11: Sau 5 lần nhập sai, tài khoản bị khóa trong 15 phút.
- Test SEC-12: OTP của Purpose `REGISTER` không thể dùng để verify cho Purpose `STAFF_LOGIN`.
- Test SEC-15: Đo độ trễ kiểm tra tài khoản không tồn tại tương đương tài khoản có tồn tại.
- Test SEC-17: Gọi gửi OTP liên tiếp bị chặn bởi Cooldown 60s.
- Test SEC-21: Khi Security Stamp thay đổi, phiên bị từ chối.

**Step 2: Chạy toàn bộ test suite**
Run: `dotnet test`
Expected: 100% tests pass.

**Step 3: Cập nhật tài liệu `README.md`**
Bổ sung tài liệu luồng Authentication, cách đăng ký độc giả qua OTP, đăng nhập thủ thư qua Gmail OTP, danh sách tài khoản test và ma trận bảo mật 22 cases.

**Step 4: Commit Task 11**
Run: `git commit -m "docs: finalize security tests and update README with authentication workflows"`

---

## Hướng Dẫn Thực Thi Tiếp Theo

Kế hoạch đã được ghi nhận chi tiết tại: [`docs/plans/2026-10-31-authentication-and-authorization.md`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/docs/plans/2026-10-31-authentication-and-authorization.md).

Bạn có muốn bắt đầu thực thi ngay theo kế hoạch này bằng lệnh **/executing-plans** từ **Task 1** không?
