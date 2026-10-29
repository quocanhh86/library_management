namespace web_mvc_management.Common;

/// <summary>
/// Định nghĩa thực thể có khóa chính dạng generic.
/// </summary>
public interface IEntity<TId>
{
    TId Id { get; set; }
}

/// <summary>
/// Định nghĩa thực thể có thông tin kiểm toán (Audit Log).
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Định nghĩa thực thể hỗ trợ trạng thái mềm.
/// </summary>
public interface ISoftDelete
{
    string Status { get; set; }
}
