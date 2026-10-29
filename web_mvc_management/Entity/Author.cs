namespace web_mvc_management.Entity;

/// <summary>
/// Tác giả sách (bảng authors).
/// </summary>
public class Author : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    public string? Biography { get; set; }

    // Navigation properties
    public virtual ICollection<BookAuthor> BookAuthors { get; set; } = [];
}

