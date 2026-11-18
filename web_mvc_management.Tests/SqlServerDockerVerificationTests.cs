using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Data.Interceptors;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Models;
using web_mvc_management.Models.ViewModels.Auth;
using web_mvc_management.Models.ViewModels.Circulation;
using web_mvc_management.Models.ViewModels.Reader;
using web_mvc_management.Models.ViewModels.Reports;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Repositories;
using web_mvc_management.Services;
using web_mvc_management.Tests.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Tests;

/// <summary>
/// Bộ kiểm thử tích hợp trực tiếp trên Docker SQL Server thật:
/// 1. Kiểm toán toàn bộ 16 bảng nghiệp vụ + bảng otps, ràng buộc CHECK, Filtered Unique Index, Trigger SQL,
///    140 ảnh SVG vật lý và toàn bộ kịch bản dữ liệu mẫu trên database `library_management`.
/// 2. Thực thi End-to-End 100% luồng nghiệp vụ, Transaction khóa bi quan (`sp_getapplock` + `WITH (UPDLOCK, ROWLOCK)`),
///    đua tranh song song (Parallel Concurrency Race), Xóa cứng 2 bước (`XOA`) và chặn 4 file Malware Upload thật
///    trên database SQL Server cô lập `library_management_sql_e2e` (tự động dọn sạch sau khi chạy).
/// </summary>
public sealed class SqlServerDockerVerificationTests(ITestOutputHelper output)
{
    private static readonly FastLocalSqlConnectionInterceptor SqlConnectionInterceptor = new();
    private static readonly FastLocalSqlCommandInterceptor SqlCommandInterceptor = new();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "app_management.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc chứa app_management.slnx.");
    }

    private static (IConfiguration Config, string MasterConnStr, string AppConnStr) LoadSqlServerConfig(string? overrideDbName = null)
    {
        var repoRoot = FindRepoRoot();
        var envPath = Path.Combine(repoRoot, ".env");
        if (File.Exists(envPath))
        {
            DotNetEnv.Env.NoClobber().Load(envPath);
        }

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var rawConn = config.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(rawConn))
        {
            var host = config["DatabaseSettings:Host"] ?? "127.0.0.1";
            var port = config["DatabaseSettings:Port"] ?? "1433";
            var db = config["DatabaseSettings:Database"] ?? "library_management";
            var user = config["DatabaseSettings:Username"] ?? "sa";
            var pass = config["DatabaseSettings:Password"] ?? string.Empty;
            rawConn = $"Server={host},{port};Database={db};User Id={user};Password={pass};TrustServerCertificate=True;";
        }

        var builder = new SqlConnectionStringBuilder(rawConn)
        {
            DataSource = "127.0.0.1,1433",
            TrustServerCertificate = true,
            ConnectTimeout = 10
        };

        var appBuilder = new SqlConnectionStringBuilder(builder.ConnectionString);
        if (!string.IsNullOrWhiteSpace(overrideDbName))
        {
            appBuilder.InitialCatalog = overrideDbName;
        }

        var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString)
        {
            InitialCatalog = "master"
        };

        return (config, masterBuilder.ConnectionString, appBuilder.ConnectionString);
    }

    private static ApplicationDbContext CreateSqlServerContext(
        string connectionString,
        IUserContext? userContext = null,
        IDateTimeProvider? dateTimeProvider = null)
    {
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", false);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sql.CommandTimeout(60);
            })
            .AddInterceptors(SqlConnectionInterceptor, SqlCommandInterceptor)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning))
            .Options;

        return new ApplicationDbContext(options, userContext, dateTimeProvider);
    }

    private static async Task<bool> IsDockerSqlServerReachableAsync(string masterConnStr)
    {
        try
        {
            await using var conn = new SqlConnection(masterConnStr);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task VerifyDockerSqlServer_MasterDatasetV3_All16Tables_Constraints_Images_AndExamInvariants()
    {
        var repoRoot = FindRepoRoot();
        var wwwrootDir = Path.Combine(repoRoot, "web_mvc_management", "wwwroot");
        var (_, masterConnStr, appConnStr) = LoadSqlServerConfig();

        Assert.True(
            await IsDockerSqlServerReachableAsync(masterConnStr),
            "Không kết nối được Docker SQL Server tại 127.0.0.1:1433.");

        await using var db = CreateSqlServerContext(appConnStr);
        Assert.True(db.Database.IsSqlServer());

        // 1. Kiểm tra lịch sử EF Migration, 17 bảng vật lý, Trigger SQL, Filtered Unique Indexes & Check Constraints
        await using (var conn = new SqlConnection(appConnStr))
        {
            await conn.OpenAsync();

            var expectedTables = new[]
            {
                "library_settings", "reader_types", "staff", "shelves", "categories",
                "publishers", "authors", "readers", "books", "book_authors",
                "book_copies", "book_images", "borrow_slips", "borrow_details",
                "fines", "reservations", "otps"
            };

            foreach (var tbl in expectedTables)
            {
                await using var cmd = new SqlCommand(
                    "SELECT COUNT(1) FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = @name;",
                    conn);
                cmd.Parameters.AddWithValue("@name", tbl);
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.True(exists == 1, $"Thiếu bảng vật lý dbo.{tbl} trên SQL Server.");
            }

            await using (var migCmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.[__EFMigrationsHistory] WHERE [MigrationId] = N'20261104021457_InitialCreate';",
                conn))
            {
                var migCount = Convert.ToInt32(await migCmd.ExecuteScalarAsync());
                Assert.Equal(1, migCount);
            }

            await using (var trgCmd = new SqlCommand(
                "SELECT COUNT(1) FROM sys.triggers WHERE name = N'trg_borrow_details_sync_copy' AND is_disabled = 0;",
                conn))
            {
                var trgCount = Convert.ToInt32(await trgCmd.ExecuteScalarAsync());
                Assert.True(trgCount == 1, "Trigger SQL trg_borrow_details_sync_copy chưa được tạo hoặc đang bị disable.");
            }

            await using (var idxCmd = new SqlCommand(
                "SELECT COUNT(1) FROM sys.indexes WHERE is_unique = 1 AND has_filter = 1;",
                conn))
            {
                var filteredUniqueIdxCount = Convert.ToInt32(await idxCmd.ExecuteScalarAsync());
                output.WriteLine($"[SQL SERVER SCHEMA] Filtered Unique Indexes = {filteredUniqueIdxCount}");
                Assert.True(filteredUniqueIdxCount >= 8, $"Số Filtered Unique Index thực tế ({filteredUniqueIdxCount}) ít hơn thiết kế.");
            }

            await using (var chkCmd = new SqlCommand(
                "SELECT COUNT(1) FROM sys.check_constraints WHERE is_disabled = 0;",
                conn))
            {
                var checkCount = Convert.ToInt32(await chkCmd.ExecuteScalarAsync());
                output.WriteLine($"[SQL SERVER SCHEMA] Enabled Check Constraints = {checkCount}");
                Assert.True(checkCount >= 10, $"Số Check Constraint thực tế ({checkCount}) ít hơn thiết kế.");
            }
        }

        // 2. Kiểm tra dữ liệu toàn bộ 16 bảng nghiệp vụ (Active vs Thùng rác DELETED)
        var settings = await db.LibrarySettings.AsNoTracking().ToListAsync();
        Assert.Single(settings, s => s.Status == EntityStatus.Active);
        var activeSetting = settings.Single(s => s.Status == EntityStatus.Active);
        Assert.Equal(2000m, activeSetting.OverdueFinePerDay);
        Assert.Equal((short)50, activeSetting.DamagedFinePercent);
        Assert.Equal((short)150, activeSetting.LostFinePercent);
        Assert.Equal(50000m, activeSetting.MaxUnpaidFine);
        Assert.Equal((short)48, activeSetting.ReservationHoldHours);

        var readerTypes = await db.ReaderTypes.AsNoTracking().ToListAsync();
        Assert.Equal(3, readerTypes.Count(rt => rt.Status == EntityStatus.Active));
        Assert.Contains(readerTypes, rt => rt.Status == EntityStatus.Deleted && rt.Code == "ALUMNI_OLD");

        var staffList = await db.Staff.AsNoTracking().ToListAsync();
        Assert.Contains(staffList, s => s.Username == "admin" && s.Role == AppRoles.Admin && s.Status == EntityStatus.Active);
        Assert.Contains(staffList, s => s.Username == "librarian01" && s.Role == AppRoles.Librarian && s.Status == EntityStatus.Active);
        Assert.Contains(staffList, s => s.Username == "librarian_locked" && s.FailedLoginAttempts >= 5 && s.LockedUntil > DateTimeOffset.UtcNow);
        Assert.Contains(staffList, s => s.Username == "staff_deleted" && s.Status == EntityStatus.Deleted);

        var categories = await db.Categories.AsNoTracking().ToListAsync();
        Assert.Equal(9, categories.Count(c => c.Status == EntityStatus.Active));
        Assert.Contains(categories, c => c.Status == EntityStatus.Deleted);

        var publishers = await db.Publishers.AsNoTracking().ToListAsync();
        Assert.True(publishers.Count(p => p.Status == EntityStatus.Active) >= 6);
        Assert.Contains(publishers, p => p.Status == EntityStatus.Deleted);

        var authors = await db.Authors.AsNoTracking().ToListAsync();
        Assert.True(authors.Count(a => a.Status == EntityStatus.Active) >= 10, $"Active authors = {authors.Count(a => a.Status == EntityStatus.Active)}");
        Assert.Contains(authors, a => a.Status == EntityStatus.Deleted);

        var shelves = await db.Shelves.AsNoTracking().ToListAsync();
        Assert.True(shelves.Count(s => s.Status == EntityStatus.Active) >= 8);
        Assert.Contains(shelves, s => s.Status == EntityStatus.Deleted && s.Code == "KE-CU-99");

        // 3. Kiểm tra 28 Đầu sách (26 Active + 2 Deleted), dấu Tiếng Việt Unicode & 140 file ảnh SVG trên ổ đĩa
        var books = await db.Books.AsNoTracking().ToListAsync();
        Assert.Equal(26, books.Count(b => b.Status == EntityStatus.Active));
        Assert.Equal(2, books.Count(b => b.Status == EntityStatus.Deleted));
        Assert.All(books, b => Assert.DoesNotContain("??", b.Title));

        var bookImages = await db.BookImages.AsNoTracking().Where(i => i.Status == EntityStatus.Active).ToListAsync();
        Assert.Equal(140, bookImages.Count);
        foreach (var img in bookImages)
        {
            if (img.ImageUrl.StartsWith('/'))
            {
                var physicalPath = Path.Combine(wwwrootDir, img.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(physicalPath), $"File ảnh sách bị thiếu trên ổ đĩa: {physicalPath}");
            }
        }

        // 4. Kiểm tra 76 Bản sao sách & Bất biến toàn vẹn (BORROWED == Open Loans, RESERVED == READY Reservations)
        var copies = await db.BookCopies.AsNoTracking().ToListAsync();
        var activeCopies = copies.Where(c => c.Status == EntityStatus.Active).ToList();
        Assert.Equal(75, activeCopies.Count);
        Assert.Contains(copies, c => c.Status == EntityStatus.Deleted && c.Barcode == "BC-202610-0064");

        var openLoans = await db.BorrowDetails.AsNoTracking()
            .Where(d => d.Status == EntityStatus.Active && d.ReturnedAt == null)
            .ToListAsync();

        var borrowedCopyIds = activeCopies.Where(c => c.CopyStatus == CopyStatus.Borrowed).Select(c => c.Id).OrderBy(x => x).ToList();
        var openLoanCopyIds = openLoans.Select(d => d.CopyId).OrderBy(x => x).ToList();
        Assert.Equal(borrowedCopyIds, openLoanCopyIds);

        var readyReservations = await db.Reservations.AsNoTracking()
            .Where(r => r.Status == EntityStatus.Active && r.ReservationStatus == ReservationStatus.Ready)
            .ToListAsync();

        var reservedCopyIds = activeCopies.Where(c => c.CopyStatus == CopyStatus.Reserved).Select(c => c.Id).OrderBy(x => x).ToList();
        var readyAssignedCopyIds = readyReservations.Where(r => r.AssignedCopyId.HasValue).Select(r => r.AssignedCopyId!.Value).OrderBy(x => x).ToList();
        Assert.Equal(reservedCopyIds, readyAssignedCopyIds);

        // Kiểm tra 3 đầu sách Hot có 0 bản sao AVAILABLE
        var hotIsbns = new[] { "978-1484289105", "978-6047625066", "978-6040359032" };
        foreach (var isbn in hotIsbns)
        {
            var hotBook = books.Single(b => b.Isbn == isbn);
            var avail = activeCopies.Count(c => c.BookId == hotBook.Id && c.CopyStatus == CopyStatus.Available);
            Assert.Equal(0, avail);
        }

        // 5. Kiểm tra Độc giả, Phạt (UNPAID / PAID / WAIVED) & Đặt trước (4 trạng thái)
        var readers = await db.Readers.AsNoTracking().ToListAsync();
        var expectedCoreCards = new[]
        {
            "SV20260001", "SV20260002", "SV20260003", "SV20260004",
            "GV20260001", "KH20260001", "SV20260098", "SV20260099", "SV20260100"
        };
        foreach (var card in expectedCoreCards)
        {
            Assert.Contains(readers, r => r.CardNo == card);
        }
        Assert.Contains(readers, r => r.Email == "quocanhn352@gmail.com");
        Assert.Contains(readers, r => r.Email == "attendancestudent86@gmail.com");

        var sv03 = readers.Single(r => r.CardNo == "SV20260003");
        var sv03Unpaid = await (
            from f in db.Fines.AsNoTracking()
            join d in db.BorrowDetails.AsNoTracking() on f.BorrowDetailId equals d.Id
            join s in db.BorrowSlips.AsNoTracking() on d.SlipId equals s.Id
            where s.ReaderId == sv03.Id && f.Status == EntityStatus.Active && f.FineStatus == FineStatus.Unpaid
            select f.Amount
        ).SumAsync();
        Assert.Equal(72000m, sv03Unpaid);

        var allReservations = await db.Reservations.AsNoTracking().Where(r => r.Status == EntityStatus.Active).ToListAsync();
        Assert.Contains(allReservations, r => r.ReservationStatus == ReservationStatus.Ready);
        Assert.Contains(allReservations, r => r.ReservationStatus == ReservationStatus.Pending);
        Assert.Contains(allReservations, r => r.ReservationStatus == ReservationStatus.Fulfilled);
        Assert.Contains(allReservations, r => r.ReservationStatus == ReservationStatus.Cancelled);

        output.WriteLine(
            $"[AUDIT PASS] Books={books.Count}, Images={bookImages.Count}, ActiveCopies={activeCopies.Count}, " +
            $"OpenLoans={openLoans.Count}, ReadyReservations={readyReservations.Count}, Readers={readers.Count}, Staff={staffList.Count}");
    }

    [Fact]
    public async Task VerifyDockerSqlServer_FullEndToEndWorkflows_Triggers_AppLockConcurrency_AndSecurityUploads()
    {
        var repoRoot = FindRepoRoot();
        var tempDbName = $"library_management_sql_e2e_{Guid.NewGuid():N}"[..36];
        var (config, masterConnStr, tempConnStr) = LoadSqlServerConfig(tempDbName);

        Assert.True(
            await IsDockerSqlServerReachableAsync(masterConnStr),
            "Không kết nối được Docker SQL Server tại 127.0.0.1:1433.");

        try
        {
            var realClock = new DateTimeProvider();
            var userCtx = new FakeUserContext();
            var emailSvc = new FakeEmailService(realClock);

            // 0. Khởi tạo DB mới tinh trên Docker SQL Server qua DbInitializer (Migrate + Trigger + Seed V3) & kiểm tra tính lũy đẳng
            await using (var initCtx = CreateSqlServerContext(tempConnStr, userCtx, realClock))
            {
                await DbInitializer.InitializeAsync(initCtx, config, forceReset: false);
                await DbInitializer.InitializeAsync(initCtx, config, forceReset: false);
            }

            await using var db = CreateSqlServerContext(tempConnStr, userCtx, realClock);
            using var uow = new UnitOfWork(db);

            var adminStaff = await db.Staff.AsNoTracking().FirstAsync(s => s.Username == "admin");
            var librarianStaff = await db.Staff.AsNoTracking().FirstAsync(s => s.Username == "librarian01");
            userCtx.SetUser(adminStaff.Id, adminStaff.Username, adminStaff.Role, adminStaff.Email);

            var policy = new CirculationPolicy(uow, realClock);
            var queue = new ReservationQueueEngine(uow, policy, realClock);
            var loanDesk = new LoanDeskService(uow, policy, queue, userCtx, realClock);
            var reservationSvc = new ReservationService(uow, policy, queue, realClock);
            var fineSvc = new FineService(uow, userCtx, realClock);
            var lookupSvc = new CatalogLookupService(uow);
            var publicCatalog = new PublicCatalogService(uow, lookupSvc);
            var libraryInfo = new LibraryInfoService(uow);
            var readerPortal = new ReaderPortalService(uow, policy, queue, realClock);
            var readerAdmin = new ReaderAdminService(uow, realClock, queue);
            var staffAdmin = new StaffAdminService(uow, userCtx);
            var reportSvc = new ReportService(uow, policy, realClock);
            var dashboardSvc = new DashboardService(uow, realClock);
            var otpSvc = new OtpService(uow, realClock);
            var rateLimiter = new IpRateLimiterService(realClock);
            var authSvc = new AuthService(uow, otpSvc, emailSvc, rateLimiter, realClock, NullLogger<AuthService>.Instance);

            var webEnv = new LocalWebHostEnvironment(Path.Combine(repoRoot, "web_mvc_management"));
            var cloudinarySvc = new CloudinaryService(
                Options.Create(new CloudinarySettings()),
                new DummyHttpClientFactory(),
                realClock,
                webEnv,
                NullLogger<CloudinaryService>.Instance);
            var bookAdmin = new BookAdminService(uow, realClock, cloudinarySvc);
            var bookCopySvc = new BookCopyService(uow, realClock, queue);

            // ===== KỊCH BẢN 1: Trang chủ, Gợi ý AJAX & Tra cứu OPAC (có dấu + không dấu trên SQL Server) =====
            var infoVm = await libraryInfo.GetAsync();
            Assert.Equal(3, infoVm.LoanRules.Count);
            Assert.Equal(2000m, infoVm.OverdueFinePerDay);

            var suggestAccent = await publicCatalog.SuggestAsync("Lập trình", 6);
            var suggestNoAccent = await publicCatalog.SuggestAsync("Lap trinh", 6);
            Assert.NotEmpty(suggestAccent);
            Assert.NotEmpty(suggestNoAccent);

            foreach (var scope in new[] { "all", "textbook", "thesis", "journal" })
            {
                var catVm = await publicCatalog.GetPublicCatalogAsync(null, null, null, null, null, false, 1, scope);
                Assert.NotEmpty(catVm.Books);
            }

            var sampleDetail = await publicCatalog.GetPublicBookDetailAsync(suggestAccent[0].Id);
            Assert.NotNull(sampleDetail);
            Assert.Equal(5, sampleDetail.Images.Count);

            // ===== KỊCH BẢN 2: 4 Chốt chặn Lưu thông & Lập phiếu mượn + Nhận sách đặt trước (READY -> FULFILLED) =====
            var sv01 = await db.Readers.AsNoTracking().SingleAsync(r => r.CardNo == "SV20260001");
            var sv02 = await db.Readers.AsNoTracking().SingleAsync(r => r.CardNo == "SV20260002");
            var sv03 = await db.Readers.AsNoTracking().SingleAsync(r => r.CardNo == "SV20260003");
            var sv04 = await db.Readers.AsNoTracking().SingleAsync(r => r.CardNo == "SV20260004");
            var svExpired = await db.Readers.AsNoTracking().SingleAsync(r => r.CardNo == "SV20260098");
            var svLocked = await db.Readers.AsNoTracking().SingleAsync(r => r.CardNo == "SV20260099");

            // Chốt 1: Đang có sách quá hạn
            var blockOverdue = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel { ReaderId = sv02.Id, Barcodes = ["BC-202610-0001"] });
            Assert.False(blockOverdue.IsSuccess);

            // Chốt 2: Nợ phạt vượt trần 50.000đ (72.000đ)
            var blockFine = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel { ReaderId = sv03.Id, Barcodes = ["BC-202610-0001"] });
            Assert.False(blockFine.IsSuccess);

            // Chốt 3: Đang mượn đủ 5/5 cuốn
            var blockQuota = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel { ReaderId = sv04.Id, Barcodes = ["BC-202610-0001"] });
            Assert.False(blockQuota.IsSuccess);

            // Chốt 4: Thẻ hết hạn & Thẻ bị khóa
            var blockExpired = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel { ReaderId = svExpired.Id, Barcodes = ["BC-202610-0001"] });
            var blockLocked = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel { ReaderId = svLocked.Id, Barcodes = ["BC-202610-0001"] });
            Assert.False(blockExpired.IsSuccess);
            Assert.False(blockLocked.IsSuccess);

            // Chốt 5: Chặn mượn trùng đầu sách đang giữ (SV20260001 đang giữ BC-202610-0003 thuộc Clean Code -> mượn BC-202610-0001 phải bị chặn!)
            var blockDuplicateBook = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel
            {
                ReaderId = sv01.Id,
                Barcodes = ["BC-202610-0001"]
            });
            Assert.False(blockDuplicateBook.IsSuccess);

            // Lập phiếu mượn thành công trên SQL Server (gồm 1 cuốn AVAILABLE BC-202610-0004 + 1 cuốn RESERVED BC-202610-0008 riêng cho SV20260001)
            var borrowOk = await loanDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel
            {
                ReaderId = sv01.Id,
                Barcodes = ["BC-202610-0004", "BC-202610-0008"],
                Note = "Kiểm thử SQL Server thật"
            });
            Assert.True(borrowOk.IsSuccess, borrowOk.Message);

            var copy04AfterBorrow = await db.BookCopies.AsNoTracking().SingleAsync(c => c.Barcode == "BC-202610-0004");
            var copy08AfterBorrow = await db.BookCopies.AsNoTracking().SingleAsync(c => c.Barcode == "BC-202610-0008");
            Assert.Equal(CopyStatus.Borrowed, copy04AfterBorrow.CopyStatus);
            Assert.Equal(CopyStatus.Borrowed, copy08AfterBorrow.CopyStatus);

            var fulfilledRes = await db.Reservations.AsNoTracking()
                .SingleAsync(r => r.ReaderId == sv01.Id && r.AssignedCopyId == copy08AfterBorrow.Id);
            Assert.Equal(ReservationStatus.Fulfilled, fulfilledRes.ReservationStatus);

            // ===== KỊCH BẢN 3: Trả sách quá hạn sinh phạt, Trả sách kích hoạt Bẫy giữ sách (Copy Trap) & Trả sách hỏng =====
            // 3a. Trả BC-202610-0006 (SV20260002 đang giữ quá hạn) -> tự động sinh phạt OVERDUE
            var detail0006 = await db.BorrowDetails.AsNoTracking()
                .Include(d => d.BookCopy)
                .SingleAsync(d => d.BookCopy.Barcode == "BC-202610-0006" && d.ReturnedAt == null);
            var retOverdue = await loanDesk.ReturnAsync(new ReturnSubmitViewModel
            {
                ReaderId = sv02.Id,
                Items = [new ReturnItemViewModel { DetailId = detail0006.Id, Selected = true, Condition = ReturnCondition.Good }]
            });
            Assert.True(retOverdue.IsSuccess, retOverdue.Message);
            var autoFine = await db.Fines.AsNoTracking().SingleAsync(f => f.BorrowDetailId == detail0006.Id && f.FineType == FineType.Overdue);
            Assert.True(autoFine.Amount >= 10000m);

            // 3b. Trả BC-202610-0024 (Sách Hot #1) ở trạng thái GOOD -> Copy Trap giữ lại RESERVED & nhảy PENDING -> READY cho SV20260003
            var detail0024 = await db.BorrowDetails.AsNoTracking()
                .Include(d => d.BookCopy)
                .SingleAsync(d => d.BookCopy.Barcode == "BC-202610-0024" && d.ReturnedAt == null);
            var retTrap = await loanDesk.ReturnAsync(new ReturnSubmitViewModel
            {
                ReaderId = sv02.Id,
                Items = [new ReturnItemViewModel { DetailId = detail0024.Id, Selected = true, Condition = ReturnCondition.Good }]
            });
            Assert.True(retTrap.IsSuccess, retTrap.Message);

            var copy0024After = await db.BookCopies.AsNoTracking().SingleAsync(c => c.Barcode == "BC-202610-0024");
            Assert.Equal(CopyStatus.Reserved, copy0024After.CopyStatus);
            var promotedRes = await db.Reservations.AsNoTracking()
                .SingleAsync(r => r.ReaderId == sv03.Id && r.BookId == copy0024After.BookId && r.Status == EntityStatus.Active);
            Assert.Equal(ReservationStatus.Ready, promotedRes.ReservationStatus);
            Assert.Equal(copy0024After.Id, promotedRes.AssignedCopyId);

            // 3c. Trả BC-202610-0003 (SV20260001) bị DAMAGED -> phạt 50% giá bìa & CopyStatus = DAMAGED
            var detail0003 = await db.BorrowDetails.AsNoTracking()
                .Include(d => d.BookCopy)
                .SingleAsync(d => d.BookCopy.Barcode == "BC-202610-0003" && d.ReturnedAt == null);
            var retDamaged = await loanDesk.ReturnAsync(new ReturnSubmitViewModel
            {
                ReaderId = sv01.Id,
                Items = [new ReturnItemViewModel { DetailId = detail0003.Id, Selected = true, Condition = ReturnCondition.Damaged, Note = " Ướt góc trang " }]
            });
            Assert.True(retDamaged.IsSuccess, retDamaged.Message);
            var copy0003After = await db.BookCopies.AsNoTracking().SingleAsync(c => c.Barcode == "BC-202610-0003");
            Assert.Equal(CopyStatus.Damaged, copy0003After.CopyStatus);

            // ===== KỊCH BẢN 4: Thu tiền phạt & Phân quyền Miễn phạt (Librarian bị chặn, Admin thành công) =====
            var sv03UnpaidFines = await (
                from f in db.Fines.AsNoTracking()
                join d in db.BorrowDetails.AsNoTracking() on f.BorrowDetailId equals d.Id
                join s in db.BorrowSlips.AsNoTracking() on d.SlipId equals s.Id
                where s.ReaderId == sv03.Id && f.FineStatus == FineStatus.Unpaid
                orderby f.Amount
                select f
            ).ToListAsync();
            Assert.Equal(2, sv03UnpaidFines.Count);

            var payFineRes = await fineSvc.PayFineAsync(sv03UnpaidFines[0].Id, PaymentMethod.Cash);
            Assert.True(payFineRes.IsSuccess, payFineRes.Message);

            // Thủ thư thử miễn phạt -> bị từ chối
            userCtx.SetUser(librarianStaff.Id, librarianStaff.Username, librarianStaff.Role, librarianStaff.Email);
            var librarianWaive = await fineSvc.WaiveFineAsync(sv03UnpaidFines[1].Id, "Thủ thư thử miễn phạt");
            Assert.False(librarianWaive.IsSuccess);

            // Admin miễn phạt kèm lý do -> thành công
            userCtx.SetUser(adminStaff.Id, adminStaff.Username, adminStaff.Role, adminStaff.Email);
            var adminWaive = await fineSvc.WaiveFineAsync(sv03UnpaidFines[1].Id, "Miễn giảm hỗ trợ sinh viên khó khăn");
            Assert.True(adminWaive.IsSuccess, adminWaive.Message);

            // ===== KỊCH BẢN 5 & 6: Đăng ký OTP, Đăng nhập 2FA Bạn đọc, Gia hạn Online (0/2, 1/2, 2/2), Staff Brute-Force Unlock =====
            var guestType = (await authSvc.GetActiveReaderTypesAsync()).Single(t => t.Code == "GUEST");
            var dupPhoneAttempt = await authSvc.SendRegisterOtpAsync(new ReaderRegisterViewModel
            {
                Username = "sql_e2e_dup_phone",
                FullName = "Thử Trùng SĐT",
                Email = "sql_e2e_dup@library.utc.edu.vn",
                Phone = "0987654321", // Đã thuộc về SV20260001
                ReaderTypeId = guestType.Id,
                Password = "Password@123",
                ConfirmPassword = "Password@123"
            }, "127.0.0.10");
            Assert.False(dupPhoneAttempt.IsSuccess);

            var regModel = new ReaderRegisterViewModel
            {
                Username = "sql_e2e_guest",
                FullName = "Độc Giả Kiểm Thử SQL",
                Email = "sql_e2e_guest@library.utc.edu.vn",
                Phone = "0939777666",
                ReaderTypeId = guestType.Id,
                Password = "Password@123",
                ConfirmPassword = "Password@123"
            };
            var sendRegOtp = await authSvc.SendRegisterOtpAsync(regModel, "127.0.0.10");
            Assert.True(sendRegOtp.IsSuccess, sendRegOtp.Message);
            var regComplete = await authSvc.CompleteRegistrationAsync(regModel, emailSvc.LastOtpCode!, "127.0.0.10");
            Assert.True(regComplete.IsSuccess, regComplete.Message);
            Assert.StartsWith("KH", regComplete.Data!.CardNo);

            // Đăng nhập Cổng Bạn Đọc (`quocanhn352@gmail.com`) & kiểm tra 3 trạng thái gia hạn (0/2, 1/2, 2/2)
            var defaultSeedPassword = config["SeedSettings:DefaultPassword"] ?? "Admin@123456";
            var loginStep1 = await authSvc.InitiateReaderLoginAsync(new ReaderLoginViewModel
            {
                UsernameOrEmail = "quocanhn352@gmail.com",
                Password = defaultSeedPassword
            }, "127.0.0.11");
            Assert.True(loginStep1.IsSuccess, loginStep1.Message);

            var loginStep2 = await authSvc.CompleteReaderLoginOtpAsync(loginStep1.Data!.Email, emailSvc.LastOtpCode!, "127.0.0.11");
            Assert.True(loginStep2.IsSuccess, loginStep2.Message);

            var qaReader = await db.Readers.AsNoTracking().SingleAsync(r => r.Email == "quocanhn352@gmail.com");
            var qaOpenLoans = await db.BorrowDetails.AsNoTracking()
                .Where(d => d.BorrowSlip.ReaderId == qaReader.Id && d.ReturnedAt == null && d.Status == EntityStatus.Active)
                .OrderBy(d => d.RenewCount)
                .ToListAsync();
            Assert.Equal(3, qaOpenLoans.Count);

            var loanRenew0 = qaOpenLoans[0];
            var loanRenewLast = qaOpenLoans[1];
            var loanMaxRenew = qaOpenLoans[2];

            Assert.True((await readerPortal.RenewLoanAsync(qaReader.Id, loanRenew0.Id)).IsSuccess);
            Assert.True((await readerPortal.RenewLoanAsync(qaReader.Id, loanRenewLast.Id)).IsSuccess);
            Assert.False((await readerPortal.RenewLoanAsync(qaReader.Id, loanMaxRenew.Id)).IsSuccess); // Chạm trần lượt gia hạn

            // Đặt trước trực tuyến cuốn Hot #2 (`978-6047625066`) từ Cổng Bạn Đọc
            var hotBook2 = await db.Books.AsNoTracking().SingleAsync(b => b.Isbn == "978-6047625066");
            var onlineRes = await readerPortal.CreateReservationAsync(qaReader.Id, hotBook2.Id);
            Assert.True(onlineRes.IsSuccess, onlineRes.Message);

            // Kiểm tra tài khoản Cán bộ bị khóa Brute-Force (`librarian_locked`) -> Mở khóa -> Đăng nhập OTP thành công
            var lockedStaff = await db.Staff.AsNoTracking().SingleAsync(s => s.Username == "librarian_locked");
            var blockedStaffLogin = await authSvc.InitiateStaffLoginAsync(new StaffLoginViewModel { Email = lockedStaff.Email }, "127.0.0.12");
            Assert.False(blockedStaffLogin.IsSuccess);

            var unlockRes = await staffAdmin.UnlockStaffAccountAsync(lockedStaff.Id);
            Assert.True(unlockRes.IsSuccess, unlockRes.Message);

            var unlockedStaffLogin = await authSvc.InitiateStaffLoginAsync(new StaffLoginViewModel { Email = lockedStaff.Email }, "127.0.0.12");
            Assert.True(unlockedStaffLogin.IsSuccess, unlockedStaffLogin.Message);

            // ===== KỊCH BẢN 7: Xóa mềm -> Khôi phục -> Xóa cứng 3 bước ('XOA') & Chặn 100% 4 File Malware Upload thật =====
            var cleanBook = await db.Books.AsNoTracking().SingleAsync(b => b.Isbn == "978-6047625059");
            Assert.False((await bookAdmin.HardDeleteBookAsync(cleanBook.Id, "XOA")).IsSuccess); // Phải xóa mềm trước
            Assert.True((await bookAdmin.DeleteBookAsync(cleanBook.Id)).IsSuccess);
            Assert.True((await bookAdmin.RestoreBookAsync(cleanBook.Id)).IsSuccess);
            Assert.True((await bookAdmin.DeleteBookAsync(cleanBook.Id)).IsSuccess);
            Assert.False((await bookAdmin.HardDeleteBookAsync(cleanBook.Id, "SAI_MA")).IsSuccess);

            // Chặn xóa vĩnh viễn đầu sách khi còn bản sao vật lý trong thùng rác -> xóa vĩnh viễn các bản sao trước rồi xóa đầu sách (Xóa cứng 3 bước)
            Assert.False((await bookAdmin.HardDeleteBookAsync(cleanBook.Id, "XOA")).IsSuccess);
            var cleanBookCopies = await db.BookCopies.AsNoTracking().Where(c => c.BookId == cleanBook.Id).ToListAsync();
            foreach (var cp in cleanBookCopies)
            {
                var cpHardDel = await bookCopySvc.HardDeleteBookCopyAsync(cp.Id, "XOA");
                Assert.True(cpHardDel.IsSuccess, cpHardDel.Message);
            }
            var hardDelClean = await bookAdmin.HardDeleteBookAsync(cleanBook.Id, "XOA");
            Assert.True(hardDelClean.IsSuccess, hardDelClean.Message);

            var trashBook = await db.Books.AsNoTracking().SingleAsync(b => b.Isbn == "978-6040009901");
            var hardDelTrash = await bookAdmin.HardDeleteBookAsync(trashBook.Id, "XOA");
            Assert.True(hardDelTrash.IsSuccess, hardDelTrash.Message);

            // Kiểm tra 4 file mã độc thật trong du_lieu_demo_di_thi/2_file_test_bao_mat_upload/
            var malwareDir = Path.Combine(repoRoot, "du_lieu_demo_di_thi", "2_file_test_bao_mat_upload");
            var malwareFiles = new (string FileName, string ContentType)[]
            {
                ("01_malware_exe_gia_danh_anh.png", "image/png"),
                ("02_text_doi_duoi_anh.jpg", "image/jpeg"),
                ("03_anh_polyglot_chua_xss.png", "image/png"),
                ("04_webshell.aspx.png", "image/png")
            };
            foreach (var (fileName, contentType) in malwareFiles)
            {
                var fullPath = Path.Combine(malwareDir, fileName);
                var bytes = await File.ReadAllBytesAsync(fullPath);
                await using var ms = new MemoryStream(bytes);
                var formFile = new FormFile(ms, 0, bytes.Length, "CoverFile", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = contentType
                };
                var uploadAttempt = await cloudinarySvc.UploadBookImageAsync(formFile);
                Assert.False(uploadAttempt.IsSuccess, $"Lỗ hổng: File giả mạo '{fileName}' không bị chặn!");
            }

            // Kiểm tra upload ảnh hợp lệ thật từ Bo_01_ASPNET_Core_10_In_Action/01_bia_truoc.png
            var validImgPath = Path.Combine(repoRoot, "du_lieu_demo_di_thi", "1_anh_sach_de_upload", "Bo_01_ASPNET_Core_10_In_Action", "01_bia_truoc.png");
            var validBytes = await File.ReadAllBytesAsync(validImgPath);
            await using (var validMs = new MemoryStream(validBytes))
            {
                var validFormFile = new FormFile(validMs, 0, validBytes.Length, "CoverFile", "01_bia_truoc.png")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "image/png"
                };
                var validUpload = await cloudinarySvc.UploadBookImageAsync(validFormFile);
                Assert.True(validUpload.IsSuccess, validUpload.Message);
                await cloudinarySvc.DeleteImageAsync(validUpload.Data!.PublicId);
            }

            // Kiểm tra Dashboard & Báo cáo + Xuất CSV trên SQL Server thật
            var dashVm = await dashboardSvc.GetDashboardAsync(DateTime.UtcNow.Year);
            Assert.True(dashVm.TotalBooks > 0);
            var reportVm = await reportSvc.GetReportDashboardAsync(new ReportFilter { Preset = "30d" });
            Assert.NotNull(reportVm);
            var csvBytes = await reportSvc.ExportOverdueLoansCsvAsync();
            var csvContent = System.Text.Encoding.UTF8.GetString(csvBytes);
            Assert.Contains("Số thẻ", csvContent);

            // ===== KIỂM THỬ ĐUA TRANH SONG SONG TRÊN SQL SERVER THẬT (sp_getapplock + WITH (UPDLOCK, ROWLOCK)) =====
            var lecturer01 = await db.Readers.AsNoTracking().SingleAsync(r => r.CardNo == "GV20260001");
            var guest01 = await db.Readers.AsNoTracking().SingleAsync(r => r.CardNo == "KH20260001");
            const string contestedBarcode = "BC-202610-0001";

            async Task<web_mvc_management.Common.ServiceResult<Guid>> RaceBorrowAsync(Guid readerId)
            {
                await using var scopedDb = CreateSqlServerContext(tempConnStr, userCtx, realClock);
                using var scopedUow = new UnitOfWork(scopedDb);
                var scopedPolicy = new CirculationPolicy(scopedUow, realClock);
                var scopedQueue = new ReservationQueueEngine(scopedUow, scopedPolicy, realClock);
                var scopedDesk = new LoanDeskService(scopedUow, scopedPolicy, scopedQueue, userCtx, realClock);

                return await scopedDesk.CreateBorrowSlipAsync(new BorrowSubmitViewModel
                {
                    ReaderId = readerId,
                    Barcodes = [contestedBarcode]
                });
            }

            var raceResults = await Task.WhenAll(
                RaceBorrowAsync(lecturer01.Id),
                RaceBorrowAsync(guest01.Id));

            var winCount = raceResults.Count(r => r.IsSuccess);
            var loseCount = raceResults.Count(r => !r.IsSuccess);
            output.WriteLine($"[CONCURRENCY RACE] Win={winCount}, Lose={loseCount} | LoseMsg='{raceResults.FirstOrDefault(r => !r.IsSuccess)?.Message}'");

            Assert.Equal(1, winCount);
            Assert.Equal(1, loseCount);

            var openCountForContested = await db.BorrowDetails.AsNoTracking()
                .CountAsync(d => d.BookCopy.Barcode == contestedBarcode && d.ReturnedAt == null && d.Status == EntityStatus.Active);
            Assert.Equal(1, openCountForContested);
        }
        finally
        {
            // Dọn sạch hoàn toàn database tạm library_management_sql_e2e để giữ môi trường Docker sạch 100%
            try
            {
                SqlConnection.ClearAllPools();
                await using var masterConn = new SqlConnection(masterConnStr);
                await masterConn.OpenAsync();
                await using var dropCmd = new SqlCommand(
                    $"""
                    IF DB_ID(N'{tempDbName}') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{tempDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{tempDbName}];
                    END
                    """,
                    masterConn);
                await dropCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                output.WriteLine($"[CLEANUP WARNING] Không xóa được DB tạm {tempDbName}: {ex.Message}");
            }
        }
    }

    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class LocalWebHostEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "web_mvc_management";
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
        public string ContentRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = Path.Combine(contentRoot, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(Path.Combine(contentRoot, "wwwroot"));
    }
}
