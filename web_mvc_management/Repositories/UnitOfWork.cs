using System.Collections.Concurrent;
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

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return _currentTransaction;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _currentTransaction;
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
            await RollbackTransactionAsync(cancellationToken);
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
