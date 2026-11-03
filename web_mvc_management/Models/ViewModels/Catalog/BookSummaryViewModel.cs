namespace web_mvc_management.Models.ViewModels.Catalog;

/// <summary>
/// Thông tin bản sao hiển thị công khai trên trang chi tiết sách.
/// </summary>
public class PublicBookCopyViewModel
{
    public Guid Id { get; init; }
    public string Barcode { get; init; } = string.Empty;
    public string ShelfCode { get; init; } = string.Empty;
    public string ShelfDescription { get; init; } = string.Empty;
    public string CopyStatus { get; init; } = string.Empty;
}

/// <summary>
/// Một đầu sách ở dạng tóm tắt và chi tiết cho cổng bạn đọc: hàng kết quả tìm kiếm, gợi ý, "Sách mới nhập", chi tiết đầu sách.
/// </summary>
public class BookSummaryViewModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public IReadOnlyList<Guid> AuthorIds { get; init; } = [];
    public string Publisher { get; init; } = string.Empty;
    public Guid? PublisherId { get; init; }
    public short? PublicationYear { get; init; }
    public string? CoverUrl { get; init; }
    public string? Isbn { get; init; }
    public string? Edition { get; init; }
    public string? Language { get; init; }
    public int? PageCount { get; init; }
    public string? Description { get; init; }

    /// <summary>Ký hiệu xếp giá / mã kệ đại diện.</summary>
    public string CallNumber { get; init; } = string.Empty;

    public Guid? CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>Khóa thể loại dùng làm tham số lọc và màu bìa: civil, logistics, it, science, survey, humanities.</summary>
    public string CategoryKey { get; init; } = string.Empty;

    public int AvailableCopies { get; init; }
    public int TotalCopies { get; init; }

    /// <summary>Hạn trả sớm nhất trong các bản đang mượn, để báo "có thể có lại từ ngày…" khi hết bản.</summary>
    public DateOnly? EarliestDueDate { get; init; }

    /// <summary>Danh sách các bản sao vật lý kèm mã vạch và vị trí kệ (hiển thị ở trang chi tiết).</summary>
    public IReadOnlyList<PublicBookCopyViewModel> Copies { get; init; } = [];
}
