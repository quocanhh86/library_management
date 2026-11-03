using web_mvc_management.Common;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Models.ViewModels.Catalog;

namespace web_mvc_management.Services;

public interface ICatalogMasterService
{
    // Select Options cho Dropdown
    Task<IReadOnlyList<SelectOptionViewModel>> GetActiveCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActiveAuthorsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActivePublishersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActiveShelvesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActiveBooksSelectAsync(CancellationToken ct = default);

    // 1. Thể loại sách (Categories)
    Task<PagedResult<CategoryItemViewModel>> GetCategoriesPagedAsync(BaseFilter filter, CancellationToken ct = default);
    Task<ServiceResult> SaveCategoryAsync(CategoryFormViewModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteCategoryAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestoreCategoryAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeleteCategoryAsync(Guid id, string? confirmCode, CancellationToken ct = default);

    // 2. Tác giả (Authors)
    Task<PagedResult<AuthorItemViewModel>> GetAuthorsPagedAsync(BaseFilter filter, CancellationToken ct = default);
    Task<ServiceResult> SaveAuthorAsync(AuthorFormViewModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteAuthorAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestoreAuthorAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeleteAuthorAsync(Guid id, string? confirmCode, CancellationToken ct = default);

    // 3. Nhà xuất bản (Publishers)
    Task<PagedResult<PublisherItemViewModel>> GetPublishersPagedAsync(BaseFilter filter, CancellationToken ct = default);
    Task<ServiceResult> SavePublisherAsync(PublisherFormViewModel model, CancellationToken ct = default);
    Task<ServiceResult> DeletePublisherAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestorePublisherAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeletePublisherAsync(Guid id, string? confirmCode, CancellationToken ct = default);

    // 4. Vị trí kệ sách (Shelves)
    Task<PagedResult<ShelfItemViewModel>> GetShelvesPagedAsync(BaseFilter filter, CancellationToken ct = default);
    Task<ServiceResult> SaveShelfAsync(ShelfFormViewModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteShelfAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> RestoreShelfAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult> HardDeleteShelfAsync(Guid id, string? confirmCode, CancellationToken ct = default);
}
