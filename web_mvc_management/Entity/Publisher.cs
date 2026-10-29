namespace web_mvc_management.Entity;

/// <summary>
/// Nhà xuất bản (bảng publishers).
/// </summary>
public class Publisher : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    // Navigation properties
    public virtual ICollection<Book> Books { get; set; } = [];
}

