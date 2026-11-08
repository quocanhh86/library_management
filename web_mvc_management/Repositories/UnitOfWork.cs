using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using web_mvc_management.Data;
using web_mvc_management.Entity;

namespace web_mvc_management.Repositories;

/// <summary>
/// Triển khai UnitOfWork quản lý vòng đời DbContext và Transaction.
/// </summary>
public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    private readonly ApplicationDbContext _context = context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _currentTransaction;

    public IBaseRepository<Staff> Staff => Repository<Staff>();
    public IBaseRepository<ReaderType> ReaderTypes => Repository<ReaderType>();
    public IBaseRepository<Reader> Readers => Repository<Reader>();
    public IBaseRepository<Category> Categories => Repository<Category>();
    public IBaseRepository<Publisher> Publishers => Repository<Publisher>();
    public IBaseRepository<Author> Authors => Repository<Author>();
    public IBaseRepository<Shelf> Shelves => Repository<Shelf>();
    public IBaseRepository<Book> Books => Repository<Book>();
    public IBaseRepository<BookAuthor> BookAuthors => Repository<BookAuthor>();
    public IBaseRepository<BookCopy> BookCopies => Repository<BookCopy>();
    public IBaseRepository<BookImage> BookImages => Repository<BookImage>();
    public IBaseRepository<BorrowSlip> BorrowSlips => Repository<BorrowSlip>();
    public IBaseRepository<BorrowDetail> BorrowDetails => Repository<BorrowDetail>();
    public IBaseRepository<Fine> Fines => Repository<Fine>();
    public IBaseRepository<Reservation> Reservations => Repository<Reservation>();
    public IBaseRepository<LibrarySetting> LibrarySettings => Repository<LibrarySetting>();
    public IBaseRepository<OtpEntry> Otps => Repository<OtpEntry>();

    public IBaseRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        return (IBaseRepository<TEntity>)_repositories.GetOrAdd(
            typeof(TEntity),
            _ => new BaseRepository<TEntity>(_context)
        );
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => await _context.SaveChangesAsync(cancellationToken);

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        await BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

    public async Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return _currentTransaction;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        return _currentTransaction;
    }

    public async Task<IDbContextTransaction> BeginTransactionWithLockAsync(
        string lockResource = "LIB_MUTATION_LOCK",
        int lockTimeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        var tx = await BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var safeResource = string.IsNullOrWhiteSpace(lockResource) ? "LIB_MUTATION_LOCK" : lockResource.Trim();
            var rcParam = new Microsoft.Data.SqlClient.SqlParameter("@rc", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            var resourceParam = new Microsoft.Data.SqlClient.SqlParameter("@Resource", SqlDbType.NVarChar, 255)
            {
                Value = safeResource
            };
            var timeoutParam = new Microsoft.Data.SqlClient.SqlParameter("@LockTimeout", SqlDbType.Int)
            {
                Value = lockTimeoutMs
            };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @rc = sys.sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @LockTimeout;",
                [rcParam, resourceParam, timeoutParam],
                cancellationToken);

            var lockResult = rcParam.Value is int rc ? rc : -999;
            if (lockResult < 0)
            {
                throw new TimeoutException($"Không thể lấy khóa giao dịch đồng thời cho tài nguyên '{safeResource}' (mã: {lockResult}).");
            }

            return tx;
        }
        catch
        {
            await RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task ClearStaffAuditReferencesAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.staff            SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.reader_types     SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.readers          SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.categories       SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.publishers       SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.authors          SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.shelves          SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.books            SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.book_authors     SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.book_copies      SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.book_images      SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.borrow_slips     SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.borrow_details   SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.fines            SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.reservations     SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            UPDATE dbo.library_settings SET created_by = CASE WHEN created_by = {0} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {0} THEN NULL ELSE updated_by END WHERE created_by = {0} OR updated_by = {0};
            """;

        await _context.Database.ExecuteSqlRawAsync(sql, [staffId], cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
        }
        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
