namespace web_mvc_management.Constant;

/// <summary>
/// Trạng thái hoạt động chung của các bản ghi (status).
/// </summary>
public static class EntityStatus
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
    public const string Deleted = "DELETED";

    public static readonly string[] AllStatuses = [Active, Inactive, Deleted];
}

