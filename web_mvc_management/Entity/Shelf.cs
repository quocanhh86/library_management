namespace web_mvc_management.Entity;

/// <summary>
/// Vị trí kệ sách (bảng shelves).
/// </summary>
public class Shelf : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Navigation properties
    public virtual ICollection<BookCopy> BookCopies { get; set; } = [];
}

