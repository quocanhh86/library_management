using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Services;

public interface IBookService
{
    // -------------------------------------------------------------------------
    // 1. Tra cứu Danh mục sách (Public & Reader Portal)
    // -------------------------------------------------------------------------
    Task<IReadOnlyList<BookSummaryViewModel>> SuggestAsync(string keyword, int limit = 6, CancellationToken ct = default);
    Task<IReadOnlyList<BookSummaryViewModel>> GetNewArrivalsAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<TopicViewModel>> GetTopicsAsync(CancellationToken ct = default);
    Task<CatalogViewModel> GetPublicCatalogAsync(
        string? keyword,
        string? category,
        Guid? authorId,
        Guid? publisherId,
        int? year,
        bool available,
        int page,
        CancellationToken ct = default);
    Task<BookSummaryViewModel?> GetPublicBookDetailAsync(Guid id, CancellationToken ct = default);

    // -------------------------------------------------------------------------
    // 2. Quản lý Đầu sách & Nhập sách mới (/Admin/Books)
    // -------------------------------------------------------------------------
    Task<PagedResult<AdminBookListItemViewModel>> GetAdminBooksPagedAsync(AdminBookFilter filter, CancellationToken ct = default);
    Task<BookUpsertViewModel?> GetBookForEditAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<Guid>> CreateBookWithCopiesAsync(BookUpsertViewModel model, CancellationToken ct = default);
    Task<ServiceResult> UpdateBookAsync(BookUpsertViewModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteBookAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestoreBookAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeleteBookAsync(Guid id, string? confirmCode, CancellationToken ct = default);

    // -------------------------------------------------------------------------
    // 3. Quản lý Bản sao / Mã vạch & Thanh lý, Hủy sách hỏng/mất (/Admin/BookCopies)
    // -------------------------------------------------------------------------
    Task<PagedResult<AdminBookCopyListItemViewModel>> GetBookCopiesPagedAsync(AdminBookCopyFilter filter, CancellationToken ct = default);
    Task<ServiceResult<int>> CreateBookCopiesBatchAsync(BookCopyBatchCreateViewModel model, CancellationToken ct = default);
    Task<ServiceResult> UpdateBookCopyAsync(BookCopyUpdateViewModel model, CancellationToken ct = default);
    Task<ServiceResult> DisposeOrMarkCopyAsync(BookCopyDisposeViewModel model, CancellationToken ct = default);
    Task<ServiceResult> SoftDeleteBookCopyAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestoreBookCopyAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeleteBookCopyAsync(Guid id, string? confirmCode, CancellationToken ct = default);
}
