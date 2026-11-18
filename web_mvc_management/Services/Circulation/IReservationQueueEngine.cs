using web_mvc_management.Entity;

namespace web_mvc_management.Services;

/// <summary>
/// Bộ điều phối hàng đợi đặt trước FIFO (Copy Trap): gán bản sao cho người xếp hàng hợp lệ tiếp theo hoặc trả về kho AVAILABLE.
/// </summary>
public interface IReservationQueueEngine
{
    Task AssignCopyToNextPendingOrReleaseAsync(
        BookCopy copy,
        Guid? excludeReaderId = null,
        Guid? excludeReservationId = null,
        CancellationToken ct = default);

    Task ExpireOverdueHoldsAsync(CancellationToken ct = default);

    Task CancelReaderOpenReservationsAsync(Guid readerId, CancellationToken ct = default);
}
