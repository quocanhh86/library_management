# Quy Chuẩn Xử Lý Xóa Mềm (Soft Delete), Xóa Cứng (Hard Delete) & CRUD Nâng Cao

> Tài liệu đặc tả kỹ thuật cho dự án **Hệ Thống Quản Lý Thư Viện UTC (ASP.NET Core MVC .NET 10.0 + EF Core + SQL Server)**.

---

## 1. Kiến Trúc Xóa 2 Bước (Two-Step Deletion Lifecycle)

Toàn bộ các thực thể danh mục và quản trị trong hệ thống (`Book`, `BookCopy`, `Category`, `Publisher`, `Author`, `Shelf`, `Reader`, `Staff`, `ReaderType`) đều cài đặt giao diện [`ISoftDelete`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Common/ISoftDelete.cs) với cột `status VARCHAR(10)` trên SQL Server gồm 3 trạng thái chuẩn trong [`EntityStatus`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Constant/EntityStatus.cs):

- `ACTIVE`: Đang hoạt động bình thường.
- `INACTIVE`: Tạm ẩn / Ngừng kích hoạt tạm thời.
- `DELETED`: Đã xóa mềm (nằm trong mục **Đã xóa mềm / Thùng rác**).

### Sơ đồ chuyển trạng thái vòng đời xóa 2 bước

```text
[ACTIVE / INACTIVE]
       │
       ├── (Bước 1: Xóa mềm - Soft Delete) ──> [DELETED (Thùng rác)]
       │                                              │
       │   <── (Khôi phục - Restore về ACTIVE) ───────┘
       │                                              │
       └── (Bước 2: Xóa vĩnh viễn - Hard Delete kèm mã xác nhận "XOA")
                                                      │
                                                      ▼
                                        [Xóa vật lý khỏi SQL Server]
```

### Quy tắc bắt buộc của quy trình Xóa 2 Bước
1. **Không bao giờ xóa vật lý trực tiếp từ trạng thái `ACTIVE` hoặc `INACTIVE`**:
   - Mọi thao tác xóa trên danh sách chính chỉ thực hiện **Bước 1 (Xóa mềm)**: chuyển `entity.Status = EntityStatus.Deleted`.
2. **Khôi phục (`Restore`)**:
   - Chỉ áp dụng cho bản ghi đang có `Status == EntityStatus.Deleted`, chuyển lại về `EntityStatus.Active` sau khi kiểm tra không vi phạm ràng buộc duy nhất (ví dụ: ISBN, mã kệ, tên thể loại).
3. **Bước 2 — Xóa vĩnh viễn (`HardDelete`)**:
   - Bắt buộc bản ghi phải đang ở trạng thái `Status == EntityStatus.Deleted` (nếu chưa xóa mềm sẽ trả về mã lỗi `ErrorCode.HardDeleteRequiresSoftDeleteFirst`).
   - Bắt buộc người dùng nhập đúng mã xác nhận `confirmCode = "XOA"` (không phân biệt hoa thường, kiểm tra qua `IsValidHardDeleteConfirm`, nếu sai trả về `ErrorCode.HardDeleteConfirmInvalid`).
   - Bắt buộc kiểm tra không còn bất kỳ dữ liệu phát sinh / lịch sử giao dịch liên đới trước khi gọi `Delete(entity)`.

---

## 2. Ma Trận Điều Kiện Tiên Quyết (Pre-Conditions) Cho Xóa Mềm & Xóa Cứng

Mọi thao tác Xóa mềm, Khôi phục và Xóa cứng bắt buộc mở giao dịch kèm khóa độc quyền [`await unitOfWork.BeginTransactionWithLockAsync("LIB_MUTATION_LOCK", 10000, ct)`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Repositories/UnitOfWork.cs#L63-L102) trước khi kiểm tra điều kiện:

| Thực thể | Điều kiện chặn Xóa mềm (`Soft Delete`) | Xử lý tự động khi Xóa mềm | Điều kiện chặn Xóa vĩnh viễn (`Hard Delete`) |
| :--- | :--- | :--- | :--- |
| **Đầu sách (`Book`)** | - Có bản sao vật lý đang `BORROWED` hoặc `RESERVED`<br>- Hoặc có dòng `BorrowDetails` chưa trả (`ReturnedAt == null`)<br>- Hoặc có phiếu đặt trước đang `PENDING` hoặc `READY` | - Đổi `book.Status = DELETED`<br>- Đổi toàn bộ `BookCopies` chưa xóa mềm của sách đó sang `Status = DELETED` (giữ nguyên `CopyStatus` vật lý) | - Phải đang `DELETED`<br>- Chặn nếu từng phát sinh lịch sử mượn (`BorrowDetails`) hoặc lịch sử đặt trước (`Reservations`) |
| **Bản sao (`BookCopy`)** | - `CopyStatus == BORROWED` hoặc `CopyStatus == RESERVED`<br>- Hoặc đang nằm trong `BorrowDetails` chưa trả (`ReturnedAt == null`)<br>- Hoặc đang được gán giữ chỗ (`AssignedCopyId`) cho phiếu đặt trước `READY` | - Đổi `copy.Status = DELETED` (không gọi `DbSet.Update(copy)` dư thừa để tránh ghi đè `CopyStatus`) | - Phải đang `DELETED`<br>- Chặn nếu từng phát sinh lịch sử mượn (`BorrowDetails.ExistsAsync(CopyId)`) hoặc lịch sử gán đặt trước (`Reservations.ExistsAsync(AssignedCopyId)`) |
| **Thể loại (`Category`)** | - Còn đầu sách chưa xóa mềm (`Books.ExistsAsync(CategoryId && Status != DELETED)`) | - Đổi `category.Status = DELETED` | - Phải đang `DELETED`<br>- Chặn nếu còn bất kỳ đầu sách nào (kể cả đã xóa mềm) tham chiếu tới `CategoryId` |
| **Nhà xuất bản (`Publisher`)** | - Còn đầu sách chưa xóa mềm (`Books.ExistsAsync(PublisherId && Status != DELETED)`) | - Đổi `publisher.Status = DELETED` | - Phải đang `DELETED`<br>- Chặn nếu còn bất kỳ đầu sách nào tham chiếu tới `PublisherId` |
| **Tác giả (`Author`)** | - Còn liên kết với đầu sách chưa xóa mềm (`BookAuthors` có `Book.Status != DELETED`) | - Đổi `author.Status = DELETED` | - Phải đang `DELETED`<br>- Chặn nếu còn liên kết trong `dbo.book_authors` |
| **Kệ sách (`Shelf`)** | - Còn bản sao chưa xóa mềm đặt trên kệ (`BookCopies.ExistsAsync(ShelfId && Status != DELETED)`) | - Đổi `shelf.Status = DELETED` | - Phải đang `DELETED`<br>- Chặn nếu còn bất kỳ bản sao nào tham chiếu tới `ShelfId` |
| **Độc giả (`Reader`)** | - Còn sách đang mượn chưa trả (`BorrowDetails.ReturnedAt == null`)<br>- Hoặc còn khoản phạt chưa thanh toán (`Fines.FineStatus == UNPAID`) | - Đổi `reader.Status = DELETED`, `CardStatus = LOCKED`, xoay mới `SecurityStamp`<br>- Tự động hủy (`CANCELLED`) mọi phiếu đặt trước `PENDING`/`READY` của độc giả đó và nhả bản sao `RESERVED` cho bạn đọc kế tiếp hoặc về `AVAILABLE` | - Phải đang `DELETED` (Chỉ `Admin` mới có quyền)<br>- Chặn nếu độc giả đã có lịch sử phiếu mượn (`BorrowSlips`) hoặc lịch sử đặt trước (`Reservations`) |
| **Nhân sự (`Staff`)** | - Không được tự xóa chính mình (`id == CurrentUserId`)<br>- Không được xóa mềm tài khoản `ADMIN` đang hoạt động cuối cùng của hệ thống | - Đổi `staff.Status = DELETED`, xoay mới `SecurityStamp` để thu hồi phiên ngay lập tức | - Phải đang `DELETED` và không phải chính mình<br>- Chặn nếu cán bộ đã từng lập phiếu mượn (`IssuedBy`), nhận sách trả (`ReceivedBy`) hoặc xử lý tiền phạt (`HandledBy`)<br>- Tự động gọi `ClearStaffAuditReferencesAsync` xóa tham chiếu `created_by`/`updated_by` trên 16 bảng trước khi xóa vật lý |
| **Loại độc giả (`ReaderType`)** | - Còn độc giả chưa xóa mềm thuộc loại thẻ này (`Readers.ExistsAsync(ReaderTypeId && Status != DELETED)`) | - Đổi `readerType.Status = DELETED` | - Phải đang `DELETED`<br>- Chặn nếu còn bất kỳ hồ sơ độc giả nào tham chiếu tới `ReaderTypeId` |

---

## 3. Kỹ Thuật CRUD Nâng Cao (Advanced CRUD Patterns)

### 3.1. Upsert Phức Hợp Đa Bảng Trong 1 Giao Dịch (Master-Detail Atomic Upsert)
Khi thêm mới hoặc cập nhật Đầu sách ([`BookService.CreateBookWithCopiesAsync`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Services/BookService.cs#L232-L310) & [`UpdateBookAsync`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Services/BookService.cs#L312-L369)), hệ thống xử lý nguyên tử (Atomic) trên 4 bảng trong cùng một `BeginTransactionWithLockAsync`:
1. **`dbo.books`**: Lưu thông tin biên mục đầu sách, chuẩn hóa ISBN và kiểm tra trùng lặp ISBN trên toàn bảng (bao gồm cả dòng `DELETED`).
2. **`dbo.book_authors`**: Đồng bộ danh sách tác giả đồng biên soạn kèm thứ tự `AuthorOrder` (`1..N`). Khi cập nhật, xóa danh sách liên kết cũ và tái tạo danh sách mới theo đúng thứ tự người dùng chọn.
3. **`dbo.book_copies`**: Tự động sinh lô bản sao vật lý ban đầu (`InitialCopyCount`) kèm mã vạch duy nhất (`BarcodeHelper.GenerateCopyBarcode(date, seq)`), gán kệ sách `ShelfId`, giá bìa `CopyPrice` và trạng thái `AVAILABLE`.
4. **`dbo.book_images` + Cloudinary**:
   - Hỗ trợ tải lên đồng thời nhiều tệp ảnh (`COVER`, `BACK_COVER`, `TABLE_OF_CONTENTS`, `PREVIEW_PAGE`, `GALLERY`) lên Cloudinary thông qua [`ICloudinaryService`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Services/ICloudinaryService.cs).
   - Tự động chọn ảnh bìa chính (`IsPrimary = true`) hoặc ảnh có `ImageType == COVER` đầu tiên để đồng bộ ngược vào cột `Book.CoverUrl` thông qua `SyncBookPrimaryCoverAsync`.

### 3.2. Kiểm Tra Trùng Lặp Unique Constraint Xuyên Trạng Thái (Cross-Status Uniqueness Check)
Các ràng buộc `UNIQUE` trên SQL Server (`uq_books_isbn`, `uq_categories_name`, `uq_publishers_name`, `uq_shelves_code`, `uq_reader_types_code`) kiểm tra tính duy nhất trên **toàn bộ các dòng trong bảng**, bao gồm cả các dòng đang có `status = 'DELETED'`.
- **Anti-Pattern (Sai)**: Chỉ kiểm tra `x.Status != EntityStatus.Deleted` trong code C# $\rightarrow$ Khi người dùng tạo mới bản ghi trùng tên/mã với một bản ghi đã xóa mềm, EF Core sẽ văng `SqlException 2627/2601`.
- **Best Practice (Đúng)**: Truy vấn `FirstOrDefaultAsync` không lọc bỏ `DELETED`. Nếu phát hiện bản ghi trùng đang có `Status == EntityStatus.Deleted`, trả về `ServiceResult.ValidationError` với thông báo rõ ràng: *"Bản ghi đang nằm trong mục Đã xóa mềm (DELETED). Vui lòng khôi phục hoặc xóa vĩnh viễn bản ghi cũ trước."*

### 3.3. Tối Ưu EF Core Change Tracker (Tránh Ghi Đè Cột Không Thay Đổi)
- Khi tải thực thể với `asNoTracking: false`, EF Core Change Tracker đã tự động theo dõi từng thuộc tính.
- Khi xóa mềm (`entity.Status = EntityStatus.Deleted`), **không gọi lại** `unitOfWork.Repository.Update(entity)` trên thực thể đang được track. Nhờ đó, EF Core chỉ sinh câu lệnh SQL `UPDATE ... SET [status] = 'DELETED', [updated_at] = @p0, [updated_by] = @p1 WHERE [id] = @p2`, tuyệt đối không chạm vào các cột nhạy cảm như `[copy_status]`.

### 3.4. Tìm Kiếm Đa Phạm Vi (`Document Scopes`), Lọc Nâng Cao & Phân Trang AJAX
- **Phân loại phạm vi tài liệu (`scope`)**: Tự động ánh xạ `textbook` (Giáo trình), `thesis` (Luận văn & Đồ án), `journal` (Tạp chí khoa học) dựa trên biểu thức LINQ kết hợp tên thể loại và từ khóa tiêu đề.
- **Lọc nâng cao đa tiêu chí (`#advancedSearch`)**: Kết hợp `keyword`, `scope`, `category`, `authorId`, `publisherId`, `year`, `available` (chỉ lấy đầu sách có ít nhất 1 bản sao `Status == ACTIVE && CopyStatus == AVAILABLE`).
- **Tương tác AJAX**: Trên cổng bạn đọc (`/Reader/BorrowHistory`, `/Reader/Fines`, `/Books/Suggest`), Controller kiểm tra `IsAjaxRequest` để trả về `PartialView` giúp lọc và chuyển trang mượt mà không tải lại toàn trang.
