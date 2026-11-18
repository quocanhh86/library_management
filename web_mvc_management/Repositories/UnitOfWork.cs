using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using web_mvc_management.Common;
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

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return Task.FromResult(_currentTransaction);
        }

        _currentTransaction = _context.Database.BeginTransaction(isolationLevel);
        return Task.FromResult(_currentTransaction);
    }

    public async Task<IDbContextTransaction> BeginTransactionWithLockAsync(
        string lockResource = "LIB_MUTATION_LOCK",
        int lockTimeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        var tx = await BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // Khóa ứng dụng sp_getapplock chỉ áp dụng trên SQL Server; provider nhẹ (SQLite In-Memory test) dùng transaction nguyên bản
            if (_context.Database.IsSqlServer())
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
            }

            return tx;
        }
        catch
        {
            await RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<ServiceResult<T>> ExecuteInLockedTransactionAsync<T>(
        Func<Task<ServiceResult<T>>> action,
        string lockResource = "LIB_MUTATION_LOCK",
        int lockTimeoutMs = 10000,
        CancellationToken cancellationToken = default) =>
        ExecuteInLockedTransactionCoreAsync(action, r => r.IsSuccess, lockResource, lockTimeoutMs, cancellationToken);

    public Task<ServiceResult> ExecuteInLockedTransactionAsync(
        Func<Task<ServiceResult>> action,
        string lockResource = "LIB_MUTATION_LOCK",
        int lockTimeoutMs = 10000,
        CancellationToken cancellationToken = default) =>
        ExecuteInLockedTransactionCoreAsync(action, r => r.IsSuccess, lockResource, lockTimeoutMs, cancellationToken);

    private async Task<TResult> ExecuteInLockedTransactionCoreAsync<TResult>(
        Func<Task<TResult>> action,
        Func<TResult, bool> isSuccess,
        string lockResource,
        int lockTimeoutMs,
        CancellationToken cancellationToken)
    {
        await BeginTransactionWithLockAsync(lockResource, lockTimeoutMs, cancellationToken);
        try
        {
            var result = await action();
            if (isSuccess(result))
            {
                await CommitTransactionAsync(cancellationToken);
            }
            else
            {
                await RollbackTransactionAsync(CancellationToken.None);
            }
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task ClearStaffAuditReferencesAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        var schemaPrefix = _context.Database.IsSqlServer() ? "dbo." : string.Empty;
        string[] tables =
        [
            "staff", "reader_types", "readers", "categories", "publishers", "authors", "shelves",
            "books", "book_authors", "book_copies", "book_images", "borrow_slips",
            "borrow_details", "fines", "reservations", "library_settings"
        ];

        var sql = string.Join(
            Environment.NewLine,
            tables.Select(t =>
                $"UPDATE {schemaPrefix}{t} SET created_by = CASE WHEN created_by = {{0}} THEN NULL ELSE created_by END, updated_by = CASE WHEN updated_by = {{0}} THEN NULL ELSE updated_by END WHERE created_by = {{0}} OR updated_by = {{0}};"));

        await _context.Database.ExecuteSqlRawAsync(sql, [staffId], cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            if (_currentTransaction != null)
            {
                _currentTransaction.Commit();
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
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Rollback();
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
