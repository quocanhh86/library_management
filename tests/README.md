# Kiểm thử quản lý thư viện

## Chạy lại

Yêu cầu Windows, .NET SDK 10 và SQL Server LocalDB có instance `MSSQLLocalDB`.

```powershell
dotnet restore app_management.slnx
dotnet test app_management.slnx --configuration Release --logger "trx;LogFileName=verification-release.trx"
```

Mỗi ca nghiệp vụ tạo database `LibraryTests_<GUID>`, áp dụng migration thật và xóa database đó khi kết thúc. Không dùng chuỗi kết nối trong `.env`, không thay đổi database của ứng dụng. Test upload dùng thư mục riêng trong thư mục output và tự dọn dẹp. Kết quả TRX nằm trong `LibraryManagement.Tests/TestResults/` (không đưa vào Git).

## Phạm vi

- SQL Server thật: migration, mượn/gia hạn/trả; từ chối thẻ khóa/hết hạn, mã vạch trùng/không tồn tại/đang mượn; phạt quá hạn/hỏng/mất, thu phạt hai lần; đặt trước, chuyển hàng đợi và hủy đặt trước; hủy phiếu và rollback khi lỗi ở lần lưu thứ hai.
- Danh mục: thêm/sửa/xóa/khôi phục đầu sách, tạo bản sao ban đầu, tìm kiếm công khai; CRUD thể loại và kiểm tra trùng; thêm tác giả/nhà xuất bản/kệ.
- Quản trị: tạo tài khoản cán bộ, trùng tài khoản, chặn tự xóa, xóa/khôi phục và lưu quy định thư viện.
- Xác thực: đăng ký mở thẻ, mật khẩu + OTP độc giả, OTP cán bộ, khóa sau năm lần sai; OTP hết hạn, chống dùng lại, sai mục đích, gửi lại, thu hồi và giới hạn tần suất. Chặn đăng nhập khi tài khoản bị vô hiệu hóa trong thời gian chờ OTP.
- Báo cáo: số liệu mượn/quá hạn, xuất CSV, sách không có kệ, biên ngày, phiếu ngay sau nửa đêm; phân trang với `int.MaxValue`.
- HTTP: render Razor các trang công khai và quản trị, chi tiết/in phiếu, sửa sách; từ chối khách/độc giả vào trang quản trị; giới hạn quyền thủ thư; từ chối POST thiếu antiforgery và gửi form hợp lệ có antiforgery để kiểm tra dữ liệu đã lưu.
- Upload: từ chối định dạng/MIME không hỗ trợ, file rỗng/quá lớn; lưu và xóa ảnh cục bộ.

## Các lỗi đã tái hiện và sửa

1. Tràn số tính offset phân trang, gây SQL OFFSET âm hoặc trả nhầm trang đầu.
2. Báo cáo ném exception tại ngày nhỏ nhất/lớn nhất; chuẩn hóa khoảng chỉ có ngày bắt đầu ở tương lai.
3. Hủy phiếu nhiều cuốn lưu một phần khi database lỗi. Đã thêm transaction và test tiêm lỗi để kiểm chứng rollback bằng DbContext mới.
4. Độc giả/cán bộ bị vô hiệu hóa sau bước đầu vẫn hoàn tất OTP. Đã kiểm tra lại trạng thái trước xác thực OTP.
5. Bộ lọc báo cáo/lịch sử dùng ngày UTC trong khi ngày nghiệp vụ là ngày địa phương, bỏ sót phiếu sau nửa đêm. Đã dùng cùng offset từ IDateTimeProvider cho mốc ngày và nhóm thống kê.

## Giới hạn cần biết

- Đây là kiểm thử hồi quy các luồng nêu trên, không phải chứng nhận mọi nhánh code đều không có lỗi.
- HTTP test dùng TestServer với controller, Razor, DI, middleware và SQL thật; danh tính test thay thế bước xác thực cookie. Không chạy entry point Program/seed production hay kiểm tra tương tác JavaScript trong trình duyệt, bố cục, in giấy thực tế, hoặc cookie xuyên nhiều trình duyệt.
- Email dùng fake để nhận OTP trong test; không gửi email ra ngoài. Cloudinary trực tiếp chưa kiểm thử; đã kiểm thử lưu ảnh cục bộ.
- Dashboard và danh sách độc giả còn dùng MockAdminPortalService/dữ liệu mẫu. Hiển thị thành công không có nghĩa những màn hình này đã có nghiệp vụ cập nhật database thật.
- Chưa kiểm thử tải lớn hoặc nhiều quầy ghi đồng thời. CreateBorrowSlipAsync hiện chưa có rowversion/khóa cạnh tranh cho bản sao; cần xử lý và kiểm thử trước khi dùng nhiều quầy đồng thời.
- Chưa bao phủ mọi biến thể CRUD, xóa vĩnh viễn, miễn phạt, dữ liệu cũ và lỗi dịch vụ bên ngoài. Không chạy migration hay thay đổi dữ liệu thật trong đợt kiểm tra này.

Kiểm tra bổ sung: `node --check` cho site.js/admin.js/otp.js; `dotnet list app_management.slnx package --vulnerable --include-transitive` không báo package có lỗ hổng theo nguồn NuGet tại thời điểm kiểm tra.
