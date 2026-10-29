namespace web_mvc_management.Entity;

/// <summary>
/// Loại độc giả (bảng reader_types: sinh viên, giảng viên, v.v.).
/// </summary>
public class ReaderType : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public short MaxBorrowBooks { get; set; }

    public short LoanDays { get; set; }

    public short MaxRenewals { get; set; }

    public short RenewDays { get; set; }

    public short CardValidMonths { get; set; }

    public string? Description { get; set; }

    // Navigation properties
    public virtual ICollection<Reader> Readers { get; set; } = [];
}

