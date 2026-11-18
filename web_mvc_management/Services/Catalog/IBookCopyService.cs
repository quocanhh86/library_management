using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.AdminCatalog;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ quản trị Bản sao vật lý / Mã vạch và Thanh lý, Hủy sách hỏng mất (/Admin/BookCopies).
/// </summary>
public interface IBookCopyService
{
    Task<PagedResult<AdminBookCopyListItemViewModel>> GetBookCopiesPagedAsync(AdminBookCopyFilter filter, CancellationToken ct = default);

    Task<ServiceResult<int>> CreateBookCopiesBatchAsync(BookCopyBatchCreateViewModel model, CancellationToken ct = default);

    Task<ServiceResult> UpdateBookCopyAsync(BookCopyUpdateViewModel model, CancellationToken ct = default);

    Task<ServiceResult> DisposeOrMarkCopyAsync(BookCopyDisposeViewModel model, CancellationToken ct = default);

    Task<ServiceResult> SoftDeleteBookCopyAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> RestoreBookCopyAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> HardDeleteBookCopyAsync(Guid id, string? confirmCode, CancellationToken ct = default);
}
