using web_mvc_management.Common;
using web_mvc_management.Constant;

namespace web_mvc_management.Entity;

/// <summary>
/// Lớp cơ sở cho các Entity có khóa chính Guid và trường Audit log.
/// </summary>
public abstract class BaseEntity : IEntity<Guid>, IAuditableEntity, ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Trạng thái bản ghi (ACTIVE, INACTIVE, DELETED).
    /// </summary>
    public string Status { get; set; } = EntityStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }
}
