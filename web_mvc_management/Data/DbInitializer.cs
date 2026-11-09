using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Util;

namespace web_mvc_management.Data;

/// <summary>
/// Khởi tạo bộ dữ liệu chuẩn mực, đầy đủ 100% kịch bản nghiệp vụ và hình ảnh sách phục vụ demo & bảo vệ đồ án Quản lý thư viện UTC.
/// </summary>
public static class DbInitializer
{
    private const string ExamDatasetVersionMarker = "[EXAM-MASTER-DATASET-2026-V3]";

    public static async Task InitializeAsync(ApplicationDbContext context, IConfiguration? configuration = null, bool forceReset = false)
    {
        // 0. Đảm bảo bảng dbo.book_images và các constraint chuẩn tồn tại trên SQL Server
        const string ensureSchemaSql = """
            IF OBJECT_ID('dbo.book_images', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.book_images (
                    id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    book_id UNIQUEIDENTIFIER NOT NULL,
                    image_url NVARCHAR(500) NOT NULL,
                    public_id NVARCHAR(255) NULL,
                    image_type VARCHAR(30) NOT NULL DEFAULT 'COVER',
                    caption NVARCHAR(200) NULL,
                    display_order INT NOT NULL DEFAULT 0,
                    is_primary BIT NOT NULL DEFAULT 0,
                    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
                    created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                    updated_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                    created_by UNIQUEIDENTIFIER NULL,
                    updated_by UNIQUEIDENTIFIER NULL,
                    CONSTRAINT fk_book_images_books FOREIGN KEY (book_id) REFERENCES dbo.books(id) ON DELETE CASCADE
                );
                CREATE INDEX ix_book_images_book_id_status_display_order ON dbo.book_images(book_id, status, display_order);
            END

            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.books') AND name = 'language' AND system_type_id <> 231)
            BEGIN
                IF OBJECT_ID('dbo.df_books_language', 'D') IS NOT NULL
                    ALTER TABLE dbo.books DROP CONSTRAINT df_books_language;
                ALTER TABLE dbo.books ALTER COLUMN language NVARCHAR(50) NULL;
                UPDATE dbo.books SET language = N'Tiếng Việt' WHERE language LIKE 'Ti%ng Vi%t';
            END

            IF EXISTS (
                SELECT 1
                FROM sys.check_constraints
                WHERE COL_NAME(parent_object_id, parent_column_id) = 'status'
                  AND definition NOT LIKE '%DELETED%'
            )
            BEGIN
                DECLARE @statusConstraintSql NVARCHAR(MAX) = N'';
                SELECT @statusConstraintSql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
                    + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' + CHAR(13)
                    + N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
                    + N' ADD CONSTRAINT ' + QUOTENAME(name) + N' CHECK ([status] IN (''ACTIVE'', ''INACTIVE'', ''DELETED''));' + CHAR(13)
                FROM sys.check_constraints
                WHERE COL_NAME(parent_object_id, parent_column_id) = 'status'
                  AND definition NOT LIKE '%DELETED%';

                IF LEN(@statusConstraintSql) > 0
                    EXEC sp_executesql @statusConstraintSql;
            END
            """;
        await context.Database.ExecuteSqlRawAsync(ensureSchemaSql);

        var hasExamDatasetV3 = await context.BorrowSlips.AnyAsync(s => s.Note != null && s.Note.Contains(ExamDatasetVersionMarker));
        if (forceReset || !hasExamDatasetV3)
        {
            await ResetTransactionalAndCatalogDataAsync(context);
        }

        // 1. Cấu hình quy định thư viện (LibrarySettings)
        var existingSetting = await context.LibrarySettings.FirstOrDefaultAsync();
        if (existingSetting == null)
        {
            await context.LibrarySettings.AddAsync(new LibrarySetting
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                OverdueFinePerDay = 2000m,
                DamagedFinePercent = 50,
                LostFinePercent = 150,
                MaxUnpaidFine = 50000m,
                ReservationHoldHours = 48,
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }
        else if (forceReset)
        {
            existingSetting.OverdueFinePerDay = 2000m;
            existingSetting.DamagedFinePercent = 50;
            existingSetting.LostFinePercent = 150;
            existingSetting.MaxUnpaidFine = 50000m;
            existingSetting.ReservationHoldHours = 48;
            existingSetting.Status = EntityStatus.Active;
            existingSetting.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        // 2. Loại độc giả (ReaderTypes: 3 Active + 1 Deleted trong Thùng rác)
        var studentType = await EnsureReaderTypeAsync(context, "STUDENT", "Sinh viên", 5, 14, 2, 7, 48,
            "Độc giả là sinh viên hệ chính quy và chất lượng cao các khóa", EntityStatus.Active);
        var lecturerType = await EnsureReaderTypeAsync(context, "LECTURER", "Giảng viên", 10, 30, 3, 14, 60,
            "Độc giả là giảng viên, nghiên cứu viên, cán bộ công tác tại trường", EntityStatus.Active);
        var guestType = await EnsureReaderTypeAsync(context, "GUEST", "Khách ngoài", 2, 7, 1, 3, 12,
            "Độc giả tự do, học viên bồi dưỡng ngắn hạn đăng ký thẻ đọc", EntityStatus.Active);
        await EnsureReaderTypeAsync(context, "ALUMNI_OLD", "Cựu sinh viên (Chế độ cũ)", 3, 10, 1, 5, 24,
            "Phân loại thẻ cựu sinh viên giai đoạn trước (Lưu trong thùng rác để demo Khôi phục / Xóa cứng)", EntityStatus.Deleted);

        // 3. Nhân viên & Quản trị viên (Staff)
        var configuredDefaultPassword = configuration?["SeedSettings:DefaultPassword"]
            ?? Environment.GetEnvironmentVariable("SeedSettings__DefaultPassword");

        var hasConfiguredPassword = !string.IsNullOrWhiteSpace(configuredDefaultPassword) &&
            !configuredDefaultPassword.Equals("your_seed_password", StringComparison.OrdinalIgnoreCase);

        var effectiveDefaultPassword = hasConfiguredPassword
            ? configuredDefaultPassword!.Trim()
            : "Admin@123456";

        var defaultPasswordHash = PasswordHasher.HashPassword(effectiveDefaultPassword);

        await EnsureCoreStaffAccountsAsync(context, defaultPasswordHash);

        var seedUsers = GetSeedUsers(configuration, effectiveDefaultPassword, hasConfiguredPassword);
        var usedStaffUsernames = new HashSet<string>(
            await context.Staff.Select(x => x.Username.ToLower()).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var u in seedUsers)
        {
            var staffUser = await context.Staff.FirstOrDefaultAsync(x => x.Email.ToLower() == u.Email);
            if (staffUser == null)
            {
                var passHash = u.HasExplicitPassword ? PasswordHasher.HashPassword(u.Password) : null;
                var candidateUsername = u.Username;
                var suffix = 1;
                while (usedStaffUsernames.Contains(candidateUsername))
                {
                    candidateUsername = $"{u.Username}{suffix++}";
                }
                usedStaffUsernames.Add(candidateUsername);

                staffUser = new Staff
                {
                    Id = Guid.NewGuid(),
                    Username = candidateUsername,
                    Email = u.Email,
                    FullName = u.FullName,
                    PasswordHash = passHash ?? defaultPasswordHash,
                    Phone = "0981112233",
                    Role = u.Role,
                    Status = EntityStatus.Active,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await context.Staff.AddAsync(staffUser);
            }
            else
            {
                if (staffUser.Role != u.Role) staffUser.Role = u.Role;
                if (staffUser.FullName != u.FullName) staffUser.FullName = u.FullName;
                if (u.HasExplicitPassword && !PasswordHasher.VerifyPassword(u.Password, staffUser.PasswordHash))
                {
                    staffUser.PasswordHash = PasswordHasher.HashPassword(u.Password);
                }
                if (staffUser.Status != EntityStatus.Active) staffUser.Status = EntityStatus.Active;
                if (string.IsNullOrWhiteSpace(staffUser.SecurityStamp))
                {
                    staffUser.SecurityStamp = Guid.NewGuid().ToString("N");
                }
            }
        }
        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        var adminStaff = await context.Staff.FirstOrDefaultAsync(x => x.Role == AppRoles.Admin && x.Status == EntityStatus.Active)
            ?? await context.Staff.FirstAsync();
        var librarianStaff = await context.Staff.FirstOrDefaultAsync(x => x.Role == AppRoles.Librarian && x.Status == EntityStatus.Active)
            ?? adminStaff;

        // 4. Kệ sách (Shelves: 8 Active + 1 Deleted)
        var shelvesByCode = await EnsureShelvesAsync(context, adminStaff.Id);

        // 5. Thể loại sách (Categories: 8 Active + 1 Deleted - phủ kín mọi chủ đề trên Trang chủ)
        var categoriesByKey = await EnsureCategoriesAsync(context, adminStaff.Id);

        // 6. Nhà xuất bản (Publishers: 8 Active + 1 Deleted)
        var publishersByKey = await EnsurePublishersAsync(context, adminStaff.Id);

        // 7. Tác giả (Authors: 14 Active + 1 Deleted)
        var authorsByKey = await EnsureAuthorsAsync(context, adminStaff.Id);

        // 8. Độc giả (Readers: Đầy đủ các kịch bản đi thi + tài khoản cá nhân từ .env)
        var today = DateOnly.FromDateTime(DateTime.Today);
        await EnsureExamReadersAsync(context, studentType.Id, lecturerType.Id, guestType.Id, defaultPasswordHash, adminStaff.Id, today);

        var usedReaderUsernames = new HashSet<string>(
            await context.Readers.Select(x => x.Username.ToLower()).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);
        var usedReaderCardNos = new HashSet<string>(
            await context.Readers.Select(x => x.CardNo.ToUpper()).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var u in seedUsers)
        {
            var readerUser = await context.Readers.FirstOrDefaultAsync(x => x.Email.ToLower() == u.Email);
            var targetTypeId = u.Role == AppRoles.Admin ? lecturerType.Id : studentType.Id;

            if (readerUser == null)
            {
                var passHash = u.HasExplicitPassword ? PasswordHasher.HashPassword(u.Password) : null;
                var baseReaderUsername = u.Username.Replace("_admin", "_reader").Replace("_librarian", "_student");
                var candidateUsername = baseReaderUsername;
                var uSuffix = 1;
                while (usedReaderUsernames.Contains(candidateUsername))
                {
                    candidateUsername = $"{baseReaderUsername}{uSuffix++}";
                }
                usedReaderUsernames.Add(candidateUsername);

                var candidateCardNo = u.CardNo;
                var cardSeq = 8900;
                var cardPrefix = u.Role == AppRoles.Admin ? "GV" : "SV";
                while (usedReaderCardNos.Contains(candidateCardNo))
                {
                    candidateCardNo = $"{cardPrefix}2026{cardSeq++}";
                }
                usedReaderCardNos.Add(candidateCardNo);

                readerUser = new Reader
                {
                    Id = Guid.NewGuid(),
                    ReaderTypeId = targetTypeId,
                    Username = candidateUsername,
                    Email = u.Email,
                    PasswordHash = passHash ?? defaultPasswordHash,
                    FullName = u.FullName,
                    Phone = "0981112233",
                    CardNo = candidateCardNo,
                    MemberCode = u.MemberCode,
                    DateOfBirth = new DateOnly(2003, 6, 15),
                    Address = "Số 3 Cầu Giấy, Láng Thượng, Đống Đa, Hà Nội",
                    CardIssuedDate = today.AddMonths(-6),
                    CardExpiryDate = today.AddYears(4),
                    CardStatus = LibraryConstants.CardStatus.Active,
                    Status = EntityStatus.Active,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaff.Id
                };
                await context.Readers.AddAsync(readerUser);
            }
            else
            {
                if (u.HasExplicitPassword && !PasswordHasher.VerifyPassword(u.Password, readerUser.PasswordHash))
                {
                    readerUser.PasswordHash = PasswordHasher.HashPassword(u.Password);
                }
                if (readerUser.Status != EntityStatus.Active) readerUser.Status = EntityStatus.Active;
                if (readerUser.CardStatus != LibraryConstants.CardStatus.Active) readerUser.CardStatus = LibraryConstants.CardStatus.Active;
                if (string.IsNullOrWhiteSpace(readerUser.SecurityStamp))
                {
                    readerUser.SecurityStamp = Guid.NewGuid().ToString("N");
                }
            }
        }
        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        // 9. Đầu sách (26 Active + 2 Deleted = 28 cuốn), Tác giả sách (BookAuthors) và Bản sao vật lý (BookCopies)
        await EnsureMasterBooksAndCopiesAsync(context, categoriesByKey, publishersByKey, authorsByKey, shelvesByCode, adminStaff.Id, today);

        // 10. Khởi tạo Bộ sưu tập 5 Hình ảnh / Đầu sách (Bìa trước + Bìa sau + Mục lục + 2 Trang đọc thử)
        await SeedSampleBookImagesAsync(context, adminStaff.Id);

        // 11. Khởi tạo dữ liệu Lưu thông, Mượn trả, Phạt & Đặt trước cho toàn bộ Kịch bản Đi thi
        await EnsureMasterCirculationScenariosAsync(context, adminStaff.Id, librarianStaff.Id, today);
    }

    private static async Task ResetTransactionalAndCatalogDataAsync(ApplicationDbContext context)
    {
        const string cleanupSql = """
            DELETE FROM dbo.fines;
            DELETE FROM dbo.reservations;
            DELETE FROM dbo.borrow_details;
            DELETE FROM dbo.borrow_slips;
            DELETE FROM dbo.book_images;
            DELETE FROM dbo.book_copies;
            DELETE FROM dbo.book_authors;
            DELETE FROM dbo.books;
            DELETE FROM dbo.categories;
            DELETE FROM dbo.publishers;
            DELETE FROM dbo.authors;
            DELETE FROM dbo.shelves;
            DELETE FROM dbo.readers;
            DELETE FROM dbo.reader_types;
            """;
        await context.Database.ExecuteSqlRawAsync(cleanupSql);
        context.ChangeTracker.Clear();
    }

    private static async Task<ReaderType> EnsureReaderTypeAsync(
        ApplicationDbContext context,
        string code,
        string name,
        byte maxBooks,
        short loanDays,
        byte maxRenewals,
        short renewDays,
        short validMonths,
        string description,
        string status)
    {
        var item = await context.ReaderTypes.FirstOrDefaultAsync(x => x.Code == code);
        if (item == null)
        {
            item = new ReaderType
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                MaxBorrowBooks = maxBooks,
                LoanDays = loanDays,
                MaxRenewals = maxRenewals,
                RenewDays = renewDays,
                CardValidMonths = validMonths,
                Description = description,
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await context.ReaderTypes.AddAsync(item);
            await context.SaveChangesAsync();
        }
        else
        {
            item.Name = name;
            item.MaxBorrowBooks = maxBooks;
            item.LoanDays = loanDays;
            item.MaxRenewals = maxRenewals;
            item.RenewDays = renewDays;
            item.CardValidMonths = validMonths;
            item.Description = description;
            item.Status = status;
            await context.SaveChangesAsync();
        }
        return item;
    }

    private static async Task EnsureCoreStaffAccountsAsync(ApplicationDbContext context, string defaultPasswordHash)
    {
        var specs = new (string Username, string Email, string FullName, string Phone, string Role, string Status, int FailedAttempts, DateTimeOffset? LockedUntil)[]
        {
            ("admin", "admin@library.utc.edu.vn", "TS. Nguyễn Quốc Anh (Quản trị viên Hệ thống)", "0901234567", AppRoles.Admin, EntityStatus.Active, 0, null),
            ("librarian01", "thuthu01@library.utc.edu.vn", "Trần Thị Mai (Thủ thư Ca Sáng)", "0912345678", AppRoles.Librarian, EntityStatus.Active, 0, null),
            ("librarian02", "thuthu02@library.utc.edu.vn", "Nguyễn Văn Tuấn (Thủ thư Ca Chiều)", "0913456789", AppRoles.Librarian, EntityStatus.Active, 0, null),
            ("librarian_locked", "thuthu.khoa@library.utc.edu.vn", "Lê Hoàng Nam (Tạm khóa Brute-Force demo)", "0914567890", AppRoles.Librarian, EntityStatus.Active, 5, DateTimeOffset.UtcNow.AddHours(3)),
            ("staff_deleted", "nhanvien.nghi@library.utc.edu.vn", "Phạm Thị Hương (Nhân sự đã nghỉ - Thùng rác)", "0915678901", AppRoles.Librarian, EntityStatus.Deleted, 0, null)
        };

        foreach (var s in specs)
        {
            var existing = await context.Staff.FirstOrDefaultAsync(x => x.Username == s.Username || x.Email == s.Email);
            if (existing == null)
            {
                await context.Staff.AddAsync(new Staff
                {
                    Id = Guid.NewGuid(),
                    Username = s.Username,
                    Email = s.Email,
                    FullName = s.FullName,
                    PasswordHash = defaultPasswordHash,
                    Phone = s.Phone,
                    Role = s.Role,
                    Status = s.Status,
                    FailedLoginAttempts = s.FailedAttempts,
                    LockedUntil = s.LockedUntil,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.FullName = s.FullName;
                existing.Status = s.Status;
                existing.FailedLoginAttempts = s.FailedAttempts;
                existing.LockedUntil = s.LockedUntil;
                if (string.IsNullOrWhiteSpace(existing.SecurityStamp))
                {
                    existing.SecurityStamp = Guid.NewGuid().ToString("N");
                }
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, Shelf>> EnsureShelvesAsync(ApplicationDbContext context, Guid adminStaffId)
    {
        var specs = new (string Code, string Description, string Status)[]
        {
            ("KE-IT-01", "Tầng 1 - Khu A1: Kỹ thuật Phần mềm, C#, .NET & Kiến trúc Hệ thống", EntityStatus.Active),
            ("KE-IT-02", "Tầng 1 - Khu A2: Cơ sở dữ liệu, Mạng máy tính & An toàn thông tin", EntityStatus.Active),
            ("KE-AI-01", "Tầng 1 - Khu A3: Trí tuệ nhân tạo, Học sâu (Deep Learning) & Khoa học dữ liệu", EntityStatus.Active),
            ("KE-GT-01", "Tầng 2 - Khu B1: Kỹ thuật Xây dựng Cầu đường, Hầm & Giao thông đô thị", EntityStatus.Active),
            ("KE-CK-01", "Tầng 2 - Khu B2: Cơ khí Động lực, Kỹ thuật Ô tô điện & Đường sắt tốc độ cao", EntityStatus.Active),
            ("KE-TD-01", "Tầng 2 - Khu B3: Trắc địa công trình, Địa chất, Viễn thám UAV & Bản đồ GIS", EntityStatus.Active),
            ("KE-KT-01", "Tầng 3 - Khu C1: Kinh tế Vận tải, Quản trị Logistics & Chuỗi cung ứng quốc tế", EntityStatus.Active),
            ("KE-CB-01", "Tầng 3 - Khu C2: Toán ứng dụng, Khoa học cơ bản, Văn học & Kỹ năng học thuật", EntityStatus.Active),
            ("KE-VH-01", "Tầng 3 - Khu C3: Phòng đọc Luận văn, Đồ án tốt nghiệp & Tạp chí khoa học", EntityStatus.Active),
            ("KE-CU-99", "Tầng hầm B1: Kệ kho lưu trữ tạm đã ngưng sử dụng (Dữ liệu Thùng rác demo)", EntityStatus.Deleted)
        };

        var result = new Dictionary<string, Shelf>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in specs)
        {
            var item = await context.Shelves.FirstOrDefaultAsync(x => x.Code == s.Code);
            if (item == null)
            {
                item = new Shelf
                {
                    Id = Guid.NewGuid(),
                    Code = s.Code,
                    Description = s.Description,
                    Status = s.Status,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                };
                await context.Shelves.AddAsync(item);
            }
            else
            {
                item.Description = s.Description;
                item.Status = s.Status;
            }
            result[s.Code] = item;
        }
        await context.SaveChangesAsync();
        return result;
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(ApplicationDbContext context, Guid adminStaffId)
    {
        var specs = new (string Key, string Name, string Description, string Status)[]
        {
            ("IT", "Công nghệ thông tin & Lập trình", "Giáo trình kỹ thuật phần mềm, lập trình .NET, cấu trúc dữ liệu, giải thuật và mạng máy tính", EntityStatus.Active),
            ("DB", "Cơ sở dữ liệu & Hệ thống thông tin", "Thiết kế cơ sở dữ liệu quan hệ, SQL Server, Oracle, phân tích thiết kế hệ thống thông tin", EntityStatus.Active),
            ("AI", "Trí tuệ nhân tạo & Công nghệ số", "Học máy (Machine Learning), Học sâu (Deep Learning), Thị giác máy tính và IoT giao thông", EntityStatus.Active),
            ("GT", "Kỹ thuật Giao thông & Cầu đường", "Thiết kế cầu dây văng, đường ô tô cao tốc, kết cấu bê tông cốt thép và quy hoạch giao thông UTC", EntityStatus.Active),
            ("CK", "Cơ khí Giao thông & Kỹ thuật Ô tô", "Động cơ đốt trong, ô tô điện - hybrid, đầu máy toa xe đường sắt đô thị và tự động hóa cơ khí", EntityStatus.Active),
            ("TD", "Trắc địa, Địa chất & GIS Công trình", "Trắc địa đại cương, định vị vệ tinh GNSS, quét Laser 3D, bản đồ học và hệ thống thông tin địa lý GIS", EntityStatus.Active),
            ("KT", "Kinh tế & Quản trị logistics", "Kinh tế vận tải đa phương thức, quản trị chuỗi cung ứng số, khai thác cảng biển và kho bãi", EntityStatus.Active),
            ("CB", "Toán học & Khoa học cơ bản", "Giải tích, đại số tuyến tính, xác suất thống kê kỹ thuật, phương pháp tính và vật lý đại cương", EntityStatus.Active),
            ("VH", "Văn học & Kỹ năng mềm", "Phương pháp nghiên cứu khoa học, kỹ năng học tập đại học, ngoại ngữ chuyên ngành và văn học", EntityStatus.Active),
            ("DEL", "Danh mục lưu trữ nội bộ cũ (Đã xóa mềm)", "Thể loại mẫu nằm trong Thùng rác để demo Khôi phục hoặc Xóa vĩnh viễn", EntityStatus.Deleted)
        };

        var result = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in specs)
        {
            var item = await context.Categories.FirstOrDefaultAsync(x => x.Name == c.Name);
            if (item == null)
            {
                item = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = c.Name,
                    Description = c.Description,
                    Status = c.Status,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                };
                await context.Categories.AddAsync(item);
            }
            else
            {
                item.Description = c.Description;
                item.Status = c.Status;
            }
            result[c.Key] = item;
        }
        await context.SaveChangesAsync();
        return result;
    }

    private static async Task<Dictionary<string, Publisher>> EnsurePublishersAsync(ApplicationDbContext context, Guid adminStaffId)
    {
        var specs = new (string Key, string Name, string Address, string Phone, string Email, string Website, string Status)[]
        {
            ("GTVT", "NXB Giao Thông Vận Tải", "80B Trần Hưng Đạo, Hoàn Kiếm, Hà Nội", "02439423588", "nxbgtvt@fpt.vn", "https://nxbgtvt.vn", EntityStatus.Active),
            ("GD", "NXB Giáo Dục Việt Nam", "81 Trần Hưng Đạo, Hoàn Kiếm, Hà Nội", "02438220801", "contact@nxbgd.vn", "https://nxbgd.vn", EntityStatus.Active),
            ("TTTT", "NXB Thông Tin & Truyền Thông", "115 Trần Duy Hưng, Cầu Giấy, Hà Nội", "02435772138", "nxb.tttt@mic.gov.vn", "https://nxbthongtintruyenthong.vn", EntityStatus.Active),
            ("KHKT", "NXB Khoa Học & Kỹ Thuật", "70 Trần Hưng Đạo, Hoàn Kiếm, Hà Nội", "02439422443", "nxbkhkt@hn.vnn.vn", "https://nxbkhkt.com.vn", EntityStatus.Active),
            ("DHQG", "NXB Đại Học Quốc Gia Hà Nội", "16 Hàng Chuối, Hai Bà Trưng, Hà Nội", "02439714896", "nxbdhqghn@vnu.edu.vn", "https://press.vnu.edu.vn", EntityStatus.Active),
            ("XD", "NXB Xây Dựng", "37 Lê Đại Hành, Hai Bà Trưng, Hà Nội", "02439741416", "info@nxbxaydung.com.vn", "https://nxbxaydung.com.vn", EntityStatus.Active),
            ("TRE", "NXB Trẻ", "161B Lý Chính Thắng, Quận 3, TP.HCM", "02839316289", "hopthu@nxbtre.com.vn", "https://nxbtre.com.vn", EntityStatus.Active),
            ("OREILLY", "O'Reilly Media", "1005 Gravenstein Hwy North, Sebastopol, CA", "+17078277000", "info@oreilly.com", "https://www.oreilly.com", EntityStatus.Active),
            ("DEL", "NXB Liên Hiệp Kỹ Thuật Cũ (Thùng rác)", "Số 10 Nguyễn Công Trứ, Hà Nội", "02438112233", "archive@oldpub.vn", "https://oldpub.vn", EntityStatus.Deleted)
        };

        var result = new Dictionary<string, Publisher>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in specs)
        {
            var item = await context.Publishers.FirstOrDefaultAsync(x => x.Name == p.Name);
            if (item == null)
            {
                item = new Publisher
                {
                    Id = Guid.NewGuid(),
                    Name = p.Name,
                    Address = p.Address,
                    Phone = p.Phone,
                    Email = p.Email,
                    Website = p.Website,
                    Status = p.Status,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                };
                await context.Publishers.AddAsync(item);
            }
            else
            {
                item.Address = p.Address;
                item.Phone = p.Phone;
                item.Email = p.Email;
                item.Website = p.Website;
                item.Status = p.Status;
            }
            result[p.Key] = item;
        }
        await context.SaveChangesAsync();
        return result;
    }

    private static async Task<Dictionary<string, Author>> EnsureAuthorsAsync(ApplicationDbContext context, Guid adminStaffId)
    {
        var specs = new (string Key, string FullName, string Biography, string Status)[]
        {
            ("BOB", "Robert C. Martin (Uncle Bob)", "Chuyên gia kỹ nghệ phần mềm quốc tế, tác giả bộ sách kinh điển Clean Code và Clean Architecture.", EntityStatus.Active),
            ("FOWLER", "Martin Fowler", "Kiến trúc sư trưởng tại ThoughtWorks, tác giả Refactoring và Patterns of Enterprise Application Architecture.", EntityStatus.Active),
            ("TANENBAUM", "Andrew S. Tanenbaum", "Giáo sư Khoa học máy tính tại Vrije Universiteit, tác giả bộ sách kinh điển Computer Networks.", EntityStatus.Active),
            ("CORMEN", "GS. Thomas H. Cormen", "Giáo sư Khoa học máy tính tại Dartmouth College, đồng tác giả cuốn Introduction to Algorithms (CLRS).", EntityStatus.Active),
            ("FREEMAN", "Adam Freeman", "Chuyên gia công nghệ .NET và Web, tác giả loạt sách Pro ASP.NET Core MVC và Entity Framework Core.", EntityStatus.Active),
            ("THUY", "GS.TS. Nguyễn Thanh Thủy", "Giảng viên cao cấp, chuyên gia đầu ngành về Cơ sở dữ liệu, Trí tuệ nhân tạo và Học sâu tại Việt Nam.", EntityStatus.Active),
            ("QUE", "PGS.TS. Trần Đình Quế", "Chuyên gia đầu ngành về Quy hoạch giao thông đô thị, Hệ thống thông tin và Trí tuệ nhân tạo.", EntityStatus.Active),
            ("HUNG", "PGS.TS. Nguyễn Văn Hùng", "Giảng viên cao cấp Khoa Công trình - Trường ĐH Giao thông Vận tải, chủ biên nhiều giáo trình Cầu đường.", EntityStatus.Active),
            ("LANG", "GS.TS. Phạm Văn Lang", "Chuyên gia Cơ khí Động lực, Kỹ thuật Ô tô hiện đại và Mô phỏng tối ưu hóa kỹ thuật.", EntityStatus.Active),
            ("SUA", "GS.TS. Từ Sỹ Sùa", "Giảng viên cao cấp Khoa Vận tải Kinh tế - ĐH Giao thông Vận tải, chuyên gia đầu ngành về Logistics và Vận tải đa phương thức.", EntityStatus.Active),
            ("TICH", "PGS.TS. Vũ Trọng Tích", "Giảng viên Khoa Khoa học Cơ bản, tác giả nhiều giáo trình Giải tích, Đại số tuyến tính và Xác suất thống kê.", EntityStatus.Active),
            ("TUAN", "PGS.TS. Đinh Công Tuấn", "Chuyên gia Bộ môn Trắc địa - Địa chất công trình, nghiên cứu sâu về GNSS, UAV và bản đồ số GIS.", EntityStatus.Active),
            ("HOANG", "TS. Lê Minh Hoàng", "Chuyên gia thuật toán, cấu trúc dữ liệu và an toàn bảo mật ứng dụng Web hiện đại.", EntityStatus.Active),
            ("ANH", "Nguyễn Nhật Ánh", "Nhà văn Việt Nam tiêu biểu với các tác phẩm văn học trong sáng được nhiều thế hệ sinh viên yêu mến.", EntityStatus.Active),
            ("DEL", "Tác giả Bản thảo Lưu trữ (Thùng rác)", "Bản ghi tác giả đã xóa mềm dùng để minh họa chức năng Khôi phục hoặc Xóa cứng 3 bước.", EntityStatus.Deleted)
        };

        var result = new Dictionary<string, Author>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in specs)
        {
            var item = await context.Authors.FirstOrDefaultAsync(x => x.FullName == a.FullName);
            if (item == null)
            {
                item = new Author
                {
                    Id = Guid.NewGuid(),
                    FullName = a.FullName,
                    Biography = a.Biography,
                    Status = a.Status,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                };
                await context.Authors.AddAsync(item);
            }
            else
            {
                item.Biography = a.Biography;
                item.Status = a.Status;
            }
            result[a.Key] = item;
        }
        await context.SaveChangesAsync();
        return result;
    }

    private static async Task EnsureExamReadersAsync(
        ApplicationDbContext context,
        Guid studentTypeId,
        Guid lecturerTypeId,
        Guid guestTypeId,
        string defaultPasswordHash,
        Guid adminStaffId,
        DateOnly today)
    {
        var specs = new (
            string Username,
            string Email,
            string FullName,
            string Phone,
            string CardNo,
            string MemberCode,
            Guid TypeId,
            DateOnly Dob,
            string Address,
            DateOnly Issued,
            DateOnly Expiry,
            string CardStatus,
            DateTimeOffset? LockedAt,
            string? LockReason,
            string Status)[]
        {
            // 1. SV20260001: Bạn đọc chuẩn mực, 0đ nợ phạt, không quá hạn, có 1 sách giữ chỗ READY -> Demo Mượn mới & Gia hạn & Nhận sách giữ chỗ
            ("student01", "nguyenhoanglong@student.utc.edu.vn", "Nguyễn Hoàng Long", "0987654321", "SV20260001", "2026510001",
                studentTypeId, new DateOnly(2004, 5, 15), "Ký túc xá ĐH Giao Thông Vận Tải, Cầu Giấy, Hà Nội",
                today.AddMonths(-6), today.AddYears(4), LibraryConstants.CardStatus.Active, null, null, EntityStatus.Active),

            // 2. SV20260002: Bạn đọc đang giữ 1 cuốn sách QUÁ HẠN 5 NGÀY chưa trả -> Demo Chặn mượn do quá hạn & Nhận trả tự động sinh phiếu phạt
            ("student02", "lethuha@student.utc.edu.vn", "Lê Thu Hà", "0978123456", "SV20260002", "2026510002",
                studentTypeId, new DateOnly(2004, 10, 20), "Số 12 Chùa Láng, Đống Đa, Hà Nội",
                today.AddMonths(-5), today.AddYears(4), LibraryConstants.CardStatus.Active, null, null, EntityStatus.Active),

            // 3. SV20260003: Bạn đọc đã trả sách nhưng đang nợ 2 phiếu phạt UNPAID (72.000đ) -> Demo Chặn mượn do nợ phạt & Thu phạt / Miễn phạt tại quầy
            ("student03", "tranminhkhoi@student.utc.edu.vn", "Trần Minh Khôi", "0965432189", "SV20260003", "2026510003",
                studentTypeId, new DateOnly(2004, 2, 18), "Số 45 Nguyễn Chí Thanh, Đống Đa, Hà Nội",
                today.AddMonths(-8), today.AddYears(3), LibraryConstants.CardStatus.Active, null, null, EntityStatus.Active),

            // 4. SV20260004: Bạn đọc đang mượn chạm trần 5/5 cuốn tối đa của Sinh viên -> Demo Chặn mượn do hết hạn mức số sách
            ("student04", "phamthaotien@student.utc.edu.vn", "Phạm Thảo Tiên", "0945112233", "SV20260004", "2026510004",
                studentTypeId, new DateOnly(2005, 7, 12), "Số 88 Cầu Giấy, Quan Hoa, Hà Nội",
                today.AddMonths(-4), today.AddYears(4), LibraryConstants.CardStatus.Active, null, null, EntityStatus.Active),

            // 5. GV20260001: Giảng viên (Hạn mức 10 cuốn, 30 ngày), không nợ phạt -> Demo quyền lợi Giảng viên & Mượn nhiều sách
            ("lecturer01", "baopq@utc.edu.vn", "TS. Phạm Quốc Bảo", "0912987654", "GV20260001", "GV001",
                lecturerTypeId, new DateOnly(1982, 3, 10), "Khoa Công Nghệ Thông Tin, ĐH Giao Thông Vận Tải",
                today.AddYears(-1), today.AddYears(4), LibraryConstants.CardStatus.Active, null, null, EntityStatus.Active),

            // 6. KH20260001: Khách ngoài (Hạn mức 2 cuốn, 7 ngày) -> Demo phân loại độc giả Khách ngoài
            ("guest01", "dovannam@gmail.com", "Đỗ Văn Nam (Khách ngoài)", "0932114455", "KH20260001", "KH001",
                guestTypeId, new DateOnly(1998, 11, 5), "Số 19 Kim Mã, Ba Đình, Hà Nội",
                today.AddMonths(-2), today.AddMonths(10), LibraryConstants.CardStatus.Active, null, null, EntityStatus.Active),

            // 7. SV20260098: Thẻ đã HẾT HẠN (CardExpiryDate < Today) -> Demo Chặn mượn thẻ hết hạn & Gia hạn thẻ tại /Admin/Readers
            ("student_expired", "hoanggiahuy@student.utc.edu.vn", "Hoàng Gia Huy (Thẻ hết hạn demo)", "0923456781", "SV20260098", "2026510098",
                studentTypeId, new DateOnly(2002, 4, 22), "Ngõ 68 Xuân Thủy, Cầu Giấy, Hà Nội",
                today.AddYears(-4), today.AddDays(-15), LibraryConstants.CardStatus.Active, null, null, EntityStatus.Active),

            // 8. SV20260099: Thẻ đang BỊ KHÓA (LOCKED) -> Demo Chặn mượn thẻ bị khóa & Mở khóa thẻ tại /Admin/Readers
            ("student_locked", "vuminhduc@student.utc.edu.vn", "Vũ Minh Đức (Thẻ bị khóa demo)", "0934567890", "SV20260099", "2026510099",
                studentTypeId, new DateOnly(2003, 8, 25), "Ngõ 1194 Đường Láng, Đống Đa, Hà Nội",
                today.AddMonths(-6), today.AddYears(3), LibraryConstants.CardStatus.Locked, DateTimeOffset.UtcNow.AddDays(-5),
                "Làm mất tài liệu chuyên ngành chưa hoàn tất thủ tục đền bù", EntityStatus.Active),

            // 9. SV20260100: Độc giả đã xóa mềm trong Thùng rác (DELETED) -> Demo Khôi phục / Xóa vĩnh viễn tại /Admin/Readers?status=DELETED
            ("student_deleted", "ngothanhtung@student.utc.edu.vn", "Ngô Thanh Tùng (Đã xóa mềm - Thùng rác)", "0919887766", "SV20260100", "2026510100",
                studentTypeId, new DateOnly(2004, 1, 30), "Phố Vọng, Hai Bà Trưng, Hà Nội",
                today.AddMonths(-3), today.AddYears(4), LibraryConstants.CardStatus.Active, null, null, EntityStatus.Deleted)
        };

        foreach (var r in specs)
        {
            var existing = await context.Readers.FirstOrDefaultAsync(
                x => x.Username == r.Username || x.CardNo == r.CardNo || x.Email == r.Email);

            if (existing == null)
            {
                await context.Readers.AddAsync(new Reader
                {
                    Id = Guid.NewGuid(),
                    ReaderTypeId = r.TypeId,
                    Username = r.Username,
                    Email = r.Email,
                    PasswordHash = defaultPasswordHash,
                    FullName = r.FullName,
                    Phone = r.Phone,
                    CardNo = r.CardNo,
                    MemberCode = r.MemberCode,
                    DateOfBirth = r.Dob,
                    Address = r.Address,
                    CardIssuedDate = r.Issued,
                    CardExpiryDate = r.Expiry,
                    CardStatus = r.CardStatus,
                    LockedAt = r.LockedAt,
                    LockReason = r.LockReason,
                    Status = r.Status,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                });
            }
            else
            {
                existing.ReaderTypeId = r.TypeId;
                existing.FullName = r.FullName;
                existing.Phone = r.Phone;
                existing.CardNo = r.CardNo;
                existing.MemberCode = r.MemberCode;
                existing.Address = r.Address;
                existing.CardIssuedDate = r.Issued;
                existing.CardExpiryDate = r.Expiry;
                existing.CardStatus = r.CardStatus;
                existing.LockedAt = r.LockedAt;
                existing.LockReason = r.LockReason;
                existing.Status = r.Status;
                if (string.IsNullOrWhiteSpace(existing.SecurityStamp))
                {
                    existing.SecurityStamp = Guid.NewGuid().ToString("N");
                }
            }
        }
        await context.SaveChangesAsync();
    }

    private record BookSeedSpec(
        int Number,
        string Isbn,
        string Title,
        string CatKey,
        string PubKey,
        string[] AuthorKeys,
        short Year,
        string Edition,
        string Language,
        int Pages,
        string Description,
        string ShelfCode,
        decimal Price,
        (string Barcode, string InitialStatus, string? Note, string EntityStatus)[] Copies,
        string BookStatus = EntityStatus.Active);

    private static async Task EnsureMasterBooksAndCopiesAsync(
        ApplicationDbContext context,
        Dictionary<string, Category> cats,
        Dictionary<string, Publisher> pubs,
        Dictionary<string, Author> authors,
        Dictionary<string, Shelf> shelves,
        Guid adminStaffId,
        DateOnly today)
    {
        if (await context.Books.CountAsync() >= 26)
        {
            return;
        }

        // Sắp xếp CreatedAt xen kẽ giữa Giáo trình - Luận văn - Tạp chí để Trang chủ hiển thị cân đối ở mọi Tab
        var baseTime = DateTimeOffset.UtcNow.AddHours(-2);

        var specs = new List<BookSeedSpec>
        {
            // ==================== NHÓM 1: GIÁO TRÌNH & SÁCH CHUYÊN KHẢO (14 CUỐN ACTIVE) ====================
            new(1, "978-0132350884",
                "Clean Code: A Handbook of Agile Software Craftsmanship",
                "IT", "OREILLY", ["BOB"], 2020, "Tái bản lần thứ 2", "Tiếng Anh", 464,
                "Cuốn sách gối đầu giường của mọi kỹ sư phần mềm, hướng dẫn nguyên tắc đặt tên, thiết kế hàm, xử lý ngoại lệ, viết Unit Test và giữ mã nguồn sạch, dễ bảo trì.",
                "KE-IT-01", 350000m,
                [
                    ("BC-202610-0001", LibraryConstants.CopyStatus.Available, "Bản in màu bìa cứng", EntityStatus.Active),
                    ("BC-202610-0002", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0003", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(2, "978-0134494166",
                "Clean Architecture: A Craftsman's Guide to Software Structure",
                "IT", "OREILLY", ["BOB"], 2021, "Tái bản lần 1", "Tiếng Anh", 432,
                "Nguyên lý kiến trúc phần mềm SOLID, phân tách tầng Domain/Application/Infrastructure độc lập với Database và UI, tối ưu khả năng mở rộng hệ thống lớn.",
                "KE-IT-01", 380000m,
                [
                    ("BC-202610-0004", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0005", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0006", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(3, "978-0201485677",
                "Refactoring: Improving the Design of Existing Code",
                "IT", "OREILLY", ["FOWLER"], 2019, "Tái bản lần 2", "Tiếng Anh", 448,
                "Danh mục hơn 60 kỹ thuật tái cấu trúc mã nguồn (Refactoring) giúp loại bỏ Code Smells và nâng cao chất lượng thiết kế mà không làm thay đổi hành vi hệ thống.",
                "KE-IT-01", 420000m,
                [
                    ("BC-202610-0007", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0008", LibraryConstants.CopyStatus.Reserved, "Đang giữ chỗ đặt trước tại quầy cho Nguyễn Hoàng Long", EntityStatus.Active),
                    ("BC-202610-0028", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(4, "978-6048031234",
                "Giáo trình Thiết kế Hệ thống Cơ sở dữ liệu và SQL Server",
                "DB", "TTTT", ["THUY", "HOANG"], 2024, "Tái bản có chỉnh sửa", "Tiếng Việt", 380,
                "Giáo trình chuẩn đào tạo ngành CNTT về đại số quan hệ, chuẩn hóa 3NF/BCNF, thiết kế chỉ mục B-Tree, lập trình Trigger, Stored Procedure và kiểm soát giao dịch đồng thời trên SQL Server.",
                "KE-IT-02", 145000m,
                [
                    ("BC-202610-0009", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0010", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0011", LibraryConstants.CopyStatus.Damaged, "Rách gáy sách và bung trang 45, đang chờ tu sửa", EntityStatus.Active)
                ]),

            new(5, "978-0132126953",
                "Giáo trình Mạng máy tính căn bản & An toàn hệ thống",
                "IT", "TTTT", ["TANENBAUM", "HOANG"], 2023, "Tái bản lần 5", "Tiếng Việt", 520,
                "Kiến thức toàn diện về kiến trúc phân tầng OSI, bộ giao thức TCP/IP, định tuyến OSPF/BGP, bảo mật tầng Transport (TLS/SSL) và phòng chống tấn công mạng doanh nghiệp.",
                "KE-IT-02", 165000m,
                [
                    ("BC-202610-0012", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0013", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0029", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(6, "978-6047612345",
                "Quy hoạch và Tổ chức Giao thông Đô thị thông minh (ITS)",
                "GT", "GTVT", ["QUE", "HUNG"], 2024, "Xuất bản lần 1", "Tiếng Việt", 340,
                "Giáo trình cốt lõi của Trường ĐH Giao thông Vận tải về mô hình hóa nhu cầu đi lại 4 bước, thiết kế nút giao đèn tín hiệu thích ứng và hệ thống giao thông thông minh ITS.",
                "KE-GT-01", 135000m,
                [
                    ("BC-202610-0014", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0015", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0030", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(7, "978-6042081234",
                "Tôi Thấy Hoa Vàng Trên Cỏ Xanh",
                "VH", "TRE", ["ANH"], 2022, "Tái bản lần thứ 45", "Tiếng Việt", 378,
                "Tác phẩm văn học Việt Nam đương đại giàu cảm xúc, bồi dưỡng tâm hồn và góc nhìn nhân văn cho sinh viên sau những giờ học kỹ thuật căng thẳng.",
                "KE-CB-01", 95000m,
                [
                    ("BC-202610-0016", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0017", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            // ĐẦU SÁCH HOT #1 (0 bản Available, 2 bản đang được mượn) -> Sẵn sàng để bấm ĐẶT TRƯỚC ngay khi đi thi!
            new(8, "978-1484289105",
                "Lập trình Web doanh nghiệp với ASP.NET Core MVC 10 & Entity Framework Core",
                "IT", "OREILLY", ["FREEMAN", "HOANG"], 2025, "Ấn bản .NET 10 LTS", "Tiếng Việt", 612,
                "Tài liệu chuyên sâu về kiến trúc ASP.NET Core MVC Server-Side Rendering, Razor Views, Cookie Authentication, chống tấn công XSS/CSRF/SQLi và tối ưu truy vấn EF Core.",
                "KE-IT-01", 285000m,
                [
                    ("BC-202610-0024", LibraryConstants.CopyStatus.Available, "Sẽ chuyển sang BORROWED qua phiếu mượn", EntityStatus.Active),
                    ("BC-202610-0025", LibraryConstants.CopyStatus.Available, "Sẽ chuyển sang BORROWED qua phiếu mượn", EntityStatus.Active)
                ]),

            new(9, "978-0262046305",
                "Cấu trúc dữ liệu và Giải thuật nâng cao (Introduction to Algorithms)",
                "IT", "DHQG", ["CORMEN", "HOANG"], 2024, "Ấn bản lần thứ 4", "Tiếng Việt", 680,
                "Phân tích độ phức tạp thuật toán, cây đỏ-đen, bảng băm, quy hoạch động, thuật toán đồ thị Dijkstra, A* ứng dụng trong tìm đường ngắn nhất trên mạng lưới giao thông.",
                "KE-IT-01", 260000m,
                [
                    ("BC-202610-0026", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0027", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0031", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(10, "978-6047631029",
                "Giáo trình Kết cấu Bê tông cốt thép và Thiết kế Cầu đường bộ",
                "GT", "XD", ["HUNG"], 2024, "Tái bản lần 3", "Tiếng Việt", 425,
                "Nguyên lý tính toán cấu kiện chịu uốn, chịu nén lệch tâm, kiểm toán trạng thái giới hạn cường độ và sử dụng cho kết cấu nhịp cầu bê tông cốt thép dự ứng lực.",
                "KE-GT-01", 175000m,
                [
                    ("BC-202610-0032", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0033", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0034", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(11, "978-6047631043",
                "Giáo trình Quản trị Logistics và Vận tải Đa phương thức Quốc tế",
                "KT", "GTVT", ["SUA"], 2024, "Xuất bản lần 2", "Tiếng Việt", 392,
                "Tổ chức vận tải kết hợp đường biển - đường sắt - đường bộ, nghiệp vụ giao nhận hàng hóa xuất nhập khẩu (Incoterms), quản trị kho bãi智能 và tối ưu chi phí logistics.",
                "KE-KT-01", 155000m,
                [
                    ("BC-202610-0035", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0036", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0037", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(12, "978-6040351050",
                "Giáo trình Giải tích, Đại số tuyến tính và Xác suất thống kê kỹ thuật",
                "CB", "GD", ["TICH"], 2023, "Tái bản lần 6", "Tiếng Việt", 410,
                "Cung cấp nền tảng toán học cao cấp về ma trận, trị riêng, phương trình vi phân, biến ngẫu nhiên và kiểm định giả thuyết thống kê cho sinh viên khối ngành kỹ thuật.",
                "KE-CB-01", 125000m,
                [
                    ("BC-202610-0038", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0039", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0040", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(13, "978-6040352019",
                "Giáo trình Trắc địa đại cương và Định vị vệ tinh GNSS trong xây dựng",
                "TD", "KHKT", ["TUAN"], 2024, "Xuất bản lần 1", "Tiếng Việt", 348,
                "Phương pháp đo góc, đo dài, đo cao hình học, bình sai lưới khống chế trắc địa và ứng dụng công nghệ GNSS/RTK trong bố trí công trình cầu hầm chính xác cao.",
                "KE-TD-01", 140000m,
                [
                    ("BC-202610-0041", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0042", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0043", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(14, "978-6047631036",
                "Giáo trình Kỹ thuật Ô tô điện, Hybrid và Hệ thống điều khiển tự động",
                "CK", "KHKT", ["LANG"], 2025, "Xuất bản lần đầu", "Tiếng Việt", 396,
                "Cấu tạo hệ thống truyền lực xe điện (BEV/PHEV), quản lý nhiệt pin Lithium-Ion (BMS), phanh tái sinh năng lượng và mạng truyền thông CAN/LIN trên ô tô hiện đại.",
                "KE-CK-01", 180000m,
                [
                    ("BC-202610-0044", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0045", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            // ==================== NHÓM 2: LUẬN VĂN & ĐỒ ÁN TỐT NGHIỆP (6 CUỐN ACTIVE) ====================
            new(15, "978-6047625011",
                "Luận văn Thạc sĩ: Ứng dụng học sâu dự báo lưu lượng giao thông đô thị Hà Nội",
                "AI", "GTVT", ["THUY", "QUE"], 2025, "Luận văn Thạc sĩ Kỹ thuật", "Tiếng Việt", 128,
                "Luận văn thạc sĩ nghiên cứu mô hình mạng nơ-ron đồ thị không-thời gian (ST-GNN) kết hợp YOLOv10 phân tích camera giao thông và dự báo ùn tắc tại các nút giao trọng điểm.",
                "KE-AI-01", 85000m,
                [
                    ("BC-202610-0018", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0019", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(16, "978-6047625028",
                "Đồ án Tốt nghiệp: Thiết kế kết cấu nhịp cầu dây văng vượt sông lớn theo tiêu chuẩn AASHTO LRFD",
                "GT", "GTVT", ["HUNG", "QUE"], 2025, "Đồ án Tốt nghiệp Kỹ sư", "Tiếng Việt", 165,
                "Đồ án tốt nghiệp xuất sắc ngành Kỹ thuật Xây dựng Cầu - Đường bộ, mô phỏng nội lực dầm chủ, trụ tháp chữ A và căng kéo cáp văng bằng phần mềm MIDAS Civil.",
                "KE-GT-01", 90000m,
                [
                    ("BC-202610-0020", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0046", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            // ĐẦU SÁCH HOT #2 (0 bản Available, 2 bản đang được mượn) -> Sẵn sàng để ĐẶT TRƯỚC trên tab Luận văn & Đồ án!
            new(17, "978-6047625066",
                "Khóa luận Tốt nghiệp: Xây dựng hệ thống quản lý thư viện số thông minh tích hợp chống gian lận đa lớp",
                "DB", "DHQG", ["THUY", "HOANG"], 2026, "Khóa luận Tốt nghiệp Xuất sắc", "Tiếng Việt", 148,
                "Khóa luận tốt nghiệp xây dựng hệ thống quản lý thư viện trên nền tảng ASP.NET Core MVC 10, SQL Server, áp dụng khóa sp_getapplock chống Race Condition và 38 lớp bảo vệ an toàn thông tin.",
                "KE-VH-01", 95000m,
                [
                    ("BC-202610-0047", LibraryConstants.CopyStatus.Available, "Sẽ chuyển sang BORROWED qua phiếu mượn", EntityStatus.Active),
                    ("BC-202610-0048", LibraryConstants.CopyStatus.Available, "Sẽ chuyển sang BORROWED qua phiếu mượn", EntityStatus.Active)
                ]),

            new(18, "978-6047625059",
                "Luận án Tiến sĩ: Tối ưu hóa mạng lưới trung tâm Logistics vùng Thủ đô bằng thuật toán Meta-heuristics",
                "KT", "GTVT", ["SUA"], 2025, "Luận án Tiến sĩ Kinh tế", "Tiếng Việt", 210,
                "Luận án tiến sĩ đề xuất mô hình quy hoạch thực nghiệm vị trí cảng cạn ICD và trung tâm phân phối hàng hóa xanh tại vùng kinh tế trọng điểm Bắc Bộ.",
                "KE-VH-01", 110000m,
                [
                    ("BC-202610-0049", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0050", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(19, "978-6047625097",
                "Đồ án Tốt nghiệp: Ứng dụng công nghệ quét Laser 3D và GIS trong quan trắc biến dạng hầm đường bộ",
                "TD", "GTVT", ["TUAN"], 2025, "Đồ án Tốt nghiệp Kỹ sư", "Tiếng Việt", 154,
                "Đồ án tốt nghiệp ứng dụng đám mây điểm (Point Cloud) từ thiết bị Terrestrial Laser Scanner kết hợp WebGIS để theo dõi chuyển vị vỏ hầm xuyên núi.",
                "KE-TD-01", 95000m,
                [
                    ("BC-202610-0051", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0052", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(20, "978-6047625073",
                "Luận văn Thạc sĩ: Nghiên cứu hệ thống điều khiển tự hành và phanh khẩn cấp AEB trên ô tô điện đô thị",
                "CK", "KHKT", ["LANG"], 2025, "Luận văn Thạc sĩ Cơ khí", "Tiếng Việt", 136,
                "Luận văn thạc sĩ mô phỏng thuật toán hợp nhất cảm biến Radar - Camera (Sensor Fusion) trên MATLAB/Simulink và CarSim cho hệ thống phanh tự động khẩn cấp.",
                "KE-CK-01", 90000m,
                [
                    ("BC-202610-0053", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0054", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            // ==================== NHÓM 3: TẠP CHÍ KHOA HỌC & KỶ YẾU (6 CUỐN ACTIVE) ====================
            new(21, "978-6047625035",
                "Tạp chí Khoa học Giao thông Vận tải - Chuyên san Công nghệ số & Hạ tầng thông minh",
                "GT", "GTVT", ["QUE", "HUNG"], 2025, "Tập 76, Số 02 (ISSN 1859-2724)", "Tiếng Việt", 142,
                "Tạp chí khoa học định kỳ của Trường Đại học Giao thông Vận tải (ISSN 1859-2724) công bố các công trình nghiên cứu mới về BIM, vật liệu mặt đường bền vững và điều khiển giao thông thông minh.",
                "KE-VH-01", 75000m,
                [
                    ("BC-202610-0021", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0022", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(22, "978-6047625042",
                "Kỷ yếu Hội nghị Khoa học Công nghệ Giao thông Vận tải & Logistics xanh lần thứ VII",
                "KT", "TTTT", ["SUA", "THUY"], 2024, "Kỷ yếu Hội nghị Khoa học", "Tiếng Việt", 296,
                "Tập hợp 42 bài báo khoa học phản biện độc lập về chuyển đổi xanh trong vận tải đa phương thức, cảng thông minh và tối ưu chuỗi cung ứng số tại Việt Nam.",
                "KE-KT-01", 115000m,
                [
                    ("BC-202610-0023", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0055", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(23, "978-6048039018",
                "Tạp chí Công nghệ Thông tin và Truyền thông - Chuyên san An toàn không gian mạng & AI",
                "AI", "TTTT", ["THUY", "HOANG"], 2025, "Số Đặc biệt Quý IV/2025", "Tiếng Việt", 168,
                "Chuyên san khoa học công bố các nghiên cứu về phát hiện xâm nhập mạng bằng học sâu, mật mã hậu lượng tử và bảo mật ứng dụng Web trước các tấn công OWASP Top 10.",
                "KE-AI-01", 80000m,
                [
                    ("BC-202610-0056", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0057", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(24, "978-6047625080",
                "Journal of Transportation Science and Engineering - Special Issue on High-Speed Rail",
                "CK", "GTVT", ["LANG", "HUNG"], 2026, "Vol. 18, Issue 01 (ISSN 2615-9910)", "Tiếng Anh", 156,
                "Ấn phẩm tiếng Anh quốc tế của Trường ĐH Giao thông Vận tải chuyên sâu về công nghệ đường sắt tốc độ cao Bắc - Nam, động lực học đoàn tàu và hệ thống tín hiệu ERTMS/ETCS.",
                "KE-CK-01", 95000m,
                [
                    ("BC-202610-0058", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0059", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active)
                ]),

            new(25, "978-6040359025",
                "Tạp chí Toán học Ứng dụng và Mô phỏng số trong Kỹ thuật Công trình",
                "CB", "GD", ["TICH"], 2025, "Tập 42, Số 03 (ISSN 1859-3100)", "Tiếng Việt", 134,
                "Các bài báo nghiên cứu về phương pháp phần tử hữu hạn (FEM), giải tích số và mô hình xác suất đánh giá độ tin cậy kết cấu công trình cầu hầm.",
                "KE-CB-01", 70000m,
                [
                    ("BC-202610-0060", LibraryConstants.CopyStatus.Available, null, EntityStatus.Active),
                    ("BC-202610-0061", LibraryConstants.CopyStatus.Lost, "Bạn đọc báo làm mất khi đi thực tập, đã bồi thường 150% giá trị", EntityStatus.Active)
                ]),

            // ĐẦU SÁCH HOT #3 (0 bản Available, 1 bản đang được mượn) -> Sẵn sàng để ĐẶT TRƯỚC trên tab Tạp chí khoa học!
            new(26, "978-6040359032",
                "Kỷ yếu Hội thảo Quốc gia về Trắc địa Bản đồ và Viễn thám thông minh (GIS-UTC 2025)",
                "TD", "KHKT", ["TUAN"], 2025, "Kỷ yếu Hội thảo Quốc gia", "Tiếng Việt", 240,
                "Tuyển tập báo cáo khoa học về công nghệ bay chụp UAV, LiDAR, bản sao số (Digital Twin) hạ tầng giao thông và hệ thống thông tin địa lý 3D.",
                "KE-TD-01", 120000m,
                [
                    ("BC-202610-0062", LibraryConstants.CopyStatus.Available, "Sẽ chuyển sang BORROWED qua phiếu mượn", EntityStatus.Active),
                    ("BC-202610-0063", LibraryConstants.CopyStatus.Disposed, "Bản in thử nghiệm đã thanh lý định kỳ", EntityStatus.Active),
                    ("BC-202610-0064", LibraryConstants.CopyStatus.Available, "Bản sao đã xóa mềm trong Thùng rác để demo Khôi phục / Xóa cứng", EntityStatus.Deleted)
                ]),

            // ==================== NHÓM 4: SÁCH ĐÃ XÓA MỀM TRONG THÙNG RÁC (2 CUỐN DELETED) ====================
            new(27, "978-6040009901",
                "Giáo trình Lập trình Turbo Pascal 7.0 & FoxPro căn bản (Bản lưu trữ 2005)",
                "IT", "GD", ["HOANG"], 2005, "Xuất bản lần 1", "Tiếng Việt", 260,
                "Đầu sách thế hệ cũ đã ngừng giảng dạy, được đưa vào Thùng rác (DELETED) để demo chức năng Khôi phục hoặc Xóa vĩnh viễn (XOA) khi bảo vệ đồ án.",
                "KE-IT-01", 45000m, [], EntityStatus.Deleted),

            new(28, "978-6040009918",
                "Tài liệu Hướng dẫn Vận hành và Bảo dưỡng Đầu máy Hơi nước Cổ điển",
                "CK", "GTVT", ["LANG"], 1998, "Bản lưu trữ lịch sử", "Tiếng Việt", 195,
                "Tài liệu lịch sử ngành đường sắt đã chuyển sang lưu trữ số, đặt ở trạng thái Đã xóa mềm (DELETED) để minh họa quy trình xóa 2 bước an toàn.",
                "KE-CK-01", 50000m, [], EntityStatus.Deleted)
        };

        // Phân bổ CreatedAt sao cho 6 cuốn mới nhất trên Trang chủ bao gồm 2 Giáo trình + 2 Luận văn + 2 Tạp chí
        var newestOrder = new[]
        {
            1, 15, 21, 8, 16, 22,
            2, 17, 23, 4, 18, 24,
            6, 19, 25, 10, 20, 26,
            3, 5, 7, 9, 11, 12, 13, 14, 27, 28
        };
        var rankMap = new Dictionary<int, int>();
        for (var idx = 0; idx < newestOrder.Length; idx++)
        {
            rankMap[newestOrder[idx]] = idx;
        }

        foreach (var s in specs)
        {
            var rank = rankMap.GetValueOrDefault(s.Number, s.Number);
            var createdAt = baseTime.AddMinutes(100 - rank * 3);
            var slug = $"book-{s.Number:00}";

            var book = new Book
            {
                Id = Guid.NewGuid(),
                Title = s.Title,
                Isbn = s.Isbn,
                CategoryId = cats[s.CatKey].Id,
                PublisherId = pubs[s.PubKey].Id,
                PublicationYear = s.Year,
                Edition = s.Edition,
                Language = s.Language,
                PageCount = s.Pages,
                Description = s.Description,
                CoverUrl = $"/uploads/books/seed/{slug}-cover.svg",
                Status = s.BookStatus,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = adminStaffId
            };
            await context.Books.AddAsync(book);

            short order = 1;
            foreach (var aKey in s.AuthorKeys)
            {
                await context.BookAuthors.AddAsync(new BookAuthor
                {
                    BookId = book.Id,
                    AuthorId = authors[aKey].Id,
                    AuthorOrder = order++,
                    Status = EntityStatus.Active,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt,
                    CreatedBy = adminStaffId
                });
            }

            foreach (var c in s.Copies)
            {
                var isDisposed = c.InitialStatus == LibraryConstants.CopyStatus.Disposed;
                await context.BookCopies.AddAsync(new BookCopy
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ShelfId = shelves[s.ShelfCode].Id,
                    Barcode = c.Barcode,
                    CopyStatus = c.InitialStatus,
                    Price = s.Price,
                    AcquiredDate = today.AddMonths(-4),
                    DisposedAt = isDisposed ? createdAt.AddDays(5) : null,
                    DisposalReason = isDisposed ? "Thanh lý bản in thử nghiệm định kỳ theo biên bản kiểm kê" : null,
                    Note = c.Note,
                    Status = c.EntityStatus,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt,
                    CreatedBy = adminStaffId
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureMasterCirculationScenariosAsync(
        ApplicationDbContext context,
        Guid adminStaffId,
        Guid librarianStaffId,
        DateOnly today)
    {
        if (await context.BorrowSlips.AnyAsync(s => s.Note != null && s.Note.Contains(ExamDatasetVersionMarker)))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var student1 = await context.Readers.FirstAsync(x => x.Username == "student01");
        var student2 = await context.Readers.FirstAsync(x => x.Username == "student02");
        var student3 = await context.Readers.FirstAsync(x => x.Username == "student03");
        var student4 = await context.Readers.FirstAsync(x => x.Username == "student04");
        var lecturer1 = await context.Readers.FirstAsync(x => x.Username == "lecturer01");
        var guest1 = await context.Readers.FirstAsync(x => x.Username == "guest01");

        var copyByCode = await context.BookCopies.ToDictionaryAsync(c => c.Barcode, StringComparer.OrdinalIgnoreCase);
        var bookByIsbn = await context.Books.Where(b => b.Isbn != null).ToDictionaryAsync(b => b.Isbn!, StringComparer.OrdinalIgnoreCase);
        var defaultShelfId = await context.Shelves.Where(s => s.Status == EntityStatus.Active).Select(s => s.Id).FirstAsync();

        // =====================================================================
        // KỊCH BẢN 1: student01 (SV20260001 - Nguyễn Hoàng Long)
        // - Đang mượn 1 cuốn trong hạn (BC-202610-0003 - Clean Code, còn 9 ngày, RenewCount = 0/2) -> Demo Gia hạn hoặc Trả đúng hạn
        // - Có 1 phiếu lịch sử đã trả đúng hạn
        // - Có 1 đặt trước READY (BC-202610-0008 - Refactoring) đang giữ tại quầy -> Demo Mượn sách giữ chỗ tại quầy!
        // - Nợ phạt = 0đ -> Đủ điều kiện mượn sách mới ngay lập tức!
        // =====================================================================
        var slipS1History = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = student1.Id,
            IssuedBy = librarianStaffId,
            BorrowedAt = now.AddDays(-32),
            Note = $"Phiếu mượn tài liệu đầu học kỳ (Đã hoàn trả đúng hạn) {ExamDatasetVersionMarker}",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-32),
            UpdatedAt = now.AddDays(-20),
            CreatedBy = librarianStaffId
        };
        var detailS1History = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipS1History.Id,
            CopyId = copyByCode["BC-202610-0001"].Id,
            DueDate = today.AddDays(-18),
            ReturnedAt = now.AddDays(-20),
            ReceivedBy = librarianStaffId,
            RenewCount = 1,
            ReturnCondition = LibraryConstants.ReturnCondition.Good,
            ReturnNote = "Sinh viên bảo quản sách tốt, hoàn trả trước hạn 2 ngày",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-32),
            UpdatedAt = now.AddDays(-20),
            CreatedBy = librarianStaffId
        };

        var slipS1Active = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = student1.Id,
            IssuedBy = adminStaffId,
            BorrowedAt = now.AddDays(-5),
            Note = "Sinh viên mượn sách phục vụ đồ án môn học Công nghệ phần mềm",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-5),
            CreatedBy = adminStaffId
        };
        var detailS1Active = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipS1Active.Id,
            CopyId = copyByCode["BC-202610-0003"].Id,
            DueDate = today.AddDays(9),
            RenewCount = 0,
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-5),
            CreatedBy = adminStaffId
        };

        var resS1Ready = new Reservation
        {
            Id = Guid.NewGuid(),
            ReaderId = student1.Id,
            BookId = bookByIsbn["978-0201485677"].Id, // Refactoring
            AssignedCopyId = copyByCode["BC-202610-0008"].Id,
            ReservationStatus = LibraryConstants.ReservationStatus.Ready,
            ReservedAt = now.AddHours(-18),
            ReadyAt = now.AddHours(-4),
            ExpiresAt = now.AddHours(44),
            ClosedAt = null,
            Status = EntityStatus.Active,
            CreatedAt = now.AddHours(-18),
            UpdatedAt = now.AddHours(-4),
            CreatedBy = adminStaffId
        };

        await context.BorrowSlips.AddRangeAsync([slipS1History, slipS1Active]);
        await context.BorrowDetails.AddRangeAsync([detailS1History, detailS1Active]);
        await context.Reservations.AddAsync(resS1Ready);
        await context.SaveChangesAsync();

        // =====================================================================
        // KỊCH BẢN 2: student02 (SV20260002 - Lê Thu Hà)
        // - Đang giữ 1 cuốn QUÁ HẠN 5 NGÀY chưa trả (BC-202610-0006 - Clean Architecture, DueDate = today - 5)
        //   (Lưu ý: KHÔNG tạo sẵn Fine cho dòng chưa trả này để khi Nhận trả tại quầy, hệ thống tự động tính & sinh phiếu phạt 10.000đ!)
        // - Và đang mượn 1 cuốn của Đầu sách Hot #1 (BC-202610-0024 - ASP.NET Core MVC 10)
        // =====================================================================
        var slipS2Overdue = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = student2.Id,
            IssuedBy = librarianStaffId,
            BorrowedAt = now.AddDays(-19),
            Note = "Mượn tài liệu nghiên cứu kiến trúc phần mềm (Đang quá hạn 5 ngày)",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-19),
            UpdatedAt = now.AddDays(-19),
            CreatedBy = librarianStaffId
        };
        var detailS2Overdue = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipS2Overdue.Id,
            CopyId = copyByCode["BC-202610-0006"].Id,
            DueDate = today.AddDays(-5), // Quá hạn 5 ngày -> dự kiến phạt 10.000 đ khi trả
            RenewCount = 0,
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-19),
            UpdatedAt = now.AddDays(-19),
            CreatedBy = librarianStaffId
        };
        var detailS2HotBook = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipS2Overdue.Id,
            CopyId = copyByCode["BC-202610-0024"].Id, // Bản 1 của sách Hot #1 (ASP.NET Core MVC 10)
            DueDate = today.AddDays(6),
            RenewCount = 0,
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-8),
            UpdatedAt = now.AddDays(-8),
            CreatedBy = librarianStaffId
        };

        await context.BorrowSlips.AddAsync(slipS2Overdue);
        await context.BorrowDetails.AddRangeAsync([detailS2Overdue, detailS2HotBook]);
        await context.SaveChangesAsync();

        // =====================================================================
        // KỊCH BẢN 3: student03 (SV20260003 - Trần Minh Khôi)
        // - Đã trả sách nhưng đang nợ 2 khoản phạt UNPAID:
        //   1) Quá hạn 6 ngày (12.000 đ)
        //   2) Làm hư hỏng sách (60.000 đ)
        //   Tổng nợ phạt = 72.000 đ (> trần 50.000 đ) -> Demo Chặn mượn vì nợ phạt & Demo Thu phạt / Miễn phạt tại /Circulation/Fines!
        // =====================================================================
        var slipS3Returned = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = student3.Id,
            IssuedBy = librarianStaffId,
            BorrowedAt = now.AddDays(-25),
            Note = "Phiếu mượn đã trả nhưng phát sinh phạt quá hạn và hư hỏng chưa thanh toán",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-25),
            UpdatedAt = now.AddDays(-3),
            CreatedBy = librarianStaffId
        };
        var detailS3Late = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipS3Returned.Id,
            CopyId = copyByCode["BC-202610-0009"].Id,
            DueDate = today.AddDays(-11),
            ReturnedAt = now.AddDays(-5),
            ReceivedBy = librarianStaffId,
            RenewCount = 0,
            ReturnCondition = LibraryConstants.ReturnCondition.Good,
            ReturnNote = "Trả trễ hạn 6 ngày, chờ bạn đọc thanh toán tiền phạt",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-25),
            UpdatedAt = now.AddDays(-5),
            CreatedBy = librarianStaffId
        };
        var detailS3Damaged = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipS3Returned.Id,
            CopyId = copyByCode["BC-202610-0010"].Id,
            DueDate = today.AddDays(-11),
            ReturnedAt = now.AddDays(-3),
            ReceivedBy = librarianStaffId,
            RenewCount = 1,
            ReturnCondition = LibraryConstants.ReturnCondition.Good, // Giữ bản sao 0010 AVAILABLE trên kệ sau khi đã tu sửa bìa
            ReturnNote = "Làm ướt và rách nhẹ bìa phụ, lập biên bản bồi thường 50% giá trị",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-25),
            UpdatedAt = now.AddDays(-3),
            CreatedBy = librarianStaffId
        };

        var fineS3OverdueUnpaid = new Fine
        {
            Id = Guid.NewGuid(),
            BorrowDetailId = detailS3Late.Id,
            FineType = LibraryConstants.FineType.Overdue,
            OverdueDays = 6,
            Amount = 12000m,
            FineStatus = LibraryConstants.FineStatus.Unpaid,
            HandledBy = null,
            PaidAt = null,
            PaymentMethod = null,
            Note = "Phạt trả sách quá hạn 6 ngày (6 ngày x 2.000 đ/ngày)",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-5),
            CreatedBy = librarianStaffId
        };
        var fineS3DamagedUnpaid = new Fine
        {
            Id = Guid.NewGuid(),
            BorrowDetailId = detailS3Damaged.Id,
            FineType = LibraryConstants.FineType.Damaged,
            OverdueDays = null,
            Amount = 60000m,
            FineStatus = LibraryConstants.FineStatus.Unpaid,
            HandledBy = null,
            PaidAt = null,
            PaymentMethod = null,
            Note = "Bồi thường 50% giá trị sách do làm ướt và rách trang mục lục",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-3),
            CreatedBy = librarianStaffId
        };

        await context.BorrowSlips.AddAsync(slipS3Returned);
        await context.BorrowDetails.AddRangeAsync([detailS3Late, detailS3Damaged]);
        await context.Fines.AddRangeAsync([fineS3OverdueUnpaid, fineS3DamagedUnpaid]);
        await context.SaveChangesAsync();

        // =====================================================================
        // KỊCH BẢN 4: student04 (SV20260004 - Phạm Thảo Tiên)
        // - Đang mượn đủ 5/5 cuốn tối đa của Sinh viên -> Demo Chặn mượn do chạm trần MaxBorrowBooks = 5!
        //   (Trong đó bao gồm các bản của Sách Hot #1, #2, #3 để các đầu sách Hot có Available = 0 phục vụ demo Đặt trước!)
        // =====================================================================
        var slipS4MaxLimit = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = student4.Id,
            IssuedBy = adminStaffId,
            BorrowedAt = now.AddDays(-4),
            Note = "Sinh viên mượn trọn bộ 5 tài liệu ôn thi và làm khóa luận tốt nghiệp (Đạt trần 5/5 cuốn)",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-4),
            UpdatedAt = now.AddDays(-4),
            CreatedBy = adminStaffId
        };
        var s4Barcodes = new[]
        {
            "BC-202610-0025", // Bản 2 của Sách Hot #1 (ASP.NET Core MVC 10) -> Sách #8 hết bản sẵn sàng!
            "BC-202610-0047", // Bản 1 của Sách Hot #2 (Khóa luận Thư viện số)
            "BC-202610-0048", // Bản 2 của Sách Hot #2 (Khóa luận Thư viện số) -> Sách #17 hết bản sẵn sàng!
            "BC-202610-0062", // Bản duy nhất của Sách Hot #3 (Kỷ yếu GIS-UTC 2025) -> Sách #26 hết bản sẵn sàng!
            "BC-202610-0032"  // Bản 1 của Giáo trình Kết cấu BTCT
        };
        var s4Details = s4Barcodes.Select((code, idx) => new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipS4MaxLimit.Id,
            CopyId = copyByCode[code].Id,
            DueDate = today.AddDays(10 - idx),
            RenewCount = (short)(idx == 0 ? 1 : 0),
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-4),
            UpdatedAt = now.AddDays(-4),
            CreatedBy = adminStaffId
        }).ToList();

        await context.BorrowSlips.AddAsync(slipS4MaxLimit);
        await context.BorrowDetails.AddRangeAsync(s4Details);
        await context.SaveChangesAsync();

        // =====================================================================
        // KỊCH BẢN 5: lecturer01 (GV20260001 - TS. Phạm Quốc Bảo) & guest01 (KH20260001)
        // =====================================================================
        var slipLecturer = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = lecturer1.Id,
            IssuedBy = adminStaffId,
            BorrowedAt = now.AddDays(-7),
            Note = "Giảng viên mượn tài liệu biên soạn đề cương bài giảng cao học",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-7),
            UpdatedAt = now.AddDays(-7),
            CreatedBy = adminStaffId
        };
        var detailLec1 = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipLecturer.Id,
            CopyId = copyByCode["BC-202610-0015"].Id,
            DueDate = today.AddDays(23),
            RenewCount = 0,
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-7),
            UpdatedAt = now.AddDays(-7),
            CreatedBy = adminStaffId
        };
        var detailLec2 = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipLecturer.Id,
            CopyId = copyByCode["BC-202610-0058"].Id,
            DueDate = today.AddDays(23),
            RenewCount = 0,
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-7),
            UpdatedAt = now.AddDays(-7),
            CreatedBy = adminStaffId
        };

        var slipGuest = new BorrowSlip
        {
            Id = Guid.NewGuid(),
            ReaderId = guest1.Id,
            IssuedBy = librarianStaffId,
            BorrowedAt = now.AddDays(-2),
            Note = "Bạn đọc khách ngoài mượn sách đọc ngắn hạn 7 ngày",
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-2),
            CreatedBy = librarianStaffId
        };
        var detailGuest = new BorrowDetail
        {
            Id = Guid.NewGuid(),
            SlipId = slipGuest.Id,
            CopyId = copyByCode["BC-202610-0017"].Id,
            DueDate = today.AddDays(5),
            RenewCount = 0,
            Status = EntityStatus.Active,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-2),
            CreatedBy = librarianStaffId
        };

        var resLecPending = new Reservation
        {
            Id = Guid.NewGuid(),
            ReaderId = lecturer1.Id,
            BookId = bookByIsbn["978-6047625066"].Id, // Đầu sách Hot #2 (Khóa luận Thư viện số - 0 bản rảnh)
            AssignedCopyId = null,
            ReservationStatus = LibraryConstants.ReservationStatus.Pending,
            ReservedAt = now.AddHours(-8),
            ReadyAt = null,
            ExpiresAt = null,
            ClosedAt = null,
            Status = EntityStatus.Active,
            CreatedAt = now.AddHours(-8),
            UpdatedAt = now.AddHours(-8),
            CreatedBy = adminStaffId
        };

        await context.BorrowSlips.AddRangeAsync([slipLecturer, slipGuest]);
        await context.BorrowDetails.AddRangeAsync([detailLec1, detailLec2, detailGuest]);
        await context.Reservations.AddAsync(resLecPending);
        await context.SaveChangesAsync();

        // =====================================================================
        // KỊCH BẢN 6: Cổng Bạn Đọc Tự Phục Vụ cho các tài khoản cá nhân từ .env
        // (quocanh_reader, anhnq_reader, attendance_student...)
        // Đảm bảo đủ 4 màn hình:
        // - Sách đang mượn: 1 cuốn còn hạn dài (0/2 gia hạn), 1 cuốn sắp đến hạn còn 2 ngày (1/2 gia hạn), 1 cuốn đã hết lượt gia hạn (2/2)
        // - Lịch sử mượn: 5 dòng đã hoàn trả (đúng hạn, trả trễ, hư hỏng nhẹ)
        // - Tiền phạt: Đủ 3 trạng thái UNPAID (6.000đ < 50.000đ để không khóa quyền gia hạn online), PAID (2 khoản: CASH & TRANSFER), WAIVED (1 khoản)
        // - Đặt trước: Đủ 4 trạng thái READY, PENDING, FULFILLED, CANCELLED
        // =====================================================================
        var coreUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "student01", "student02", "student03", "student04",
            "lecturer01", "guest01", "student_expired", "student_locked", "student_deleted"
        };

        var envReaders = await context.Readers
            .Where(r => r.Status == EntityStatus.Active && !coreUsernames.Contains(r.Username))
            .OrderBy(r => r.CardNo)
            .ToListAsync();

        var templateByBookId = await context.BookCopies
            .Where(c => c.Status == EntityStatus.Active)
            .GroupBy(c => c.BookId)
            .Select(g => new
            {
                BookId = g.Key,
                ShelfId = g.OrderBy(x => x.Barcode).Select(x => x.ShelfId).FirstOrDefault(),
                Price = g.OrderBy(x => x.Barcode).Select(x => x.Price).FirstOrDefault() ?? 150000m
            })
            .ToDictionaryAsync(x => x.BookId);

        var portalBookSets = new (string Active1Isbn, string Active2Isbn, string Active3Isbn, string ReadyIsbn)[]
        {
            ("978-0132350884", "978-6047612345", "978-0262046305", "978-6047625035"),
            ("978-0132126953", "978-6047631043", "978-6040351050", "978-6047625042"),
            ("978-6040352019", "978-6047631036", "978-6047625097", "978-6047625080")
        };

        var nextBarcodeSeq = 65;

        for (var rIdx = 0; rIdx < envReaders.Count; rIdx++)
        {
            var envReader = envReaders[rIdx];
            var bookSet = portalBookSets[rIdx % portalBookSets.Length];

            async Task<BookCopy> CreateDedicatedCopyAsync(string isbn, string initialCopyStatus, string? note = null)
            {
                var book = bookByIsbn[isbn];
                var tpl = templateByBookId.GetValueOrDefault(book.Id);
                var code = $"BC-202610-{nextBarcodeSeq++:D4}";
                var copy = new BookCopy
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ShelfId = tpl?.ShelfId ?? defaultShelfId,
                    Barcode = code,
                    CopyStatus = initialCopyStatus,
                    Price = tpl?.Price ?? 150000m,
                    AcquiredDate = today.AddMonths(-5),
                    Note = note,
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddMonths(-5),
                    UpdatedAt = now,
                    CreatedBy = adminStaffId
                };
                await context.BookCopies.AddAsync(copy);
                await context.SaveChangesAsync();
                return copy;
            }

            var cActive1 = await CreateDedicatedCopyAsync(bookSet.Active1Isbn, LibraryConstants.CopyStatus.Available);
            var cActive2 = await CreateDedicatedCopyAsync(bookSet.Active2Isbn, LibraryConstants.CopyStatus.Available);
            var cActive3 = await CreateDedicatedCopyAsync(bookSet.Active3Isbn, LibraryConstants.CopyStatus.Available);
            var cReservedReady = await CreateDedicatedCopyAsync(
                bookSet.ReadyIsbn,
                LibraryConstants.CopyStatus.Reserved,
                $"Đang giữ chỗ đặt trước tại quầy cho {envReader.FullName} ({envReader.CardNo})");

            var cHist1 = copyByCode["BC-202610-0004"];
            var cHist2 = copyByCode["BC-202610-0012"];
            var cHist3 = copyByCode["BC-202610-0014"];
            var cHist4 = copyByCode["BC-202610-0018"];
            var cHist5 = copyByCode["BC-202610-0026"];

            var slipActive = new BorrowSlip
            {
                Id = Guid.NewGuid(),
                ReaderId = envReader.Id,
                IssuedBy = adminStaffId,
                BorrowedAt = now.AddDays(-6),
                Note = "Phiếu mượn tổng hợp học kỳ [DEMO-FULL-FLOW-V2]",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now.AddDays(-2),
                CreatedBy = adminStaffId
            };

            var dActiveNormal = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipActive.Id,
                CopyId = cActive1.Id,
                DueDate = today.AddDays(10),
                RenewCount = 0, // Bấm gia hạn trực tuyến thành công ngay
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddDays(-4),
                CreatedBy = adminStaffId
            };
            var dActiveDueSoon = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipActive.Id,
                CopyId = cActive2.Id,
                DueDate = today.AddDays(2), // Sắp đến hạn (còn 2 ngày), đã gia hạn 1/2 -> Bấm gia hạn thêm được
                RenewCount = 1,
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-12),
                UpdatedAt = now.AddDays(-5),
                CreatedBy = adminStaffId
            };
            var dActiveMaxRenew = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipActive.Id,
                CopyId = cActive3.Id,
                DueDate = today.AddDays(6), // Đã hết lượt gia hạn (2/2) -> Demo nút khóa gia hạn
                RenewCount = 2,
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-16),
                UpdatedAt = now.AddDays(-2),
                CreatedBy = adminStaffId
            };

            var slipHist = new BorrowSlip
            {
                Id = Guid.NewGuid(),
                ReaderId = envReader.Id,
                IssuedBy = librarianStaffId,
                BorrowedAt = now.AddDays(-35),
                Note = "Phiếu mượn tài liệu tham khảo chuyên ngành (Đã hoàn trả)",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-35),
                UpdatedAt = now.AddDays(-7),
                CreatedBy = librarianStaffId
            };

            var dHistOnTime1 = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipHist.Id,
                CopyId = cHist1.Id,
                DueDate = today.AddDays(-21),
                ReturnedAt = now.AddDays(-23),
                ReceivedBy = librarianStaffId,
                RenewCount = 1,
                ReturnCondition = LibraryConstants.ReturnCondition.Good,
                ReturnNote = "Sách nguyên vẹn, trả trước hạn 2 ngày",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-35),
                UpdatedAt = now.AddDays(-23),
                CreatedBy = librarianStaffId
            };
            var dHistLateUnpaid = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipHist.Id,
                CopyId = cHist2.Id,
                DueDate = today.AddDays(-10),
                ReturnedAt = now.AddDays(-7),
                ReceivedBy = librarianStaffId,
                RenewCount = 0,
                ReturnCondition = LibraryConstants.ReturnCondition.Good,
                ReturnNote = "Trả trễ 3 ngày, lập phiếu thu phạt quá hạn",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-24),
                UpdatedAt = now.AddDays(-7),
                CreatedBy = librarianStaffId
            };
            var dHistLatePaid = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipHist.Id,
                CopyId = cHist3.Id,
                DueDate = today.AddDays(-21),
                ReturnedAt = now.AddDays(-17),
                ReceivedBy = adminStaffId,
                RenewCount = 0,
                ReturnCondition = LibraryConstants.ReturnCondition.Good,
                ReturnNote = "Trả trễ 4 ngày, đã nộp phạt chuyển khoản tại quầy",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-35),
                UpdatedAt = now.AddDays(-17),
                CreatedBy = adminStaffId
            };
            var dHistDamagedPaid = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipHist.Id,
                CopyId = cHist4.Id,
                DueDate = today.AddDays(-22),
                ReturnedAt = now.AddDays(-23),
                ReceivedBy = adminStaffId,
                RenewCount = 1,
                ReturnCondition = LibraryConstants.ReturnCondition.Good,
                ReturnNote = "Rách nhẹ góc bìa phụ, đã thu phí bồi thường tại quầy",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-28),
                UpdatedAt = now.AddDays(-23),
                CreatedBy = adminStaffId
            };
            var dHistWaived = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipHist.Id,
                CopyId = cHist5.Id,
                DueDate = today.AddDays(-30),
                ReturnedAt = now.AddDays(-29),
                ReceivedBy = adminStaffId,
                RenewCount = 0,
                ReturnCondition = LibraryConstants.ReturnCondition.Good,
                ReturnNote = "Trễ 1 ngày do trùng lịch nghỉ lễ toàn trường",
                Status = EntityStatus.Active,
                CreatedAt = now.AddDays(-44),
                UpdatedAt = now.AddDays(-29),
                CreatedBy = adminStaffId
            };

            await context.BorrowSlips.AddRangeAsync([slipActive, slipHist]);
            await context.BorrowDetails.AddRangeAsync([
                dActiveNormal, dActiveDueSoon, dActiveMaxRenew,
                dHistOnTime1, dHistLateUnpaid, dHistLatePaid, dHistDamagedPaid, dHistWaived
            ]);

            await context.Fines.AddRangeAsync(
            [
                new Fine
                {
                    Id = Guid.NewGuid(),
                    BorrowDetailId = dHistLateUnpaid.Id,
                    FineType = LibraryConstants.FineType.Overdue,
                    OverdueDays = 3,
                    Amount = 6000m,
                    FineStatus = LibraryConstants.FineStatus.Unpaid,
                    HandledBy = null,
                    PaidAt = null,
                    PaymentMethod = null,
                    Note = "Phạt trả sách quá hạn 3 ngày (2.000 đ/ngày), chờ bạn đọc nộp tại quầy lưu hành",
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddDays(-7),
                    UpdatedAt = now.AddDays(-7),
                    CreatedBy = librarianStaffId
                },
                new Fine
                {
                    Id = Guid.NewGuid(),
                    BorrowDetailId = dHistLatePaid.Id,
                    FineType = LibraryConstants.FineType.Overdue,
                    OverdueDays = 4,
                    Amount = 8000m,
                    FineStatus = LibraryConstants.FineStatus.Paid,
                    HandledBy = adminStaffId,
                    PaidAt = now.AddDays(-17),
                    PaymentMethod = LibraryConstants.PaymentMethod.BankTransfer,
                    Note = "Đã thu tiền phạt quá hạn 4 ngày qua chuyển khoản ngân hàng tại quầy lưu hành",
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddDays(-17),
                    UpdatedAt = now.AddDays(-17),
                    CreatedBy = adminStaffId
                },
                new Fine
                {
                    Id = Guid.NewGuid(),
                    BorrowDetailId = dHistDamagedPaid.Id,
                    FineType = LibraryConstants.FineType.Damaged,
                    OverdueDays = null,
                    Amount = 15000m,
                    FineStatus = LibraryConstants.FineStatus.Paid,
                    HandledBy = adminStaffId,
                    PaidAt = now.AddDays(-23),
                    PaymentMethod = LibraryConstants.PaymentMethod.Cash,
                    Note = "Phí bồi thường hư hỏng nhẹ bìa phụ tài liệu (đã thu tiền mặt tại quầy)",
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddDays(-23),
                    UpdatedAt = now.AddDays(-23),
                    CreatedBy = adminStaffId
                },
                new Fine
                {
                    Id = Guid.NewGuid(),
                    BorrowDetailId = dHistWaived.Id,
                    FineType = LibraryConstants.FineType.Overdue,
                    OverdueDays = 1,
                    Amount = 2000m,
                    FineStatus = LibraryConstants.FineStatus.Waived,
                    HandledBy = adminStaffId,
                    PaidAt = null,
                    PaymentMethod = null,
                    Note = "Miễn giảm phí quá hạn 1 ngày do trùng kỳ nghỉ lễ chính thức của nhà trường",
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddDays(-29),
                    UpdatedAt = now.AddDays(-29),
                    CreatedBy = adminStaffId
                }
            ]);

            await context.Reservations.AddRangeAsync(
            [
                new Reservation
                {
                    Id = Guid.NewGuid(),
                    ReaderId = envReader.Id,
                    BookId = cReservedReady.BookId,
                    AssignedCopyId = cReservedReady.Id,
                    ReservationStatus = LibraryConstants.ReservationStatus.Ready,
                    ReservedAt = now.AddHours(-16),
                    ReadyAt = now.AddHours(-2),
                    ExpiresAt = now.AddHours(46),
                    ClosedAt = null,
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddHours(-16),
                    UpdatedAt = now.AddHours(-2),
                    CreatedBy = adminStaffId
                },
                new Reservation
                {
                    Id = Guid.NewGuid(),
                    ReaderId = envReader.Id,
                    BookId = bookByIsbn["978-6040359032"].Id, // Kỷ yếu GIS-UTC 2025 (Đang hết bản sẵn sàng - để 2 đầu sách Hot IT/Luận văn rảnh 100% cho bạn đọc bấm Đặt trước live!)
                    AssignedCopyId = null,
                    ReservationStatus = LibraryConstants.ReservationStatus.Pending,
                    ReservedAt = now.AddHours(-5).AddMinutes(rIdx * 5),
                    ReadyAt = null,
                    ExpiresAt = null,
                    ClosedAt = null,
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddHours(-5),
                    UpdatedAt = now.AddHours(-5),
                    CreatedBy = adminStaffId
                },
                new Reservation
                {
                    Id = Guid.NewGuid(),
                    ReaderId = envReader.Id,
                    BookId = bookByIsbn["978-6047625011"].Id,
                    AssignedCopyId = null,
                    ReservationStatus = LibraryConstants.ReservationStatus.Fulfilled,
                    ReservedAt = now.AddDays(-18),
                    ReadyAt = now.AddDays(-17),
                    ExpiresAt = now.AddDays(-15),
                    ClosedAt = now.AddDays(-16),
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddDays(-18),
                    UpdatedAt = now.AddDays(-16),
                    CreatedBy = adminStaffId
                },
                new Reservation
                {
                    Id = Guid.NewGuid(),
                    ReaderId = envReader.Id,
                    BookId = bookByIsbn["978-6047625028"].Id,
                    AssignedCopyId = null,
                    ReservationStatus = LibraryConstants.ReservationStatus.Cancelled,
                    ReservedAt = now.AddDays(-11),
                    ReadyAt = null,
                    ExpiresAt = null,
                    ClosedAt = now.AddDays(-9),
                    Status = EntityStatus.Active,
                    CreatedAt = now.AddDays(-11),
                    UpdatedAt = now.AddDays(-9),
                    CreatedBy = adminStaffId
                }
            ]);

            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedSampleBookImagesAsync(ApplicationDbContext context, Guid adminStaffId)
    {
        var cwd = Directory.GetCurrentDirectory();
        var webRoot = Directory.Exists(Path.Combine(cwd, "wwwroot"))
            ? Path.Combine(cwd, "wwwroot")
            : Path.Combine(cwd, "web_mvc_management", "wwwroot");

        var seedDir = Path.Combine(webRoot, "uploads", "books", "seed");
        Directory.CreateDirectory(seedDir);

        var booksWithFullGallery = new HashSet<Guid>(
            await context.BookImages
                .Where(img => img.Status == EntityStatus.Active)
                .GroupBy(img => img.BookId)
                .Where(g => g.Count() >= 5)
                .Select(g => g.Key)
                .ToListAsync());

        var books = await context.Books
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.BookAuthors)
                .ThenInclude(ba => ba.Author)
            .OrderBy(b => b.CoverUrl)
            .ThenBy(b => b.Isbn)
            .ToListAsync();

        var palettes = new (string Bg1, string Bg2, string Accent, string BadgeBg)[]
        {
            ("#0F2A4A", "#1E4E79", "#F59E0B", "#FEF3C7"),
            ("#133A32", "#1F6354", "#34D399", "#D1FAE5"),
            ("#2E1A47", "#4E2A70", "#C084FC", "#F3E8FF"),
            ("#4A1D1F", "#7A2E32", "#F87171", "#FEE2E2"),
            ("#172554", "#1D4ED8", "#38BDF8", "#E0F2FE"),
            ("#3B2F1E", "#66502C", "#FBBF24", "#FEF3C7"),
            ("#064E3B", "#047857", "#6EE7B7", "#D1FAE5"),
            ("#312E81", "#4338CA", "#A5B4FC", "#E0E7FF")
        };

        var imagesToInsert = new List<BookImage>();

        for (var i = 0; i < books.Count; i++)
        {
            var book = books[i];
            var palette = palettes[i % palettes.Length];
            var slug = $"book-{i + 1:00}";

            var authorsText = book.BookAuthors.Count > 0
                ? string.Join(", ", book.BookAuthors.OrderBy(ba => ba.AuthorOrder).Where(ba => ba.Author != null).Select(ba => ba.Author.FullName))
                : "Hội đồng Khoa học ĐH GTVT";

            var scopeBadge = ResolveScopeBadgeText(book);
            var categoryTitle = book.Category?.Name ?? "Khoa học & Công nghệ GTVT";
            var publisherName = book.Publisher?.Name ?? "NXB Giao Thông Vận Tải";
            var yearText = (book.PublicationYear ?? 2025).ToString();
            var isbnText = book.Isbn ?? "978-604-7625-00";

            // 1. Ảnh bìa trước (COVER)
            var coverRelUrl = $"/uploads/books/seed/{slug}-cover.svg";
            var coverSvg = BuildCoverSvg(book.Title, authorsText, categoryTitle, scopeBadge, isbnText, publisherName, yearText, palette.Bg1, palette.Bg2, palette.Accent);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-cover.svg"), coverSvg, Encoding.UTF8);

            // 2. Ảnh bìa sau (BACK_COVER)
            var backRelUrl = $"/uploads/books/seed/{slug}-back.svg";
            var backSvg = BuildBackCoverSvg(book.Title, book.Description ?? "", categoryTitle, isbnText, publisherName, yearText, book.PageCount ?? 320, palette.Bg1, palette.Bg2, palette.Accent);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-back.svg"), backSvg, Encoding.UTF8);

            // 3. Ảnh Mục lục (TABLE_OF_CONTENTS)
            var tocRelUrl = $"/uploads/books/seed/{slug}-toc.svg";
            var tocSvg = BuildPageSvg(
                book.Title,
                "MỤC LỤC CHI TIẾT (TABLE OF CONTENTS)",
                [
                    $"Lời nói đầu & Hướng dẫn sử dụng tài liệu ({scopeBadge}) ............. Trang 03",
                    $"Chương 1: Cơ sở lý thuyết và nguyên lý nền tảng về {categoryTitle} ... Trang 12",
                    "Chương 2: Phân tích mô hình, kiến trúc hệ thống & Tiêu chuẩn kỹ thuật . Trang 54",
                    "Chương 3: Phương pháp tính toán, thiết kế và thuật toán tối ưu ........ Trang 118",
                    "Chương 4: Ứng dụng thực tiễn và Nghiên cứu tình huống (Case Studies) .. Trang 186",
                    "Chương 5: Đánh giá thực nghiệm, Kiểm định chất lượng & An toàn ........ Trang 242",
                    "Tài liệu tham khảo, Phụ lục tra cứu & Bài tập thực hành ............... Trang 295"
                ],
                "Trang i — Mục lục ấn phẩm",
                palette.Bg1,
                includeDiagram: false);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-toc.svg"), tocSvg, Encoding.UTF8);

            // 4. Ảnh Trang đọc thử 1 (PREVIEW_PAGE)
            var p1RelUrl = $"/uploads/books/seed/{slug}-page-01.svg";
            var p1Svg = BuildPageSvg(
                book.Title,
                "CHƯƠNG 1: TỔNG QUAN & CƠ SỞ LÝ THUYẾT CỐT LÕI",
                [
                    $"1.1. Đặt vấn đề và bối cảnh nghiên cứu ({publisherName}, {yearText}):",
                    book.Description ?? "Tài liệu chuyên khảo phục vụ đào tạo đại học và sau đại học tại Trường Đại học Giao thông Vận tải.",
                    $"1.2. Tác giả biên soạn: {authorsText}.",
                    "1.3. Các nguyên lý và tiêu chuẩn kỹ thuật bắt buộc tuân thủ:",
                    "• Đảm bảo tính chính xác toán học, toàn vẹn dữ liệu và khả năng mở rộng hệ thống.",
                    "• Kết hợp chặt chẽ giữa mô hình lý thuyết với quy chuẩn kỹ thuật quốc gia (TCVN / Quốc tế)."
                ],
                "Trang 12 — Đọc thử Chương 1",
                palette.Bg1,
                includeDiagram: false);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-page-01.svg"), p1Svg, Encoding.UTF8);

            // 5. Ảnh Trang đọc thử 2 (PREVIEW_PAGE - có sơ đồ khối kiến trúc)
            var p2RelUrl = $"/uploads/books/seed/{slug}-page-02.svg";
            var p2Svg = BuildPageSvg(
                book.Title,
                "CHƯƠNG 2: MÔ HÌNH KIẾN TRÚC & BÀI TẬP THỰC HÀNH",
                [
                    "2.1. Sơ đồ quy trình phân tích, thiết kế và kiểm định 4 giai đoạn:",
                    "Quy trình chuẩn áp dụng cho sinh viên, kỹ sư và học viên cao học khi triển khai đề tài.",
                    "2.2. Câu hỏi ôn tập và Bài tập thảo luận cuối chương:",
                    $"• Câu 1: Phân tích các yếu tố ảnh hưởng trực tiếp đến hiệu năng mô hình trong {categoryTitle}.",
                    $"• Câu 2: Thực hành bài toán mẫu (Mã ISBN: {isbnText}) và đối chiếu kết quả tại phòng đọc UTC."
                ],
                "Trang 25 — Đọc thử Chương 2",
                palette.Bg1,
                includeDiagram: true);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-page-02.svg"), p2Svg, Encoding.UTF8);

            book.CoverUrl = coverRelUrl;

            if (booksWithFullGallery.Contains(book.Id))
            {
                continue;
            }

            var existingBookImages = await context.BookImages.Where(img => img.BookId == book.Id).ToListAsync();
            if (existingBookImages.Count > 0)
            {
                context.BookImages.RemoveRange(existingBookImages);
            }

            imagesToInsert.AddRange(
            [
                new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = coverRelUrl,
                    PublicId = $"seed/{slug}-cover",
                    ImageType = LibraryConstants.BookImageType.Cover,
                    Caption = "Ảnh bìa trước (Bìa chính)",
                    DisplayOrder = 0,
                    IsPrimary = true,
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                },
                new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = backRelUrl,
                    PublicId = $"seed/{slug}-back",
                    ImageType = LibraryConstants.BookImageType.BackCover,
                    Caption = "Ảnh bìa sau & Mã vạch ISBN",
                    DisplayOrder = 1,
                    IsPrimary = false,
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                },
                new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = tocRelUrl,
                    PublicId = $"seed/{slug}-toc",
                    ImageType = LibraryConstants.BookImageType.TableOfContents,
                    Caption = "Mục lục chi tiết ấn phẩm",
                    DisplayOrder = 2,
                    IsPrimary = false,
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                },
                new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = p1RelUrl,
                    PublicId = $"seed/{slug}-page-01",
                    ImageType = LibraryConstants.BookImageType.PreviewPage,
                    Caption = "Trang đọc thử — Chương 1 (Tr. 12)",
                    DisplayOrder = 3,
                    IsPrimary = false,
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                },
                new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = p2RelUrl,
                    PublicId = $"seed/{slug}-page-02",
                    ImageType = LibraryConstants.BookImageType.PreviewPage,
                    Caption = "Trang đọc thử — Chương 2 & Sơ đồ (Tr. 25)",
                    DisplayOrder = 4,
                    IsPrimary = false,
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaffId
                }
            ]);
        }

        if (imagesToInsert.Count > 0)
        {
            await context.BookImages.AddRangeAsync(imagesToInsert);
        }
        await context.SaveChangesAsync();
    }

    private static string ResolveScopeBadgeText(Book b)
    {
        var combined = StringUtil.RemoveVietnameseSigns($"{b.Title} {b.Edition} {b.Description}").ToLowerInvariant();
        if (combined.Contains("luan van") || combined.Contains("do an") || combined.Contains("khoa luan") || combined.Contains("luan an"))
        {
            return "LUẬN VĂN & ĐỒ ÁN";
        }
        if (combined.Contains("tap chi") || combined.Contains("journal") || combined.Contains("ky yeu") || combined.Contains("hoi thao"))
        {
            return "TẠP CHÍ KHOA HỌC";
        }
        return "GIÁO TRÌNH CHUYÊN NGÀNH";
    }

    private static string BuildCoverSvg(
        string title,
        string authors,
        string category,
        string scopeBadge,
        string isbn,
        string publisher,
        string year,
        string bg1,
        string bg2,
        string accent)
    {
        var lines = WrapText(title, 25, 4);
        var titleTspans = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            var dy = i == 0 ? "0" : "40";
            titleTspans.Append($"<tspan x=\"64\" dy=\"{dy}\">{WebUtility.HtmlEncode(lines[i])}</tspan>");
        }

        var shortAuthors = authors.Length > 46 ? authors[..43] + "..." : authors;
        var shortCategory = category.Length > 42 ? category[..39] + "..." : category;

        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 840" width="600" height="840">
              <defs>
                <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0%" stop-color="{{bg1}}"/>
                  <stop offset="100%" stop-color="{{bg2}}"/>
                </linearGradient>
                <linearGradient id="spine" x1="0" y1="0" x2="1" y2="0">
                  <stop offset="0%" stop-color="#020617" stop-opacity="0.55"/>
                  <stop offset="65%" stop-color="#FFFFFF" stop-opacity="0.08"/>
                  <stop offset="100%" stop-color="#000000" stop-opacity="0.22"/>
                </linearGradient>
              </defs>
              <rect width="600" height="840" rx="16" fill="url(#bg)"/>
              <rect width="34" height="840" rx="8" fill="url(#spine)"/>
              <rect x="42" y="26" width="532" height="788" rx="12" fill="none" stroke="{{accent}}" stroke-width="2" stroke-opacity="0.45"/>
              <rect x="64" y="56" width="215" height="32" rx="6" fill="{{accent}}"/>
              <text x="171" y="77" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="800" fill="#0F172A" text-anchor="middle">{{WebUtility.HtmlEncode(scopeBadge)}}</text>
              <text x="550" y="77" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="700" fill="#E2E8F0" text-anchor="end">UTC LIBRARY • {{WebUtility.HtmlEncode(year)}}</text>
              <text x="64" y="130" font-family="Segoe UI, Arial, sans-serif" font-size="14" font-weight="600" fill="{{accent}}" letter-spacing="0.5">{{WebUtility.HtmlEncode(shortCategory.ToUpperInvariant())}}</text>
              <text x="64" y="188" font-family="Georgia, Times New Roman, serif" font-size="31" font-weight="700" fill="#FFFFFF">{{titleTspans}}</text>
              <line x1="64" y1="358" x2="265" y2="358" stroke="{{accent}}" stroke-width="4"/>
              <text x="64" y="395" font-family="Segoe UI, Arial, sans-serif" font-size="17" font-weight="600" fill="#F8FAFC">{{WebUtility.HtmlEncode(shortAuthors)}}</text>
              <g transform="translate(340, 445)">
                <circle cx="110" cy="110" r="105" fill="{{accent}}" fill-opacity="0.10" stroke="{{accent}}" stroke-width="2" stroke-opacity="0.45"/>
                <circle cx="110" cy="110" r="72" fill="{{accent}}" fill-opacity="0.14" stroke="#FFFFFF" stroke-width="1.5" stroke-opacity="0.35"/>
                <polygon points="110,52 162,142 58,142" fill="none" stroke="{{accent}}" stroke-width="2.5" stroke-opacity="0.7"/>
                <circle cx="110" cy="112" r="16" fill="{{accent}}" fill-opacity="0.85"/>
              </g>
              <rect x="64" y="685" width="486" height="96" rx="10" fill="#0F172A" fill-opacity="0.45" stroke="#FFFFFF" stroke-opacity="0.16"/>
              <text x="84" y="722" font-family="Segoe UI, Arial, sans-serif" font-size="17" font-weight="700" fill="#FFFFFF">{{WebUtility.HtmlEncode(publisher)}}</text>
              <text x="84" y="752" font-family="Consolas, monospace" font-size="15" font-weight="600" fill="{{accent}}">ISBN: {{WebUtility.HtmlEncode(isbn)}}</text>
              <text x="530" y="752" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="600" fill="#CBD5E1" text-anchor="end">ĐH GIAO THÔNG VẬN TẢI</text>
            </svg>
            """;
    }

    private static string BuildBackCoverSvg(
        string title,
        string description,
        string category,
        string isbn,
        string publisher,
        string year,
        int pageCount,
        string bg1,
        string bg2,
        string accent)
    {
        var descLines = WrapText(
            string.IsNullOrWhiteSpace(description)
                ? "Ấn phẩm học thuật chuẩn mực phục vụ công tác giảng dạy, học tập và nghiên cứu khoa học tại Trường Đại học Giao thông Vận tải."
                : description,
            52,
            5);
        var descSvg = new StringBuilder();
        var y = 235;
        foreach (var line in descLines)
        {
            descSvg.AppendLine($"<text x=\"68\" y=\"{y}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"15.5\" fill=\"#E2E8F0\">{WebUtility.HtmlEncode(line)}</text>");
            y += 28;
        }

        var shortTitle = title.Length > 52 ? title[..49] + "..." : title;

        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 840" width="600" height="840">
              <defs>
                <linearGradient id="bgBack" x1="1" y1="0" x2="0" y2="1">
                  <stop offset="0%" stop-color="{{bg1}}"/>
                  <stop offset="100%" stop-color="{{bg2}}"/>
                </linearGradient>
              </defs>
              <rect width="600" height="840" rx="16" fill="url(#bgBack)"/>
              <rect x="32" y="28" width="536" height="784" rx="12" fill="none" stroke="{{accent}}" stroke-width="1.8" stroke-opacity="0.4"/>
              <text x="68" y="82" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="700" fill="{{accent}}" letter-spacing="1">GIỚI THIỆU NỘI DUNG ẤN PHẨM (BÌA SAU)</text>
              <text x="68" y="122" font-family="Georgia, serif" font-size="21" font-weight="700" fill="#FFFFFF">{{WebUtility.HtmlEncode(shortTitle)}}</text>
              <line x1="68" y1="148" x2="532" y2="148" stroke="{{accent}}" stroke-width="2" stroke-opacity="0.6"/>
              <text x="68" y="192" font-family="Segoe UI, Arial, sans-serif" font-size="15" font-weight="700" fill="#F8FAFC">Tóm tắt học thuật:</text>
              {{descSvg}}
              <rect x="68" y="405" width="464" height="165" rx="10" fill="#0F172A" fill-opacity="0.42" stroke="#FFFFFF" stroke-opacity="0.15"/>
              <text x="90" y="440" font-family="Segoe UI, Arial, sans-serif" font-size="15" font-weight="700" fill="{{accent}}">THÔNG TIN LƯU HÀNH TẠI THƯ VIỆN ĐH GTVT:</text>
              <text x="90" y="472" font-family="Segoe UI, Arial, sans-serif" font-size="14" fill="#F1F5F9">• Chuyên ngành: {{WebUtility.HtmlEncode(category)}}</text>
              <text x="90" y="500" font-family="Segoe UI, Arial, sans-serif" font-size="14" fill="#F1F5F9">• Quy cách: Khổ 16x24 cm • Số trang: {{pageCount}} trang • Năm XB: {{WebUtility.HtmlEncode(year)}}</text>
              <text x="90" y="528" font-family="Segoe UI, Arial, sans-serif" font-size="14" fill="#F1F5F9">• Đối tượng: Sinh viên, Học viên Cao học, Nghiên cứu sinh &amp; Giảng viên</text>
              <text x="90" y="554" font-family="Segoe UI, Arial, sans-serif" font-size="14" fill="#F1F5F9">• Đơn vị phát hành: {{WebUtility.HtmlEncode(publisher)}}</text>
              <rect x="305" y="625" width="227" height="135" rx="8" fill="#FFFFFF"/>
              <text x="418" y="648" font-family="Consolas, monospace" font-size="12" font-weight="700" fill="#0F172A" text-anchor="middle">ISBN {{WebUtility.HtmlEncode(isbn)}}</text>
              <g fill="#0F172A" transform="translate(328, 658)">
                <rect x="0" y="0" width="4" height="68"/><rect x="7" y="0" width="2" height="68"/><rect x="13" y="0" width="6" height="68"/>
                <rect x="23" y="0" width="2" height="68"/><rect x="29" y="0" width="4" height="68"/><rect x="37" y="0" width="8" height="68"/>
                <rect x="49" y="0" width="2" height="68"/><rect x="55" y="0" width="4" height="68"/><rect x="63" y="0" width="6" height="68"/>
                <rect x="73" y="0" width="2" height="68"/><rect x="79" y="0" width="4" height="68"/><rect x="87" y="0" width="2" height="68"/>
                <rect x="93" y="0" width="8" height="68"/><rect x="105" y="0" width="4" height="68"/><rect x="113" y="0" width="2" height="68"/>
                <rect x="119" y="0" width="6" height="68"/><rect x="129" y="0" width="4" height="68"/><rect x="137" y="0" width="2" height="68"/>
                <rect x="143" y="0" width="6" height="68"/><rect x="153" y="0" width="4" height="68"/><rect x="161" y="0" width="2" height="68"/>
                <rect x="167" y="0" width="6" height="68"/><rect x="176" y="0" width="3" height="68"/>
              </g>
              <text x="418" y="746" font-family="Consolas, monospace" font-size="13" font-weight="700" fill="#1E293B" text-anchor="middle">9 786047 625008</text>
              <text x="68" y="700" font-family="Segoe UI, Arial, sans-serif" font-size="15" font-weight="700" fill="#FFFFFF">TRƯỜNG ĐẠI HỌC GIAO THÔNG VẬN TẢI</text>
              <text x="68" y="726" font-family="Segoe UI, Arial, sans-serif" font-size="13" fill="#CBD5E1">Trung tâm Thông tin - Thư viện (Số 3 Cầu Giấy, Hà Nội)</text>
            </svg>
            """;
    }

    private static string BuildPageSvg(
        string bookTitle,
        string heading,
        IReadOnlyList<string> paragraphs,
        string footerPage,
        string accentColor,
        bool includeDiagram)
    {
        var bodyBuilder = new StringBuilder();
        var currentY = 205;

        foreach (var para in paragraphs)
        {
            var wrapped = WrapText(para, 58, 3);
            foreach (var line in wrapped)
            {
                bodyBuilder.AppendLine($"<text x=\"65\" y=\"{currentY}\" font-family=\"Georgia, serif\" font-size=\"15.5\" fill=\"#1E293B\">{WebUtility.HtmlEncode(line)}</text>");
                currentY += 26;
            }
            currentY += 14;
        }

        if (includeDiagram)
        {
            var diagY = Math.Min(currentY + 10, 520);
            bodyBuilder.AppendLine($$"""
                <g transform="translate(65, {{diagY}})">
                  <rect x="0" y="0" width="470" height="150" rx="8" fill="#F8FAFC" stroke="#CBD5E1" stroke-width="1.5"/>
                  <text x="235" y="28" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="700" fill="{{accentColor}}" text-anchor="middle">HÌNH 2.1 — SƠ ĐỒ KIẾN TRÚC &amp; QUY TRÌNH THỰC THI TIÊU CHUẨN</text>
                  <rect x="18" y="48" width="96" height="54" rx="6" fill="{{accentColor}}" fill-opacity="0.12" stroke="{{accentColor}}" stroke-width="1.5"/>
                  <text x="66" y="72" font-family="Segoe UI, sans-serif" font-size="11.5" font-weight="700" fill="#0F172A" text-anchor="middle">1. Khảo sát</text>
                  <text x="66" y="90" font-family="Segoe UI, sans-serif" font-size="10.5" fill="#475569" text-anchor="middle">&amp; Đặc tả</text>
                  <text x="124" y="80" font-family="Segoe UI, sans-serif" font-size="16" font-weight="700" fill="{{accentColor}}">→</text>
                  <rect x="138" y="48" width="96" height="54" rx="6" fill="{{accentColor}}" fill-opacity="0.12" stroke="{{accentColor}}" stroke-width="1.5"/>
                  <text x="186" y="72" font-family="Segoe UI, sans-serif" font-size="11.5" font-weight="700" fill="#0F172A" text-anchor="middle">2. Mô hình</text>
                  <text x="186" y="90" font-family="Segoe UI, sans-serif" font-size="10.5" fill="#475569" text-anchor="middle">&amp; Thiết kế</text>
                  <text x="244" y="80" font-family="Segoe UI, sans-serif" font-size="16" font-weight="700" fill="{{accentColor}}">→</text>
                  <rect x="258" y="48" width="96" height="54" rx="6" fill="{{accentColor}}" fill-opacity="0.12" stroke="{{accentColor}}" stroke-width="1.5"/>
                  <text x="306" y="72" font-family="Segoe UI, sans-serif" font-size="11.5" font-weight="700" fill="#0F172A" text-anchor="middle">3. Thực thi</text>
                  <text x="306" y="90" font-family="Segoe UI, sans-serif" font-size="10.5" fill="#475569" text-anchor="middle">&amp; Tối ưu</text>
                  <text x="364" y="80" font-family="Segoe UI, sans-serif" font-size="16" font-weight="700" fill="{{accentColor}}">→</text>
                  <rect x="376" y="48" width="78" height="54" rx="6" fill="{{accentColor}}" fill-opacity="0.18" stroke="{{accentColor}}" stroke-width="1.5"/>
                  <text x="415" y="72" font-family="Segoe UI, sans-serif" font-size="11.5" font-weight="700" fill="#0F172A" text-anchor="middle">4. Kiểm định</text>
                  <text x="415" y="90" font-family="Segoe UI, sans-serif" font-size="10.5" fill="#475569" text-anchor="middle">&amp; Nghiệm thu</text>
                  <text x="235" y="132" font-family="Georgia, serif" font-size="12" font-style="italic" fill="#64748B" text-anchor="middle">Chú thích: Quy trình chuẩn áp dụng trong đào tạo và nghiên cứu thực nghiệm tại UTC.</text>
                </g>
                """);
        }

        var shortTitle = bookTitle.Length > 52 ? bookTitle[..49] + "..." : bookTitle;

        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 840" width="600" height="840">
              <rect width="600" height="840" rx="12" fill="#FCFBF7" stroke="#CBD5E1" stroke-width="3"/>
              <rect x="36" y="36" width="528" height="768" fill="none" stroke="#E2E8F0" stroke-width="1.5"/>
              <text x="65" y="76" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="700" fill="#64748B">{{WebUtility.HtmlEncode(shortTitle)}}</text>
              <line x1="65" y1="90" x2="535" y2="90" stroke="#CBD5E1" stroke-width="1.5"/>
              <rect x="65" y="114" width="470" height="44" rx="6" fill="{{accentColor}}" fill-opacity="0.09" stroke="{{accentColor}}" stroke-opacity="0.25"/>
              <text x="80" y="142" font-family="Segoe UI, Arial, sans-serif" font-size="15.5" font-weight="700" fill="{{accentColor}}">{{WebUtility.HtmlEncode(heading)}}</text>
              {{bodyBuilder}}
              <line x1="65" y1="742" x2="535" y2="742" stroke="#CBD5E1" stroke-width="1"/>
              <text x="65" y="772" font-family="Segoe UI, Arial, sans-serif" font-size="12" font-weight="600" fill="#94A3B8">THƯ VIỆN ĐẠI HỌC GIAO THÔNG VẬN TẢI — BẢN ĐỌC THỬ</text>
              <text x="535" y="772" font-family="Segoe UI, Arial, sans-serif" font-size="13.5" font-weight="700" fill="#475569" text-anchor="end">{{WebUtility.HtmlEncode(footerPage)}}</text>
            </svg>
            """;
    }

    private static List<string> WrapText(string text, int maxCharsPerLine, int maxLines)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxCharsPerLine && current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
                if (lines.Count >= maxLines - 1)
                {
                    break;
                }
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }
            current.Append(word);
        }

        if (current.Length > 0 && lines.Count < maxLines)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    private record SeedUserSetting(
        string Email,
        string Role,
        string Password,
        bool HasExplicitPassword,
        string FullName,
        string Username,
        string CardNo,
        string MemberCode);

    private static List<SeedUserSetting> GetSeedUsers(
        IConfiguration? configuration,
        string defaultPassword,
        bool hasDefaultPassword)
    {
        var indices = new SortedSet<int>(Enumerable.Range(0, 20));

        if (configuration != null)
        {
            foreach (var child in configuration.GetSection("SeedSettings:Users").GetChildren())
            {
                if (int.TryParse(child.Key, out var idx) && idx >= 0)
                {
                    indices.Add(idx);
                }
            }
        }

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            if (key != null && key.StartsWith("SeedSettings__Users__", StringComparison.OrdinalIgnoreCase))
            {
                var parts = key.Split("__", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && int.TryParse(parts[2], out var idx) && idx >= 0)
                {
                    indices.Add(idx);
                }
            }
        }

        var result = new List<SeedUserSetting>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var i in indices)
        {
            var email = configuration?[$"SeedSettings:Users:{i}:Email"]
                ?? Environment.GetEnvironmentVariable($"SeedSettings__Users__{i}__Email");

            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            email = email.Trim().ToLowerInvariant();
            if (email.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase) ||
                email.StartsWith("your_", StringComparison.OrdinalIgnoreCase) ||
                !seenEmails.Add(email))
            {
                continue;
            }

            var roleRaw = configuration?[$"SeedSettings:Users:{i}:Role"]
                ?? Environment.GetEnvironmentVariable($"SeedSettings__Users__{i}__Role");
            var role = string.Equals(roleRaw?.Trim(), AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                ? AppRoles.Admin
                : AppRoles.Librarian;

            var userPassword = configuration?[$"SeedSettings:Users:{i}:Password"]
                ?? Environment.GetEnvironmentVariable($"SeedSettings__Users__{i}__Password");
            var hasUserPassword = !string.IsNullOrWhiteSpace(userPassword) &&
                !userPassword.Equals("your_seed_password", StringComparison.OrdinalIgnoreCase);

            var password = hasUserPassword
                ? userPassword!.Trim()
                : defaultPassword;

            var emailPrefix = email.Split('@')[0];
            var fullName = configuration?[$"SeedSettings:Users:{i}:FullName"]
                ?? Environment.GetEnvironmentVariable($"SeedSettings__Users__{i}__FullName");
            fullName = string.IsNullOrWhiteSpace(fullName) ? emailPrefix : fullName.Trim();

            var roleSuffix = role == AppRoles.Admin ? "admin" : "librarian";
            var username = configuration?[$"SeedSettings:Users:{i}:Username"]
                ?? Environment.GetEnvironmentVariable($"SeedSettings__Users__{i}__Username");
            username = string.IsNullOrWhiteSpace(username) ? $"{emailPrefix}_{roleSuffix}" : username.Trim();

            var cardPrefix = role == AppRoles.Admin ? "GV" : "SV";
            var seq = 8881 + i;
            var cardNo = configuration?[$"SeedSettings:Users:{i}:CardNo"]
                ?? Environment.GetEnvironmentVariable($"SeedSettings__Users__{i}__CardNo")
                ?? $"{cardPrefix}2026{seq}";

            var memberCode = configuration?[$"SeedSettings:Users:{i}:MemberCode"]
                ?? Environment.GetEnvironmentVariable($"SeedSettings__Users__{i}__MemberCode")
                ?? $"{cardPrefix}{seq}";

            result.Add(new SeedUserSetting(
                email,
                role,
                password,
                hasUserPassword || hasDefaultPassword,
                fullName,
                username,
                cardNo,
                memberCode));
        }

        return result;
    }
}
