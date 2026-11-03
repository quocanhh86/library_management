using web_mvc_management.Constant;

namespace web_mvc_management.Entity;

/// <summary>
/// Hình ảnh của đầu sách (bảng book_images): hỗ trợ 1 đầu sách có nhiều ảnh (ảnh bìa trước, bìa sau, mục lục, trang đọc thử...).
/// Lưu trữ trên Cloudinary CDN (kèm PublicId) hoặc kho tĩnh cục bộ.
/// </summary>
public class BookImage : BaseEntity
{
    public Guid BookId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string? PublicId { get; set; }

    public string ImageType { get; set; } = LibraryConstants.BookImageType.PreviewPage;

    public string? Caption { get; set; }

    public int DisplayOrder { get; set; } = 1;

    public bool IsPrimary { get; set; }

    // Navigation property
    public virtual Book Book { get; set; } = null!;
}
