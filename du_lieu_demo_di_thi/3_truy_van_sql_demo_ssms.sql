-- ==============================================================================
-- BỘ TRUY VẤN SQL KIỂM TRA NHANH TRÊN SSMS KHI BẢO VỆ ĐỒ ÁN (EXAM MASTER DATASET V3)
-- Database: library_management (SQL Server)
-- ==============================================================================
USE [library_management];
GO

-- 1. Tổng hợp số lượng bản ghi trên toàn bộ 16 bảng nghiệp vụ (Active vs Thùng rác)
SELECT '01. library_settings' AS [Bang], COUNT(*) AS [Tong], SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END) AS [Active], SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) AS [Deleted_Trash] FROM dbo.library_settings
UNION ALL SELECT '02. reader_types', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.reader_types
UNION ALL SELECT '03. staff', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.staff
UNION ALL SELECT '04. shelves', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.shelves
UNION ALL SELECT '05. categories', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.categories
UNION ALL SELECT '06. publishers', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.publishers
UNION ALL SELECT '07. authors', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.authors
UNION ALL SELECT '08. readers', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.readers
UNION ALL SELECT '09. books', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.books
UNION ALL SELECT '10. book_authors', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.book_authors
UNION ALL SELECT '11. book_copies', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.book_copies
UNION ALL SELECT '12. book_images', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.book_images
UNION ALL SELECT '13. borrow_slips', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.borrow_slips
UNION ALL SELECT '14. borrow_details', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.borrow_details
UNION ALL SELECT '15. fines', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.fines
UNION ALL SELECT '16. reservations', COUNT(*), SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN status='DELETED' THEN 1 ELSE 0 END) FROM dbo.reservations;
GO

-- 2. Tra cứu nhanh Mã OTP 6 số mới nhất (Dùng khi phòng thi mất mạng / chờ Gmail lâu)
SELECT TOP 5
    email,
    otp_code AS [Ma_OTP_6_So],
    purpose AS [Muc_Dich],
    AttemptCount AS [So_Lan_Nhap_Sai],
    IsUsed AS [Da_Su_Dung],
    ExpiresAt AS [Het_Han_Luc],
    CreatedAt AS [Tao_Luc]
FROM dbo.otps
ORDER BY CreatedAt DESC;
GO

-- 3. Thống kê 28 Đầu sách kèm Số bản sao (Available / Borrowed / Reserved) & Số ảnh Gallery
SELECT
    b.isbn,
    b.title,
    cat.name AS [category_name],
    b.status AS [book_status],
    COUNT(DISTINCT CASE WHEN c.status = 'ACTIVE' THEN c.id END) AS [total_active_copies],
    COUNT(DISTINCT CASE WHEN c.status = 'ACTIVE' AND c.copy_status = 'AVAILABLE' THEN c.id END) AS [available_copies],
    COUNT(DISTINCT CASE WHEN c.status = 'ACTIVE' AND c.copy_status = 'BORROWED' THEN c.id END) AS [borrowed_copies],
    COUNT(DISTINCT CASE WHEN c.status = 'ACTIVE' AND c.copy_status = 'RESERVED' THEN c.id END) AS [reserved_copies],
    COUNT(DISTINCT CASE WHEN img.status = 'ACTIVE' THEN img.id END) AS [gallery_images]
FROM dbo.books b
JOIN dbo.categories cat ON cat.id = b.category_id
LEFT JOIN dbo.book_copies c ON c.book_id = b.id
LEFT JOIN dbo.book_images img ON img.book_id = b.id
GROUP BY b.isbn, b.title, cat.name, b.status, b.cover_url
ORDER BY b.cover_url;
GO

-- 4. Kiểm tra danh sách Sách đang mượn chưa trả (Open Loans)
SELECT
    r.card_no,
    r.full_name,
    c.barcode,
    b.title,
    d.due_date,
    d.renew_count,
    CASE WHEN d.due_date < CAST(GETDATE() AS DATE) THEN N'QUÁ HẠN' ELSE N'ĐANG MƯỢN' END AS [trang_thai_han]
FROM dbo.borrow_details d
JOIN dbo.borrow_slips s ON s.id = d.slip_id
JOIN dbo.readers r ON r.id = s.reader_id
JOIN dbo.book_copies c ON c.id = d.copy_id
JOIN dbo.books b ON b.id = c.book_id
WHERE d.returned_at IS NULL
ORDER BY d.due_date ASC, r.card_no ASC;
GO

-- 5. Kiểm tra danh sách Phạt (UNPAID, PAID, WAIVED)
SELECT
    f.fine_status,
    f.fine_type,
    f.amount,
    f.overdue_days,
    f.payment_method,
    r.card_no,
    r.full_name,
    c.barcode,
    f.note
FROM dbo.fines f
JOIN dbo.borrow_details d ON d.id = f.borrow_detail_id
JOIN dbo.borrow_slips s ON s.id = d.slip_id
JOIN dbo.readers r ON r.id = s.reader_id
JOIN dbo.book_copies c ON c.id = d.copy_id
ORDER BY f.fine_status, r.card_no;
GO

-- 6. Kiểm tra danh sách Đặt trước (READY, PENDING, FULFILLED, CANCELLED)
SELECT
    rv.reservation_status,
    r.card_no,
    r.full_name,
    b.isbn,
    b.title,
    c.barcode AS assigned_copy,
    rv.ready_at,
    rv.expires_at,
    rv.closed_at
FROM dbo.reservations rv
JOIN dbo.readers r ON r.id = rv.reader_id
JOIN dbo.books b ON b.id = rv.book_id
LEFT JOIN dbo.book_copies c ON c.id = rv.assigned_copy_id
ORDER BY rv.reservation_status, r.card_no;
GO
