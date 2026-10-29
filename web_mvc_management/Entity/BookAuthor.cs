using web_mvc_management.Common;
using web_mvc_management.Constant;

namespace web_mvc_management.Entity;

/// <summary>
/// Bảng liên kết nhiều-nhiều giữa sách và tác giả (bảng book_authors).
/// </summary>
public class BookAuthor : IAuditableEntity, ISoftDelete
{
    public Guid BookId { get; set; }

    public Guid AuthorId { get; set; }

    public short AuthorOrder { get; set; } = 1;

    public string Status { get; set; } = EntityStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    public virtual Book Book { get; set; } = null!;
    public virtual Author Author { get; set; } = null!;
}
