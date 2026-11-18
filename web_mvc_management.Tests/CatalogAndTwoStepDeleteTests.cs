using Microsoft.EntityFrameworkCore;
using web_mvc_management.Constant;
using web_mvc_management.Entity;
using web_mvc_management.Models.ViewModels.AdminCatalog;
using web_mvc_management.Tests.TestInfrastructure;
using Xunit;
using static web_mvc_management.Constant.LibraryConstants;

namespace web_mvc_management.Tests;

public class CatalogAndTwoStepDeleteTests
{
    [Fact]
    public async Task DeleteBook_ShouldCascadeSoftDeleteBookCopiesPreservingCopyStatus_AndBlockWhenBorrowedOrReserved()
    {
        await using var fx = new SqliteTestDbFixture();
        var staff = await fx.SeedStaffAsync();
        fx.UserContext.SetUser(staff.Id, staff.Username, staff.Role);

        var (book1, copies1, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Sách xóa được", copyCount: 2);
        copies1[1].CopyStatus = CopyStatus.Damaged;
        await fx.Context.SaveChangesAsync();

        var bookAdmin = fx.CreateBookAdminService();
        var bookCopySvc = fx.CreateBookCopyService();

        // Xóa mềm đầu sách chưa có mượn/đặt -> đầu sách DELETED VÀ các bản sao cascade DELETED nhưng giữ nguyên CopyStatus
        var delRes = await bookAdmin.DeleteBookAsync(book1.Id);
        Assert.True(delRes.IsSuccess, delRes.Message);

        var dbBook1 = await fx.Context.Books.AsNoTracking().FirstAsync(b => b.Id == book1.Id);
        var dbCopies1 = await fx.Context.BookCopies.AsNoTracking().Where(c => c.BookId == book1.Id).ToListAsync();
        Assert.Equal(EntityStatus.Deleted, dbBook1.Status);
        Assert.All(dbCopies1, c => Assert.Equal(EntityStatus.Deleted, c.Status));
        Assert.Contains(dbCopies1, c => c.Id == copies1[0].Id && c.CopyStatus == CopyStatus.Available);
        Assert.Contains(dbCopies1, c => c.Id == copies1[1].Id && c.CopyStatus == CopyStatus.Damaged);

        // Chặn xóa vĩnh viễn nếu chưa xóa vĩnh viễn các bản sao
        var blockedHard = await bookAdmin.HardDeleteBookAsync(book1.Id, "XOA");
        Assert.False(blockedHard.IsSuccess);
        Assert.Equal(ErrorCode.CommonRecordInUse, blockedHard.Code);

        // Xóa vĩnh viễn các bản sao trước (với mã "XOA") -> sau đó xóa vĩnh viễn được đầu sách
        Assert.False((await bookCopySvc.HardDeleteBookCopyAsync(copies1[0].Id, "WRONG")).IsSuccess);
        Assert.True((await bookCopySvc.HardDeleteBookCopyAsync(copies1[0].Id, "XOA")).IsSuccess);
        Assert.True((await bookCopySvc.HardDeleteBookCopyAsync(copies1[1].Id, "XOA")).IsSuccess);

        var hardRes = await bookAdmin.HardDeleteBookAsync(book1.Id, "XOA");
        Assert.True(hardRes.IsSuccess, hardRes.Message);
        Assert.False(await fx.Context.Books.AnyAsync(b => b.Id == book1.Id));

        // Đầu sách đang có bản sao được mượn -> chặn xóa mềm
        var rt = await fx.SeedReaderTypeAsync();
        var reader = await fx.SeedReaderAsync(rt.Id, "SV900");
        var (book2, copies2, _, _, _, _) = await fx.SeedBookWithCopiesAsync("Sách đang mượn", copyCount: 1);
        await fx.SeedOpenLoanAsync(reader.Id, staff.Id, copies2[0].Id);

        var blockedSoft = await bookAdmin.DeleteBookAsync(book2.Id);
        Assert.False(blockedSoft.IsSuccess);
        Assert.Equal(ErrorCode.BookHasBorrowedCopies, blockedSoft.Code);
    }

    [Fact]
    public async Task CreateBookCopyBatch_And_RestoreBookCopy_ShouldAssignToEarliestPendingReservation()
    {
        await using var fx = new SqliteTestDbFixture();
        await fx.SeedSettingAsync(holdHours: 36);
        var rt = await fx.SeedReaderTypeAsync();
        var waiter = await fx.SeedReaderAsync(rt.Id, "SV501");

        var (book, _, _, _, _, shelf) = await fx.SeedBookWithCopiesAsync("Cơ sở dữ liệu", copyCount: 0);

        var pending = new Reservation
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            ReaderId = waiter.Id,
            ReservationStatus = ReservationStatus.Pending,
            ReservedAt = fx.DateTime.UtcNow.AddHours(-4),
            Status = EntityStatus.Active
        };
        await fx.Context.Reservations.AddAsync(pending);
        await fx.Context.SaveChangesAsync();

        var copySvc = fx.CreateBookCopyService();
        var batchRes = await copySvc.CreateBookCopiesBatchAsync(new BookCopyBatchCreateViewModel
        {
            BookId = book.Id,
            ShelfId = shelf.Id,
            Quantity = 1,
            CustomBarcode = "BC-NEW-001",
            Price = 95000m
        });
        Assert.True(batchRes.IsSuccess, batchRes.Message);

        var createdCopy = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Barcode == "BC-NEW-001");
        var promotedRes = await fx.Context.Reservations.AsNoTracking().FirstAsync(r => r.Id == pending.Id);

        Assert.Equal(CopyStatus.Reserved, createdCopy.CopyStatus);
        Assert.Equal(ReservationStatus.Ready, promotedRes.ReservationStatus);
        Assert.Equal(createdCopy.Id, promotedRes.AssignedCopyId);

        // Nhập thêm 1 bản sao thứ 2 khi không còn Pending -> phải AVAILABLE
        var extraRes = await copySvc.CreateBookCopiesBatchAsync(new BookCopyBatchCreateViewModel
        {
            BookId = book.Id,
            ShelfId = shelf.Id,
            Quantity = 1,
            CustomBarcode = "BC-NEW-002"
        });
        Assert.True(extraRes.IsSuccess, extraRes.Message);
        var extraCopy = await fx.Context.BookCopies.AsNoTracking().FirstAsync(c => c.Barcode == "BC-NEW-002");
        Assert.Equal(CopyStatus.Available, extraCopy.CopyStatus);
    }

    [Fact]
    public async Task CatalogMasterService_ShouldNormalizeTamperedDeletedStatus_AndEnforceTwoStepDeleteWithFks()
    {
        await using var fx = new SqliteTestDbFixture();
        var master = fx.CreateCatalogMasterService();

        // Giả mạo Status = "DELETED" qua form bị ép về ACTIVE
        var saveCat = await master.SaveCategoryAsync(new CategoryFormViewModel
        {
            Name = "Khoa học dữ liệu",
            Status = EntityStatus.Deleted
        });
        Assert.True(saveCat.IsSuccess, saveCat.Message);

        var cat = await fx.Context.Categories.AsNoTracking().FirstAsync(c => c.Name == "Khoa học dữ liệu");
        Assert.Equal(EntityStatus.Active, cat.Status);

        // Chặn xóa cứng khi chưa xóa mềm
        var hardBeforeSoft = await master.HardDeleteCategoryAsync(cat.Id, "XOA");
        Assert.False(hardBeforeSoft.IsSuccess);
        Assert.Equal(ErrorCode.HardDeleteRequiresSoftDeleteFirst, hardBeforeSoft.Code);

        // Kiểm tra Author & Publisher & Shelf 2 bước xóa sạch sẽ
        Assert.True((await master.SaveAuthorAsync(new AuthorFormViewModel { FullName = "Nguyễn Văn Tác Giả", Status = EntityStatus.Deleted })).IsSuccess);
        var author = await fx.Context.Authors.AsNoTracking().FirstAsync(a => a.FullName == "Nguyễn Văn Tác Giả");
        Assert.Equal(EntityStatus.Active, author.Status);

        Assert.True((await master.DeleteAuthorAsync(author.Id)).IsSuccess);
        Assert.True((await master.RestoreAuthorAsync(author.Id)).IsSuccess);
        Assert.True((await master.DeleteAuthorAsync(author.Id)).IsSuccess);
        Assert.True((await master.HardDeleteAuthorAsync(author.Id, "XOA")).IsSuccess);
        Assert.False(await fx.Context.Authors.AnyAsync(a => a.Id == author.Id));

        Assert.True((await master.SaveShelfAsync(new ShelfFormViewModel { Code = "a1-01", Description = "Tầng 1", Status = EntityStatus.Active })).IsSuccess);
        var shelf = await fx.Context.Shelves.AsNoTracking().FirstAsync(s => s.Code == "A1-01");
        Assert.True((await master.DeleteShelfAsync(shelf.Id)).IsSuccess);
        Assert.True((await master.HardDeleteShelfAsync(shelf.Id, "xoa")).IsSuccess);
    }

    [Fact]
    public async Task BookAdmin_CreateUpdateWithImageCleanup_And_PublicCatalogQueries_ShouldWorkEndToEnd()
    {
        await using var fx = new SqliteTestDbFixture();
        var staff = await fx.SeedStaffAsync();
        fx.UserContext.SetUser(staff.Id, staff.Username, staff.Role);

        var master = fx.CreateCatalogMasterService();
        await master.SaveCategoryAsync(new CategoryFormViewModel { Name = "Công nghệ thông tin", Status = EntityStatus.Active });
        await master.SaveAuthorAsync(new AuthorFormViewModel { FullName = "Ngô Quốc Anh", Status = EntityStatus.Active });
        await master.SaveAuthorAsync(new AuthorFormViewModel { FullName = "Trần Minh Tuấn", Status = EntityStatus.Active });
        await master.SavePublisherAsync(new PublisherFormViewModel { Name = "NXB Giao thông Vận tải", Status = EntityStatus.Active });
        await master.SaveShelfAsync(new ShelfFormViewModel { Code = "IT-01", Description = "Kệ CNTT", Status = EntityStatus.Active });

        var catId = (await fx.Context.Categories.FirstAsync()).Id;
        var authors = await fx.Context.Authors.OrderBy(a => a.FullName).ToListAsync();
        var pubId = (await fx.Context.Publishers.FirstAsync()).Id;
        var shelfId = (await fx.Context.Shelves.FirstAsync()).Id;

        var bookAdmin = fx.CreateBookAdminService();
        var createRes = await bookAdmin.CreateBookWithCopiesAsync(new BookUpsertViewModel
        {
            Title = "Giáo trình Lập trình Web .NET",
            Isbn = "978-604-999-001-2",
            CategoryId = catId,
            PublisherId = pubId,
            AuthorIds = [authors[0].Id],
            PublicationYear = 2026,
            ShelfId = shelfId,
            InitialCopyCount = 2,
            CopyPrice = 150000m,
            CoverUrl = "https://cdn.example.com/cover.jpg",
            Status = EntityStatus.Active
        });
        Assert.True(createRes.IsSuccess, createRes.Message);
        var bookId = createRes.Data;

        // Gắn ảnh có PublicId để kiểm tra xóa ảnh khỏi Cloudinary khi UpdateBookAsync
        var extraImage = new BookImage
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            ImageUrl = "/uploads/books/old-page.jpg",
            PublicId = "fake:old-page-id",
            ImageType = BookImageType.PreviewPage,
            DisplayOrder = 1,
            IsPrimary = false,
            Status = EntityStatus.Active
        };
        await fx.Context.BookImages.AddAsync(extraImage);
        await fx.Context.SaveChangesAsync();

        var updateRes = await bookAdmin.UpdateBookAsync(new BookUpsertViewModel
        {
            Id = bookId,
            Title = "Giáo trình Lập trình Web .NET (Tái bản)",
            Isbn = "978-604-999-001-2",
            CategoryId = catId,
            PublisherId = pubId,
            AuthorIds = [authors[1].Id],
            PublicationYear = 2026,
            DeleteImageIds = [extraImage.Id],
            Status = EntityStatus.Active
        });
        Assert.True(updateRes.IsSuccess, updateRes.Message);
        Assert.Contains("fake:old-page-id", fx.CloudinaryService.DeletedPublicIds);

        var pubCatalog = fx.CreatePublicCatalogService();
        var catalog = await pubCatalog.GetPublicCatalogAsync("lap trinh web", null, null, null, null, available: true, page: 1);
        Assert.Single(catalog.Books);
        Assert.Equal(2, catalog.Books[0].AvailableCopies);
        Assert.Contains("Trần Minh Tuấn", catalog.Books[0].Authors);

        var suggestions = await pubCatalog.SuggestAsync("tai ban", 5);
        Assert.Single(suggestions);

        var detail = await pubCatalog.GetPublicBookDetailAsync(bookId);
        Assert.NotNull(detail);
        Assert.Equal(2, detail.Copies.Count);
    }
}
