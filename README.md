# UTC Library Management System (ASP.NET Core MVC .NET 10.0)

> Hệ thống Quản Lý Thư Viện Đại Học Giao Thông Vận Tải (UTC) — xây dựng với mô hình **ASP.NET Core MVC Server-Side Rendering (.NET 10.0)**, Microsoft SQL Server, Entity Framework Core, Bootstrap 5 và jQuery.

---

## 📋 Yêu Cầu Môi Trường

- **.NET SDK:** .NET 10.0 trở lên (`net10.0`).
- **Cơ sở dữ liệu:** Microsoft SQL Server 2019/2022 (hoặc SQL Server Express / Docker container).
- **Công cụ khuyến nghị:** Visual Studio 2022+ / Visual Studio Code / JetBrains Rider.

---

## 🚀 Hướng Dẫn Bắt Đầu Nhanh

### 1. Clone Kho Mã Nguồn

```bash
git clone <repo-url>
cd Library_management
```

### 2. Thiết Lập Biến Môi Trường (.env)

Tạo file `.env` tại thư mục gốc từ file `.env.example`:

```bash
cp .env.example .env
```

Cập nhật thông tin kết nối CSDL, SMTP, tài khoản khởi tạo và Cloudinary trong `.env` (tại thư mục gốc dự án):

```env
# Database Configuration (ASP.NET Core Hierarchical IConfiguration)
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=library_management;User Id=sa;Password=your_db_password;TrustServerCertificate=True;"
DatabaseSettings__Host=localhost
DatabaseSettings__Port=1433
DatabaseSettings__Database=library_management
DatabaseSettings__Username=sa
DatabaseSettings__Password=your_db_password

# Mail SMTP Configuration (Binds to MailSettings section)
MailSettings__Host=smtp.gmail.com
MailSettings__Port=587
MailSettings__Username=your_email@gmail.com
MailSettings__Password=your_app_password
MailSettings__SenderDisplayName="Thư Viện UTC"
MailSettings__Protocol=smtp
MailSettings__Auth=true
MailSettings__StartTls=true
MailSettings__StartTlsRequired=true

# Seed Configuration & Cloudinary
SeedSettings__DefaultPassword=your_seed_password
CloudinarySettings__CloudName=your_cloud_name
CloudinarySettings__ApiKey=your_api_key
CloudinarySettings__ApiSecret=your_api_secret
```

### 3. Cập Nhật Migration & Cơ Sở Dữ Liệu

```bash
cd web_mvc_management
dotnet ef database update
```

### 4. Khởi Chạy Ứng Dụng

```bash
dotnet run --project web_mvc_management
```

Truy cập trên trình duyệt: `https://localhost:5001` hoặc `http://localhost:5024`

---

## 🏛️ Quy Chuẩn Kiến Trúc Bắt Buộc (Architectural Tenets)

1. **Mô hình kiến trúc:** **ASP.NET Core MVC Server-Side Rendering (Razor Views `.cshtml`)**.
2. **KHÔNG DÙNG REST API / KHÔNG DÙNG SPA:**
   - Không tạo API Controller trả về thuần JSON như ứng dụng SPA (React, Vue, Angular).
   - Không dùng JWT Token cho xác thực client.
   - Mọi tương tác chuyển trang đều thông qua MVC Controller kế thừa `BaseController`, trả về `View()`, `PartialView()`, hoặc `RedirectToAction()`.
   - Với tương tác không tải lại trang (lọc sách, tìm kiếm, phân trang ajax, modal): Sử dụng **jQuery AJAX** tải **PartialView** hoặc nhận JSON trạng thái qua `AjaxSuccess()` / `AjaxError()`.
3. **Xác thực & Phân quyền:** Cookie Authentication (`CookieAuthenticationDefaults`) kết hợp Stateful Server-Side Session (`HttpContext.Session`) với 3 vai trò chuẩn:
   - `Admin` (Quản trị viên)
   - `Librarian` (Thủ thư)
   - `Reader` (Độc giả)
4. **Phân tách trách nhiệm (SOLID & Thin Controller):**
   - **Controller (Thin):** Chỉ tiếp nhận HTTP Request, kiểm tra `ModelState.IsValid`, gọi Service, ghi flash message vào `TempData` và trả về View/Redirect. **Tuyệt đối không inject `ApplicationDbContext` vào Controller**.
   - **Service (Fat Business Logic):** Xử lý mọi nghiệp vụ, tính toán, kiểm tra bảo mật (brute-force, lockout, OTP, rate-limit), trả kết quả qua `ServiceResult` hoặc `ServiceResult<T>`.
   - **Repository & UnitOfWork:** Đóng gói thao tác truy vấn CSDL qua EF Core.
5. **Bảo mật Khóa & Biến Môi Trường (`.env`):**
   - Tất cả key, API, secret và cấu hình nhạy cảm bảo mật (Connection String chứa mật khẩu, SMTP App Password, Cloudinary API Key/Secret, Seed Password/Account,...) **bắt buộc đặt trong file `.env` duy nhất tại thư mục gốc dự án** (cùng cấp với `.env.example`, đã chặn trong `.gitignore`).
   - **Tuyệt đối không hardcode trong code C#, Razor Views hay `appsettings.json`** (`appsettings*.json` giữ rỗng `{}` và `.env.example` chỉ chứa placeholder mẫu an toàn).

---

## 📁 Cấu Trúc Thư Mục Dự Án

```
web_mvc_management/
├── Common/                   # Thành phần dùng chung: ServiceResult, BaseFilter, PaginationParams, IPagedResult
├── Constant/                 # Định danh tập trung: ErrorCode, ErrorMessages, ScreenConstants, AppRoles, LibraryConstants
├── Controllers/              # MVC Controllers kế thừa BaseController (AccountController, HomeController,...)
├── Data/                     # ApplicationDbContext, DbInitializer (Seed Data)
├── Entity/                   # Database Entities ánh xạ CSDL qua EF Core (Reader, Staff, Book, BorrowSlip,...)
├── Extensions/               # Đăng ký Dependency Injection, SessionExtensions, QueryableExtensions
├── Helper/                   # Tiện ích bổ trợ (BarcodeHelper, CurrencyHelper, DateTimeHelper, PaginationHelper)
├── Infrastructure/           # Hạ tầng hệ thống (IDateTimeProvider, IUserContext, SecurityStampValidator)
├── Middlewares/              # ExceptionHandlingMiddleware toàn cục
├── Models/
│   └── ViewModels/           # Form DTO / ViewModels định kiểu hiển thị cho Razor Views (Auth, Books, Readers,...)
├── Repositories/             # Data Access layer (IBaseRepository, BaseRepository, IUnitOfWork, UnitOfWork)
├── Services/                 # Business logic nghiệp vụ (IAuthService, IOtpService, IEmailService,...)
├── Util/                     # Thuật toán mã hóa & tiện ích chuỗi (PasswordHasher PBKDF2, StringUtil)
├── Views/                    # Razor Views (.cshtml)
│   ├── Shared/               # _Layout.cshtml, _Pagination.cshtml, _ValidationScriptsPartial.cshtml
│   ├── Account/              # Login.cshtml, Register.cshtml, VerifyOtp.cshtml, StaffLogin.cshtml, AccessDenied.cshtml
│   └── Home/                 # Index.cshtml, Privacy.cshtml
└── wwwroot/                  # Static assets: css, js, lib (Bootstrap 5, Bootstrap Icons, jQuery)
```

---

## 🧰 Các Thành Phần Common Cốt Lõi Có Sẵn (Core Common)

Trước khi viết bất kỳ màn hình nào, các lập trình viên bắt buộc phải tái sử dụng các thành phần Common sau:

### 1. `BaseController` ([`web_mvc_management/Controllers/BaseController.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Controllers/BaseController.cs))

Mọi Controller đều phải kế thừa `BaseController`. Lớp này cung cấp sẵn:
- **Flash Message tự động hiển thị trên View qua `TempData`:**
  - `SetSuccessMessage(string message)`: Hiển thị thông báo thành công màu xanh lá.
  - `SetErrorMessage(string message)`: Hiển thị thông báo lỗi màu đỏ.
  - `SetWarningMessage(string message)`: Hiển thị cảnh báo màu vàng.
  - `SetInfoMessage(string message)`: Hiển thị thông tin màu xanh dương.
- **Tiện ích jQuery AJAX:**
  - `IsAjaxRequest`: Nhận diện request có phải từ jQuery AJAX hay không.
  - `AjaxSuccess(string message, object? data)`: Trả về JSON `{ success: true, message, data }`.
  - `AjaxError(string message, IEnumerable<string>? errors)`: Trả về JSON `{ success: false, message, errors }`.
- **Điều hướng ServiceResult tự động:**
  - `HandleServiceResult(result, onSuccess, onFailure)`: Tự động gán ModelState errors hoặc SetSuccessMessage và điều hướng.

### 2. `ServiceResult` & `ServiceResult<T>` ([`web_mvc_management/Common/ServiceResult.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Common/ServiceResult.cs))

Chuẩn hóa dữ liệu trả về từ tầng Service về Controller:
- `ServiceResult.Success("Thông báo")`
- `ServiceResult<T>.Success(data, "Thông báo")`
- `ServiceResult.Failure(ErrorCode.AuthInvalidCredentials)`
- `ServiceResult.Failure(ErrorMessages.Format(ErrorCode.AuthAccountLocked, minutes))`
- `ServiceResult.ValidationError(ErrorCode.RegisterDuplicateUsername)`

### 3. Quản Lý Lỗi Tập Trung: `ErrorCode` & `ErrorMessages` ([`web_mvc_management/Constant/`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Constant/))

**TUYỆT ĐỐI KHÔNG HARDCODE CHUỖI LỖI TRONG CONTROLLER HOẶC SERVICE.**
- Thêm mã lỗi vào enum [`ErrorCode.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Constant/ErrorCode.cs).
- Ánh xạ thông điệp tiếng Việt trong từ điển [`ErrorMessages.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Constant/ErrorMessages.cs).
- Lấy thông báo lỗi:
  ```csharp
  // Lỗi tĩnh
  string msg = ErrorMessages.Get(ErrorCode.AuthInvalidCredentials);

  // Lỗi động có tham số format
  string msg = ErrorMessages.Format(ErrorCode.OtpCooldownActive, 45); // "Vui lòng chờ 45 giây trước khi yêu cầu gửi lại mã OTP."
  ```

### 4. Danh Bạ Màn Hình Hệ Thống: `ScreenConstants` ([`web_mvc_management/Constant/ScreenConstants.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Constant/ScreenConstants.cs))

Mọi màn hình trong ứng dụng đều được khai báo tập trung trong `ScreenId`:
- Định danh `ScreenId`: `ScreenId.BookCatalog`, `ScreenId.CirculationBorrow`, `ScreenId.ManageReaders`,...
- Lấy đường dẫn URL Route an toàn (Type-safe): `Screens.GetPath(ScreenId.Login)` -> `"/Account/Login"`.
- Lấy tên tiếng Việt màn hình: `Screens.GetName(ScreenId.BookCatalog)` -> `"Tra Cứu & Tìm Kiếm Danh Mục Sách"`.
- Lấy mã màn hình: `Screens.GetCode(ScreenId.BookCatalog)` -> `"SCR_BOOK_CATALOG"`.

### 5. Cơ Chế Phân Trang & Lọc Dùng Chung (Pagination & Filtering)

Hỗ trợ phân trang chuẩn: `pageItemSize` mặc định = 10, hỗ trợ `pageNo` = 0 hoặc 1 đều query từ bản ghi đầu tiên trong CSDL:
- **`BaseFilter`** ([`web_mvc_management/Common/BaseFilter.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Common/BaseFilter.cs)): Chứa các trường lọc chung `Keyword`, `Status`, `SortBy`, `IsAscending`, `PageNo`, `PageItemSize`.
- **`IPagedResult<T>` & `PagedResult<T>`**: Chứa danh sách dữ liệu `Items`, `TotalItems`, `TotalPages`, `CurrentPage`, `HasPreviousPage`, `HasNextPage`.
- **Query Extension**: `query.ToPagedResultAsync(pageNo, pageSize, cancellationToken)`.
- **Partial View Phân Trang**: Gọi nhúng một dòng trong Razor View:
  ```html
  <partial name="_Pagination" model="Model.PagedBooks" />
  ```

### 6. Danh Tính Người Dùng & Thời Gian Hệ Thống

- **`IUserContext`** ([`web_mvc_management/Infrastructure/IUserContext.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Infrastructure/IUserContext.cs)):
  - `CurrentUserId`: `Guid?` của người dùng đăng nhập.
  - `CurrentUsername`: Tên tài khoản.
  - `CurrentUserRole`: Vai trò (`Admin`, `Librarian`, `Reader`).
  - `CurrentUserEmail`: Email người dùng.
- **`IDateTimeProvider`** ([`web_mvc_management/Infrastructure/IDateTimeProvider.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Infrastructure/IDateTimeProvider.cs)):
  - Dùng `dateTimeProvider.UtcNow` và `dateTimeProvider.Today` thay vì gọi trực tiếp `DateTime.UtcNow` để hỗ trợ kiểm thử và đồng bộ thời gian máy chủ.

---

## 🛠️ Quy Trình Chuẩn Khi Xây Dựng 1 Chức Năng / 1 Màn Hình

Khi được giao làm một màn hình mới (ví dụ: Quản lý sách, Mượn sách, Trả sách,...), lập trình viên thực hiện tuần tự theo 8 bước sau:

```
[Bước 1: Khai báo ScreenConstants] 
        ↓
[Bước 2: Định nghĩa ErrorCode & ErrorMessages (nếu có lỗi mới)] 
        ↓
[Bước 3: Tạo ViewModel (Form DTO kèm DataAnnotations)] 
        ↓
[Bước 4: Viết Service Interface & Implementation (Trả về ServiceResult)] 
        ↓
[Bước 5: Đăng ký DI trong ServiceCollectionExtensions] 
        ↓
[Bước 6: Viết Controller kế thừa BaseController] 
        ↓
[Bước 7: Viết Razor View (.cshtml) với Tag Helpers & _ValidationScriptsPartial] 
        ↓
[Bước 8: Kiểm tra biên dịch (dotnet build đạt 0 Error/0 Warning)]
```

---

### Bước 1: Khai Báo Màn Hình trong `ScreenConstants.cs`

Mở [`web_mvc_management/Constant/ScreenConstants.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Constant/ScreenConstants.cs):
1. Thêm định danh vào `ScreenId`:
   ```csharp
   public enum ScreenId
   {
       // ...
       ManageBooks,
   }
   ```
2. Khai báo thông tin màn hình vào từ điển `Screens.All`:
   ```csharp
   [ScreenId.ManageBooks] = new(
       ScreenId.ManageBooks,
       "SCR_MANAGE_BOOKS",
       "Quản Lý Danh Mục Đầu Sách",
       "/Admin/Books",
       [AppRoles.Admin, AppRoles.Librarian]),
   ```

---

### Bước 2: Khai Báo Mã Lỗi & Thông Điệp Tập Trung

Nếu tính năng có các case lỗi nghiệp vụ:
1. Thêm mã vào [`ErrorCode.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Constant/ErrorCode.cs):
   ```csharp
   BookNotFound,
   BookIsbnAlreadyExists,
   ```
2. Thêm thông điệp tiếng Việt tương ứng vào [`ErrorMessages.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Constant/ErrorMessages.cs):
   ```csharp
   [ErrorCode.BookNotFound] = "Không tìm thấy thông tin cuốn sách trong hệ thống.",
   [ErrorCode.BookIsbnAlreadyExists] = "Mã số tiêu chuẩn ISBN này đã tồn tại trong thư viện.",
   ```

---

### Bước 3: Tạo ViewModel (Tuyệt đối không dùng trực tiếp Database Entity trên Form)

Tạo thư mục trong `web_mvc_management/Models/ViewModels/<Module>/`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace web_mvc_management.Models.ViewModels.Books;

public class BookCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đầu sách")]
    [StringLength(255, ErrorMessage = "Tên sách không vượt quá 255 ký tự")]
    [Display(Name = "Tên đầu sách")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã ISBN")]
    [StringLength(20, ErrorMessage = "Mã ISBN không hợp lệ")]
    [Display(Name = "Mã ISBN")]
    public string Isbn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn thể loại")]
    [Display(Name = "Thể loại sách")]
    public Guid CategoryId { get; set; }
}
```

---

### Bước 4: Viết Service Nghiệp Vụ (Business Logic)

1. **Tạo Interface** trong `web_mvc_management/Services/`:
   ```csharp
   using web_mvc_management.Common;
   using web_mvc_management.Models.ViewModels.Books;

   namespace web_mvc_management.Services;

   public interface IBookService
   {
       Task<IPagedResult<BookListViewModel>> GetBooksPagedAsync(BookFilter filter, CancellationToken ct = default);
       Task<ServiceResult<Guid>> CreateBookAsync(BookCreateViewModel model, CancellationToken ct = default);
   }
   ```

2. **Triển khai Service** kế thừa Interface:
   - Sử dụng Primary Constructor để inject `ApplicationDbContext` (hoặc `IUnitOfWork`), `IUserContext`, `IDateTimeProvider`.
   - Trả về `ServiceResult` kèm `ErrorCode`:
   ```csharp
   public class BookService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider) : IBookService
   {
       public async Task<ServiceResult<Guid>> CreateBookAsync(BookCreateViewModel model, CancellationToken ct = default)
       {
           // Kiểm tra trùng lặp
           if (await context.Books.AnyAsync(x => x.Isbn == model.Isbn, ct))
           {
               return ServiceResult<Guid>.ValidationError(ErrorCode.BookIsbnAlreadyExists);
           }

           var book = new Book
           {
               Id = Guid.NewGuid(),
               Title = model.Title.Trim(),
               Isbn = model.Isbn.Trim(),
               CategoryId = model.CategoryId,
               CreatedAt = dateTimeProvider.UtcNow
           };

           await context.Books.AddAsync(book, ct);
           await context.SaveChangesAsync(ct);

           return ServiceResult<Guid>.Success(book.Id, "Thêm đầu sách mới thành công!");
       }
   }
   ```

---

### Bước 5: Đăng Ký Dependency Injection

Mở [`web_mvc_management/Extensions/ServiceCollectionExtensions.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Extensions/ServiceCollectionExtensions.cs) trong phương thức `AddApplicationServices()`:

```csharp
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    // ...
    services.AddScoped<IBookService, BookService>();
    return services;
}
```

---

### Bước 6: Viết Controller Kế Thừa `BaseController`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Books;
using web_mvc_management.Services;

namespace web_mvc_management.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Librarian)]
public class BooksController(IBookService bookService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(BookFilter filter)
    {
        var model = await bookService.GetBooksPagedAsync(filter);
        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new BookCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await bookService.CreateBookAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        SetSuccessMessage(result.Message); // Tự động hiển thị qua TempData ở _Layout.cshtml
        return RedirectToAction(nameof(Index));
    }
}
```

---

### Bước 7: Viết Razor View (`.cshtml`)

Tạo file `web_mvc_management/Views/Books/Create.cshtml`:

```html
@model web_mvc_management.Models.ViewModels.Books.BookCreateViewModel
@{
    ViewData["Title"] = "Thêm Đầu Sách Mới";
}

<div class="container py-4">
    <div class="row justify-content-center">
        <div class="col-12 col-md-8">
            <div class="card border-0 shadow-sm rounded-4">
                <div class="card-header bg-primary text-white py-3">
                    <h5 class="fw-bold mb-0"><i class="bi bi-book me-2"></i>Thêm Đầu Sách Mới</h5>
                </div>
                <div class="card-body p-4">
                    <form asp-action="Create" method="post" novalidate>
                        @Html.AntiForgeryToken()

                        <div asp-validation-summary="ModelOnly" class="alert alert-danger small mb-3"></div>

                        <div class="mb-3">
                            <label asp-for="Title" class="form-label fw-semibold"></label>
                            <input asp-for="Title" class="form-control" placeholder="Nhập tên đầu sách..." />
                            <span asp-validation-for="Title" class="text-danger small"></span>
                        </div>

                        <div class="mb-3">
                            <label asp-for="Isbn" class="form-label fw-semibold"></label>
                            <input asp-for="Isbn" class="form-control" placeholder="978-..." />
                            <span asp-validation-for="Isbn" class="text-danger small"></span>
                        </div>

                        <div class="d-flex justify-content-end gap-2 mt-4">
                            <a asp-action="Index" class="btn btn-outline-secondary">Hủy bỏ</a>
                            <button type="submit" class="btn btn-primary"><i class="bi bi-check-lg me-1"></i>Lưu thông tin</button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

---

## 🔒 Danh Sách Checklist Kiểm Tra Trước Khi Hoàn Tất (Definition of Done)

Trước khi commit mã nguồn hoặc tạo Pull Request, lập trình viên hãy tự kiểm tra bảng tiêu chí:

- [ ] **Top-Level Statements:** Chỉ có `Program.cs` sử dụng top-level statements. Tất cả các file còn lại phải có `namespace web_mvc_management...`, `public class / interface / enum`, các `using` tường minh.
- [ ] **Mô hình MVC:** Không dùng REST API Controller hay trả thuần JSON như SPA. Không dùng JWT. Sử dụng Cookie Authentication và Server-Side Session.
- [ ] **Thin Controller:** Controller kế thừa `BaseController`, không chứa business logic phức tạp, không inject `ApplicationDbContext`.
- [ ] **Tách biệt Entity & ViewModel:** Không bind trực tiếp Database Entity vào form để phòng chống Mass Assignment (SEC-18).
- [ ] **Không Hardcode Error Strings:** Toàn bộ thông báo lỗi phải lấy qua `ErrorCode` và `ErrorMessages.Get()` hoặc `ErrorMessages.Format()`.
- [ ] **Phân trang chuẩn:** Mặc định `PageItemSize = 10`, hỗ trợ `PageNo = 0` hoặc `1` query từ bản ghi đầu tiên, nhúng partial `_Pagination.cshtml`.
- [ ] **CSRF Protection:** Mọi Form POST đều có `@Html.AntiForgeryToken()` và Action có `[ValidateAntiForgeryToken]`.
- [ ] **Không Hardcode Secrets / Keys:** Tất cả key, API, secret và cấu hình nhạy cảm bảo mật bắt buộc đặt trong file `.env` tại thư mục gốc dự án; tuyệt đối không hardcode trong code C# hay `appsettings.json`.
- [ ] **Biên dịch:** Chạy lệnh `dotnet build` đạt **0 Warning(s), 0 Error(s)**.
- [ ] **Không tạo file test bừa bãi:** Không tự ý commit unit test vào thư mục mã nguồn khi không được yêu cầu.

---

## 👥 Phân Quyền Hệ Thống (`AppRoles`)

- `AppRoles.Admin`: Toàn quyền hệ thống, quản lý cán bộ, phân quyền, cấu hình thông số thư viện.
- `AppRoles.Librarian`: Nghiệp vụ thư viện: mượn trả tại quầy, quản lý danh mục sách, xử lý tiền phạt, thẻ bạn đọc.
- `AppRoles.Reader`: Độc giả: tra cứu sách trực tuyến, xem lịch sử mượn, thông tin thẻ và phiếu phạt cá nhân.

---

## 📝 License

Dự án phát triển nội bộ phục vụ bài tập lớn môn Hệ thống Quản lý Thư viện - Đại học Giao Thông Vận Tải (UTC).
