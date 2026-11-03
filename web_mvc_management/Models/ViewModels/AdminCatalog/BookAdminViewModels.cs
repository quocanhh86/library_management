using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using web_mvc_management.Common;
using web_mvc_management.Constant;
using web_mvc_management.Models.ViewModels.Catalog;

namespace web_mvc_management.Models.ViewModels.AdminCatalog;

/// <summary>
/// Bộ lọc danh mục đầu sách khu vực quản trị.
/// </summary>
public class AdminBookFilter : BaseFilter
{
    public Guid? CategoryId { get; set; }
    public Guid? AuthorId { get; set; }
    public Guid? PublisherId { get; set; }
    public short? PublicationYear { get; set; }
}

/// <summary>
/// Một dòng đầu sách trong bảng quản lý danh mục sách (/Admin/Books).
/// </summary>
public class AdminBookListItemViewModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Isbn { get; init; }
    public Guid? CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public Guid? PublisherId { get; init; }
    public string PublisherName { get; init; } = string.Empty;
    public string AuthorsText { get; init; } = string.Empty;
    public short? PublicationYear { get; init; }
    public string? Edition { get; init; }
    public string? Language { get; init; }
    public string Status { get; init; } = EntityStatus.Active;
    public int TotalCopies { get; init; }
    public int AvailableCopies { get; init; }
    public int BorrowedCopies { get; init; }
    public int DamagedOrLostCopies { get; init; }
    public int DisposedCopies { get; init; }
    public string ShelfSummary { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// ViewModel trang danh sách đầu sách kèm bộ lọc.
/// </summary>
public class AdminBooksPageViewModel
{
    public PagedResult<AdminBookListItemViewModel> PagedBooks { get; init; } = PagedResult<AdminBookListItemViewModel>.Empty();
    public AdminBookFilter Filter { get; init; } = new();
    public IReadOnlyList<SelectOptionViewModel> Categories { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Authors { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Publishers { get; init; } = [];
}

/// <summary>
/// Form thêm mới / chỉnh sửa đầu sách (hỗ trợ nhập kèm lô bản sao ban đầu và chọn vị trí kệ).
/// </summary>
public class BookUpsertViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên đầu sách.")]
    [StringLength(300, ErrorMessage = "Tên đầu sách không vượt quá 300 ký tự.")]
    [Display(Name = "Tên đầu sách")]
    public string Title { get; set; } = string.Empty;

    [StringLength(17, ErrorMessage = "Mã ISBN không vượt quá 17 ký tự.")]
    [Display(Name = "Mã ISBN")]
    public string? Isbn { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thể loại sách.")]
    [Display(Name = "Thể loại sách")]
    public Guid? CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn nhà xuất bản.")]
    [Display(Name = "Nhà xuất bản")]
    public Guid? PublisherId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ít nhất một tác giả.")]
    [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một tác giả.")]
    [Display(Name = "Tác giả")]
    public List<Guid> AuthorIds { get; set; } = [];

    [Range(1800, 2100, ErrorMessage = "Năm xuất bản phải từ 1800 đến 2100.")]
    [Display(Name = "Năm xuất bản")]
    public short? PublicationYear { get; set; }

    [StringLength(50, ErrorMessage = "Lần xuất bản không vượt quá 50 ký tự.")]
    [Display(Name = "Lần tái bản / Ấn bản")]
    public string? Edition { get; set; }

    [StringLength(50, ErrorMessage = "Ngôn ngữ không vượt quá 50 ký tự.")]
    [Display(Name = "Ngôn ngữ")]
    public string? Language { get; set; } = "Tiếng Việt";

    [Range(1, 10000, ErrorMessage = "Số trang phải từ 1 đến 10.000.")]
    [Display(Name = "Số trang")]
    public int? PageCount { get; set; }

    [StringLength(2000, ErrorMessage = "Mô tả không vượt quá 2000 ký tự.")]
    [Display(Name = "Tóm tắt nội dung")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "Đường dẫn ảnh bìa không vượt quá 500 ký tự.")]
    [Display(Name = "Đường dẫn ảnh bìa (tùy chọn)")]
    public string? CoverUrl { get; set; }

    [Required]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = EntityStatus.Active;

    // -------------------------------------------------------------------------
    // Thông tin nhập lô bản sao ban đầu (chỉ áp dụng khi thêm mới đầu sách)
    // -------------------------------------------------------------------------
    [Range(0, 100, ErrorMessage = "Số lượng bản sao nhập lần đầu từ 0 đến 100 cuốn.")]
    [Display(Name = "Số lượng bản sao nhập kho")]
    public int InitialCopyCount { get; set; } = 3;

    [Display(Name = "Vị trí kệ đặt sách")]
    public Guid? ShelfId { get; set; }

    [Range(0, 100000000, ErrorMessage = "Giá bìa không hợp lệ.")]
    [Display(Name = "Giá bìa mỗi cuốn (VNĐ)")]
    public decimal? CopyPrice { get; set; } = 120000m;

    [Display(Name = "Ngày nhập kho")]
    public DateOnly? AcquiredDate { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú lô bản sao")]
    public string? CopyNote { get; set; }

    // Danh sách chọn (Select options)
    public IReadOnlyList<SelectOptionViewModel> Categories { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Authors { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Publishers { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Shelves { get; set; } = [];
}

