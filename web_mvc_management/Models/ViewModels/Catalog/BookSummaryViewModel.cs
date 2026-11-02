namespace web_mvc_management.Models.ViewModels.Catalog;

/// <summary>
/// Một đầu sách ở dạng tóm tắt: hàng kết quả tìm kiếm, gợi ý, "Sách mới nhập".
/// </summary>
public class BookSummaryViewModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public short? PublicationYear { get; init; }
    public string? CoverUrl { get; init; }
    public string? Isbn { get; init; }

    /// <summary>TODO(xác nhận): DB chưa có cột ký hiệu xếp giá; mock dùng dạng DDC + Cutter.</summary>
    public string CallNumber { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    /// <summary>Khóa thể loại dùng làm tham số lọc ở trang tra cứu: civil, logistics, it, science, survey, humanities.</summary>
    public string CategoryKey { get; init; } = string.Empty;

    public int AvailableCopies { get; init; }
    public int TotalCopies { get; init; }

    /// <summary>Hạn trả sớm nhất trong các bản đang mượn, để báo "có thể có lại từ ngày…" khi hết bản.</summary>
    public DateOnly? EarliestDueDate { get; init; }
}
