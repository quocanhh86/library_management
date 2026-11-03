using System.ComponentModel.DataAnnotations;
using web_mvc_management.Common;
using web_mvc_management.Constant;

namespace web_mvc_management.Models.ViewModels.Circulation;

public record DeskReader(Guid Id, string FullName, string CardNo, string TypeName, DateOnly CardExpiryDate, string CardStatus, int Borrowed, int Limit, decimal UnpaidFines);
public record DeskLoan(Guid DetailId, string Title, string Barcode, DateTimeOffset BorrowedAt, DateOnly DueDate, short RenewCount, decimal OverdueFine);
public record DeskHold(string Title, string Barcode, DateTimeOffset? ExpiresAt);
public record BookOption(Guid Id, string Title);

/// <summary>
/// Màn quầy mượn / trả: thông tin thẻ, sách đang mượn, sách đang giữ và lý do bị chặn mượn.
/// </summary>
public class CirculationDeskViewModel
{
    public bool Returning { get; init; }
    public DateOnly Today { get; init; }
    public string? Card { get; init; }
    public string? Error { get; init; }
    public DeskReader? Reader { get; init; }
    public IReadOnlyList<DeskLoan> Loans { get; init; } = [];
    public IReadOnlyList<DeskHold> Holds { get; init; } = [];
    public IReadOnlyList<string> BlockReasons { get; init; } = [];
    public int LoanDays { get; init; }
    public short MaxRenewals { get; init; }
    public decimal FinePerDay { get; init; }
    public bool CanBorrow => Reader is not null && BlockReasons.Count == 0;
}

public class BorrowSubmitViewModel
{
    [Required]
    public Guid ReaderId { get; set; }
    public List<string> Barcodes { get; set; } = [];
    [MaxLength(500)]
    public string? Note { get; set; }
}

public class ReturnItemViewModel
{
    public Guid DetailId { get; set; }
    public bool Selected { get; set; }
    public string Condition { get; set; } = LibraryConstants.ReturnCondition.Good;
    [MaxLength(500)]
    public string? Note { get; set; }
}

public class ReturnSubmitViewModel
{
    [Required]
    public Guid ReaderId { get; set; }
    public List<ReturnItemViewModel> Items { get; set; } = [];
}

public class FineFilter : BaseFilter
{
    public string? FineStatus { get; set; }
}

public class FineItemViewModel
{
    public Guid Id { get; init; }
    public string ReaderName { get; init; } = "";
    public string CardNo { get; init; } = "";
    public string BookTitle { get; init; } = "";
    public string Barcode { get; init; } = "";
    public string FineType { get; init; } = "";
    public int? OverdueDays { get; init; }
    public decimal Amount { get; init; }
    public string FineStatus { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Note { get; init; }
}

public class FinesPageViewModel
{
    public PagedResult<FineItemViewModel> Fines { get; init; } = new();
    public FineFilter Filter { get; init; } = new();
    public decimal UnpaidTotal { get; init; }
}

public class ReservationFilter : BaseFilter
{
    public string? ReservationStatus { get; set; }
}

public class ReservationItemViewModel
{
    public Guid Id { get; init; }
    public string ReaderName { get; init; } = "";
    public string CardNo { get; init; } = "";
    public string BookTitle { get; init; } = "";
    public string ReservationStatus { get; init; } = "";
    public DateTimeOffset ReservedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? AssignedBarcode { get; init; }
    public int QueuePosition { get; init; }
}

public class ReservationCreateViewModel
{
    [Required, MaxLength(20)]
    public string CardNo { get; set; } = "";
    [Required]
    public Guid BookId { get; set; }
}

public class ReservationsPageViewModel
{
    public PagedResult<ReservationItemViewModel> Reservations { get; init; } = new();
    public ReservationFilter Filter { get; init; } = new();
    public IReadOnlyList<BookOption> ReservableBooks { get; init; } = [];
}
