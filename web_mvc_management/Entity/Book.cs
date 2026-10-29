namespace web_mvc_management.Entity;

/// <summary>
/// Đầu sách (bảng books).
/// </summary>
public class Book : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Isbn { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? PublisherId { get; set; }

    public short? PublicationYear { get; set; }

    public string? Edition { get; set; }

    public string? Language { get; set; }

    public int? PageCount { get; set; }

    public string? Description { get; set; }

    public string? CoverUrl { get; set; }

    // Navigation properties
    public virtual Category? Category { get; set; }
    public virtual Publisher? Publisher { get; set; }
    public virtual ICollection<BookAuthor> BookAuthors { get; set; } = [];
    public virtual ICollection<BookCopy> BookCopies { get; set; } = [];
    public virtual ICollection<Reservation> Reservations { get; set; } = [];
}

