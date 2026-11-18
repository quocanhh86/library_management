using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.AdminCatalog;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ quản trị Đầu sách & Nhập sách mới (/Admin/Books).
/// </summary>
public interface IBookAdminService
{
    Task<PagedResult<AdminBookListItemViewModel>> GetAdminBooksPagedAsync(AdminBookFilter filter, CancellationToken ct = default);

    Task<BookUpsertViewModel?> GetBookForEditAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<Guid>> CreateBookWithCopiesAsync(BookUpsertViewModel model, CancellationToken ct = default);

    Task<ServiceResult> UpdateBookAsync(BookUpsertViewModel model, CancellationToken ct = default);

    Task<ServiceResult> DeleteBookAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> RestoreBookAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> HardDeleteBookAsync(Guid id, string? confirmCode, CancellationToken ct = default);
}
