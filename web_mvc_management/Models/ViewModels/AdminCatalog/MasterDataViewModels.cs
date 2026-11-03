using System;
using System.ComponentModel.DataAnnotations;
using web_mvc_management.Common;
using web_mvc_management.Constant;

namespace web_mvc_management.Models.ViewModels.AdminCatalog;

// =============================================================================
// 1. THỂ LOẠI SÁCH (CATEGORIES)
// =============================================================================
public class CategoryItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = EntityStatus.Active;
    public int BookCount { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public class CategoryFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên thể loại sách.")]
    [StringLength(100, ErrorMessage = "Tên thể loại không vượt quá 100 ký tự.")]
    [Display(Name = "Tên thể loại")]
    public string Name { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Mô tả không vượt quá 255 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = EntityStatus.Active;
}

public class CategoryPageViewModel
{
    public PagedResult<CategoryItemViewModel> PagedItems { get; init; } = PagedResult<CategoryItemViewModel>.Empty();
    public BaseFilter Filter { get; init; } = new();
    public CategoryFormViewModel Form { get; init; } = new();
}

// =============================================================================
// 2. TÁC GIẢ (AUTHORS)
// =============================================================================
public class AuthorItemViewModel
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Biography { get; init; }
    public string Status { get; init; } = EntityStatus.Active;
    public int BookCount { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public class AuthorFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên tác giả.")]
    [StringLength(150, ErrorMessage = "Họ tên tác giả không vượt quá 150 ký tự.")]
    [Display(Name = "Họ tên tác giả")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Tiểu sử không vượt quá 1000 ký tự.")]
    [Display(Name = "Tiểu sử / Ghi chú")]
    public string? Biography { get; set; }

    [Required]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = EntityStatus.Active;
}

public class AuthorPageViewModel
{
    public PagedResult<AuthorItemViewModel> PagedItems { get; init; } = PagedResult<AuthorItemViewModel>.Empty();
    public BaseFilter Filter { get; init; } = new();
    public AuthorFormViewModel Form { get; init; } = new();
}

// =============================================================================
// 3. NHÀ XUẤT BẢN (PUBLISHERS)
// =============================================================================
public class PublisherItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string Status { get; init; } = EntityStatus.Active;
    public int BookCount { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public class PublisherFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên nhà xuất bản.")]
    [StringLength(150, ErrorMessage = "Tên nhà xuất bản không vượt quá 150 ký tự.")]
    [Display(Name = "Tên nhà xuất bản")]
    public string Name { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Địa chỉ không vượt quá 255 ký tự.")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [StringLength(20, ErrorMessage = "Số điện thoại không vượt quá 20 ký tự.")]
    [Display(Name = "Điện thoại")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [StringLength(255)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(255)]
    [Display(Name = "Website")]
    public string? Website { get; set; }

    [Required]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = EntityStatus.Active;
}

public class PublisherPageViewModel
{
    public PagedResult<PublisherItemViewModel> PagedItems { get; init; } = PagedResult<PublisherItemViewModel>.Empty();
    public BaseFilter Filter { get; init; } = new();
    public PublisherFormViewModel Form { get; init; } = new();
}

// =============================================================================
// 4. VỊ TRÍ KỆ SÁCH (SHELVES)
// =============================================================================
public class ShelfItemViewModel
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = EntityStatus.Active;
    public int CopyCount { get; init; }
    public int AvailableCopyCount { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public class ShelfFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã kệ sách (ví dụ: KE-IT-01).")]
    [StringLength(30, ErrorMessage = "Mã kệ không vượt quá 30 ký tự.")]
    [Display(Name = "Mã vị trí kệ")]
    public string Code { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Mô tả vị trí kệ không vượt quá 255 ký tự.")]
    [Display(Name = "Mô tả khu vực / tầng")]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = EntityStatus.Active;
}

public class ShelfPageViewModel
{
    public PagedResult<ShelfItemViewModel> PagedItems { get; init; } = PagedResult<ShelfItemViewModel>.Empty();
    public BaseFilter Filter { get; init; } = new();
    public ShelfFormViewModel Form { get; init; } = new();
}

