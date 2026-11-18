using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Services;

/// <summary>
/// Dịch vụ tra cứu danh sách chọn (Dropdown/Select) dùng chung cho trang công khai và quản trị.
/// </summary>
public interface ICatalogLookupService
{
    Task<IReadOnlyList<TopicViewModel>> GetTopicsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActiveCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActiveAuthorsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActivePublishersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActiveShelvesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetActiveBooksSelectAsync(CancellationToken ct = default);
}
