using web_mvc_management.Common;

namespace web_mvc_management.Models.ViewModels.Reports;

public class ReportFilter
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string Preset { get; set; } = "month"; // "today", "week", "month", "year", "custom"
    public string ActiveTab { get; set; } = "overview"; // "overview", "circulation", "overdue", "top-books", "fines"
}

public class ReportSummaryMetrics
{
    public int TotalBorrowSlips { get; init; }
    public int TotalBooksBorrowed { get; init; }
    public int TotalBooksReturned { get; init; }
    public int TotalBooksCurrentlyBorrowing { get; init; }
    public int TotalBooksOverdue { get; init; }
    public double OnTimeReturnRate { get; init; } // %

    public decimal TotalFineIncurred { get; init; }
    public decimal TotalFineCollected { get; init; }
    public decimal TotalFineWaived { get; init; }
    public decimal TotalFineUnpaid { get; init; }
}

public class DailyCirculationItem
{
    public DateOnly Date { get; init; }
    public int BorrowCount { get; init; }
    public int ReturnCount { get; init; }
    public int OverdueCount { get; init; }
}

public class OverdueLoanItem
{
    public Guid DetailId { get; init; }
    public Guid SlipId { get; init; }
    public string SlipCode { get; init; } = "";
    public string ReaderName { get; init; } = "";
    public string CardNo { get; init; } = "";
    public string ReaderTypeName { get; init; } = "";
    public string? ReaderPhone { get; init; }
    public string BookTitle { get; init; } = "";
    public string Barcode { get; init; } = "";
    public string? ShelfLocation { get; init; }
    public DateTimeOffset BorrowedAt { get; init; }
    public DateOnly DueDate { get; init; }
    public int OverdueDays { get; init; }
    public decimal EstimatedFine { get; init; }
}

public class TopBorrowedBookItem
{
    public Guid BookId { get; init; }
    public string Title { get; init; } = "";
    public string Isbn { get; init; } = "";
    public string Authors { get; init; } = "";
    public string Categories { get; init; } = "";
    public int TotalCopies { get; init; }
    public int BorrowCount { get; init; }
}

public class FineTypeSummaryItem
{
    public string FineType { get; init; } = "";
    public string TypeLabel { get; init; } = "";
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal UnpaidAmount { get; init; }
    public decimal WaivedAmount { get; init; }
}

public class ReportDashboardViewModel
{
    public ReportFilter Filter { get; init; } = new();
    public DateOnly RangeStart { get; init; }
    public DateOnly RangeEnd { get; init; }
    public ReportSummaryMetrics Metrics { get; init; } = new();
    public IReadOnlyList<DailyCirculationItem> DailyTrends { get; init; } = [];
    public IReadOnlyList<OverdueLoanItem> OverdueLoans { get; init; } = [];
    public IReadOnlyList<TopBorrowedBookItem> TopBooks { get; init; } = [];
    public IReadOnlyList<FineTypeSummaryItem> FineSummary { get; init; } = [];

    // Return condition breakdown
    public int ReturnConditionGood { get; init; }
    public int ReturnConditionDamaged { get; init; }
    public int ReturnConditionLost { get; init; }
}
