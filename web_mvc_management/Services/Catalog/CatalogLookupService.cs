using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Catalog;
using web_mvc_management.Models.ViewModels.Home;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class CatalogLookupService(IUnitOfWork unitOfWork) : ICatalogLookupService
{
    public async Task<IReadOnlyList<TopicViewModel>> GetTopicsAsync(CancellationToken ct = default) =>
        await unitOfWork.Categories.GetQueryable(asNoTracking: true)
            .Where(c => c.Status == EntityStatus.Active)
            .OrderBy(c => c.Name)
            .Select(c => new TopicViewModel(c.Id.ToString(), c.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveCategoriesAsync(CancellationToken ct = default) =>
        await unitOfWork.Categories.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel(x.Id, x.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveAuthorsAsync(CancellationToken ct = default) =>
        await unitOfWork.Authors.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectOptionViewModel(x.Id, x.FullName))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActivePublishersAsync(CancellationToken ct = default) =>
        await unitOfWork.Publishers.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel(x.Id, x.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveShelvesAsync(CancellationToken ct = default) =>
        await unitOfWork.Shelves.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Code)
            .Select(x => new SelectOptionViewModel(
                x.Id,
                string.IsNullOrWhiteSpace(x.Description) ? x.Code : $"{x.Code} – {x.Description}"))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetActiveBooksSelectAsync(CancellationToken ct = default) =>
        await unitOfWork.Books.GetQueryable(asNoTracking: true)
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.Title)
            .Select(x => new SelectOptionViewModel(
                x.Id,
                string.IsNullOrWhiteSpace(x.Isbn) ? x.Title : $"{x.Title} ({x.Isbn})"))
            .ToListAsync(ct);
}
