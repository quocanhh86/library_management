using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Services;
using web_mvc_management.Services.Mock;

namespace web_mvc_management.Controllers;

/// <summary>
/// Trang styleguide và xem trước giao diện để duyệt hình thức. Chỉ tồn tại ở môi trường Development.
/// Các trang xem trước giả lập người dùng / dữ liệu cho riêng request đó, không đăng nhập thật và không cần DB.
/// </summary>
[Route("dev")]
public class DevController(IWebHostEnvironment env) : BaseController
{
    [HttpGet("styleguide")]
    public IActionResult Styleguide()
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        return View(new PagedResult<Models.ViewModels.Catalog.BookSummaryViewModel>(MockCatalog.Books, 128, 3, 10));
    }

    /// <summary>?role=Admin|Librarian để xem menu ẩn/hiện theo vai trò.</summary>
    [HttpGet("styleguide/staff")]
    public IActionResult StaffStyleguide(string role = AppRoles.Librarian)
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        role = role == AppRoles.Admin ? AppRoles.Admin : AppRoles.Librarian;
        var name = role == AppRoles.Admin ? "Nguyễn Quốc Anh" : "Trần Thu Hà";
        ActAs(name, role);
        return View(MockCatalog.Books);
    }

    /// <summary>Trang chủ khi độc giả đã đăng nhập.</summary>
    [HttpGet("home-reader")]
    public async Task<IActionResult> HomeAsReader(
        [FromServices] IBookService bookService,
        [FromServices] ILibraryInfoService libraryInfoService,
        [FromServices] IReaderPortalService readerPortalService,
        [FromServices] IDateTimeProvider dateTimeProvider,
        CancellationToken ct)
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        ActAs("Lê Minh Châu", AppRoles.Reader);
        var model = await HomeController.BuildModelAsync(bookService, libraryInfoService, readerPortalService, dateTimeProvider, null, ct);
        return View("~/Views/Home/Index.cshtml", model);
    }

    /// <summary>Trang đăng ký với loại độc giả mẫu (bản thật đọc từ DB).</summary>
    [HttpGet("register")]
    public IActionResult Register()
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        ViewBag.ReaderTypes = new List<ReaderType>
        {
            new() { Name = "Sinh viên", MaxBorrowBooks = 5, LoanDays = 14 },
            new() { Name = "Giảng viên", MaxBorrowBooks = 10, LoanDays = 30 },
            new() { Name = "Khách ngoài", MaxBorrowBooks = 2, LoanDays = 7 },
        };
        return View("~/Views/Account/Register.cshtml", new ReaderRegisterViewModel());
    }

    /// <summary>Bước nhập mã. ?error=1 để xem trạng thái nhập sai mã.</summary>
    [HttpGet("verify-otp")]
    public IActionResult VerifyOtp(bool error = false)
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        if (error)
        {
            ModelState.AddModelError(nameof(VerifyOtpViewModel.OtpCode), ErrorMessages.Format(ErrorCode.OtpInvalid, 3));
        }
        return View("~/Views/Account/VerifyOtp.cshtml", new VerifyOtpViewModel
        {
            Email = "lethuha@gmail.com",
            MaskedEmail = "l*****a@gmail.com",
            Purpose = AuthService.PurposeReaderLogin
        });
    }

    private void ActAs(string name, string role) =>
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Role, role)], "DevPreview"));
}
