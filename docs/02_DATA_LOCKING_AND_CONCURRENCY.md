# Cơ Chế Khóa Dữ Liệu (Data Locking) & Kiểm Soát Đồng Thời (Concurrency Control)

> Tài liệu đặc tả kiến trúc khóa dữ liệu đa tầng cho dự án **Hệ Thống Quản Lý Thư Viện UTC (ASP.NET Core MVC .NET 10.0 + SQL Server)**.

---

## 1. Tại Sao Phải Dùng Khóa Dữ Liệu Trong Hệ Thống Thư Viện?

Trong môi trường đa người dùng (nhiều Thủ thư tại quầy lưu thông, Quản trị viên quản lý danh mục, và hàng trăm Bạn đọc đặt trước/gia hạn trực tuyến cùng lúc), nếu chỉ dùng mức cô lập mặc định `READ COMMITTED` của SQL Server sẽ xảy ra 3 bài toán xung đột kinh điển:

1. **Xung đột Xóa mềm vs. Mượn sách / Đặt trước (TOCTOU Race Condition)**:
   - Tại thời điểm $t_1$, Admin bấm **Xóa mềm** đầu sách $B$; câu lệnh `ExistsAsync` kiểm tra thấy không có bản sao nào đang `BORROWED` và nhả khóa đọc (`S-Lock`) ngay lập tức.
   - Tại thời điểm $t_1 + 1\text{ms}$, Thủ thư bấm **Lập phiếu mượn** cuốn sách $B$; câu lệnh kiểm tra thấy `Book.Status == ACTIVE` và `Copy.CopyStatus == AVAILABLE`.
   - Tại thời điểm $t_2$, cả hai giao dịch cùng `COMMIT` $\rightarrow$ Đầu sách $B$ bị đánh dấu `DELETED` trong khi bản sao của nó lại đang `BORROWED` bởi độc giả!
2. **Xung đột Trả sách / Hủy đặt trước vs. Hàng đợi Đặt trước (`Copy Trap` Race Condition)**:
   - Hai bản sao của cùng một đầu sách được trả về quầy cùng lúc hoặc hai thủ thư cùng thao tác hủy phiếu đặt trước, dễ dẫn đến việc gán cả 2 bản sao cho cùng 1 phiếu đặt trước `PENDING` đầu hàng đợi.
3. **Conversion Deadlock (`SqlException 1205`) khi dùng `SERIALIZABLE` thuần túy**:
   - Nếu chỉ đổi `IsolationLevel.Serializable` mà không lấy khóa độc quyền từ đầu giao dịch, hai giao dịch cùng đọc (`RangeS-S`) rồi cùng nâng cấp lên khóa ghi (`X`) trên cùng bảng `books`/`book_copies` sẽ khóa chéo nhau và bị SQL Server kill một nhánh với lỗi `Error 1205 (Deadlock victim)`.

---

## 2. Phân Loại 4 Cơ Chế Khóa Dữ Liệu Đang Áp Dụng Trong Hệ Thống

### Loại 1: Khóa Bi Quan Cấp Giao Dịch Trên SQL Server (`sys.sp_getapplock` — Pessimistic Transaction Application Lock)

Được triển khai tập trung tại [`UnitOfWork.BeginTransactionWithLockAsync`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Repositories/UnitOfWork.cs#L63-L102):

```csharp
public async Task<IDbContextTransaction> BeginTransactionWithLockAsync(
    string lockResource = "LIB_MUTATION_LOCK",
    int lockTimeoutMs = 10000,
    CancellationToken cancellationToken = default)
{
    var tx = await BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    try
    {
        var safeResource = string.IsNullOrWhiteSpace(lockResource) ? "LIB_MUTATION_LOCK" : lockResource.Trim();
        var rcParam = new Microsoft.Data.SqlClient.SqlParameter("@rc", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        var resourceParam = new Microsoft.Data.SqlClient.SqlParameter("@Resource", SqlDbType.NVarChar, 255)
        {
            Value = safeResource
        };
        var timeoutParam = new Microsoft.Data.SqlClient.SqlParameter("@LockTimeout", SqlDbType.Int)
        {
            Value = lockTimeoutMs
        };

        await _context.Database.ExecuteSqlRawAsync(
            "EXEC @rc = sys.sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @LockTimeout;",
            [rcParam, resourceParam, timeoutParam],
            cancellationToken);

        var lockResult = rcParam.Value is int rc ? rc : -999;
        if (lockResult < 0)
        {
            throw new TimeoutException($"Không thể lấy khóa giao dịch đồng thời cho tài nguyên '{safeResource}' (mã: {lockResult}).");
        }

        return tx;
    }
    catch
    {
        await RollbackTransactionAsync(CancellationToken.None);
        throw;
    }
}
```

#### Đặc tính kỹ thuật của `sp_getapplock`:
- **Quản lý bởi SQL Server Lock Manager**: Hoạt động chính xác ngay cả khi triển khai nhiều instance web app kết nối chung một SQL Server.
- **`@LockMode = 'Exclusive'`**: Tuần tự hóa (Serialize) hoàn toàn các thao tác ghi xung đột (Xóa mềm, Khôi phục, Xóa cứng, Lập phiếu mượn, Trả sách, Đặt trước, Hủy đặt trước) mà không chặn các truy vấn đọc (`SELECT` tra cứu danh mục của bạn đọc vẫn chạy song song 100% không bị chờ khóa).
- **`@LockOwner = 'Transaction'`**: Khóa tự động được SQL Server giải phóng ngay lập tức khi `CommitTransactionAsync()` hoặc `RollbackTransactionAsync()` (hoặc khi kết nối bị ngắt), **tuyệt đối không bao giờ bị rò rỉ khóa (lock leak)**.
- **Thực thi qua `ExecuteSqlRawAsync` + `SqlParameter` `OUTPUT`**: Không dùng `SqlQueryRaw<int>().SingleAsync()` vì EF Core 10 sẽ bọc câu lệnh vào subquery `SELECT TOP(2) ... FROM (...) AS [t]` gây lỗi cú pháp T-SQL với `EXEC`.
- **Mã trả về (`@rc`) của `sp_getapplock`**:
  - `0`: Lấy khóa thành công ngay lập tức (Synchronous grant).
  - `1`: Lấy khóa thành công sau khi chờ giao dịch trước hoàn tất (Granted after waiting).
  - `< 0` (`-1`: Timeout, `-2`: Canceled, `-3`: Deadlock, `-999`: Error): Hệ thống tự động `Rollback` và ném `TimeoutException` an toàn.

---

### Loại 2: Khóa Bất Đồng Bộ Theo Khóa Định Danh Trong Bộ Nhớ (`SemaphoreSlim` Per-Key Concurrency Lock)

Được triển khai tại [`OtpService.VerifyOtpAsync`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Services/OtpService.cs#L23-L81):

```csharp
private static readonly ConcurrentDictionary<string, SemaphoreSlim> ConcurrencyLocks = new();

var lockKey = $"{normalizedEmail}:{purpose}";
var semaphore = ConcurrencyLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
await semaphore.WaitAsync(cancellationToken);
try
{
    // Đọc bản ghi OTP chưa sử dụng, kiểm tra hạn, tăng FailedAttempts hoặc đánh dấu IsUsed = true
}
finally
{
    semaphore.Release();
}
```

#### Mục đích:
- Chống tấn công **Race Condition Brute-Force OTP (`SEC-14`)**: Khi kẻ tấn công bắn 50 HTTP request song song cùng một mili-giây với 50 mã OTP khác nhau cho cùng một `email:purpose`, `SemaphoreSlim(1, 1)` ép 50 request của email đó phải đi qua vùng kiểm tra `FailedAttempts` theo thứ tự từng chiếc một. Ngay ở request sai thứ 5, mã OTP bị đốt hủy (`IsUsed = true`), 45 request còn lại lập tức bị từ chối.

---

### Loại 3: Khóa Cửa Sổ Trượt Nguyên Tử (`Sliding-Window Monitor Lock`)

Được triển khai tại [`IpRateLimiterService`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Services/IpRateLimiterService.cs) và [`EmailService`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Services/EmailService.cs):

- Sử dụng `ConcurrentDictionary<string, Queue<DateTimeOffset>>` kết hợp `lock (queue)` trên từng hàng đợi của từng cặp `IP:Action` (ví dụ: `127.0.0.1:LOGIN`, `127.0.0.1:VERIFY_OTP`, `127.0.0.1:REGISTER_SEND_OTP`).
- Đảm bảo thao tác dọn các mốc thời gian cũ quá 60 giây (`queue.Dequeue()`) và kiểm tra ngưỡng (`queue.Count >= maxRequestsPerMinute`) diễn ra nguyên tử (Thread-safe 100%) với độ trễ $< 0.01\text{ms}$.

---

### Loại 4: Cơ Chế Chống Treo Khóa Kết Nối SQL Client Trên Windows (`FastLocalSqlConnectionInterceptor`)

Được triển khai tại [`ServiceCollectionExtensions.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Extensions/ServiceCollectionExtensions.cs#L21-L47) và [`Program.cs`](file:///d:/Document/Education/UTC/assignment_library_management/Library_management/web_mvc_management/Program.cs#L9):

- Bật `AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true)`.
- Sử dụng `FastLocalSqlConnectionInterceptor` chuyển thao tác lấy kết nối từ Connection Pool sang đường dẫn đồng bộ (`connection.Open()`), kết hợp `SafeCancellationTokenModelBinderProvider` khi gắn trình gỡ lỗi (`Debugger.IsAttached`), giúp loại bỏ hoàn toàn hiện tượng treo async `SNI.dll` (`Win32Exception: The wait operation timed out`).
