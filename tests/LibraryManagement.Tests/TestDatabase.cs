using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using web_mvc_management.Data;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;
using web_mvc_management.Repositories;
using web_mvc_management.Services;
using web_mvc_management.Constant;
using static web_mvc_management.Constant.LibraryConstants;

namespace LibraryManagement.Tests;

public sealed class TestClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 7, 5, 0, 0, TimeSpan.Zero);
    public DateTimeOffset Now => UtcNow.ToOffset(TimeSpan.FromHours(7));
    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
}

public sealed class TestUser : IUserContext
{
    public Guid? CurrentUserId { get; set; } = Guid.NewGuid();
    public string? CurrentUsername => "tester";
    public string? CurrentUserRole => AppRoles.Admin;
    public string? CurrentUserEmail => "tester@example.invalid";
    public bool IsAuthenticated => CurrentUserId.HasValue;
    public bool IsInRole(string role) => role == CurrentUserRole;
}

// Each test owns a uniquely named LocalDB database. Never read application .env credentials.
public sealed class TestDatabase : IAsyncDisposable
{
    public string Name { get; } = "LibraryTests_" + Guid.NewGuid().ToString("N");
    public string ConnectionString => $@"Server=(localdb)\MSSQLLocalDB;Database={Name};Integrated Security=true;TrustServerCertificate=true";
    public TestClock Clock { get; } = new();
    public TestUser User { get; } = new();
    public FailSaveInterceptor Failure { get; } = new();
    public ApplicationDbContext Db { get; }
    public UnitOfWork Uow { get; }
    public CirculationService Circulation { get; }
    public Reader Reader { get; private set; } = null!;
    public BookCopy Copy { get; private set; } = null!;
    public TestDatabase()
    {
        Db = NewContext();
        Uow = new UnitOfWork(Db);
        Circulation = new(Uow, User, Clock);
    }
    public ApplicationDbContext NewContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).AddInterceptors(Failure).Options, User, Clock);
    public async Task InitializeAsync()
    {
        await Db.Database.MigrateAsync();
        var type = new ReaderType { Id = Guid.NewGuid(), Code = "TEST", Name = "Test", MaxBorrowBooks = 5, LoanDays = 14, MaxRenewals = 2, RenewDays = 7, CardValidMonths = 12 };
        Reader = new Reader { Id = Guid.NewGuid(), ReaderType = type, Username = "reader", Email = "reader@example.invalid", PasswordHash = "test", FullName = "Test Reader", CardNo = "TEST001", CardIssuedDate = Clock.Today, CardExpiryDate = Clock.Today.AddYears(1), CardStatus = CardStatus.Active };
        Copy = new BookCopy { Id = Guid.NewGuid(), Barcode = "TEST-COPY", Book = new Book { Id = Guid.NewGuid(), Title = "Test Book" }, Price = 100000, AcquiredDate = Clock.Today };
        Db.AddRange(type, Reader, Copy, new Staff { Id = User.CurrentUserId!.Value, Username = "tester", Email = "tester@example.invalid", FullName = "Tester", PasswordHash = "test", Role = AppRoles.Admin }, new LibrarySetting { Id = Guid.NewGuid(), OverdueFinePerDay = 2000, DamagedFinePercent = 50, LostFinePercent = 150, ReservationHoldHours = 48 });
        await Db.SaveChangesAsync();
    }
    public async ValueTask DisposeAsync()
    {
        await Uow.DisposeAsync();
        await using var cleanup = NewContext();
        if (!Name.StartsWith("LibraryTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe test database name");
        await cleanup.Database.EnsureDeletedAsync();
    }
}

public sealed class FailSaveInterceptor : SaveChangesInterceptor
{
    public int SavesUntilFailure { get; set; } = -1;
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (--SavesUntilFailure == 0) throw new InvalidOperationException("Injected database failure");
        return ValueTask.FromResult(result);
    }
}
