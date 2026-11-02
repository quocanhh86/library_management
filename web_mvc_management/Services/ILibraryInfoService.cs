using web_mvc_management.Models.ViewModels.Home;

namespace web_mvc_management.Services;

public interface ILibraryInfoService
{
    Task<LibraryInfoViewModel> GetAsync(CancellationToken ct = default);
}
