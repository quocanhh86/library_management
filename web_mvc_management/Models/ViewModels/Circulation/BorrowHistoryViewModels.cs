using System.ComponentModel.DataAnnotations;
using web_mvc_management.Common;

namespace web_mvc_management.Models.ViewModels.Circulation;

public class BorrowSlipFilter : BaseFilter
{
    public string? BorrowStatus { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class BorrowSlipItemViewModel
{
    public Guid Id { get; init; }
    public string SlipCode { get; init; } = "";
    public Guid ReaderId { get; init; }
    public string ReaderName { get; init; } = "";
    public string CardNo { get; init; } = "";
    public string ReaderTypeName { get; init; } = "";
    public DateTimeOffset BorrowedAt { get; init; }
    public string IssuedByStaffName { get; init; } = "";
    public int TotalBooks { get; init; }
    public int ReturnedBooks { get; init; }
    public bool HasOverdue { get; init; }
    public string Status { get; init; } = "";
    public string? Note { get; init; }
    public IReadOnlyList<string> BookTitles { get; init; } = [];
    public bool IsFullyReturned => TotalBooks > 0 && ReturnedBooks >= TotalBooks;
}

public class BorrowSlipDetailItemViewModel
{
    public Guid DetailId { get; init; }
    public string BookTitle { get; init; } = "";
    public string Barcode { get; init; } = "";
    public string? ShelfLocation { get; init; }
    public decimal? Price { get; init; }
    public DateOnly DueDate { get; init; }
    public short RenewCount { get; init; }
    public DateTimeOffset? ReturnedAt { get; init; }
    public string? ReturnCondition { get; init; }
    public string? ReceivedByStaffName { get; init; }
    public string? ReturnNote { get; init; }
    public bool IsReturned => ReturnedAt.HasValue;
    public bool IsOverdue { get; init; }
    public int OverdueDays { get; init; }
}

public class BorrowSlipDetailViewModel
{
    public Guid Id { get; init; }
    public string SlipCode { get; init; } = "";
    public Guid ReaderId { get; init; }
    public string ReaderName { get; init; } = "";
    public string CardNo { get; init; } = "";
    public string ReaderTypeName { get; init; } = "";
    public string ReaderEmail { get; init; } = "";
    public string? ReaderPhone { get; init; }
    public DateTimeOffset BorrowedAt { get; init; }
    public string IssuedByStaffName { get; init; } = "";
    public string? Note { get; init; }
    public int TotalBooks { get; init; }
    public int ReturnedBooks { get; init; }
    public bool CanCancel { get; init; }
    public IReadOnlyList<BorrowSlipDetailItemViewModel> Items { get; init; } = [];
}

public class BorrowHistoryPageViewModel
{
    public PagedResult<BorrowSlipItemViewModel> Slips { get; init; } = new();
    public BorrowSlipFilter Filter { get; init; } = new();
    public int TotalActiveSlips { get; init; }
    public int TotalOverdueSlips { get; init; }
    public int TotalReturnedSlips { get; init; }
}

public class BorrowCancelViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập lý do hủy phiếu mượn")]
    [MaxLength(500, ErrorMessage = "Lý do không vượt quá 500 ký tự")]
    public string Reason { get; set; } = "";
}
