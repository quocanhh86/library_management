using Microsoft.EntityFrameworkCore.Storage;
using web_mvc_management.Entity;

namespace web_mvc_management.Repositories;

/// <summary>
/// Quản lý giao dịch và điều phối các Repository dùng chung một DbContext transaction.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IBaseRepository<Staff> Staff { get; }
    IBaseRepository<ReaderType> ReaderTypes { get; }
    IBaseRepository<Reader> Readers { get; }
    IBaseRepository<Category> Categories { get; }
    IBaseRepository<Publisher> Publishers { get; }
    IBaseRepository<Author> Authors { get; }
    IBaseRepository<Shelf> Shelves { get; }
    IBaseRepository<Book> Books { get; }
    IBaseRepository<BookAuthor> BookAuthors { get; }
    IBaseRepository<BookCopy> BookCopies { get; }
    IBaseRepository<BookImage> BookImages { get; }
    IBaseRepository<BorrowSlip> BorrowSlips { get; }
    IBaseRepository<BorrowDetail> BorrowDetails { get; }
    IBaseRepository<Fine> Fines { get; }
    IBaseRepository<Reservation> Reservations { get; }
    IBaseRepository<LibrarySetting> LibrarySettings { get; }
    IBaseRepository<OtpEntry> Otps { get; }

    /// <summary>
    /// Lấy instance Repository generic cho thực thể tương ứng.
    /// </summary>
    IBaseRepository<TEntity> Repository<TEntity>() where TEntity : class;

    /// <summary>
    /// Lưu mọi thay đổi vào cơ sở dữ liệu.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắt đầu một database transaction.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắt đầu một database transaction với mức cô lập tùy chỉnh.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(
        System.Data.IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắt đầu một database transaction kèm khóa Pessimistic (sp_getapplock) gắn theo vòng đời Transaction
    /// để chống Race Condition (TOCTOU) và Deadlock khi xóa mềm / xóa cứng / mượn trả đồng thời.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionWithLockAsync(
        string lockResource = "LIB_MUTATION_LOCK",
        int lockTimeoutMs = 10000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gỡ tham chiếu audit (created_by, updated_by) của một Staff trước khi xóa vĩnh viễn.
    /// </summary>
    Task ClearStaffAuditReferencesAsync(Guid staffId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác nhận (commit) transaction hiện tại.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy bỏ (rollback) transaction hiện tại khi xảy ra lỗi.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
