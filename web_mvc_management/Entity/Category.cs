namespace web_mvc_management.Entity;

/// <summary>
/// Thể loại sách (bảng categories).
/// </summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Navigation properties
    public virtual ICollection<Book> Books { get; set; } = [];
}

