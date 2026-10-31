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
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
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
        await context.SaveChangesAsync();

        // 3. Nhân viên & Quản trị viên (Staff)
        var defaultPassword = Environment.GetEnvironmentVariable("DEFAULT_SEED_PASSWORD") ?? "123456";
        var defaultPasswordHash = PasswordHasher.HashPassword(defaultPassword);

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

        // Nạp cấu hình 3 tài khoản hạt giống từ biến môi trường .env (Email, Role, Mật khẩu tùy biến)
        var seedUsers = new List<SeedUserSetting>
        {
            GetSeedUser(1, "anhnqth01267@fpt.edu.vn", AppRoles.Admin, defaultPassword, "Nguyễn Quốc Anh (Admin FPT)", "anhnq_admin", "GV20268881", "GV8881"),
            GetSeedUser(2, "quocanhn352@gmail.com", AppRoles.Admin, defaultPassword, "Nguyễn Quốc Anh (Admin Tổng)", "quocanh_admin", "SV20268888", "SV8888"),
            GetSeedUser(3, "attendancestudent86@gmail.com", AppRoles.Librarian, defaultPassword, "Cán Bộ Thủ Thư (Attendance)", "attendance_librarian", "SV20268889", "SV8889")
        };

        // Đồng bộ 3 tài khoản vào bảng Staff
        foreach (var u in seedUsers)
        {
            var staffUser = await context.Staff.FirstOrDefaultAsync(x => x.Email.ToLower() == u.Email);
            var passHash = PasswordHasher.HashPassword(u.Password);

            if (staffUser == null)
            {
                staffUser = new Staff
                {
                    Id = Guid.NewGuid(),
                    Username = u.Username,
                    Email = u.Email,
                    FullName = u.FullName,
                    PasswordHash = passHash,
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
                staffUser.Role = u.Role;
                staffUser.FullName = u.FullName;
                staffUser.PasswordHash = passHash;
                staffUser.Status = EntityStatus.Active;
                if (string.IsNullOrWhiteSpace(staffUser.SecurityStamp))
                {
                    staffUser.SecurityStamp = Guid.NewGuid().ToString("N");
                }
            }
        }
        await context.SaveChangesAsync();

        Staff adminStaff = await context.Staff.FirstOrDefaultAsync(x => x.Role == AppRoles.Admin)
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

        // Đồng bộ 3 tài khoản hạt giống vào bảng Readers để kiểm thử Cổng Độc giả (/Account/Login)
        foreach (var u in seedUsers)
        {
            var readerUser = await context.Readers.FirstOrDefaultAsync(x => x.Email.ToLower() == u.Email);
            var passHash = PasswordHasher.HashPassword(u.Password);
            var targetTypeId = u.Role == AppRoles.Admin ? lecturerType.Id : studentType.Id;

            if (readerUser == null)
            {
                readerUser = new Reader
                {
                    Id = Guid.NewGuid(),
                    ReaderTypeId = targetTypeId,
                    Username = u.Username.Replace("_admin", "_reader").Replace("_librarian", "_student"),
                    Email = u.Email,
                    PasswordHash = passHash,
                    FullName = u.FullName,
                    Phone = "0981112233",
                    CardNo = u.CardNo,
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
                readerUser.PasswordHash = passHash;
                readerUser.Status = EntityStatus.Active;
                readerUser.CardStatus = LibraryConstants.CardStatus.Active;
                if (string.IsNullOrWhiteSpace(readerUser.SecurityStamp))
                {
                    readerUser.SecurityStamp = Guid.NewGuid().ToString("N");
                }
            }
        }
        await context.SaveChangesAsync();

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

        // 10. Phiếu mượn mẫu (BorrowSlips) & Chi tiết mượn (BorrowDetails)
        if (!await context.BorrowSlips.AnyAsync())
        {
            var student1 = await context.Readers.FirstAsync(x => x.Username == "student01");
            var student2 = await context.Readers.FirstAsync(x => x.Username == "student02");

            var copyCleanCodeBorrowed = await context.BookCopies.FirstAsync(x => x.Barcode == "BC-202610-0003");
            var copyCleanArchOverdue = await context.BookCopies.FirstAsync(x => x.Barcode == "BC-202610-0006");

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

    private record SeedUserSetting(
        string Email,
        string Role,
        string Password,
        string FullName,
        string Username,
        string CardNo,
        string MemberCode);

    private static SeedUserSetting GetSeedUser(
        int index,
        string defaultEmail,
        string defaultRole,
        string defaultPassword,
        string defaultFullName,
        string defaultUsername,
        string defaultCardNo,
        string defaultMemberCode)
    {
        var email = Environment.GetEnvironmentVariable($"SEED_USER_{index}_EMAIL");
        email = string.IsNullOrWhiteSpace(email) ? defaultEmail : email.Trim().ToLowerInvariant();

        var role = Environment.GetEnvironmentVariable($"SEED_USER_{index}_ROLE");
        role = string.IsNullOrWhiteSpace(role) ? defaultRole : role.Trim();
        role = string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? AppRoles.Admin
            : AppRoles.Librarian;

        var defaultPass = Environment.GetEnvironmentVariable("DEFAULT_SEED_PASSWORD") ?? "123456";
        var password = Environment.GetEnvironmentVariable($"SEED_USER_{index}_PASSWORD");
        password = string.IsNullOrWhiteSpace(password) ? defaultPass : password.Trim();

        var fullName = Environment.GetEnvironmentVariable($"SEED_USER_{index}_NAME");
        fullName = string.IsNullOrWhiteSpace(fullName) ? defaultFullName : fullName.Trim();

        return new SeedUserSetting(
            email,
            role,
            password,
            fullName,
            defaultUsername,
            defaultCardNo,
            defaultMemberCode
        );
    }
}
