using web_mvc_management.Constant;

namespace web_mvc_management.Models.ViewModels.Reader;

public class ReaderLoanItemViewModel
{
    public Guid DetailId { get; init; }
    public string SlipNo { get; init; } = string.Empty;
    public string Barcode { get; init; } = string.Empty;
    public string BookTitle { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public DateTimeOffset BorrowedAt { get; init; }
    public DateOnly DueDate { get; init; }
    public DateTimeOffset? ReturnedAt { get; init; }
    public short RenewCount { get; init; }
    public short MaxRenewals { get; init; }
    public string ReturnCondition { get; init; } = LibraryConstants.ReturnCondition.Good;
}

public class ReaderFineItemViewModel
{
    public Guid Id { get; init; }
    public string SlipNo { get; init; } = string.Empty;
    public string BookTitle { get; init; } = string.Empty;
    public string FineType { get; init; } = LibraryConstants.FineType.Overdue;
    public int? OverdueDays { get; init; }
    public decimal Amount { get; init; }
    public string FineStatus { get; init; } = LibraryConstants.FineStatus.Unpaid;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public string? Note { get; init; }
}

public class ReaderReservationItemViewModel
{
    public Guid Id { get; init; }
    public Guid BookId { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public string? AssignedBarcode { get; init; }
    public string ReservationStatus { get; init; } = LibraryConstants.ReservationStatus.Pending;
    public DateTimeOffset ReservedAt { get; init; }
    public DateTimeOffset? ReadyAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public class ReaderPortalPageViewModel
{
    public string FullName { get; init; } = string.Empty;
    public string CardNo { get; init; } = string.Empty;
    public string? MemberCode { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string ReaderTypeName { get; init; } = string.Empty;
    public short MaxBorrowBooks { get; init; }
    public short LoanDays { get; init; }
    public DateOnly CardIssuedDate { get; init; }
    public DateOnly CardExpiryDate { get; init; }
    public string CardStatus { get; init; } = LibraryConstants.CardStatus.Active;
    public DateOnly Today { get; init; }
    public decimal TotalUnpaidFine { get; init; }
    public IReadOnlyList<ReaderLoanItemViewModel> ActiveLoans { get; init; } = [];
    public IReadOnlyList<ReaderLoanItemViewModel> BorrowHistory { get; init; } = [];
    public IReadOnlyList<ReaderFineItemViewModel> Fines { get; init; } = [];
    public IReadOnlyList<ReaderReservationItemViewModel> Reservations { get; init; } = [];
}

