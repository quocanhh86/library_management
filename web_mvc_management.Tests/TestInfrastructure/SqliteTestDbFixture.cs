using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Data;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Repositories;
using web_mvc_management.Services;
using web_mvc_management.Util;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Tests.TestInfrastructure;

/// <summary>
/// DbContext SQLite In-Memory chuyên dụng cho kiểm thử:
/// chuyển đổi DateTimeOffset -> Int64 (UTC ticks) và Decimal -> Double để truy vấn/sắp xếp/tổng hợp SQLite chính xác.
/// </summary>
public sealed class TestApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IUserContext? userContext = null,
    IDateTimeProvider? dateTimeProvider = null)
    : ApplicationDbContext(options, userContext, dateTimeProvider)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var dtoConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));

        var nullableDtoConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.UtcTicks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

        var decimalConverter = new ValueConverter<decimal, double>(
            v => (double)v,
            v => (decimal)v);

        var nullableDecimalConverter = new ValueConverter<decimal?, double?>(
            v => v.HasValue ? (double)v.Value : null,
            v => v.HasValue ? (decimal)v.Value : null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(dtoConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableDtoConverter);
                }
                else if (property.ClrType == typeof(decimal))
                {
                    property.SetValueConverter(decimalConverter);
                }
                else if (property.ClrType == typeof(decimal?))
                {
                    property.SetValueConverter(nullableDecimalConverter);
                }
            }
        }
    }
}

/// <summary>
/// Interceptor giả lập trigger SQL trg_borrow_details_sync_copy:
/// 1. Khi INSERT BorrowDetail chưa trả: kiểm tra CopyStatus phải là AVAILABLE hoặc RESERVED (sai ném InvalidOperationException 50002), rồi đổi sang BORROWED.
/// 2. Khi UPDATE BorrowDetail từ ReturnedAt == null sang != null với ReturnCondition == GOOD: đổi CopyStatus sang AVAILABLE.
/// </summary>
public sealed class SqliteBorrowCopySyncInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SyncCopies(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SyncCopies(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void SyncCopies(DbContext? context)
    {
        if (context is not ApplicationDbContext db)
        {
            return;
        }

        foreach (var entry in db.ChangeTracker.Entries<BorrowDetail>().ToList())
        {
            var detail = entry.Entity;

            if (entry.State == EntityState.Added &&
                detail.Status != EntityStatus.Deleted &&
                detail.ReturnedAt == null)
            {
                var copy = db.BookCopies.Local.FirstOrDefault(c => c.Id == detail.CopyId)
                           ?? db.BookCopies.First(c => c.Id == detail.CopyId);

                if (copy.CopyStatus is not (CopyStatus.Available or CopyStatus.Reserved))
                {
                    throw new InvalidOperationException(
                        $"Trigger violation 50002: Copy '{copy.Barcode}' đang có trạng thái '{copy.CopyStatus}', bắt buộc phải là AVAILABLE hoặc RESERVED trước khi chèn borrow_details.");
                }

                copy.CopyStatus = CopyStatus.Borrowed;
            }
            else if (entry.State == EntityState.Modified)
            {
                var origReturnedAt = entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(BorrowDetail.ReturnedAt));
                if (origReturnedAt == null &&
                    detail.ReturnedAt != null &&
                    detail.ReturnCondition == ReturnCondition.Good)
                {
                    var copy = db.BookCopies.Local.FirstOrDefault(c => c.Id == detail.CopyId)
                               ?? db.BookCopies.First(c => c.Id == detail.CopyId);
                    copy.CopyStatus = CopyStatus.Available;
                }
            }
        }
    }
}

public sealed class FakeDateTimeProvider(DateTimeOffset? initialUtcNow = null) : IDateTimeProvider
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public DateTimeOffset UtcNow { get; private set; } =
        initialUtcNow ?? new DateTimeOffset(2026, 11, 18, 3, 0, 0, TimeSpan.Zero); // 10:00 UTC+7

    public DateTimeOffset Now => UtcNow.ToOffset(VietnamOffset);

    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    public void SetUtcNow(DateTimeOffset value) => UtcNow = value;

    public void AdvanceDays(int days) => UtcNow = UtcNow.AddDays(days);

    public void AdvanceHours(int hours) => UtcNow = UtcNow.AddHours(hours);
}

public sealed class FakeUserContext : IUserContext
{
    public Guid? CurrentUserId { get; private set; }
    public string? CurrentUsername { get; private set; }
    public string? CurrentUserRole { get; private set; }
    public string? CurrentUserEmail { get; private set; }
    public bool IsAuthenticated => CurrentUserId.HasValue;

    public bool IsInRole(string role) =>
        string.Equals(CurrentUserRole, role, StringComparison.OrdinalIgnoreCase);

    public void SetUser(Guid id, string username, string role, string? email = null)
    {
        CurrentUserId = id;
        CurrentUsername = username;
        CurrentUserRole = role;
        CurrentUserEmail = email ?? $"{username}@test.local";
    }

    public void Clear()
    {
        CurrentUserId = null;
        CurrentUsername = null;
        CurrentUserRole = null;
        CurrentUserEmail = null;
    }
}

public sealed class FakeEmailService(IDateTimeProvider dateTimeProvider) : IEmailService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldownTracker = new();

    public List<(string Email, string Code, string Purpose)> SentEmails { get; } = [];
    public string? LastOtpCode => SentEmails.LastOrDefault().Code;

    public int GetRemainingCooldownSeconds(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return 0;
        }

        var key = email.Trim().ToLowerInvariant();
        if (_cooldownTracker.TryGetValue(key, out var lastSent))
        {
            var elapsed = dateTimeProvider.UtcNow - lastSent;
            var remaining = EmailService.CooldownSeconds - (int)elapsed.TotalSeconds;
            return remaining > 0 ? remaining : 0;
        }

        return 0;
    }

    public Task<ServiceResult> SendOtpEmailAsync(
        string toEmail,
        string otpCode,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var key = toEmail.Trim().ToLowerInvariant();
        var remaining = GetRemainingCooldownSeconds(key);
        if (remaining > 0)
        {
            return Task.FromResult(ServiceResult.Failure(
                $"Yêu cầu gửi mã quá nhanh. Vui lòng chờ {remaining} giây nữa trước khi gửi lại."));
        }

        SentEmails.Add((key, otpCode, purpose));
        _cooldownTracker[key] = dateTimeProvider.UtcNow;
        return Task.FromResult(ServiceResult.Success("Đã gửi mã mới tới email của bạn."));
    }
}

public sealed class FakeCloudinaryService : ICloudinaryService
{
    public List<string> DeletedPublicIds { get; } = [];

    public Task<ServiceResult<ImageUploadResult>> UploadBookImageAsync(IFormFile file, CancellationToken ct = default)
    {
        var pid = $"fake:{Guid.NewGuid():N}";
        return Task.FromResult(ServiceResult<ImageUploadResult>.Success(
            new ImageUploadResult($"/uploads/books/{pid}.jpg", pid, IsCloudinary: false)));
    }

    public Task<ServiceResult> DeleteImageAsync(string? publicId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(publicId))
        {
            DeletedPublicIds.Add(publicId);
        }
        return Task.FromResult(ServiceResult.Success());
    }
}

public sealed class SqliteTestDbFixture : IDisposable, IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public TestApplicationDbContext Context { get; }
    public FakeDateTimeProvider DateTime { get; } = new();
    public FakeUserContext UserContext { get; } = new();
    public FakeEmailService EmailService { get; }
    public FakeCloudinaryService CloudinaryService { get; } = new();
    public UnitOfWork UnitOfWork { get; }

    public SqliteTestDbFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        EmailService = new FakeEmailService(DateTime);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqliteBorrowCopySyncInterceptor())
            .Options;

        Context = new TestApplicationDbContext(options, UserContext, DateTime);
        Context.Database.EnsureCreated();
        UnitOfWork = new UnitOfWork(Context);
    }

    // ===== Service Factories =====

    public ICirculationPolicy CreateCirculationPolicy() => new CirculationPolicy(UnitOfWork, DateTime);
    public IReservationQueueEngine CreateQueueEngine() => new ReservationQueueEngine(UnitOfWork, CreateCirculationPolicy(), DateTime);
    public ILoanDeskService CreateLoanDeskService() => new LoanDeskService(UnitOfWork, CreateCirculationPolicy(), CreateQueueEngine(), UserContext, DateTime);
    public IReservationService CreateReservationService() => new ReservationService(UnitOfWork, CreateCirculationPolicy(), CreateQueueEngine(), DateTime);
    public IFineService CreateFineService() => new FineService(UnitOfWork, UserContext, DateTime);
    public ICatalogLookupService CreateCatalogLookupService() => new CatalogLookupService(UnitOfWork);
    public IPublicCatalogService CreatePublicCatalogService() => new PublicCatalogService(UnitOfWork, CreateCatalogLookupService());
    public IBookAdminService CreateBookAdminService() => new BookAdminService(UnitOfWork, DateTime, CloudinaryService);
    public IBookCopyService CreateBookCopyService() => new BookCopyService(UnitOfWork, DateTime, CreateQueueEngine());
    public ICatalogMasterService CreateCatalogMasterService() => new CatalogMasterService(UnitOfWork);
    public IReaderPortalService CreateReaderPortalService() => new ReaderPortalService(UnitOfWork, CreateCirculationPolicy(), CreateQueueEngine(), DateTime);
    public IReaderAdminService CreateReaderAdminService() => new ReaderAdminService(UnitOfWork, DateTime, CreateQueueEngine());
    public IStaffAdminService CreateStaffAdminService() => new StaffAdminService(UnitOfWork, UserContext);
    public IReaderTypeService CreateReaderTypeService() => new ReaderTypeService(UnitOfWork);
    public ILibrarySettingService CreateLibrarySettingService() => new LibrarySettingService(UnitOfWork);
    public IDashboardService CreateDashboardService() => new DashboardService(UnitOfWork, DateTime);
    public IReportService CreateReportService() => new ReportService(UnitOfWork, CreateCirculationPolicy(), DateTime);
    public ILibraryInfoService CreateLibraryInfoService() => new LibraryInfoService(UnitOfWork);
    public IOtpService CreateOtpService() => new OtpService(UnitOfWork, DateTime);
    public IIpRateLimiterService CreateRateLimiter() => new IpRateLimiterService(DateTime);
    public IAuthService CreateAuthService() => new AuthService(
        UnitOfWork,
        CreateOtpService(),
        EmailService,
        CreateRateLimiter(),
        DateTime,
        NullLogger<AuthService>.Instance);

    // ===== Seed Helpers =====

    public async Task<LibrarySetting> SeedSettingAsync(
        decimal overduePerDay = 2000m,
        short damagedPercent = 50,
        short lostPercent = 100,
        decimal maxUnpaidFine = 50000m,
        short holdHours = 48)
    {
        var setting = new LibrarySetting
        {
            Id = Guid.NewGuid(),
            OverdueFinePerDay = overduePerDay,
            DamagedFinePercent = damagedPercent,
            LostFinePercent = lostPercent,
            MaxUnpaidFine = maxUnpaidFine,
            ReservationHoldHours = holdHours,
            Status = EntityStatus.Active,
            UpdatedAt = DateTime.UtcNow
        };
        await Context.LibrarySettings.AddAsync(setting);
        await Context.SaveChangesAsync();
        return setting;
    }

    public async Task<Staff> SeedStaffAsync(
        string username = "staff1",
        string role = AppRoles.Librarian,
        string? email = null,
        string status = EntityStatus.Active)
    {
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email ?? $"{username}@utc.edu.vn",
            PasswordHash = PasswordHasher.HashPassword("Staff@123"),
            FullName = $"Cán bộ {username}",
            Role = role,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Status = status
        };
        await Context.Staff.AddAsync(staff);
        await Context.SaveChangesAsync();
        return staff;
    }

    public async Task<ReaderType> SeedReaderTypeAsync(
        string code = "STUDENT",
        string name = "Sinh viên",
        short maxBorrow = 5,
        short loanDays = 14,
        short maxRenewals = 2,
        short renewDays = 7,
        short validMonths = 48)
    {
        var rt = new ReaderType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            MaxBorrowBooks = maxBorrow,
            LoanDays = loanDays,
            MaxRenewals = maxRenewals,
            RenewDays = renewDays,
            CardValidMonths = validMonths,
            Status = EntityStatus.Active
        };
        await Context.ReaderTypes.AddAsync(rt);
        await Context.SaveChangesAsync();
        return rt;
    }

    public async Task<Reader> SeedReaderAsync(
        Guid readerTypeId,
        string cardNo,
        string? username = null,
        string? email = null,
        string cardStatus = CardStatus.Active,
        int expiryOffsetDays = 365,
        string status = EntityStatus.Active,
        string password = "Reader@123")
    {
        var uname = username ?? cardNo.ToLowerInvariant();
        var reader = new Reader
        {
            Id = Guid.NewGuid(),
            ReaderTypeId = readerTypeId,
            Username = uname,
            Email = email ?? $"{uname}@student.utc.edu.vn",
            PasswordHash = PasswordHasher.HashPassword(password),
            FullName = $"Độc giả {cardNo}",
            Phone = $"09{Random.Shared.Next(10000000, 99999999)}",
            CardNo = cardNo,
            CardIssuedDate = DateTime.Today.AddDays(-30),
            CardExpiryDate = DateTime.Today.AddDays(expiryOffsetDays),
            CardStatus = cardStatus,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Status = status
        };
        await Context.Readers.AddAsync(reader);
        await Context.SaveChangesAsync();
        return reader;
    }

    public async Task<(Book Book, List<BookCopy> Copies, Category Category, Author Author, Publisher Publisher, Shelf Shelf)> SeedBookWithCopiesAsync(
        string title = "Cấu trúc dữ liệu và giải thuật",
        int copyCount = 2,
        decimal? copyPrice = 100000m,
        string? isbn = null,
        string barcodePrefix = "BC-TEST")
    {
        var category = new Category { Id = Guid.NewGuid(), Name = $"Chuyên ngành {Guid.NewGuid().ToString("N")[..4]}", Status = EntityStatus.Active };
        var author = new Author { Id = Guid.NewGuid(), FullName = $"Tác giả {Guid.NewGuid().ToString("N")[..4]}", Status = EntityStatus.Active };
        var publisher = new Publisher { Id = Guid.NewGuid(), Name = $"NXB {Guid.NewGuid().ToString("N")[..4]}", Status = EntityStatus.Active };
        var shelf = new Shelf { Id = Guid.NewGuid(), Code = $"K-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}", Status = EntityStatus.Active };

        var book = new Book
        {
            Id = Guid.NewGuid(),
            Title = title,
            Isbn = isbn ?? $"978-604-{Random.Shared.Next(100000, 999999)}",
            CategoryId = category.Id,
            PublisherId = publisher.Id,
            PublicationYear = 2024,
            Status = EntityStatus.Active
        };

        var bookAuthor = new BookAuthor
        {
            BookId = book.Id,
            AuthorId = author.Id,
            AuthorOrder = 1,
            Status = EntityStatus.Active
        };

        var copies = new List<BookCopy>();
        for (var i = 1; i <= copyCount; i++)
        {
            copies.Add(new BookCopy
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                ShelfId = shelf.Id,
                Barcode = $"{barcodePrefix}-{i:D2}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                CopyStatus = CopyStatus.Available,
                Price = copyPrice,
                AcquiredDate = DateTime.Today.AddDays(-10),
                Status = EntityStatus.Active
            });
        }

        await Context.Categories.AddAsync(category);
        await Context.Authors.AddAsync(author);
        await Context.Publishers.AddAsync(publisher);
        await Context.Shelves.AddAsync(shelf);
        await Context.Books.AddAsync(book);
        await Context.BookAuthors.AddAsync(bookAuthor);
        await Context.BookCopies.AddRangeAsync(copies);
        await Context.SaveChangesAsync();

        return (book, copies, category, author, publisher, shelf);
    }

    public async Task<(BorrowSlip Slip, BorrowDetail Detail)> SeedOpenLoanAsync(
        Guid readerId,
        Guid staffId,
        Guid copyId,
        DateOnly? dueDate = null,
        DateTimeOffset? borrowedAt = null)
    {
        var borrowTime = borrowedAt ?? DateTime.UtcNow;
        var slip = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = readerId,
            IssuedBy = staffId,
            BorrowedAt = borrowTime,
            Status = EntityStatus.Active
        };
        var detail = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slip.Id,
            CopyId = copyId,
            DueDate = dueDate ?? DateTime.Today.AddDays(14),
            Status = EntityStatus.Active
        };
        await Context.BorrowSlips.AddAsync(slip);
        await Context.BorrowDetails.AddAsync(detail);
        await Context.SaveChangesAsync();
        return (slip, detail);
    }

    public async Task<Fine> SeedFineAsync(
        Guid borrowDetailId,
        decimal amount,
        string fineType = FineType.Overdue,
        string fineStatus = FineStatus.Unpaid,
        int? overdueDays = 5)
    {
        var fine = new Fine
        {
            Id = Guid.NewGuid(),
            BorrowDetailId = borrowDetailId,
            FineType = fineType,
            OverdueDays = overdueDays,
            Amount = amount,
            FineStatus = fineStatus,
            Status = EntityStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        await Context.Fines.AddAsync(fine);
        await Context.SaveChangesAsync();
        return fine;
    }

    public void Dispose()
    {
        UnitOfWork.Dispose();
        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await UnitOfWork.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
