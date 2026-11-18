using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.SystemAdmin;
using web_mvc_management.Repositories;

namespace web_mvc_management.Services;

public class LibrarySettingService(IUnitOfWork unitOfWork) : ILibrarySettingService
{
    public async Task<LibrarySettingFormViewModel> GetLibrarySettingAsync(CancellationToken ct = default)
    {
        var setting = await unitOfWork.LibrarySettings.GetQueryable(asNoTracking: true)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (setting == null)
        {
            return new LibrarySettingFormViewModel();
        }

        return new LibrarySettingFormViewModel
        {
            Id = setting.Id,
            OverdueFinePerDay = setting.OverdueFinePerDay,
            DamagedFinePercent = setting.DamagedFinePercent,
            LostFinePercent = setting.LostFinePercent,
            MaxUnpaidFine = setting.MaxUnpaidFine,
            ReservationHoldHours = setting.ReservationHoldHours,
            UpdatedAt = setting.UpdatedAt
        };
    }

    public Task<ServiceResult> SaveLibrarySettingAsync(LibrarySettingFormViewModel model, CancellationToken ct = default) =>
        unitOfWork.ExecuteInLockedTransactionAsync(async () =>
        {
            var setting = await unitOfWork.LibrarySettings.GetQueryable(asNoTracking: false)
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync(ct);

            if (setting == null)
            {
                setting = new LibrarySetting
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    OverdueFinePerDay = model.OverdueFinePerDay,
                    DamagedFinePercent = model.DamagedFinePercent,
                    LostFinePercent = model.LostFinePercent,
                    MaxUnpaidFine = model.MaxUnpaidFine,
                    ReservationHoldHours = model.ReservationHoldHours,
                    Status = EntityStatus.Active
                };
                await unitOfWork.LibrarySettings.AddAsync(setting, ct);
            }
            else
            {
                setting.OverdueFinePerDay = model.OverdueFinePerDay;
                setting.DamagedFinePercent = model.DamagedFinePercent;
                setting.LostFinePercent = model.LostFinePercent;
                setting.MaxUnpaidFine = model.MaxUnpaidFine;
                setting.ReservationHoldHours = model.ReservationHoldHours;
                setting.Status = EntityStatus.Active;
            }

            return ServiceResult.Success("Đã lưu cấu hình quy định mức phạt và vận hành thư viện.");
        }, cancellationToken: ct);
}
