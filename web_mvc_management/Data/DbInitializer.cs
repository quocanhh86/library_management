using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Util;

namespace web_mvc_management.Data;

/// <summary>
/// Khởi tạo dữ liệu mẫu sạch và chuẩn mực phục vụ demo đồ án Quản lý thư viện.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context, IConfiguration? configuration = null)
    {
        // 0. Đảm bảo bảng dbo.book_images tồn tại trên SQL Server (nâng cấp tự động không cần xóa DB cũ)
        const string ensureBookImagesTableSql = """
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
            """;
        await context.Database.ExecuteSqlRawAsync(ensureBookImagesTableSql);

        // 1. Cấu hình quy định thư viện (LibrarySettings)
        if (!await context.LibrarySettings.AnyAsync())
        {
            var setting = new LibrarySetting
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
            };
            await context.LibrarySettings.AddAsync(setting);
            await context.SaveChangesAsync();
        }

        // 2. Loại độc giả (ReaderTypes)
        var studentType = await context.ReaderTypes.FirstOrDefaultAsync(x => x.Code == "STUDENT");
        if (studentType == null)
        {
            studentType = new ReaderType
            {
                Id = Guid.NewGuid(),
                Code = "STUDENT",
                Name = "Sinh viên",
                MaxBorrowBooks = 5,
                LoanDays = 14,
                MaxRenewals = 2,
                RenewDays = 7,
                CardValidMonths = 48,
                Description = "Độc giả là sinh viên hệ chính quy các khóa",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await context.ReaderTypes.AddAsync(studentType);
        }

        var lecturerType = await context.ReaderTypes.FirstOrDefaultAsync(x => x.Code == "LECTURER");
        if (lecturerType == null)
        {
            lecturerType = new ReaderType
            {
                Id = Guid.NewGuid(),
                Code = "LECTURER",
                Name = "Giảng viên",
                MaxBorrowBooks = 10,
                LoanDays = 30,
                MaxRenewals = 3,
                RenewDays = 14,
                CardValidMonths = 60,
                Description = "Độc giả là giảng viên, cán bộ nghiên cứu",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await context.ReaderTypes.AddAsync(lecturerType);
        }

        var guestType = await context.ReaderTypes.FirstOrDefaultAsync(x => x.Code == "GUEST");
        if (guestType == null)
        {
            guestType = new ReaderType
            {
                Id = Guid.NewGuid(),
                Code = "GUEST",
                Name = "Khách ngoài",
                MaxBorrowBooks = 2,
                LoanDays = 7,
                MaxRenewals = 1,
                RenewDays = 3,
                CardValidMonths = 12,
                Description = "Độc giả tự do đăng ký thẻ thư viện",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await context.ReaderTypes.AddAsync(guestType);
        }
        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        // 3. Nhân viên & Quản trị viên (Staff)
        var configuredDefaultPassword = configuration?["SeedSettings:DefaultPassword"]
            ?? Environment.GetEnvironmentVariable("SeedSettings__DefaultPassword");

        var hasConfiguredPassword = !string.IsNullOrWhiteSpace(configuredDefaultPassword) &&
            !configuredDefaultPassword.Equals("your_seed_password", StringComparison.OrdinalIgnoreCase);

        var effectiveDefaultPassword = hasConfiguredPassword
            ? configuredDefaultPassword!.Trim()
            : Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

        var defaultPasswordHash = PasswordHasher.HashPassword(effectiveDefaultPassword);

        if (!await context.Staff.AnyAsync())
        {
            var defaultAdmin = new Staff
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@library.utc.edu.vn",
                FullName = "Quản trị viên Hệ thống",
                PasswordHash = defaultPasswordHash,
                Phone = "0901234567",
                Role = AppRoles.Admin,
                Status = EntityStatus.Active,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var librarian1 = new Staff
            {
                Id = Guid.NewGuid(),
                Username = "librarian01",
                Email = "thuthu01@library.utc.edu.vn",
                FullName = "Trần Thị Mai (Thủ thư)",
                PasswordHash = defaultPasswordHash,
                Phone = "0912345678",
                Role = AppRoles.Librarian,
                Status = EntityStatus.Active,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await context.Staff.AddRangeAsync([defaultAdmin, librarian1]);
            await context.SaveChangesAsync();
        }

        // Nạp danh sách tài khoản hạt giống từ IConfiguration (SeedSettings:Users) / biến môi trường .env
        var seedUsers = GetSeedUsers(configuration, effectiveDefaultPassword, hasConfiguredPassword);

        // Đồng bộ các tài khoản hạt giống vào bảng Staff (đảm bảo không trùng Username)
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

        var adminStaff = await context.Staff.FirstOrDefaultAsync(x => x.Role == AppRoles.Admin)
            ?? await context.Staff.FirstAsync();

        // 4. Kệ sách (Shelves)
        if (!await context.Shelves.AnyAsync())
        {
            var shelves = new List<Shelf>
            {
                new() { Id = Guid.NewGuid(), Code = "KE-IT-01", Description = "Kệ tầng 1 - Lập trình C#, .NET & Web", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Code = "KE-IT-02", Description = "Kệ tầng 1 - Cơ sở dữ liệu & Mạng máy tính", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Code = "KE-GT-01", Description = "Kệ tầng 2 - Kỹ thuật Công trình & Cầu đường", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Code = "KE-KT-01", Description = "Kệ tầng 2 - Kinh tế Vận tải & Logistics", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Code = "KE-CB-01", Description = "Kệ tầng 3 - Khoa học cơ bản & Toán ứng dụng", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Code = "KE-VH-01", Description = "Kệ tầng 3 - Văn học & Kỹ năng sống", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id }
            };
            await context.Shelves.AddRangeAsync(shelves);
            await context.SaveChangesAsync();
        }

        // 5. Thể loại sách (Categories)
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Id = Guid.NewGuid(), Name = "Công nghệ thông tin & Lập trình", Description = "Sách chuyên ngành CNTT, kỹ thuật phần mềm, cấu trúc dữ liệu", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "Cơ sở dữ liệu & Hệ thống thông tin", Description = "Sách SQL Server, PostgreSQL, thiết kế CSDL", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "Kỹ thuật Giao thông & Cầu đường", Description = "Tài liệu chuyên ngành kỹ thuật xây dựng công trình giao thông UTC", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "Kinh tế & Quản trị logistics", Description = "Kinh tế vận tải, quản lý chuỗi cung ứng, xuất nhập khẩu", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "Toán học & Khoa học cơ bản", Description = "Giải tích, đại số tuyến tính, xác suất thống kê", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "Văn học & Kỹ năng mềm", Description = "Tiểu thuyết, kỹ năng giao tiếp, phương pháp học đại học", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id }
            };
            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        // 6. Nhà xuất bản (Publishers)
        if (!await context.Publishers.AnyAsync())
        {
            var publishers = new List<Publisher>
            {
                new() { Id = Guid.NewGuid(), Name = "NXB Giáo Dục Việt Nam", Address = "81 Trần Hưng Đạo, Hoàn Kiếm, Hà Nội", Phone = "02438220801", Email = "contact@nxbgd.vn", Website = "https://nxbgd.vn", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "NXB Thông Tin & Truyền Thông", Address = "115 Trần Duy Hưng, Cầu Giấy, Hà Nội", Phone = "02435772138", Email = "nxb.tttt@mic.gov.vn", Website = "https://nxbthongtintruyenthong.vn", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "NXB Giao Thông Vận Tải", Address = "80B Trần Hưng Đạo, Hoàn Kiếm, Hà Nội", Phone = "02439423588", Email = "nxbgtvt@fpt.vn", Website = "https://nxbgtvt.vn", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "NXB Trẻ", Address = "161B Lý Chính Thắng, Quận 3, TP.HCM", Phone = "02839316289", Email = "hopthu@nxbtre.com.vn", Website = "https://nxbtre.com.vn", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), Name = "O'Reilly Media", Address = "1005 Gravenstein Hwy North, Sebastopol, CA", Phone = "+17078277000", Email = "info@oreilly.com", Website = "https://www.oreilly.com", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id }
            };
            await context.Publishers.AddRangeAsync(publishers);
            await context.SaveChangesAsync();
        }

        // 7. Tác giả (Authors)
        if (!await context.Authors.AnyAsync())
        {
            var authors = new List<Author>
            {
                new() { Id = Guid.NewGuid(), FullName = "Robert C. Martin (Uncle Bob)", Biography = "Tác giả nổi tiếng thế giới về Clean Code, Clean Architecture và phương pháp Agile.", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), FullName = "Martin Fowler", Biography = "Chuyên gia hàng đầu thế giới về kiến trúc phần mềm, Design Patterns và Refactoring.", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), FullName = "Andrew S. Tanenbaum", Biography = "Giáo sư khoa học máy tính, tác giả của bộ sách kinh điển Mạng máy tính và Hệ điều hành.", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), FullName = "GS.TS. Nguyễn Thanh Thủy", Biography = "Giảng viên cao cấp, tác giả nhiều giáo trình Cơ sở dữ liệu và Trí tuệ nhân tạo tại Việt Nam.", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), FullName = "PGS.TS. Trần Đình Quế", Biography = "Chuyên gia đầu ngành về kỹ thuật cầu đường và quy hoạch giao thông Trường ĐH Giao thông vận tải.", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), FullName = "Nguyễn Nhật Ánh", Biography = "Nhà văn Việt Nam nổi tiếng với nhiều tác phẩm văn học tuổi học trò được yêu thích.", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id }
            };
            await context.Authors.AddRangeAsync(authors);
            await context.SaveChangesAsync();
        }

        // 8. Độc giả (Readers)
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!await context.Readers.AnyAsync())
        {
            var readers = new List<Reader>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ReaderTypeId = studentType.Id,
                    Username = "student01",
                    Email = "nguyenhoanglong@student.utc.edu.vn",
                    PasswordHash = defaultPasswordHash,
                    FullName = "Nguyễn Hoàng Long",
                    Phone = "0987654321",
                    CardNo = "SV20260001",
                    MemberCode = "2026510001",
                    DateOfBirth = new DateOnly(2004, 5, 15),
                    Address = "Ký túc xá ĐH Giao Thông Vận Tải, Cầu Giấy, Hà Nội",
                    CardIssuedDate = today.AddMonths(-2),
                    CardExpiryDate = today.AddYears(4),
                    CardStatus = LibraryConstants.CardStatus.Active,
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaff.Id
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ReaderTypeId = studentType.Id,
                    Username = "student02",
                    Email = "lethuha@student.utc.edu.vn",
                    PasswordHash = defaultPasswordHash,
                    FullName = "Lê Thu Hà",
                    Phone = "0978123456",
                    CardNo = "SV20260002",
                    MemberCode = "2026510002",
                    DateOfBirth = new DateOnly(2004, 10, 20),
                    Address = "Số 12 Chùa Láng, Đống Đa, Hà Nội",
                    CardIssuedDate = today.AddMonths(-1),
                    CardExpiryDate = today.AddYears(4),
                    CardStatus = LibraryConstants.CardStatus.Active,
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaff.Id
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ReaderTypeId = lecturerType.Id,
                    Username = "lecturer01",
                    Email = "baopq@utc.edu.vn",
                    PasswordHash = defaultPasswordHash,
                    FullName = "TS. Phạm Quốc Bảo",
                    Phone = "0912987654",
                    CardNo = "GV20260001",
                    MemberCode = "GV001",
                    DateOfBirth = new DateOnly(1982, 3, 10),
                    Address = "Khoa Công Nghệ Thông Tin, ĐH Giao Thông Vận Tải",
                    CardIssuedDate = today.AddYears(-1),
                    CardExpiryDate = today.AddYears(4),
                    CardStatus = LibraryConstants.CardStatus.Active,
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaff.Id
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ReaderTypeId = studentType.Id,
                    Username = "student_locked",
                    Email = "vuminhduc@student.utc.edu.vn",
                    PasswordHash = defaultPasswordHash,
                    FullName = "Vũ Minh Đức (Thẻ bị khóa demo)",
                    Phone = "0934567890",
                    CardNo = "SV20260099",
                    MemberCode = "2026510099",
                    DateOfBirth = new DateOnly(2003, 8, 25),
                    Address = "Ngõ 1194 Đường Láng, Hà Nội",
                    CardIssuedDate = today.AddMonths(-6),
                    CardExpiryDate = today.AddYears(3),
                    CardStatus = LibraryConstants.CardStatus.Locked,
                    LockedAt = DateTimeOffset.UtcNow.AddDays(-5),
                    LockReason = "Làm mất sách chưa nộp tiền phạt",
                    Status = EntityStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = adminStaff.Id
                }
            };
            await context.Readers.AddRangeAsync(readers);
            await context.SaveChangesAsync();
        }

        // Đồng bộ các tài khoản hạt giống vào bảng Readers để kiểm thử Cổng Độc giả (/Account/Login)
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
                    DateOfBirth = new DateOnly(2000, 1, 1),
                    Address = "Hà Nội",
                    CardIssuedDate = today,
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

        // 9. Sách (Books), Tác giả sách (BookAuthors), và Bản sao (BookCopies)
        if (!await context.Books.AnyAsync())
        {
            var catIt = await context.Categories.FirstAsync(x => x.Name.Contains("Công nghệ thông tin"));
            var catDb = await context.Categories.FirstAsync(x => x.Name.Contains("Cơ sở dữ liệu"));
            var catGt = await context.Categories.FirstAsync(x => x.Name.Contains("Kỹ thuật Giao thông"));
            var catVh = await context.Categories.FirstAsync(x => x.Name.Contains("Văn học"));

            var pubOreilly = await context.Publishers.FirstAsync(x => x.Name.Contains("O'Reilly"));
            var pubGtvt = await context.Publishers.FirstAsync(x => x.Name.Contains("Giao Thông Vận Tải"));
            var pubTttt = await context.Publishers.FirstAsync(x => x.Name.Contains("Thông Tin"));
            var pubTre = await context.Publishers.FirstAsync(x => x.Name.Contains("Trẻ"));

            var authorBob = await context.Authors.FirstAsync(x => x.FullName.Contains("Uncle Bob"));
            var authorFowler = await context.Authors.FirstAsync(x => x.FullName.Contains("Martin Fowler"));
            var authorTanenbaum = await context.Authors.FirstAsync(x => x.FullName.Contains("Tanenbaum"));
            var authorThuy = await context.Authors.FirstAsync(x => x.FullName.Contains("Nguyễn Thanh Thủy"));
            var authorQue = await context.Authors.FirstAsync(x => x.FullName.Contains("Trần Đình Quế"));
            var authorAnh = await context.Authors.FirstAsync(x => x.FullName.Contains("Nguyễn Nhật Ánh"));

            var shelfIt1 = await context.Shelves.FirstAsync(x => x.Code == "KE-IT-01");
            var shelfIt2 = await context.Shelves.FirstAsync(x => x.Code == "KE-IT-02");
            var shelfGt = await context.Shelves.FirstAsync(x => x.Code == "KE-GT-01");
            var shelfVh = await context.Shelves.FirstAsync(x => x.Code == "KE-VH-01");

            var bookCleanCode = new Book
            {
                Id = Guid.NewGuid(),
                Title = "Clean Code: A Handbook of Agile Software Craftsmanship",
                Isbn = "978-0132350884",
                CategoryId = catIt.Id,
                PublisherId = pubOreilly.Id,
                PublicationYear = 2020,
                Edition = "Tái bản lần thứ 2",
                Language = "Tiếng Anh",
                PageCount = 464,
                Description = "Cuốn sách gối đầu giường của mọi lập trình viên chuyên nghiệp, hướng dẫn viết mã sạch, dễ đọc, dễ bảo trì.",
                CoverUrl = "/images/books/clean-code.jpg",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var bookCleanArch = new Book
            {
                Id = Guid.NewGuid(),
                Title = "Clean Architecture: A Craftsman's Guide to Software Structure",
                Isbn = "978-0134494166",
                CategoryId = catIt.Id,
                PublisherId = pubOreilly.Id,
                PublicationYear = 2021,
                Edition = "Tái bản lần 1",
                Language = "Tiếng Anh",
                PageCount = 432,
                Description = "Nguyên lý kiến trúc phần mềm hướng đối tượng, tách biệt tầng nghiệp vụ, tối ưu khả năng kiểm thử.",
                CoverUrl = "/images/books/clean-architecture.jpg",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var bookRefactoring = new Book
            {
                Id = Guid.NewGuid(),
                Title = "Refactoring: Improving the Design of Existing Code",
                Isbn = "978-0201485677",
                CategoryId = catIt.Id,
                PublisherId = pubOreilly.Id,
                PublicationYear = 2019,
                Edition = "Tái bản lần 2",
                Language = "Tiếng Anh",
                PageCount = 448,
                Description = "Kỹ nghệ tái cấu trúc mã nguồn mà không làm thay đổi hành vi bên ngoài của hệ thống.",
                CoverUrl = "/images/books/refactoring.jpg",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var bookSqlDb = new Book
            {
                Id = Guid.NewGuid(),
                Title = "Giáo trình Thiết kế Hệ thống Cơ sở dữ liệu và SQL Server",
                Isbn = "978-6048031234",
                CategoryId = catDb.Id,
                PublisherId = pubTttt.Id,
                PublicationYear = 2023,
                Edition = "Xuất bản lần đầu",
                Language = "Tiếng Việt",
                PageCount = 380,
                Description = "Giáo trình chuẩn cho sinh viên ngành CNTT về mô hình quan hệ, chuẩn hóa dữ liệu và truy vấn T-SQL tối ưu.",
                CoverUrl = "/images/books/csdl-sqlserver.jpg",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var bookNetworks = new Book
            {
                Id = Guid.NewGuid(),
                Title = "Giáo trình Mạng máy tính căn bản & nâng cao",
                Isbn = "978-0132126953",
                CategoryId = catIt.Id,
                PublisherId = pubTttt.Id,
                PublicationYear = 2022,
                Edition = "Tái bản lần 5",
                Language = "Tiếng Việt",
                PageCount = 520,
                Description = "Kiến thức nền tảng về mô hình OSI, TCP/IP, định tuyến và an toàn mạng máy tính.",
                CoverUrl = "/images/books/computer-networks.jpg",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var bookTrafficEng = new Book
            {
                Id = Guid.NewGuid(),
                Title = "Quy hoạch và Tổ chức Giao thông Đô thị thông minh",
                Isbn = "978-6047612345",
                CategoryId = catGt.Id,
                PublisherId = pubGtvt.Id,
                PublicationYear = 2024,
                Edition = "Xuất bản lần 1",
                Language = "Tiếng Việt",
                PageCount = 310,
                Description = "Giáo trình chuyên ngành giao thông vận tải UTC, phân tích luồng xe, nút giao và giải pháp giảm ùn tắc đô thị.",
                CoverUrl = "/images/books/giao-thong-do-thi.jpg",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var bookLiterature = new Book
            {
                Id = Guid.NewGuid(),
                Title = "Tôi Thấy Hoa Vàng Trên Cỏ Xanh",
                Isbn = "978-6042081234",
                CategoryId = catVh.Id,
                PublisherId = pubTre.Id,
                PublicationYear = 2022,
                Edition = "Tái bản lần thứ 45",
                Language = "Tiếng Việt",
                PageCount = 378,
                Description = "Tác phẩm văn học đoạt giải, bức tranh tuổi thơ mộc mạc làng quê Việt Nam.",
                CoverUrl = "/images/books/hoa-vang-co-xanh.jpg",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var books = new List<Book> { bookCleanCode, bookCleanArch, bookRefactoring, bookSqlDb, bookNetworks, bookTrafficEng, bookLiterature };
            await context.Books.AddRangeAsync(books);
            await context.SaveChangesAsync();

            // Liên kết tác giả sách (BookAuthors)
            var bookAuthors = new List<BookAuthor>
            {
                new() { BookId = bookCleanCode.Id, AuthorId = authorBob.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { BookId = bookCleanArch.Id, AuthorId = authorBob.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { BookId = bookRefactoring.Id, AuthorId = authorFowler.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { BookId = bookSqlDb.Id, AuthorId = authorThuy.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { BookId = bookNetworks.Id, AuthorId = authorTanenbaum.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { BookId = bookTrafficEng.Id, AuthorId = authorQue.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { BookId = bookLiterature.Id, AuthorId = authorAnh.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id }
            };
            await context.BookAuthors.AddRangeAsync(bookAuthors);
            await context.SaveChangesAsync();

            // Sinh các bản sao sách (BookCopies) - Tất cả khởi tạo AVAILABLE để thỏa mãn trigger mượn
            var bookCopies = new List<BookCopy>
            {
                new() { Id = Guid.NewGuid(), BookId = bookCleanCode.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0001", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 350000m, AcquiredDate = today.AddMonths(-5), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookCleanCode.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0002", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 350000m, AcquiredDate = today.AddMonths(-5), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookCleanCode.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0003", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 350000m, AcquiredDate = today.AddMonths(-5), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },

                new() { Id = Guid.NewGuid(), BookId = bookCleanArch.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0004", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 380000m, AcquiredDate = today.AddMonths(-4), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookCleanArch.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0005", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 380000m, AcquiredDate = today.AddMonths(-4), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookCleanArch.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0006", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 380000m, AcquiredDate = today.AddMonths(-4), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },

                new() { Id = Guid.NewGuid(), BookId = bookRefactoring.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0007", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 420000m, AcquiredDate = today.AddMonths(-3), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookRefactoring.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0008", CopyStatus = LibraryConstants.CopyStatus.Reserved, Price = 420000m, AcquiredDate = today.AddMonths(-3), Note = "Đang giữ chỗ đặt trước", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },

                new() { Id = Guid.NewGuid(), BookId = bookSqlDb.Id, ShelfId = shelfIt2.Id, Barcode = "BC-202610-0009", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 120000m, AcquiredDate = today.AddMonths(-6), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookSqlDb.Id, ShelfId = shelfIt2.Id, Barcode = "BC-202610-0010", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 120000m, AcquiredDate = today.AddMonths(-6), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookSqlDb.Id, ShelfId = shelfIt2.Id, Barcode = "BC-202610-0011", CopyStatus = LibraryConstants.CopyStatus.Damaged, Price = 120000m, AcquiredDate = today.AddMonths(-6), Note = "Rách gáy sách, chờ tu sửa", Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },

                new() { Id = Guid.NewGuid(), BookId = bookNetworks.Id, ShelfId = shelfIt2.Id, Barcode = "BC-202610-0012", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 165000m, AcquiredDate = today.AddMonths(-2), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookNetworks.Id, ShelfId = shelfIt2.Id, Barcode = "BC-202610-0013", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 165000m, AcquiredDate = today.AddMonths(-2), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },

                new() { Id = Guid.NewGuid(), BookId = bookTrafficEng.Id, ShelfId = shelfGt.Id, Barcode = "BC-202610-0014", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 110000m, AcquiredDate = today.AddMonths(-1), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookTrafficEng.Id, ShelfId = shelfGt.Id, Barcode = "BC-202610-0015", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 110000m, AcquiredDate = today.AddMonths(-1), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },

                new() { Id = Guid.NewGuid(), BookId = bookLiterature.Id, ShelfId = shelfVh.Id, Barcode = "BC-202610-0016", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 95000m, AcquiredDate = today.AddMonths(-4), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id },
                new() { Id = Guid.NewGuid(), BookId = bookLiterature.Id, ShelfId = shelfVh.Id, Barcode = "BC-202610-0017", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 95000m, AcquiredDate = today.AddMonths(-4), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaff.Id }
            };
            await context.BookCopies.AddRangeAsync(bookCopies);
            await context.SaveChangesAsync();
        }

        // 9.2. Bổ sung tài liệu mẫu Luận văn & Đồ án, Tạp chí khoa học (Idempotent cho DB hiện hữu)
        await EnsureThesisAndJournalBooksAsync(context, adminStaff.Id, today);

        // 9.5. Khởi tạo Bộ sưu tập Hình ảnh Sách mẫu (BookImages: Ảnh bìa trước + Mục lục + Trang đọc thử) cho các sách chưa có ảnh
        await SeedSampleBookImagesAsync(context, adminStaff.Id);

        // 10. Phiếu mượn mẫu (BorrowSlips) & Chi tiết mượn (BorrowDetails)
        if (!await context.BorrowSlips.AnyAsync())
        {
            var student1 = await context.Readers.FirstAsync(x => x.Username == "student01");
            var student2 = await context.Readers.FirstAsync(x => x.Username == "student02");

            var copyCleanCodeBorrowed = await context.BookCopies.FirstAsync(x => x.Barcode == "BC-202610-0003");
            var copyCleanArchOverdue = await context.BookCopies.FirstAsync(x => x.Barcode == "BC-202610-0006");
            copyCleanCodeBorrowed.CopyStatus = LibraryConstants.CopyStatus.Borrowed;
            copyCleanArchOverdue.CopyStatus = LibraryConstants.CopyStatus.Borrowed;

            // Phiếu 1: Đang mượn hợp lệ (Chưa đến hạn)
            var slipActive = new BorrowSlip
            {
                Id = Guid.NewGuid(),
                ReaderId = student1.Id,
                IssuedBy = adminStaff.Id,
                BorrowedAt = DateTimeOffset.UtcNow.AddDays(-5),
                Note = "Sinh viên mượn phục vụ làm đồ án tốt nghiệp",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var detailActive = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipActive.Id,
                CopyId = copyCleanCodeBorrowed.Id,
                DueDate = today.AddDays(9), // Còn 9 ngày nữa mới hết hạn
                RenewCount = 0,
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            // Phiếu 2: Đang mượn nhưng ĐÃ QUÁ HẠN 3 NGÀY (phục vụ demo tính phạt quá hạn)
            var slipOverdue = new BorrowSlip
            {
                Id = Guid.NewGuid(),
                ReaderId = student2.Id,
                IssuedBy = adminStaff.Id,
                BorrowedAt = DateTimeOffset.UtcNow.AddDays(-17),
                Note = "Mượn nghiên cứu kiến trúc phần mềm",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            var detailOverdue = new BorrowDetail
            {
                Id = Guid.NewGuid(),
                SlipId = slipOverdue.Id,
                CopyId = copyCleanArchOverdue.Id,
                DueDate = today.AddDays(-3), // Hạn trả là 3 ngày trước -> Quá hạn 3 ngày
                RenewCount = 0,
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };

            await context.BorrowSlips.AddRangeAsync([slipActive, slipOverdue]);
            await context.BorrowDetails.AddRangeAsync([detailActive, detailOverdue]);
            await context.SaveChangesAsync();

            // 11. Phiếu phạt mẫu (Fines)
            // Lưu ý: fine_status = UNPAID thì handled_by = NULL (thỏa mãn constraint ck_fines_handler)
            var fineOverdue = new Fine
            {
                Id = Guid.NewGuid(),
                BorrowDetailId = detailOverdue.Id,
                FineType = LibraryConstants.FineType.Overdue,
                OverdueDays = 3,
                Amount = 3 * 2000m, // 3 ngày x 2.000 đ = 6.000 đ
                FineStatus = LibraryConstants.FineStatus.Unpaid,
                HandledBy = null,
                PaidAt = null,
                PaymentMethod = null,
                Note = "Phạt quá hạn 3 ngày (tự động tính theo quy định thư viện)",
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };
            await context.Fines.AddAsync(fineOverdue);
            await context.SaveChangesAsync();
        }

        // 12. Đặt trước mẫu (Reservations)
        if (!await context.Reservations.AnyAsync())
        {
            var student1 = await context.Readers.FirstAsync(x => x.Username == "student01");
            var bookRefactoring = await context.Books.FirstAsync(x => x.Title.Contains("Refactoring"));
            var copyReserved = await context.BookCopies.FirstAsync(x => x.Barcode == "BC-202610-0008");

            var resReady = new Reservation
            {
                Id = Guid.NewGuid(),
                ReaderId = student1.Id,
                BookId = bookRefactoring.Id,
                AssignedCopyId = copyReserved.Id,
                ReservationStatus = LibraryConstants.ReservationStatus.Ready,
                ReservedAt = DateTimeOffset.UtcNow.AddHours(-12),
                ReadyAt = DateTimeOffset.UtcNow.AddHours(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(46), // Hạn giữ 48h
                ClosedAt = null,
                Status = EntityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminStaff.Id
            };
            await context.Reservations.AddAsync(resReady);
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureThesisAndJournalBooksAsync(ApplicationDbContext context, Guid adminStaffId, DateOnly today)
    {
        if (await context.Books.AnyAsync(b => b.Isbn == "978-6047625011"))
        {
            return;
        }

        var catIt = await context.Categories.FirstOrDefaultAsync(x => x.Name.Contains("Công nghệ thông tin"))
            ?? await context.Categories.FirstAsync();
        var catGt = await context.Categories.FirstOrDefaultAsync(x => x.Name.Contains("Kỹ thuật Giao thông"))
            ?? catIt;
        var catKt = await context.Categories.FirstOrDefaultAsync(x => x.Name.Contains("Kinh tế"))
            ?? catGt;

        var pubGtvt = await context.Publishers.FirstOrDefaultAsync(x => x.Name.Contains("Giao Thông Vận Tải"))
            ?? await context.Publishers.FirstAsync();
        var pubTttt = await context.Publishers.FirstOrDefaultAsync(x => x.Name.Contains("Thông Tin"))
            ?? pubGtvt;

        var authorThuy = await context.Authors.FirstOrDefaultAsync(x => x.FullName.Contains("Nguyễn Thanh Thủy"))
            ?? await context.Authors.FirstAsync();
        var authorQue = await context.Authors.FirstOrDefaultAsync(x => x.FullName.Contains("Trần Đình Quế"))
            ?? authorThuy;

        var shelfIt1 = await context.Shelves.FirstOrDefaultAsync(x => x.Code == "KE-IT-01")
            ?? await context.Shelves.FirstAsync();
        var shelfGt = await context.Shelves.FirstOrDefaultAsync(x => x.Code == "KE-GT-01")
            ?? shelfIt1;
        var shelfKt = await context.Shelves.FirstOrDefaultAsync(x => x.Code == "KE-KT-01")
            ?? shelfGt;

        var thesisAiTraffic = new Book
        {
            Id = Guid.NewGuid(),
            Title = "Luận văn Thạc sĩ: Ứng dụng học sâu dự báo lưu lượng giao thông đô thị Hà Nội",
            Isbn = "978-6047625011",
            CategoryId = catIt.Id,
            PublisherId = pubGtvt.Id,
            PublicationYear = 2025,
            Edition = "Luận văn Thạc sĩ Kỹ thuật",
            Language = "Tiếng Việt",
            PageCount = 128,
            Description = "Luận văn thạc sĩ nghiên cứu mô hình mạng nơ-ron đồ thị không-thời gian (ST-GNN) phân tích camera giao thông và dự báo ùn tắc tại các nút giao trọng điểm.",
            Status = EntityStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            CreatedBy = adminStaffId
        };

        var thesisBridgeDesign = new Book
        {
            Id = Guid.NewGuid(),
            Title = "Đồ án Tốt nghiệp: Thiết kế kết cấu nhịp cầu dây văng vượt sông lớn theo tiêu chuẩn AASHTO LRFD",
            Isbn = "978-6047625028",
            CategoryId = catGt.Id,
            PublisherId = pubGtvt.Id,
            PublicationYear = 2025,
            Edition = "Đồ án Tốt nghiệp Kỹ sư",
            Language = "Tiếng Việt",
            PageCount = 165,
            Description = "Đồ án tốt nghiệp xuất sắc ngành Kỹ thuật Xây dựng Cầu - Đường bộ, mô phỏng nội lực dầm chủ, trụ tháp và cáp văng bằng phần mềm MIDAS Civil.",
            Status = EntityStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            CreatedBy = adminStaffId
        };

        var journalTransportSci = new Book
        {
            Id = Guid.NewGuid(),
            Title = "Tạp chí Khoa học Giao thông Vận tải - Chuyên san Công nghệ số & Hạ tầng thông minh",
            Isbn = "978-6047625035",
            CategoryId = catGt.Id,
            PublisherId = pubGtvt.Id,
            PublicationYear = 2025,
            Edition = "Tập 76, Số 02 (2025)",
            Language = "Tiếng Việt",
            PageCount = 142,
            Description = "Tạp chí khoa học định kỳ của Trường Đại học Giao thông Vận tải (ISSN 1859-2724) công bố các công trình nghiên cứu mới về BIM, vật liệu mặt đường bền vững và điều khiển giao thông thông minh.",
            Status = EntityStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedBy = adminStaffId
        };

        var journalLogisticsConf = new Book
        {
            Id = Guid.NewGuid(),
            Title = "Kỷ yếu Hội nghị Khoa học Công nghệ Giao thông Vận tải & Logistics xanh lần thứ VII",
            Isbn = "978-6047625042",
            CategoryId = catKt.Id,
            PublisherId = pubTttt.Id,
            PublicationYear = 2024,
            Edition = "Kỷ yếu Hội nghị Khoa học",
            Language = "Tiếng Việt",
            PageCount = 296,
            Description = "Tập hợp các bài báo khoa học phản biện độc lập về chuyển đổi xanh trong vận tải đa phương thức, tối ưu hóa kho bãi và chuỗi cung ứng số tại Việt Nam.",
            Status = EntityStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            CreatedBy = adminStaffId
        };

        await context.Books.AddRangeAsync([thesisAiTraffic, thesisBridgeDesign, journalTransportSci, journalLogisticsConf]);
        await context.SaveChangesAsync();

        await context.BookAuthors.AddRangeAsync(
        [
            new BookAuthor { BookId = thesisAiTraffic.Id, AuthorId = authorThuy.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId },
            new BookAuthor { BookId = thesisBridgeDesign.Id, AuthorId = authorQue.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId },
            new BookAuthor { BookId = journalTransportSci.Id, AuthorId = authorQue.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId },
            new BookAuthor { BookId = journalLogisticsConf.Id, AuthorId = authorThuy.Id, AuthorOrder = 1, Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId }
        ]);

        var existingBarcodes = new HashSet<string>(await context.BookCopies.Select(c => c.Barcode).ToListAsync(), StringComparer.OrdinalIgnoreCase);
        var newCopies = new List<BookCopy>
        {
            new() { Id = Guid.NewGuid(), BookId = thesisAiTraffic.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0018", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 85000m, AcquiredDate = today.AddDays(-20), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId },
            new() { Id = Guid.NewGuid(), BookId = thesisAiTraffic.Id, ShelfId = shelfIt1.Id, Barcode = "BC-202610-0019", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 85000m, AcquiredDate = today.AddDays(-20), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId },
            new() { Id = Guid.NewGuid(), BookId = thesisBridgeDesign.Id, ShelfId = shelfGt.Id, Barcode = "BC-202610-0020", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 90000m, AcquiredDate = today.AddDays(-18), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId },
            new() { Id = Guid.NewGuid(), BookId = journalTransportSci.Id, ShelfId = shelfGt.Id, Barcode = "BC-202610-0021", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 75000m, AcquiredDate = today.AddDays(-15), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId },
            new() { Id = Guid.NewGuid(), BookId = journalTransportSci.Id, ShelfId = shelfGt.Id, Barcode = "BC-202610-0022", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 75000m, AcquiredDate = today.AddDays(-15), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId },
            new() { Id = Guid.NewGuid(), BookId = journalLogisticsConf.Id, ShelfId = shelfKt.Id, Barcode = "BC-202610-0023", CopyStatus = LibraryConstants.CopyStatus.Available, Price = 115000m, AcquiredDate = today.AddDays(-12), Status = EntityStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = adminStaffId }
        };

        await context.BookCopies.AddRangeAsync(newCopies.Where(c => !existingBarcodes.Contains(c.Barcode)));
        await context.SaveChangesAsync();
    }

    private static async Task SeedSampleBookImagesAsync(ApplicationDbContext context, Guid adminStaffId)
    {
        var cwd = Directory.GetCurrentDirectory();
        var webRoot = Directory.Exists(Path.Combine(cwd, "wwwroot"))
            ? Path.Combine(cwd, "wwwroot")
            : Path.Combine(cwd, "web_mvc_management", "wwwroot");

        var seedDir = Path.Combine(webRoot, "uploads", "books", "seed");
        Directory.CreateDirectory(seedDir);

        var booksWithImages = new HashSet<Guid>(await context.BookImages.Select(img => img.BookId).Distinct().ToListAsync());

        var books = await context.Books
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync();

        var palettes = new (string Bg1, string Bg2, string Accent)[]
        {
            ("#0F2A4A", "#1E4E79", "#F59E0B"),
            ("#143D34", "#1F6354", "#34D399"),
            ("#311B47", "#4E2A70", "#C084FC"),
            ("#4A1D1F", "#7A2E32", "#F87171"),
            ("#1E293B", "#334155", "#38BDF8"),
            ("#3B2F1E", "#66502C", "#FBBF24"),
            ("#1B3B2B", "#2D6A4F", "#74C69D")
        };

        var imagesToInsert = new List<BookImage>();

        for (var i = 0; i < books.Count; i++)
        {
            var book = books[i];
            if (booksWithImages.Contains(book.Id))
            {
                continue;
            }
            var palette = palettes[i % palettes.Length];
            var slug = $"book-{i + 1:00}";

            // 1. Ảnh bìa trước (COVER)
            var coverRelUrl = $"/uploads/books/seed/{slug}-cover.svg";
            var coverSvg = BuildCoverSvg(book.Title, book.Isbn ?? "978-604-000", book.Publisher?.Name ?? "Thư Viện UTC", palette.Bg1, palette.Bg2, palette.Accent);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-cover.svg"), coverSvg, Encoding.UTF8);

            // 2. Ảnh Mục lục (TABLE_OF_CONTENTS)
            var tocRelUrl = $"/uploads/books/seed/{slug}-toc.svg";
            var tocSvg = BuildPageSvg(
                book.Title,
                "MỤC LỤC CHI TIẾT (TABLE OF CONTENTS)",
                [
                    "Chương 1: Tổng quan kiến thức nền tảng & Nguyên lý cốt lõi .......... Trang 05",
                    "Chương 2: Phân tích cấu trúc, mô hình hóa & Tiêu chuẩn kỹ thuật .... Trang 48",
                    "Chương 3: Thiết kế hệ thống & Các mẫu thực hành tốt nhất ........... Trang 112",
                    "Chương 4: Nghiên cứu tình huống thực tế (Case Studies) .............. Trang 195",
                    "Chương 5: Câu hỏi ôn tập, Bài tập thực hành & Phụ lục tra cứu ...... Trang 280"
                ],
                "Trang i — Mục lục",
                palette.Bg1);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-toc.svg"), tocSvg, Encoding.UTF8);

            // 3. Ảnh Trang đọc thử 1 (PREVIEW_PAGE)
            var p1RelUrl = $"/uploads/books/seed/{slug}-page-01.svg";
            var p1Svg = BuildPageSvg(
                book.Title,
                "CHƯƠNG 1: DẪN NHẬP & NGUYÊN LÝ CỐT LÕI",
                [
                    "1.1. Đặt vấn đề và bối cảnh ứng dụng trong thực tiễn hiện đại:",
                    book.Description ?? "Tài liệu chuyên khảo phục vụ đào tạo và nghiên cứu tại Trường Đại học Giao thông Vận tải.",
                    "1.2. Các tiêu chuẩn chất lượng cần tuân thủ:",
                    "• Tính chính xác, tường minh và khả năng mở rộng dài hạn.",
                    "• Tối ưu hóa hiệu năng vận hành và đảm bảo an toàn dữ liệu."
                ],
                "Trang 12 — Đọc thử",
                palette.Bg1);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-page-01.svg"), p1Svg, Encoding.UTF8);

            // 4. Ảnh Trang đọc thử 2 (PREVIEW_PAGE)
            var p2RelUrl = $"/uploads/books/seed/{slug}-page-02.svg";
            var p2Svg = BuildPageSvg(
                book.Title,
                "CHƯƠNG 2: SƠ ĐỒ KIẾN TRÚC & BÀI TẬP THỰC HÀNH",
                [
                    "2.1. Quy trình triển khai tiêu chuẩn qua 4 giai đoạn:",
                    "[Khảo sát & Đặc tả]  --->  [Thiết kế Mô hình]  --->  [Thực thi]  --->  [Kiểm định]",
                    "2.2. Ghi chú dành cho sinh viên và học viên cao học:",
                    "• Sinh viên đối chiếu mã kệ " + (book.Isbn ?? "UTC") + " tại phòng đọc chuyên ngành.",
                    "• Kết hợp làm bài tập thực hành cuối mỗi chương để nắm vững lý thuyết."
                ],
                "Trang 25 — Đọc thử",
                palette.Bg1);
            await File.WriteAllTextAsync(Path.Combine(seedDir, $"{slug}-page-02.svg"), p2Svg, Encoding.UTF8);

            // Cập nhật CoverUrl của đầu sách trỏ về ảnh bìa thực tế vừa tạo
            book.CoverUrl = coverRelUrl;

            imagesToInsert.AddRange(
            [
                new BookImage
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    ImageUrl = coverRelUrl,
                    PublicId = $"seed/{slug}-cover",
                    ImageType = LibraryConstants.BookImageType.Cover,
                    Caption = "Ảnh bìa chính",
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
                    ImageUrl = tocRelUrl,
                    PublicId = $"seed/{slug}-toc",
                    ImageType = LibraryConstants.BookImageType.TableOfContents,
                    Caption = "Mục lục sách",
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
                    ImageUrl = p1RelUrl,
                    PublicId = $"seed/{slug}-page-01",
                    ImageType = LibraryConstants.BookImageType.PreviewPage,
                    Caption = "Trang đọc thử — Chương 1 (Tr. 12)",
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
                    ImageUrl = p2RelUrl,
                    PublicId = $"seed/{slug}-page-02",
                    ImageType = LibraryConstants.BookImageType.PreviewPage,
                    Caption = "Trang đọc thử — Chương 2 (Tr. 25)",
                    DisplayOrder = 3,
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
            await context.SaveChangesAsync();
        }
    }

    private static string BuildCoverSvg(string title, string isbn, string publisher, string bg1, string bg2, string accent)
    {
        var lines = WrapText(title, 24, 3);
        var titleTspans = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            var dy = i == 0 ? "0" : "42";
            titleTspans.Append($"<tspan x=\"60\" dy=\"{dy}\">{WebUtility.HtmlEncode(lines[i])}</tspan>");
        }

        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 840" width="600" height="840">
              <defs>
                <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0%" stop-color="{{bg1}}"/>
                  <stop offset="100%" stop-color="{{bg2}}"/>
                </linearGradient>
              </defs>
              <rect width="600" height="840" rx="18" fill="url(#bg)"/>
              <rect x="24" y="24" width="552" height="792" rx="12" fill="none" stroke="{{accent}}" stroke-width="2" stroke-opacity="0.45"/>
              <rect x="60" y="70" width="170" height="32" rx="6" fill="{{accent}}"/>
              <text x="145" y="92" font-family="Segoe UI, Arial, sans-serif" font-size="14" font-weight="700" fill="#0F172A" text-anchor="middle">THƯ VIỆN ĐH GTVT</text>
              <text x="60" y="210" font-family="Georgia, Times New Roman, serif" font-size="33" font-weight="700" fill="#FFFFFF">{{titleTspans}}</text>
              <line x1="60" y1="365" x2="240" y2="365" stroke="{{accent}}" stroke-width="4"/>
              <circle cx="460" cy="540" r="95" fill="{{accent}}" fill-opacity="0.12" stroke="{{accent}}" stroke-width="2" stroke-opacity="0.4"/>
              <text x="60" y="720" font-family="Segoe UI, Arial, sans-serif" font-size="18" font-weight="600" fill="#E2E8F0">{{WebUtility.HtmlEncode(publisher)}}</text>
              <text x="60" y="752" font-family="Consolas, monospace" font-size="15" fill="{{accent}}">ISBN: {{WebUtility.HtmlEncode(isbn)}}</text>
            </svg>
            """;
    }

    private static string BuildPageSvg(string bookTitle, string heading, IReadOnlyList<string> paragraphs, string footerPage, string accentColor)
    {
        var bodyBuilder = new StringBuilder();
        var currentY = 215;

        foreach (var para in paragraphs)
        {
            var wrapped = WrapText(para, 58, 3);
            foreach (var line in wrapped)
            {
                bodyBuilder.AppendLine($"<text x=\"65\" y=\"{currentY}\" font-family=\"Georgia, serif\" font-size=\"16\" fill=\"#334155\">{WebUtility.HtmlEncode(line)}</text>");
                currentY += 28;
            }
            currentY += 18;
        }

        var shortTitle = bookTitle.Length > 48 ? bookTitle[..45] + "..." : bookTitle;

        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 840" width="600" height="840">
              <rect width="600" height="840" rx="12" fill="#FCFBF7" stroke="#CBD5E1" stroke-width="3"/>
              <rect x="36" y="36" width="528" height="768" fill="none" stroke="#E2E8F0" stroke-width="1.5"/>
              <text x="65" y="78" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="600" fill="#64748B">{{WebUtility.HtmlEncode(shortTitle)}}</text>
              <line x1="65" y1="92" x2="535" y2="92" stroke="#CBD5E1" stroke-width="1.5"/>
              <rect x="65" y="122" width="470" height="44" rx="6" fill="{{accentColor}}" fill-opacity="0.08"/>
              <text x="80" y="150" font-family="Segoe UI, Arial, sans-serif" font-size="16" font-weight="700" fill="{{accentColor}}">{{WebUtility.HtmlEncode(heading)}}</text>
              {{bodyBuilder}}
              <line x1="65" y1="745" x2="535" y2="745" stroke="#E2E8F0" stroke-width="1"/>
              <text x="300" y="775" font-family="Segoe UI, Arial, sans-serif" font-size="14" font-weight="600" fill="#475569" text-anchor="middle">{{WebUtility.HtmlEncode(footerPage)}}</text>
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
