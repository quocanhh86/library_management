# Kiến Trúc Đảm Bảo Tính Toàn Vẹn & Đúng Đắn Của Dữ Liệu (Data Integrity & Consistency)

> Tài liệu đặc tả các lớp bảo vệ tính toàn vẹn dữ liệu từ tầng CSDL SQL Server đến tầng Domain Service trong dự án **Hệ Thống Quản Lý Thư Viện UTC**.

---

## 1. Bảo Vệ Toàn Vẹn Đa Tầng (3-Layer Defense-in-Depth)

Hệ thống không phụ thuộc đơn lẻ vào kiểm tra ở giao diện hay C#, mà áp dụng đồng thời **3 lớp kiểm chứng độc lập**:

```text
[Lớp 1: InputSanitizer + DataAnnotations ViewModel]
        │  (Chặn dữ liệu rác, XSS/SQLi, sai định dạng từ HTTP Request)
        ▼
[Lớp 2: Domain Service + Pessimistic Lock (sp_getapplock)]
        │  (Kiểm tra quy tắc nghiệp vụ, hạn mức, máy trạng thái trong Transaction độc quyền)
        ▼
[Lớp 3: SQL Server Constraints (PK, FK, UNIQUE, 35+ CHECK Constraints)]
           (Chốt chặn vật lý cuối cùng tại Engine CSDL — Không thể bị vượt qua)
```

---

## 2. Toàn Vẹn Miền Giá Trị Trên SQL Server (Domain Integrity — Hệ Thống `CHECK` Constraints)

Cơ sở dữ liệu `library_management` áp đặt hơn **35 ràng buộc `CHECK`** trực tiếp trên các bảng để đảm bảo không một dòng dữ liệu bất hợp lệ nào có thể tồn tại:

### 2.1. Ràng buộc Trạng thái Thực thể (`ck_*_status`)
Toàn bộ 15 bảng có cột `status` (`authors`, `book_authors`, `book_copies`, `books`, `borrow_details`, `borrow_slips`, `categories`, `fines`, `library_settings`, `publishers`, `reader_types`, `readers`, `reservations`, `shelves`, `staff`) đều được bảo vệ bởi ràng buộc:
```sql
CHECK ([status] IN ('ACTIVE', 'INACTIVE', 'DELETED'))
```
*(Được tự động kiểm tra và nâng cấp idempotent ngay khi khởi động trong [`DbInitializer.InitializeAsync`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Data/DbInitializer.cs#L48-L67)).*

### 2.2. Ràng buộc Tính Đúng Đắn Của Bản Sao Vật Lý (`dbo.book_copies`)
- `ck_book_copies_copy_status`: `[copy_status] IN ('AVAILABLE', 'BORROWED', 'RESERVED', 'DAMAGED', 'LOST', 'DISPOSED')`.
- `ck_book_copies_dispose`:
  ```sql
  ([copy_status] = 'DISPOSED' AND [disposed_at] IS NOT NULL)
  OR ([copy_status] <> 'DISPOSED' AND [disposed_at] IS NULL)
  ```
  $\rightarrow$ Đảm bảo bản sao chỉ có ngày thanh lý `disposed_at` khi và chỉ khi `copy_status = 'DISPOSED'`. Khi khôi phục bản sao thanh lý về `AVAILABLE`, hệ thống tự động xóa `DisposedAt = null` và `DisposalReason = null`.
- `ck_book_copies_price`: `[price] IS NULL OR [price] >= 0`.

### 2.3. Ràng buộc Tính Đúng Đắn Của Chi Tiết Mượn Trả (`dbo.borrow_details`)
- `ck_borrow_details_return` (Đồng bộ 3 cột trả sách):
  ```sql
  ([returned_at] IS NULL AND [return_condition] IS NULL AND [received_by] IS NULL)
  OR ([returned_at] IS NOT NULL AND [return_condition] IS NOT NULL AND [received_by] IS NOT NULL)
  ```
  $\rightarrow$ Không bao giờ xảy ra tình trạng sách đã đánh dấu trả (`returned_at IS NOT NULL`) nhưng thiếu tình trạng trả (`return_condition`) hoặc thiếu thủ thư tiếp nhận (`received_by`), và ngược lại.
- `ck_borrow_details_cond`: `[return_condition] IS NULL OR [return_condition] IN ('GOOD', 'DAMAGED', 'LOST')`.
- `ck_borrow_details_renew`: `[renew_count] >= 0`.

### 2.4. Ràng buộc Tính Đúng Đắn Của Phiếu Phạt (`dbo.fines`)
- `ck_fines_amount`: `[amount] > 0`.
- `ck_fines_fine_status`: `[fine_status] IN ('UNPAID', 'PAID', 'WAIVED')`.
- `ck_fines_type`: `[fine_type] IN ('OVERDUE', 'DAMAGED', 'LOST')`.
- `ck_fines_overdue_days`:
  ```sql
  ([fine_type] = 'OVERDUE' AND [overdue_days] IS NOT NULL AND [overdue_days] > 0)
  OR ([fine_type] <> 'OVERDUE' AND [overdue_days] IS NULL)
  ```
- `ck_fines_paid`:
  ```sql
  ([fine_status] = 'PAID' AND [paid_at] IS NOT NULL AND [payment_method] IS NOT NULL)
  OR ([fine_status] <> 'PAID' AND [paid_at] IS NULL AND [payment_method] IS NULL)
  ```
- `ck_fines_handler`:
  ```sql
  ([fine_status] = 'UNPAID' AND [handled_by] IS NULL)
  OR ([fine_status] <> 'UNPAID' AND [handled_by] IS NOT NULL)
  ```
- `ck_fines_waive_note`: Khi miễn phạt (`fine_status = 'WAIVED'`), bắt buộc cột `note` phải có lý do không rỗng.

### 2.5. Ràng buộc Tính Đúng Đắn Của Đặt Trước Sách (`dbo.reservations`)
- `ck_reservations_res_status`: `[reservation_status] IN ('PENDING', 'READY', 'FULFILLED', 'CANCELLED', 'EXPIRED')`.
- `ck_reservations_ready`:
  ```sql
  ([reservation_status] <> 'READY')
  OR ([assigned_copy_id] IS NOT NULL AND [ready_at] IS NOT NULL AND [expires_at] IS NOT NULL)
  ```
  $\rightarrow$ Khi phiếu đặt trước chuyển sang `READY` (đã giữ sách tại quầy), bắt buộc phải có đầy đủ `assigned_copy_id`, `ready_at` và `expires_at`.
- `ck_reservations_closed`:
  ```sql
  ([reservation_status] IN ('FULFILLED', 'CANCELLED', 'EXPIRED') AND [closed_at] IS NOT NULL)
  OR ([reservation_status] NOT IN ('FULFILLED', 'CANCELLED', 'EXPIRED') AND [closed_at] IS NULL)
  ```
  $\rightarrow$ Mọi phiếu kết thúc (`FULFILLED`, `CANCELLED`, `EXPIRED`) bắt buộc phải có dấu thời gian `closed_at`; mọi phiếu đang mở (`PENDING`, `READY`) phải có `closed_at IS NULL`.

### 2.6. Ràng buộc Thẻ Độc Giả (`dbo.readers`)
- `ck_readers_expiry`: `[card_expiry_date] > [card_issued_date]`.
- `ck_readers_lock`:
  ```sql
  ([card_status] = 'LOCKED' AND [locked_at] IS NOT NULL)
  OR ([card_status] <> 'LOCKED' AND [locked_at] IS NULL)
  ```

---

## 3. Bất Biến Máy Trạng Thái Lưu Thông & Cơ Chế "Bẫy Bản Sao" (`Copy Trap`)

### 3.1. Chu trình khép kín của `BookCopy.CopyStatus`
```text
                  ┌──(Độc giả mượn trực tiếp)──────────────────┐
                  │                                            ▼
[AVAILABLE] ──────┼──(Tạo đặt trước khi còn bản rảnh)──> [RESERVED] ──(Độc giả đặt trước đến nhận)──> [BORROWED]
    ▲             │                                            ▲                                          │
    │             └────────────────────────────────────────────┼──(Trả sách GOOD & có người chờ PENDING)──┘
    │                                                          │                                          │
    ├──(Hủy đặt trước / Xóa mềm độc giả & không còn ai chờ)────┘                                          │
    │                                                                                                     │
    ├──(Trả sách GOOD & không có ai đặt trước PENDING)────────────────────────────────────────────────────┤
    │                                                                                                     ▼
    └──(Admin sửa/khôi phục về AVAILABLE) <─────────────────────────────────────── [DAMAGED / LOST / DISPOSED]
```

### 3.2. Các quy tắc bất biến (Invariants) được bảo vệ nghiêm ngặt:
1. **4 Điều kiện tiên quyết khi Lập phiếu mượn (`CirculationService.CreateBorrowSlipAsync`)**:
   - Độc giả phải đang `Status == ACTIVE`, `CardStatus == ACTIVE` và `CardExpiryDate >= Today`.
   - Tổng tiền phạt `UNPAID` của độc giả không vượt quá ngưỡng `LibrarySettings.MaxUnpaidFine` (và không có khoản phạt `UNPAID` quá hạn).
   - Tổng số sách đang mượn chưa trả + số sách mượn thêm không vượt quá `ReaderType.MaxBorrowBooks`.
   - Bản sao phải thuộc đầu sách `Book.Status == ACTIVE` và bản sao `Copy.Status == ACTIVE`; nếu bản sao đang `RESERVED`, chỉ đúng độc giả có phiếu đặt trước `READY` gắn với `AssignedCopyId == copy.Id` mới được phép mượn (đồng thời chuyển phiếu đặt trước sang `FULFILLED` và ghi `ClosedAt = now`).
2. **Tự động điều phối hàng đợi đặt trước (`ReleaseCopyAsync` — Copy Trap)**:
   - Bất cứ khi nào một bản sao được trả về trong tình trạng `GOOD` (`ReturnAsync`), hoặc một phiếu đặt trước đang giữ bản sao `RESERVED` bị hủy (`CancelReservationAsync`), hoặc độc giả giữ chỗ bị xóa mềm (`ReaderAdminService.SoftDeleteAsync`):
   - Hệ thống tự động tìm phiếu đặt trước `PENDING` có `ReservedAt` sớm nhất (FIFO) của độc giả đang `Status == ACTIVE && CardStatus == ACTIVE`:
     - **Nếu có**: Gán ngay `AssignedCopyId = copy.Id`, chuyển phiếu đó sang `READY`, thiết lập `ReadyAt = now`, `ExpiresAt = now.AddHours(ReservationHoldHours)` và giữ `copy.CopyStatus = RESERVED`.
     - **Nếu không có**: Đưa `copy.CopyStatus = AVAILABLE`.
3. **Đồng bộ tự động Ảnh bìa chính (`SyncBookPrimaryCoverAsync`)**:
   - Sau mọi thao tác thêm ảnh, xóa ảnh hoặc đặt ảnh chính trên `dbo.book_images`, hệ thống tự động đảm bảo chỉ có duy nhất 1 ảnh `IsPrimary = true` cho mỗi đầu sách và cập nhật `Book.CoverUrl` khớp 100% với `ImageUrl` của ảnh chính đó.
4. **Tự động Ghi vết Kiểm toán (`ApplyAuditInformation`)**:
   - Mọi thực thể kế thừa `IAuditableEntity` khi `SaveChangesAsync` trong `ApplicationDbContext` đều được tự động gán `CreatedAt`, `UpdatedAt`, `CreatedBy` và `UpdatedBy` theo định danh cán bộ đang đăng nhập (`IUserContext.CurrentUserId`).
