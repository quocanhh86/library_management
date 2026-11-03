using System.ComponentModel.DataAnnotations;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Catalog;

namespace web_mvc_management.Models.ViewModels.AdminCatalog;

/// <summary>
/// Bộ lọc tra cứu bản sao / mã vạch (/Admin/BookCopies).
/// </summary>
public class AdminBookCopyFilter : BaseFilter
{
    public Guid? BookId { get; set; }
    public Guid? ShelfId { get; set; }
    public string? CopyStatus { get; set; }
}

/// <summary>
/// Một dòng bản sao vật lý trong bảng quản lý bản sao.
/// </summary>
public class AdminBookCopyListItemViewModel
{
    public Guid Id { get; init; }
    public Guid BookId { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public string? Isbn { get; init; }
    public string Barcode { get; init; } = string.Empty;
    public Guid? ShelfId { get; init; }
    public string ShelfCode { get; init; } = string.Empty;
    public string CopyStatus { get; init; } = LibraryConstants.CopyStatus.Available;
    public string Status { get; init; } = EntityStatus.Active;
    public decimal? Price { get; init; }
    public DateOnly AcquiredDate { get; init; }
    public DateTimeOffset? DisposedAt { get; init; }
    public string? DisposalReason { get; init; }
    public string? Note { get; init; }
}

/// <summary>
/// Form nhập bổ sung lô bản sao cho một đầu sách hiện có.
/// </summary>
public class BookCopyBatchCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn đầu sách cần nhập bản sao.")]
    [Display(Name = "Đầu sách")]
    public Guid BookId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vị trí kệ đặt sách.")]
    [Display(Name = "Vị trí kệ")]
    public Guid ShelfId { get; set; }

    [Range(1, 50, ErrorMessage = "Số lượng nhập mỗi lần từ 1 đến 50 cuốn.")]
    [Display(Name = "Số lượng cuốn")]
    public int Quantity { get; set; } = 1;

    [StringLength(50, ErrorMessage = "Mã vạch không vượt quá 50 ký tự.")]
    [Display(Name = "Mã vạch tùy chỉnh (để trống sẽ tự sinh)")]
    public string? CustomBarcode { get; set; }

    [Range(0, 100000000, ErrorMessage = "Giá tiền không hợp lệ.")]
    [Display(Name = "Giá bìa (VNĐ)")]
    public decimal? Price { get; set; } = 120000m;

    [Display(Name = "Ngày nhập")]
    public DateOnly? AcquiredDate { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }
}

/// <summary>
/// Form cập nhật vị trí kệ, giá bìa hoặc phục hồi bản sao sách đã tu sửa.
/// </summary>
public class BookCopyUpdateViewModel
{
    [Required]
    public Guid Id { get; set; }

    [Display(Name = "Vị trí kệ")]
    public Guid? ShelfId { get; set; }

    [Range(0, 100000000, ErrorMessage = "Giá tiền không hợp lệ.")]
    [Display(Name = "Giá bìa (VNĐ)")]
    public decimal? Price { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    [Display(Name = "Phục hồi về trạng thái Sẵn sàng (Available)")]
    public bool RestoreToAvailable { get; set; }
}

/// <summary>
/// Form Thanh lý / Báo hỏng / Báo mất bản sao sách.
/// </summary>
public class BookCopyDisposeViewModel
{
    [Required]
    public Guid CopyId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn hình thức xử lý bản sao.")]
    [Display(Name = "Trạng thái xử lý")]
    public string TargetStatus { get; set; } = LibraryConstants.CopyStatus.Disposed;

    [Required(ErrorMessage = "Vui lòng nhập lý do thanh lý, hỏng hoặc mất sách.")]
    [StringLength(255, ErrorMessage = "Lý do không vượt quá 255 ký tự.")]
    [Display(Name = "Lý do thanh lý / hỏng / mất")]
    public string DisposalReason { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Ghi chú không vượt quá 500 ký tự.")]
    [Display(Name = "Ghi chú chi tiết")]
    public string? Note { get; set; }
}

/// <summary>
/// ViewModel trang quản lý bản sao sách (/Admin/BookCopies).
/// </summary>
public class AdminBookCopiesPageViewModel
{
    public PagedResult<AdminBookCopyListItemViewModel> PagedCopies { get; init; } = PagedResult<AdminBookCopyListItemViewModel>.Empty();
    public AdminBookCopyFilter Filter { get; init; } = new();
    public IReadOnlyList<SelectOptionViewModel> Books { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Shelves { get; init; } = [];
    public BookCopyBatchCreateViewModel BatchForm { get; init; } = new();
}

