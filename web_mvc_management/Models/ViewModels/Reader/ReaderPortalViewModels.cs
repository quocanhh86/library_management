using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Shared;

namespace web_mvc_management.Models.ViewModels.Reader;

public class ReaderBorrowHistoryFilter : BaseFilter
{
    /// <summary>
    /// Lọc theo trạng thái lượt mượn: "" (Tất cả), "BORROWING" (Đang mượn), "RETURNED" (Đã trả), "OVERDUE" (Quá hạn).
    /// </summary>
    public string? LoanState { get; set; }
}

public class ReaderFineFilter : BaseFilter
{
    /// <summary>
    /// Lọc theo trạng thái phiếu phạt: "" (Tất cả), "UNPAID", "PAID", "WAIVED".
    /// </summary>
    public string? FineStatus { get; set; }

    /// <summary>
    /// Lọc theo loại vi phạm: "" (Tất cả), "OVERDUE", "DAMAGED", "LOST".
    /// </summary>
    public string? FineType { get; set; }
}

public class ReaderLoanItemViewModel
{
    public Guid DetailId { get; init; }
    public Guid SlipId { get; init; }
    public Guid BookId { get; init; }
    public string SlipNo { get; init; } = string.Empty;
    public string Barcode { get; init; } = string.Empty;
    public string ShelfCode { get; init; } = string.Empty;
    public string BookTitle { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public string? CoverUrl { get; init; }
    public DateTimeOffset BorrowedAt { get; init; }
    public DateOnly DueDate { get; init; }
    public DateTimeOffset? ReturnedAt { get; init; }
    public short RenewCount { get; init; }
    public short MaxRenewals { get; init; }
    public short RenewDays { get; init; }
    public string ReturnCondition { get; init; } = LibraryConstants.ReturnCondition.Good;
    public string? ReturnNote { get; init; }
    public decimal EstimatedOverdueFine { get; init; }
    public decimal RecordedFineAmount { get; init; }
    public bool CanRenew { get; init; }
    public string? RenewBlockReason { get; init; }
    public string LoanStatusCode { get; init; } = StatusBadge.Loan.Active;

    public string ReturnConditionLabel => ReturnCondition switch
    {
        LibraryConstants.ReturnCondition.Damaged => "Hư hỏng",
        LibraryConstants.ReturnCondition.Lost => "Báo mất",
        _ => "Nguyên vẹn"
    };
}

public class ReaderFineItemViewModel
{
    public Guid Id { get; init; }
    public Guid BorrowDetailId { get; init; }
    public Guid BookId { get; init; }
    public string SlipNo { get; init; } = string.Empty;
    public string Barcode { get; init; } = string.Empty;
    public string BookTitle { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public string FineType { get; init; } = LibraryConstants.FineType.Overdue;
    public int? OverdueDays { get; init; }
    public decimal Amount { get; init; }
    public string FineStatus { get; init; } = LibraryConstants.FineStatus.Unpaid;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset BorrowedAt { get; init; }
    public DateOnly DueDate { get; init; }
    public DateTimeOffset? ReturnedAt { get; init; }

    public string FineTypeLabel => FineType switch
    {
        LibraryConstants.FineType.Overdue => OverdueDays is > 0 ? $"Trả trễ {OverdueDays} ngày" : "Trả quá hạn",
        LibraryConstants.FineType.Damaged => "Làm hỏng sách",
        LibraryConstants.FineType.Lost => "Làm mất sách",
        _ => FineType
    };

    public string PaymentMethodLabel => PaymentMethod switch
    {
        LibraryConstants.PaymentMethod.Cash => "Tiền mặt tại quầy",
        LibraryConstants.PaymentMethod.BankTransfer => "Chuyển khoản",
        LibraryConstants.PaymentMethod.Online => "Trực tuyến",
        _ => "—"
    };
}

public class ReaderReservationItemViewModel
{
    public Guid Id { get; init; }
    public Guid BookId { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public string? AssignedBarcode { get; init; }
    public string? ShelfCode { get; init; }
    public string ReservationStatus { get; init; } = LibraryConstants.ReservationStatus.Pending;
    public int QueuePosition { get; init; }
    public DateTimeOffset ReservedAt { get; init; }
    public DateTimeOffset? ReadyAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool CanCancel => ReservationStatus is LibraryConstants.ReservationStatus.Pending or LibraryConstants.ReservationStatus.Ready;
}

public class ReaderPortalPageViewModel
{
    public ScreenId ActiveScreen { get; init; } = ScreenId.ReaderProfile;
    public Guid ReaderId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string CardNo { get; init; } = string.Empty;
    public string? MemberCode { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string ReaderTypeName { get; init; } = string.Empty;
    public short MaxBorrowBooks { get; init; }
    public short LoanDays { get; init; }
    public short MaxRenewals { get; init; }
    public short RenewDays { get; init; }
    public DateOnly CardIssuedDate { get; init; }
    public DateOnly CardExpiryDate { get; init; }
    public string CardStatus { get; init; } = LibraryConstants.CardStatus.Active;
    public string EffectiveCardBadgeCode { get; init; } = LibraryConstants.CardStatus.Active;
    public string? LockReason { get; init; }
    public DateOnly Today { get; init; }
    public decimal OverdueFinePerDay { get; init; }
    public decimal MaxUnpaidFine { get; init; }

    // Chỉ số tổng hợp (KPIs)
    public int ActiveLoanCount { get; init; }
    public int OverdueLoanCount { get; init; }
    public int DueSoonLoanCount { get; init; }
    public int TotalHistoryCount { get; init; }
    public int ReturnedLoanCount { get; init; }
    public decimal TotalUnpaidFine { get; init; }
    public decimal EstimatedAccruingFine { get; init; }
    public decimal TotalPaidFine { get; init; }
    public decimal TotalWaivedFine { get; init; }
    public int UnpaidFineCount { get; init; }
    public int ActiveReservationCount { get; init; }
    public int ReadyReservationCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    // Dữ liệu chi tiết các phân trang
    public IReadOnlyList<ReaderLoanItemViewModel> ActiveLoans { get; init; } = [];
    public IReadOnlyList<ReaderLoanItemViewModel> BorrowHistory { get; init; } = [];
    public PagedResult<ReaderLoanItemViewModel> PagedBorrowHistory { get; init; } = new();
    public ReaderBorrowHistoryFilter HistoryFilter { get; init; } = new();

    public IReadOnlyList<ReaderFineItemViewModel> Fines { get; init; } = [];
    public PagedResult<ReaderFineItemViewModel> PagedFines { get; init; } = new();
    public ReaderFineFilter FineFilter { get; init; } = new();

    public IReadOnlyList<ReaderReservationItemViewModel> Reservations { get; init; } = [];
}
