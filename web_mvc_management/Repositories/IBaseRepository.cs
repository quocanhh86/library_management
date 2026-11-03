using System.Linq.Expressions;
using web_mvc_management.Common;

namespace web_mvc_management.Repositories;

/// <summary>
/// Giao diện Repository cơ sở generic hỗ trợ các thao tác CRUD và truy vấn chung.
/// </summary>
/// <typeparam name="TEntity">Kiểu thực thể</typeparam>
public interface IBaseRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Lấy IQueryable để tùy biến truy vấn nâng cao (Filter, Include, GroupBy).
    /// </summary>
    IQueryable<TEntity> GetQueryable(bool asNoTracking = true);

    /// <summary>
    /// Lấy thực thể theo Id (chỉ áp dụng cho thực thể kế thừa IEntity&lt;Guid&gt;).
    /// </summary>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy toàn bộ danh sách thực thể.
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(bool asNoTracking = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm danh sách thực thể theo điều kiện lọc.
    /// </summary>
    Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm một thực thể đầu tiên thỏa mãn điều kiện.
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra sự tồn tại của thực thể theo điều kiện.
    /// </summary>
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đếm số lượng thực thể thỏa mãn điều kiện.
    /// </summary>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Truy vấn phân trang kèm điều kiện lọc và sắp xếp.
    /// - Hỗ trợ pageIndex = 0 hoặc 1 đều truy vấn từ item 0 trong CSDL.
    /// - pageSize mặc định = 10 (nếu không truyền hoặc &lt;= 0).
    /// </summary>
    Task<PagedResult<TEntity>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes);

    /// <summary>
    /// Truy vấn phân trang từ đối tượng tham số phân trang chuẩn PaginationParams.
    /// </summary>
    Task<PagedResult<TEntity>> GetPagedAsync(
        PaginationParams pagination,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes);

    /// <summary>
    /// Thêm mới một thực thể.
    /// </summary>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm mới một danh sách thực thể.
    /// </summary>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu cập nhật một thực thể.
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Xóa vật lý một thực thể.
    /// </summary>
    void Delete(TEntity entity);

    /// <summary>
    /// Xóa vật lý nhiều thực thể.
    /// </summary>
    void DeleteRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Xóa mềm thực thể bằng cách cập nhật Status = DELETED.
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
