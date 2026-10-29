using Microsoft.EntityFrameworkCore.Storage;

namespace web_mvc_management.Repositories;

/// <summary>
/// Quản lý giao dịch và điều phối các Repository dùng chung một DbContext transaction.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
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
    /// Xác nhận (commit) transaction hiện tại.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy bỏ (rollback) transaction hiện tại khi xảy ra lỗi.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

