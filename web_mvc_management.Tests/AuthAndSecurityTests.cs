using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Services;
using web_mvc_management.Tests.TestInfrastructure;
using Xunit;

namespace web_mvc_management.Tests;

public class AuthAndSecurityTests
{
    [Fact]
    public async Task RegisterAndVerifyOtp_ShouldGenerateKhPrefixForGuest_AndBurnOtpOnFifthWrongAttempt()
    {
        await using var fx = new SqliteTestDbFixture();
        var guestType = await fx.SeedReaderTypeAsync(code: "GUEST", name: "Khách ngoài", maxBorrow: 2, loanDays: 7, validMonths: 12);

        var auth = fx.CreateAuthService();
        var activeTypes = await auth.GetActiveReaderTypesAsync();
        Assert.Single(activeTypes);
        Assert.IsType<ReaderTypeOptionViewModel>(activeTypes[0]);

        var registerVm = new ReaderRegisterViewModel
        {
            FullName = "Nguyễn Khách Mời",
            Username = "khachmoi01",
            Email = "khachmoi01@gmail.com",
            Phone = "0912345678",
            ReaderTypeId = guestType.Id,
            Password = "Password@123",
            ConfirmPassword = "Password@123"
        };

        var sendRes = await auth.SendRegisterOtpAsync(registerVm, "127.0.0.1");
        Assert.True(sendRes.IsSuccess, sendRes.Message);
        var sentCode = fx.EmailService.LastOtpCode!;

        // 4 lần sai -> báo còn attempts; lần thứ 5 -> Burn OTP!
        var otpSvc = fx.CreateOtpService();
        for (var i = 1; i <= 4; i++)
        {
            var wrong = await otpSvc.VerifyOtpAsync(registerVm.Email, AuthService.PurposeRegister, "000000");
            Assert.Equal(OtpStatus.Incorrect, wrong.Status);
            Assert.Equal(5 - i, wrong.RemainingAttempts);
        }

        var fifthWrong = await otpSvc.VerifyOtpAsync(registerVm.Email, AuthService.PurposeRegister, "000000");
        Assert.Equal(OtpStatus.Burned, fifthWrong.Status);

        // Ngay cả nhập lại đúng mã cũ cũng phải bị từ chối vì OTP đã bị đốt
        var afterBurn = await otpSvc.VerifyOtpAsync(registerVm.Email, AuthService.PurposeRegister, sentCode);
        Assert.Equal(OtpStatus.NotFound, afterBurn.Status);

        // Hết cooldown 60s -> gửi lại mã mới -> đăng ký thành công & cấp thẻ tiền tố KH
        fx.DateTime.AdvanceDays(1);
        Assert.True((await auth.ResendOtpAsync(registerVm.Email, AuthService.PurposeRegister, "127.0.0.1")).IsSuccess);
        var freshCode = fx.EmailService.LastOtpCode!;

        var completeRes = await auth.CompleteRegistrationAsync(registerVm, freshCode, "127.0.0.1");
        Assert.True(completeRes.IsSuccess, completeRes.Message);
        Assert.NotNull(completeRes.Data);
        Assert.IsType<RegisteredReaderViewModel>(completeRes.Data);
        Assert.StartsWith("KH", completeRes.Data.CardNo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReaderLogin_ShouldLockoutAfter5FailedAttempts_RotateSecurityStamp_AndSupportResendPurposeValidation()
    {
        await using var fx = new SqliteTestDbFixture();
        var rt = await fx.SeedReaderTypeAsync();
        var reader = await fx.SeedReaderAsync(rt.Id, "SV801", username: "nguyenvana", email: "vana@student.utc.edu.vn", password: "Correct@123");
        var initialStamp = reader.SecurityStamp;

        var auth = fx.CreateAuthService();

        // Từ chối purpose lạ ở ResendOtp
        var invalidPurpose = await auth.ResendOtpAsync(reader.Email, "ADMIN_TAKEOVER", "127.0.0.1");
        Assert.False(invalidPurpose.IsSuccess);
        Assert.Equal(ErrorCode.OtpPurposeMismatch, invalidPurpose.Code);

        // Nhập sai mật khẩu 5 lần -> khóa 15 phút & xoay vòng SecurityStamp
        for (var i = 1; i <= 5; i++)
        {
            var loginFail = await auth.InitiateReaderLoginAsync(new ReaderLoginViewModel
            {
                UsernameOrEmail = reader.Username,
                Password = "WrongPassword@999"
            }, "10.0.0.5");
            Assert.False(loginFail.IsSuccess);
        }

        var lockedReader = await fx.Context.Readers.AsNoTracking().FirstAsync(r => r.Id == reader.Id);
        Assert.Equal(5, lockedReader.FailedLoginAttempts);
        Assert.NotNull(lockedReader.LockedUntil);
        Assert.NotEqual(initialStamp, lockedReader.SecurityStamp);

        // Hết 15 phút khóa -> đăng nhập đúng + OTP 2FA thành công
        fx.DateTime.AdvanceHours(1);
        var step1 = await auth.InitiateReaderLoginAsync(new ReaderLoginViewModel
        {
            UsernameOrEmail = reader.Email,
            Password = "Correct@123"
        }, "10.0.0.5");
        Assert.True(step1.IsSuccess, step1.Message);

        var step2 = await auth.CompleteReaderLoginOtpAsync(reader.Email, fx.EmailService.LastOtpCode!, "10.0.0.5");
        Assert.True(step2.IsSuccess, step2.Message);
        Assert.Equal(AppRoles.Reader, step2.Data!.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);

        // SEC-09: StaffLogin Enumeration Defense cho email không tồn tại vẫn trả Success mơ hồ và KHÔNG gửi mail
        var mailCountBefore = fx.EmailService.SentEmails.Count;
        var unknownStaff = await auth.InitiateStaffLoginAsync(new StaffLoginViewModel { Email = "khongtontai@gmail.com" }, "10.0.0.6");
        Assert.True(unknownStaff.IsSuccess);
        Assert.Equal(mailCountBefore, fx.EmailService.SentEmails.Count);
    }

    [Fact]
    public void IpRateLimiter_ShouldIgnoreSpoofedHeadersFromNonLoopback_AndEnforceSlidingWindow()
    {
        var clock = new FakeDateTimeProvider();
        var limiter = new IpRateLimiterService(clock);

        var externalCtx = new DefaultHttpContext();
        externalCtx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.50");
        externalCtx.Request.Headers["X-Forwarded-For"] = "1.2.3.4";
        Assert.Equal("203.0.113.50", limiter.GetClientIp(externalCtx));

        var proxyCtx = new DefaultHttpContext();
        proxyCtx.Connection.RemoteIpAddress = IPAddress.Loopback;
        proxyCtx.Request.Headers["CF-Connecting-IP"] = "198.51.100.22";
        Assert.Equal("198.51.100.22", limiter.GetClientIp(proxyCtx));

        Assert.True(limiter.IsAllowed("198.51.100.22", "LOGIN", 2));
        Assert.True(limiter.IsAllowed("198.51.100.22", "LOGIN", 2));
        Assert.False(limiter.IsAllowed("198.51.100.22", "LOGIN", 2));

        clock.AdvanceHours(1);
        Assert.True(limiter.IsAllowed("198.51.100.22", "LOGIN", 2));
    }

    [Fact]
    public async Task StaffAdmin_ShouldProtectSelfAndLastAdmin_AndClearAuditReferencesOnHardDelete()
    {
        await using var fx = new SqliteTestDbFixture();
        var admin = await fx.SeedStaffAsync("admin_main", AppRoles.Admin);
        var librarian = await fx.SeedStaffAsync("lib_temp", AppRoles.Librarian);

        fx.UserContext.SetUser(admin.Id, admin.Username, AppRoles.Admin);
        var staffSvc = fx.CreateStaffAdminService();

        // Không cho Admin tự hạ quyền hoặc tự khóa chính mình
        var selfDemote = await staffSvc.UpdateStaffAsync(new StaffEditViewModel
        {
            Id = admin.Id,
            FullName = admin.FullName,
            Email = admin.Email,
            Role = AppRoles.Librarian,
            Status = EntityStatus.Active
        });
        Assert.False(selfDemote.IsSuccess);
        Assert.Equal(ErrorCode.StaffCannotModifySelf, selfDemote.Code);

        // Chặn xóa Admin cuối cùng
        var fakeOtherActor = Guid.NewGuid();
        fx.UserContext.SetUser(fakeOtherActor, "other", AppRoles.Admin);
        var lastAdminSoftDel = await staffSvc.SoftDeleteStaffAsync(admin.Id);
        Assert.False(lastAdminSoftDel.IsSuccess);

        // Xóa cứng thủ thư chưa có lịch sử mượn trả nhưng CÓ dấu vết audit CreatedBy trên 1 thể loại
        var master = fx.CreateCatalogMasterService();
        fx.UserContext.SetUser(librarian.Id, librarian.Username, AppRoles.Librarian);
        await master.SaveCategoryAsync(new CategoryFormViewModel { Name = "Vật lý kỹ thuật", Status = EntityStatus.Active });

        fx.UserContext.SetUser(admin.Id, admin.Username, AppRoles.Admin);
        Assert.True((await staffSvc.SoftDeleteStaffAsync(librarian.Id)).IsSuccess);
        var hardDel = await staffSvc.HardDeleteStaffAsync(librarian.Id, "XOA");
        Assert.True(hardDel.IsSuccess, hardDel.Message);

        var cat = await fx.Context.Categories.AsNoTracking().FirstAsync(c => c.Name == "Vật lý kỹ thuật");
        Assert.Null(cat.CreatedBy);
        Assert.False(await fx.Context.Staff.AnyAsync(s => s.Id == librarian.Id));
    }

    [Fact]
    public async Task SystemSettingsAndReaderTypes_ShouldPersistAndReflectInLibraryInfo()
    {
        await using var fx = new SqliteTestDbFixture();
        var settingSvc = fx.CreateLibrarySettingService();
        var readerTypeSvc = fx.CreateReaderTypeService();
        var infoSvc = fx.CreateLibraryInfoService();

        var saveSetting = await settingSvc.SaveLibrarySettingAsync(new LibrarySettingFormViewModel
        {
            OverdueFinePerDay = 3000m,
            DamagedFinePercent = 60,
            LostFinePercent = 120,
            MaxUnpaidFine = 75000m,
            ReservationHoldHours = 24
        });
        Assert.True(saveSetting.IsSuccess, saveSetting.Message);

        var saveType = await readerTypeSvc.SaveReaderTypeAsync(new ReaderTypeFormViewModel
        {
            Code = "LECTURER",
            Name = "Giảng viên",
            MaxBorrowBooks = 12,
            LoanDays = 30,
            MaxRenewals = 3,
            RenewDays = 10,
            CardValidMonths = 60,
            Status = EntityStatus.Deleted // Ép về ACTIVE
        });
        Assert.True(saveType.IsSuccess, saveType.Message);

        var info = await infoSvc.GetAsync();
        Assert.Equal(3000m, info.OverdueFinePerDay);
        Assert.Single(info.LoanRules);
        Assert.Equal(12, info.LoanRules[0].MaxBooks);

        var savedSetting = await settingSvc.GetLibrarySettingAsync();
        Assert.Equal(75000m, savedSetting.MaxUnpaidFine);

        var createdType = await fx.Context.ReaderTypes.AsNoTracking().FirstAsync(rt => rt.Code == "LECTURER");
        Assert.Equal(EntityStatus.Active, createdType.Status);
        Assert.True((await readerTypeSvc.DeleteReaderTypeAsync(createdType.Id)).IsSuccess);
        Assert.True((await readerTypeSvc.HardDeleteReaderTypeAsync(createdType.Id, "XOA")).IsSuccess);
    }
}
