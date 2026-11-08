using System;
using System.IO;
using System.Security;
using System.Text;

namespace WireframeGenerator;

public static class Program
{
    private static string Val(string html) => SecurityElement.Escape(html);

    public static void Main(string[] args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<mxfile host=\"app.diagrams.net\" modified=\"2026-11-07T14:30:00.000Z\" agent=\"Antigravity\" version=\"21.0.0\" type=\"device\">");

        // 1. Sitemap
        AppendSitemap(sb);

        // 2. Wireframe 01: Trang chu (/)
        AppendWf01Home(sb);

        // 3. Wireframe 02: Danh muc sach (/Books)
        AppendWf02Books(sb);

        // 4. Wireframe 03: Chi tiet sach (/Books/Detail/{id})
        AppendWf03Detail(sb);

        // 5. Wireframe 04: Dang nhap (/Account/Login)
        AppendWf04Login(sb);

        // 6. Wireframe 05: Dang ky the (/Account/Register)
        AppendWf04Register(sb);

        // 7. Wireframe 06: Xac thuc OTP (/Account/VerifyOtp)
        AppendWf05Otp(sb);

        // 8. Wireframe 07: Dashboard quan tri (/Admin/Dashboard)
        AppendWf07Dashboard(sb);

        // 9. Wireframe 08: Quan ly dau sach (/Admin/Books)
        AppendWf08AdminBooks(sb);

        // 10. Wireframe 09: Quay lap phieu muon (/Circulation/Borrow)
        AppendWf09Borrow(sb);

        // 11. Wireframe 10: Quay tra sach (/Circulation/Return)
        AppendWf10Return(sb);

        // 12. Wireframe 11: Quan ly tien phat (/Fines)
        AppendWf11Fines(sb);

        // 13. Wireframe 12: Quy dinh muon tra (/System/Settings)
        AppendWf12Settings(sb);

        // 14. Wireframe 13: Quan ly phieu muon (/Circulation/History)
        AppendWf13History(sb);

        // 15. Wireframe 14: Bao cao thong ke (/Reports)
        AppendWf14Reports(sb);

        sb.AppendLine("</mxfile>");

        var xml = sb.ToString();

        var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var drawioPath = Path.Combine(rootDir, "library_sitemap_and_wireframe.drawio");
        var drawPath = Path.Combine(rootDir, "library_sitemap_and_wireframe.draw");

        File.WriteAllText(drawioPath, xml, Encoding.UTF8);
        File.WriteAllText(drawPath, xml, Encoding.UTF8);

        Console.WriteLine($"Generated 15 diagrams successfully to:\n{drawioPath}\n{drawPath}");
    }

    private static void AppendSitemap(StringBuilder sb)
    {
        sb.AppendLine("  <diagram id=\"sitemap\" name=\"1. Sitemap - Kiến trúc toàn hệ thống\">");
        sb.AppendLine("    <mxGraphModel dx=\"1600\" dy=\"900\" grid=\"1\" gridSize=\"10\" guides=\"1\" tooltips=\"1\" connect=\"1\" arrows=\"1\" fold=\"1\" page=\"1\" pageScale=\"1\" pageWidth=\"1850\" pageHeight=\"1100\" math=\"0\" shadow=\"0\">");
        sb.AppendLine("      <root>");
        sb.AppendLine("        <mxCell id=\"0\" />");
        sb.AppendLine("        <mxCell id=\"1\" parent=\"0\" />");

        sb.AppendLine($"        <mxCell id=\"sm_root\" value=\"{Val("<b style=\"font-size: 16px;\">HỆ THỐNG QUẢN LÝ THƯ VIỆN ĐẠI HỌC GIAO THÔNG VẬN TẢI (UTC LIBRARY SYSTEM)</b><br><span style=\"font-size: 12px; font-weight: normal;\">Kiến trúc Điều hướng Phân hệ &amp; Danh mục Màn hình (ScreenConstants)</span>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;strokeColor=#0f0b24;fontColor=#ffffff;arcSize=14;shadow=1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"635\" y=\"30\" width=\"580\" height=\"70\" as=\"geometry\" /></mxCell>");

        // Columns
        sb.AppendLine($"        <mxCell id=\"sm_col1\" value=\"{Val("<b style=\"font-size: 13px;\">1. CỔNG KHÁCH &amp; XÁC THỰC</b><br><i style=\"font-size: 11px;\">(Public &amp; Authentication)</i>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f8cecc;strokeColor=#b85450;fontColor=#333333;fontStyle=1;arcSize=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"150\" width=\"300\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine("        <mxCell id=\"e_col1\" style=\"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;entryX=0.5;entryY=0;entryDx=0;entryDy=0;strokeWidth=2;strokeColor=#b85450;\" edge=\"1\" parent=\"1\" source=\"sm_root\" target=\"sm_col1\"><mxGeometry relative=\"1\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"sm_col2\" value=\"{Val("<b style=\"font-size: 13px;\">2. CỔNG DỊCH VỤ ĐỘC GIẢ</b><br><i style=\"font-size: 11px;\">(Reader Portal - Role: Reader)</i>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontColor=#333333;fontStyle=1;arcSize=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"380\" y=\"150\" width=\"310\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine("        <mxCell id=\"e_col2\" style=\"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;entryX=0.5;entryY=0;entryDx=0;entryDy=0;strokeWidth=2;strokeColor=#82b366;\" edge=\"1\" parent=\"1\" source=\"sm_root\" target=\"sm_col2\"><mxGeometry relative=\"1\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"sm_col3\" value=\"{Val("<b style=\"font-size: 13px;\">3. CỔNG LƯU THÔNG &amp; THU PHẠT</b><br><i style=\"font-size: 11px;\">(Circulation - Librarian &amp; Admin)</i>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontColor=#333333;fontStyle=1;arcSize=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"730\" y=\"150\" width=\"330\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine("        <mxCell id=\"e_col3\" style=\"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;entryX=0.5;entryY=0;entryDx=0;entryDy=0;strokeWidth=2;strokeColor=#d79b00;\" edge=\"1\" parent=\"1\" source=\"sm_root\" target=\"sm_col3\"><mxGeometry relative=\"1\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"sm_col4\" value=\"{Val("<b style=\"font-size: 13px;\">4. QUẢN TRỊ DANH MỤC &amp; SÁCH</b><br><i style=\"font-size: 11px;\">(Catalog - Librarian &amp; Admin)</i>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontColor=#333333;fontStyle=1;arcSize=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1100\" y=\"150\" width=\"330\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine("        <mxCell id=\"e_col4\" style=\"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;entryX=0.5;entryY=0;entryDx=0;entryDy=0;strokeWidth=2;strokeColor=#6c8ebf;\" edge=\"1\" parent=\"1\" source=\"sm_root\" target=\"sm_col4\"><mxGeometry relative=\"1\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"sm_col5\" value=\"{Val("<b style=\"font-size: 13px;\">5. BÁO CÁO &amp; CẤU HÌNH HỆ THỐNG</b><br><i style=\"font-size: 11px;\">(System &amp; Reports - Role: Admin)</i>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontColor=#333333;fontStyle=1;arcSize=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1470\" y=\"150\" width=\"310\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine("        <mxCell id=\"e_col5\" style=\"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;entryX=0.5;entryY=0;entryDx=0;entryDy=0;strokeWidth=2;strokeColor=#9673a6;\" edge=\"1\" parent=\"1\" source=\"sm_root\" target=\"sm_col5\"><mxGeometry relative=\"1\" as=\"geometry\" /></mxCell>");

        // Items Col 1
        AddBox(sb, "sm_c1_1", 40, 240, 300, 45, "#b85450", "<b>Trang chủ cổng thông tin</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: / | SCR_HOME</font>");
        AddBox(sb, "sm_c1_2", 40, 295, 300, 45, "#b85450", "<b>Đăng nhập Độc giả (Thẻ thư viện)</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Account/Login | SCR_LOGIN</font>");
        AddBox(sb, "sm_c1_3", 40, 350, 300, 45, "#b85450", "<b>Đăng nhập Cán bộ / Thủ thư</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Account/StaffLogin | SCR_STAFF_LOGIN</font>");
        AddBox(sb, "sm_c1_4", 40, 405, 300, 45, "#b85450", "<b>Đăng ký thẻ bạn đọc mới</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Account/Register | SCR_REGISTER</font>");
        AddBox(sb, "sm_c1_5", 40, 460, 300, 45, "#b85450", "<b>Xác thực mã bảo mật OTP</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Account/VerifyOtp | SCR_VERIFY_OTP</font>");
        AddBox(sb, "sm_c1_6", 40, 515, 300, 45, "#b85450", "<b>Màn hình Từ chối truy cập (403)</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Account/AccessDenied | SCR_ACCESS_DENIED</font>");

        // Items Col 2
        AddBox(sb, "sm_c2_1", 380, 240, 310, 45, "#82b366", "<b>Tra cứu &amp; Danh mục Sách mở</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Books | SCR_BOOK_CATALOG</font>");
        AddBox(sb, "sm_c2_2", 380, 295, 310, 45, "#82b366", "<b>Chi tiết Sách &amp; Đặt trước (Hold)</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Books/Detail/{id} | SCR_BOOK_DETAIL</font>");
        AddBox(sb, "sm_c2_3", 380, 350, 310, 45, "#82b366", "<b>Hồ sơ &amp; Thẻ thư viện cá nhân</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Reader/Profile | SCR_READER_PROFILE</font>");
        AddBox(sb, "sm_c2_4", 380, 405, 310, 45, "#82b366", "<b>Lịch sử mượn &amp; Gia hạn trực tuyến</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Reader/BorrowHistory | SCR_READER_BORROW_HISTORY</font>");
        AddBox(sb, "sm_c2_5", 380, 460, 310, 45, "#82b366", "<b>Tra cứu tiền phạt &amp; Biên lai nợ</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Reader/Fines | SCR_READER_FINES</font>");
        AddBox(sb, "sm_c2_6", 380, 515, 310, 45, "#82b366", "<b>Danh sách Sách đặt trước giữ chỗ</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Reader/Reservations | SCR_READER_RESERVATIONS</font>");

        // Items Col 3
        AddBox(sb, "sm_c3_1", 730, 240, 330, 45, "#d79b00", "<b>Lập phiếu mượn tài liệu tại quầy</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Circulation/Borrow | SCR_CIRCULATION_BORROW</font>");
        AddBox(sb, "sm_c3_2", 730, 295, 330, 45, "#d79b00", "<b>Nhận trả sách &amp; Thẩm định hư hại</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Circulation/Return | SCR_CIRCULATION_RETURN</font>");
        AddBox(sb, "sm_c3_3", 730, 350, 330, 45, "#d79b00", "<b>Quản lý Phiếu mượn &amp; Lịch sử lưu thông</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Circulation/History | SCR_CIRCULATION_HISTORY</font>");
        AddBox(sb, "sm_c3_4", 730, 405, 330, 45, "#d79b00", "<b>Quản lý Thu phạt &amp; Miễn giảm nợ</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Fines | SCR_MANAGE_FINES</font>");
        AddBox(sb, "sm_c3_5", 730, 460, 330, 45, "#d79b00", "<b>Quản lý Đặt trước &amp; Giữ sách</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Reservations | SCR_MANAGE_RESERVATIONS</font>");

        // Items Col 4
        AddBox(sb, "sm_c4_1", 1100, 240, 330, 45, "#6c8ebf", "<b>Bàn làm việc cán bộ (Dashboard)</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Admin/Dashboard | SCR_ADMIN_DASHBOARD</font>");
        AddBox(sb, "sm_c4_2", 1100, 295, 330, 45, "#6c8ebf", "<b>Quản lý Danh mục Đầu sách</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Admin/Books | SCR_MANAGE_BOOKS</font>");
        AddBox(sb, "sm_c4_3", 1100, 350, 330, 45, "#6c8ebf", "<b>Quản lý Cuốn sách con &amp; Mã vạch</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Admin/BookCopies | SCR_MANAGE_BOOK_COPIES</font>");
        AddBox(sb, "sm_c4_4", 1100, 405, 330, 45, "#6c8ebf", "<b>Quản lý Thể loại sách</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Admin/Categories | SCR_MANAGE_CATEGORIES</font>");
        AddBox(sb, "sm_c4_5", 1100, 460, 330, 45, "#6c8ebf", "<b>Quản lý Tác giả &amp; Nhà xuất bản</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Admin/Authors &amp; Publishers</font>");
        AddBox(sb, "sm_c4_6", 1100, 515, 330, 45, "#6c8ebf", "<b>Quản lý Kệ sách &amp; Vị trí lưu kho</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Admin/Shelves | SCR_MANAGE_SHELVES</font>");
        AddBox(sb, "sm_c4_7", 1100, 570, 330, 45, "#6c8ebf", "<b>Quản lý Hồ sơ Độc giả (Thẻ thư viện)</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Admin/Readers | SCR_MANAGE_READERS</font>");
        AddBox(sb, "sm_c4_8", 1100, 625, 330, 45, "#6c8ebf", "<b>Cấu hình Nhóm &amp; Loại độc giả</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Admin/ReaderTypes | SCR_MANAGE_READER_TYPES</font>");

        // Items Col 5
        AddBox(sb, "sm_c5_1", 1470, 240, 310, 45, "#9673a6", "<b>Báo cáo Thống kê Lưu thông &amp; Phạt</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /Reports | SCR_REPORTS_STATISTICS</font>");
        AddBox(sb, "sm_c5_2", 1470, 295, 310, 45, "#9673a6", "<b>Quản trị Tài khoản Cán bộ &amp; Phân quyền</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /System/Staff | SCR_SYSTEM_STAFF</font>");
        AddBox(sb, "sm_c5_3", 1470, 350, 310, 45, "#9673a6", "<b>Quy định Mượn trả &amp; Mức phạt vi phạm</b><br><font color=\"#666666\" style=\"font-size: 10px;\">Route: /System/Settings | SCR_SYSTEM_SETTINGS</font>");

        sb.AppendLine("      </root>");
        sb.AppendLine("    </mxGraphModel>");
        sb.AppendLine("  </diagram>");
    }

    private static void AddBox(StringBuilder sb, string id, int x, int y, int w, int h, string stroke, string html)
    {
        sb.AppendLine($"        <mxCell id=\"{id}\" value=\"{Val(html)}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor={stroke};align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" as=\"geometry\" /></mxCell>");
    }

    private static void StartDiagram(StringBuilder sb, string id, string name, string browserUrl, string browserTitle)
    {
        sb.AppendLine($"  <diagram id=\"{id}\" name=\"{Val(name)}\">");
        sb.AppendLine("    <mxGraphModel dx=\"1600\" dy=\"900\" grid=\"1\" gridSize=\"10\" guides=\"1\" tooltips=\"1\" connect=\"1\" arrows=\"1\" fold=\"1\" page=\"1\" pageScale=\"1\" pageWidth=\"1600\" pageHeight=\"1100\" math=\"0\" shadow=\"0\">");
        sb.AppendLine("      <root>");
        sb.AppendLine("        <mxCell id=\"0\" />");
        sb.AppendLine("        <mxCell id=\"1\" parent=\"0\" />");

        // Window & Browser Bar
        sb.AppendLine($"        <mxCell id=\"{id}_win\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#666666;arcSize=3;shadow=1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"30\" width=\"1520\" height=\"1020\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"{id}_bar\" value=\"{Val($"&nbsp;🔴 🟡 🟢&nbsp;&nbsp;&nbsp;&nbsp;<b>https://thuvien.utc.edu.vn{browserUrl}</b> — {browserTitle}")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#e9ecef;strokeColor=#cccccc;align=left;spacingLeft=15;fontColor=#495057;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"30\" width=\"1520\" height=\"35\" as=\"geometry\" /></mxCell>");
    }

    private static void EndDiagram(StringBuilder sb)
    {
        sb.AppendLine("      </root>");
        sb.AppendLine("    </mxGraphModel>");
        sb.AppendLine("  </diagram>");
    }

    // Helper to render Staff Shell: Sidebar + Top Header
    private static void AddStaffShell(StringBuilder sb, string prefix, string activeMenu, string breadcrumb, string pageTitle, string pageEyebrow, string pageDesc)
    {
        // Dark Sidebar
        sb.AppendLine($"        <mxCell id=\"{prefix}_side\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#1b1340;strokeColor=none;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"65\" width=\"240\" height=\"985\" as=\"geometry\" /></mxCell>");
        
        // Brand in sidebar
        sb.AppendLine($"        <mxCell id=\"{prefix}_sbrand\" value=\"{Val("<table cellpadding=\"0\" cellspacing=\"0\"><tr><td><div style=\"width: 32px; height: 32px; background: #eab308; border-radius: 50%; color: #1b1340; text-align: center; line-height: 32px; font-weight: bold; font-size: 13px;\">UTC</div></td><td style=\"padding-left: 10px;\"><b style=\"font-size: 14px; color: #ffffff;\">Thư viện UTC</b><br><span style=\"font-size: 11px; color: #94a3b8;\">Quản trị thư viện</span></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"55\" y=\"80\" width=\"210\" height=\"45\" as=\"geometry\" /></mxCell>");

        // Sidebar Navigation Groups
        string isDash = activeMenu == "Tổng quan" ? "background: #2a1f5d; border-left: 3px solid #818cf8; color: #ffffff; font-weight: bold;" : "color: #cbd5e1;";
        string isBorrow = activeMenu == "Mượn sách" ? "background: #2a1f5d; border-left: 3px solid #818cf8; color: #ffffff; font-weight: bold;" : "color: #cbd5e1;";
        string isReturn = activeMenu == "Trả sách" ? "background: #2a1f5d; border-left: 3px solid #818cf8; color: #ffffff; font-weight: bold;" : "color: #cbd5e1;";
        string isFines = activeMenu == "Thu phạt" ? "background: #2a1f5d; border-left: 3px solid #818cf8; color: #ffffff; font-weight: bold;" : "color: #cbd5e1;";
        string isHistory = activeMenu == "Phiếu mượn" ? "background: #2a1f5d; border-left: 3px solid #818cf8; color: #ffffff; font-weight: bold;" : "color: #cbd5e1;";
        string isBooks = activeMenu == "Đầu sách" ? "background: #2a1f5d; border-left: 3px solid #818cf8; color: #ffffff; font-weight: bold;" : "color: #cbd5e1;";
        string isReports = activeMenu == "Báo cáo" ? "background: #2a1f5d; border-left: 3px solid #818cf8; color: #ffffff; font-weight: bold;" : "color: #cbd5e1;";
        string isSettings = activeMenu == "Quy định mượn trả" ? "background: #2a1f5d; border-left: 3px solid #818cf8; color: #ffffff; font-weight: bold;" : "color: #cbd5e1;";

        sb.AppendLine($"        <mxCell id=\"{prefix}_snav\" value=\"{Val($"<div style=\"color: #64748b; font-size: 10px; font-weight: bold; padding: 10px 15px 4px 15px; text-transform: uppercase;\">Không gian làm việc</div><div style=\"{isDash} padding: 8px 15px; font-size: 12px;\">📊 Tổng quan</div><div style=\"color: #64748b; font-size: 10px; font-weight: bold; padding: 12px 15px 4px 15px; text-transform: uppercase;\">Nghiệp vụ hằng ngày</div><div style=\"{isBorrow} padding: 8px 15px; font-size: 12px;\">↗️ Mượn sách</div><div style=\"{isReturn} padding: 8px 15px; font-size: 12px;\">↙️ Trả sách</div><div style=\"{isFines} padding: 8px 15px; font-size: 12px;\">💰 Thu phạt</div><div style=\"{isHistory} padding: 8px 15px; font-size: 12px;\">📋 Phiếu mượn</div><div style=\"color: #64748b; font-size: 10px; font-weight: bold; padding: 12px 15px 4px 15px; text-transform: uppercase;\">Kho tài liệu &amp; bạn đọc</div><div style=\"{isBooks} padding: 8px 15px; font-size: 12px;\">📚 Đầu sách</div><div style=\"color: #cbd5e1; padding: 8px 15px; font-size: 12px;\">🏷️ Bản sao</div><div style=\"color: #cbd5e1; padding: 8px 15px; font-size: 12px;\">👤 Độc giả</div><div style=\"color: #cbd5e1; padding: 8px 15px; font-size: 12px;\">✏️ Tác giả</div><div style=\"color: #cbd5e1; padding: 8px 15px; font-size: 12px;\">🏷️ Thể loại</div><div style=\"color: #cbd5e1; padding: 8px 15px; font-size: 12px;\">🏢 Nhà xuất bản</div><div style=\"color: #cbd5e1; padding: 8px 15px; font-size: 12px;\">🗄️ Kệ sách</div><div style=\"color: #64748b; font-size: 10px; font-weight: bold; padding: 12px 15px 4px 15px; text-transform: uppercase;\">Phân tích</div><div style=\"{isReports} padding: 8px 15px; font-size: 12px;\">📈 Báo cáo</div><div style=\"color: #64748b; font-size: 10px; font-weight: bold; padding: 12px 15px 4px 15px; text-transform: uppercase;\">Quản trị hệ thống</div><div style=\"{isSettings} padding: 8px 15px; font-size: 12px;\">⚙️ Quy định mượn trả</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"135\" width=\"240\" height=\"750\" as=\"geometry\" /></mxCell>");

        // Bottom User in Sidebar
        sb.AppendLine($"        <mxCell id=\"{prefix}_suser\" value=\"{Val("<div style=\"color: #94a3b8; font-size: 11px;\">Về cổng bạn đọc ↗</div><div style=\"color: #ffffff; font-weight: bold; font-size: 12px; margin-top: 3px;\">Nguyễn Quốc Anh</div><div style=\"color: #94a3b8; font-size: 10px;\">Quản trị viên</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=bottom;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"55\" y=\"945\" width=\"210\" height=\"80\" as=\"geometry\" /></mxCell>");

        // Top Header
        sb.AppendLine($"        <mxCell id=\"{prefix}_thdr\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"280\" y=\"65\" width=\"1280\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"{prefix}_tbcrumb\" value=\"{Val($"<span style=\"color: #64748b; font-size: 12px;\">{breadcrumb}</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"72\" width=\"600\" height=\"40\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"{prefix}_tdate\" value=\"{Val("<span style=\"color: #64748b; font-size: 12px;\">07/11/2026</span> &nbsp;&nbsp;<span style=\"background: #f1f5f9; padding: 4px 10px; border-radius: 4px; font-size: 11px; color: #334155; font-weight: bold;\">👤 Quản trị viên</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=right;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1250\" y=\"72\" width=\"280\" height=\"40\" as=\"geometry\" /></mxCell>");

        // Page Head
        sb.AppendLine($"        <mxCell id=\"{prefix}_phead\" value=\"{Val($"<span style=\"color: #64748b; font-size: 11px; text-transform: uppercase; font-weight: bold;\">{pageEyebrow}</span><h1 style=\"margin: 2px 0 4px 0; font-size: 24px; color: #0f172a;\">{pageTitle}</h1><p style=\"margin: 0; color: #64748b; font-size: 12px;\">{pageDesc}</p>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"135\" width=\"850\" height=\"70\" as=\"geometry\" /></mxCell>");
    }

    // 01. TRANG CHỦ (/)
    private static void AppendWf01Home(StringBuilder sb)
    {
        StartDiagram(sb, "wf_01_home", "01. Wireframe - Trang chủ (/)", "/", "Trang chủ Cổng thông tin Thư viện UTC");

        // Topbar
        sb.AppendLine($"        <mxCell id=\"wf01_hdr\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"65\" width=\"1520\" height=\"65\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf01_brand\" value=\"{Val("<table cellpadding=\"0\" cellspacing=\"0\"><tr><td><div style=\"width: 36px; height: 36px; background: #eab308; border-radius: 50%; color: #1b1340; text-align: center; line-height: 36px; font-weight: bold; font-size: 13px;\">UTC</div></td><td style=\"padding-left: 12px;\"><b style=\"font-size: 14px; color: #1b1340;\">Thư viện UTC</b><br><span style=\"font-size: 11px; color: #64748b;\">Trường Đại học Giao thông Vận tải</span></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"60\" y=\"73\" width=\"350\" height=\"50\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf01_navlinks\" value=\"{Val("<a href=\"#\" style=\"color: #1b1340; font-weight: bold; text-decoration: underline; border-bottom: 2px solid #1b1340; padding-bottom: 4px;\">Trang chủ</a>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href=\"#\" style=\"color: #475569; text-decoration: none;\">Tra cứu tài liệu</a>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href=\"#\" style=\"color: #475569; text-decoration: none;\">Dịch vụ thư viện</a>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href=\"#\" style=\"color: #475569; text-decoration: none;\">Giới thiệu</a>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontSize=13;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"480\" y=\"73\" width=\"520\" height=\"50\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf01_auth_btns\" value=\"{Val("<a href=\"#\" style=\"color: #1b1340; font-size: 13px; font-weight: 500; text-decoration: none;\">Đăng nhập</a>&nbsp;&nbsp;&nbsp;&nbsp;<a href=\"#\" style=\"background: #1b1340; color: white; padding: 9px 18px; border-radius: 6px; font-size: 13px; font-weight: bold; text-decoration: none;\">Đăng ký thẻ</a>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=right;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1250\" y=\"73\" width=\"290\" height=\"50\" as=\"geometry\" /></mxCell>");

        // Notice Bar
        sb.AppendLine($"        <mxCell id=\"wf01_notice\" value=\"{Val("• <b>Thông tin bạn đọc:</b> Mang theo thẻ thư viện khi sử dụng dịch vụ mượn – trả tài liệu. &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href=\"#\" style=\"color: #1b1340; text-decoration: none;\">Lịch phục vụ →</a>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;fontColor=#475569;align=left;spacingLeft=25;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"130\" width=\"1520\" height=\"35\" as=\"geometry\" /></mxCell>");

        // Hero Pill
        sb.AppendLine($"        <mxCell id=\"wf01_eyebrow\" value=\"{Val("🏛️ CỔNG THÔNG TIN THƯ VIỆN KHOA HỌC &amp; CÔNG NGHỆ GTVT")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f1f5f9;strokeColor=#cbd5e1;fontColor=#475569;fontSize=11;fontStyle=1;arcSize=14;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"620\" y=\"200\" width=\"360\" height=\"32\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf01_hero_title\" value=\"{Val("<h1 style=\"margin: 0; font-size: 34px; color: #1b1340;\">Khám phá tri thức, mở rộng<br>tương lai.</h1><p style=\"margin: 12px 0 0 0; color: #64748b; font-size: 14px;\">Tra cứu và khám phá tài liệu phục vụ học tập, nghiên cứu và giảng dạy tại UTC.</p>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=center;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"450\" y=\"240\" width=\"700\" height=\"100\" as=\"geometry\" /></mxCell>");

        // Search Card
        sb.AppendLine($"        <mxCell id=\"wf01_scard\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=6;shadow=1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"340\" y=\"370\" width=\"920\" height=\"125\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf01_scopes\" value=\"{Val("<span style=\"background: #1b1340; color: white; padding: 7px 14px; border-radius: 4px; font-weight: bold; font-size: 12px;\">Tất cả tài liệu</span>&nbsp;&nbsp;<span style=\"color: #64748b; font-size: 12px; padding: 7px 10px;\">Giáo trình</span>&nbsp;&nbsp;<span style=\"color: #64748b; font-size: 12px; padding: 7px 10px;\">Luận văn &amp; Đồ án</span>&nbsp;&nbsp;<span style=\"color: #64748b; font-size: 12px; padding: 7px 10px;\">Tạp chí khoa học</span>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href=\"#\" style=\"color: #b45309; font-size: 12px; text-decoration: none; font-weight: 500;\">Tìm kiếm nâng cao ☵</a>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"360\" y=\"380\" width=\"880\" height=\"30\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf01_sin\" value=\"{Val("<span style=\"color: #94a3b8; font-size: 14px;\">🔍 &nbsp;Tên sách, tác giả hoặc ISBN</span>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=15;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"360\" y=\"425\" width=\"720\" height=\"50\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf01_sbtn\" value=\"{Val("🔍 <b>Tìm kiếm</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1095\" y=\"425\" width=\"145\" height=\"50\" as=\"geometry\" /></mxCell>");

        // Topic Pills (from home.png)
        sb.AppendLine($"        <mxCell id=\"wf01_tpills\" value=\"{Val("<b>CHỦ ĐỀ TÌM KIẾM:</b> &nbsp;&nbsp;<span style=\"background: #f1f5f9; border: 1px solid #e2e8f0; padding: 6px 12px; border-radius: 6px; font-size: 11px;\">💡 Cơ sở dữ liệu &amp; Hệ thống thông tin</span>&nbsp;&nbsp;<span style=\"background: #f1f5f9; border: 1px solid #e2e8f0; padding: 6px 12px; border-radius: 6px; font-size: 11px;\">💡 Công nghệ thông tin &amp; Lập trình</span>&nbsp;&nbsp;<span style=\"background: #f1f5f9; border: 1px solid #e2e8f0; padding: 6px 12px; border-radius: 6px; font-size: 11px;\">💡 Kinh tế &amp; Quản trị logistics</span>&nbsp;&nbsp;<span style=\"background: #f1f5f9; border: 1px solid #e2e8f0; padding: 6px 12px; border-radius: 6px; font-size: 11px;\">💡 Kỹ thuật Giao thông &amp; Cầu đường</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=center;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"200\" y=\"515\" width=\"1200\" height=\"35\" as=\"geometry\" /></mxCell>");

        // 4 Metric Stat Cards (from home.png)
        for (int i = 0; i < 4; i++)
        {
            int cx = 340 + i * 235;
            string icon = i == 0 ? "📖" : i == 1 ? "📁" : i == 2 ? "📋" : "📅";
            string num = i == 0 ? "6" : i == 1 ? "6" : i == 2 ? "5" : "14";
            string lbl = i == 0 ? "Sách mới trên trang" : i == 1 ? "Chuyên ngành tra cứu" : i == 2 ? "Sách / lượt mượn sinh viên" : "Ngày mượn sinh viên";

            sb.AppendLine($"        <mxCell id=\"wf01_st_{i}\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=6;shadow=1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"{cx}\" y=\"575\" width=\"215\" height=\"85\" as=\"geometry\" /></mxCell>");
            sb.AppendLine($"        <mxCell id=\"wf01_stt_{i}\" value=\"{Val($"<table cellpadding=\"0\" cellspacing=\"0\"><tr><td style=\"font-size: 24px; padding-right: 10px;\">{icon}</td><td><b style=\"font-size: 20px; color: #1b1340;\">{num}</b><br><span style=\"font-size: 11px; color: #64748b;\">{lbl}</span></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"{cx + 15}\" y=\"582\" width=\"185\" height=\"70\" as=\"geometry\" /></mxCell>");
        }

        // Home Note
        sb.AppendLine($"        <mxCell id=\"wf01_hnote\" value=\"{Val("<span style=\"color: #64748b; font-size: 11px;\">Hạn mức mượn theo cấu hình hiện tại; vui lòng xác nhận quy định tại quầy.</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=center;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"500\" y=\"675\" width=\"600\" height=\"25\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 02. DANH MỤC SÁCH (/Books - from 02_books.png)
    private static void AppendWf02Books(StringBuilder sb)
    {
        StartDiagram(sb, "wf_02_books", "02. Wireframe - Danh mục sách (/Books)", "/Books", "Tra cứu danh mục sách &amp; Bản sao có sẵn (UTC Library)");

        // Topbar
        sb.AppendLine($"        <mxCell id=\"wf02_hdr\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"65\" width=\"1520\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf02_brand\" value=\"{Val("<table cellpadding=\"0\" cellspacing=\"0\"><tr><td><div style=\"width: 32px; height: 32px; background: #eab308; border-radius: 50%; color: #1b1340; text-align: center; line-height: 32px; font-weight: bold; font-size: 11px;\">UTC</div></td><td style=\"padding-left: 10px;\"><b style=\"font-size: 13px; color: #1b1340;\">Thư viện UTC</b><br><span style=\"font-size: 10px; color: #64748b;\">Trường Đại học Giao thông Vận tải</span></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"60\" y=\"70\" width=\"320\" height=\"50\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf02_sin\" value=\"{Val("<span style=\"color: #94a3b8; font-size: 12px;\">Tên sách, tác giả hoặc ISBN &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;[ / ] &nbsp;🔍</span>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#cbd5e1;align=left;spacingLeft=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"420\" y=\"73\" width=\"420\" height=\"40\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf02_btns\" value=\"{Val("<a href=\"#\" style=\"color: #1b1340; font-size: 12px; text-decoration: underline;\">Đăng ký thẻ</a>&nbsp;&nbsp;&nbsp;&nbsp;<span style=\"border: 1px solid #cbd5e1; padding: 6px 14px; border-radius: 4px; font-size: 12px; font-weight: bold; color: #1b1340;\">Đăng nhập</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=right;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1300\" y=\"70\" width=\"240\" height=\"50\" as=\"geometry\" /></mxCell>");

        // Page Head
        sb.AppendLine($"        <mxCell id=\"wf02_phead\" value=\"{Val("<a href=\"#\" style=\"color: #64748b; font-size: 12px;\">Trang chủ</a><h1 style=\"margin: 6px 0 4px 0; font-size: 24px; color: #1b1340;\">Tra cứu danh mục sách</h1><p style=\"margin: 0; color: #64748b; font-size: 12px;\">Tìm tài liệu theo thể loại, tác giả, nhà xuất bản, năm xuất bản và kiểm tra số bản sao có sẵn trên kệ.</p>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"70\" y=\"135\" width=\"1000\" height=\"65\" as=\"geometry\" /></mxCell>");

        // Filter Box (Cream Background #f6f4ee from 02_books.png)
        sb.AppendLine($"        <mxCell id=\"wf02_fbox\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f6f4ee;strokeColor=#e7e2d4;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"70\" y=\"210\" width=\"1450\" height=\"125\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf02_f_kw\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Tên sách, tác giả hoặc ISBN</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px; color: #94a3b8;\">Nhập từ khóa...</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"95\" y=\"225\" width=\"380\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf02_f_cat\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Thể loại</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px;\">Tất cả thể loại ▼</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"495\" y=\"225\" width=\"280\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf02_f_yr\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Năm xuất bản</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px; color: #94a3b8;\">VD: 2023</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"795\" y=\"225\" width=\"180\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf02_btn_s\" value=\"{Val("🔍 <b>Tìm sách</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1000\" y=\"243\" width=\"120\" height=\"38\" as=\"geometry\" /></mxCell>");

        // Filter Row 2 (from 02_books.png)
        sb.AppendLine($"        <mxCell id=\"wf02_frow2\" value=\"{Val("Tác giả: <span style=\"background: #fff; border: 1px solid #cbd5e1; padding: 5px 12px; border-radius: 4px; font-size: 12px;\">Tất cả tác giả ▼</span>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;Nhà xuất bản: <span style=\"background: #fff; border: 1px solid #cbd5e1; padding: 5px 12px; border-radius: 4px; font-size: 12px;\">Tất cả NXB ▼</span>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<label><input type=\"checkbox\"> Chỉ sách còn bản sẵn sàng</label>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href=\"#\" style=\"color: #1b1340; text-decoration: underline;\">Xóa bộ lọc</a>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"95\" y=\"290\" width=\"1000\" height=\"35\" as=\"geometry\" /></mxCell>");

        // Results Count
        sb.AppendLine($"        <mxCell id=\"wf02_count\" value=\"{Val("Tìm thấy <b>7</b> đầu sách")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontSize=12;color=#475569;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"70\" y=\"345\" width=\"300\" height=\"30\" as=\"geometry\" /></mxCell>");

        // Book Cards (Exact from 02_books.png)
        string[] titles = ["Tôi Thấy Hoa Vàng Trên Cỏ Xanh", "Quy hoạch và Tổ chức Giao thông Đô thị thông minh", "Giáo trình Mạng máy tính căn bản & nâng cao", "Giáo trình Thiết kế Hệ thống Cơ sở dữ liệu và SQL Server"];
        string[] authors = ["Nguyễn Nhật Ánh", "PGS.TS. Trần Đình Quế", "Andrew S. Tanenbaum", "GS.TS. Nguyễn Thanh Thủy"];
        string[] pubs = ["NXB Trẻ, 2022", "NXB Giao Thông Vận Tải, 2024", "NXB Thông Tin & Truyền Thông, 2022", "NXB Bách Khoa Hà Nội, 2023"];
        string[] shelves = ["KE-VH-01", "KE-GT-01", "KE-IT-02", "KE-IT-02"];
        string[] avails = ["Còn 1/2", "Còn 2/2", "Còn 2/2", "Còn 2/3"];

        for (int i = 0; i < 4; i++)
        {
            int ry = 385 + i * 115;
            sb.AppendLine($"        <mxCell id=\"wf02_card_{i}\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"70\" y=\"{ry}\" width=\"1450\" height=\"105\" as=\"geometry\" /></mxCell>");
            sb.AppendLine($"        <mxCell id=\"wf02_cimg_{i}\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#2d5a3f;strokeColor=#1e3d2b;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"90\" y=\"{ry + 15}\" width=\"55\" height=\"75\" as=\"geometry\" /></mxCell>");
            sb.AppendLine($"        <mxCell id=\"wf02_ctxt_{i}\" value=\"{Val($"<b style=\"font-size: 15px; color: #1b1340;\">{titles[i]}</b><br><span style=\"color: #475569; font-size: 12px;\">{authors[i]}</span><br><span style=\"color: #64748b; font-size: 11px;\">{pubs[i]}</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"160\" y=\"{ry + 15}\" width=\"800\" height=\"75\" as=\"geometry\" /></mxCell>");
            sb.AppendLine($"        <mxCell id=\"wf02_cst_{i}\" value=\"{Val($"<div style=\"text-align: right;\"><span style=\"background: #ecfdf5; color: #047857; padding: 4px 10px; border-radius: 12px; font-weight: bold; font-size: 11px;\">✓ {avails[i]}</span><br><span style=\"color: #64748b; font-size: 11px; display: inline-block; margin-top: 6px;\">{shelves[i]}</span></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=right;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1320\" y=\"{ry + 20}\" width=\"180\" height=\"65\" as=\"geometry\" /></mxCell>");
        }

        EndDiagram(sb);
    }

    // 03. CHI TIẾT SÁCH (/Books/Detail/{id} - from 03_detail.png)
    private static void AppendWf03Detail(StringBuilder sb)
    {
        StartDiagram(sb, "wf_03_detail", "03. Wireframe - Chi tiết sách (/Books/Detail/{id})", "/Books/Detail/b3ac9d66", "Chi tiết tài liệu &amp; Bộ ảnh đọc thử (UTC Library)");

        // Topbar
        sb.AppendLine($"        <mxCell id=\"wf03_top\" value=\"{Val("<a href=\"#\" style=\"color: #1b1340; text-decoration: none; font-size: 13px;\">← <u>Trở về danh mục sách</u></a>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"70\" y=\"75\" width=\"300\" height=\"35\" as=\"geometry\" /></mxCell>");

        // Left Book Cover Box (Green cover from 03_detail.png)
        sb.AppendLine($"        <mxCell id=\"wf03_cov_box\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1e3d2b;strokeColor=#142b1e;arcSize=6;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"70\" y=\"120\" width=\"300\" height=\"420\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf03_cov_badge\" value=\"{Val("Ảnh bìa trước")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#000000;fontColor=#ffffff;fontSize=10;arcSize=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"85\" y=\"135\" width=\"90\" height=\"24\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf03_cov_txt\" value=\"{Val("<b style=\"font-size: 16px; color: white;\">Tôi Thấy Hoa Vàng Trên Cỏ Xanh</b><br><span style=\"color: #a7f3d0; font-size: 11px;\">Nguyễn Nhật Ánh</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"85\" y=\"200\" width=\"270\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf03_btn_read\" value=\"{Val("⛶ <b>Đọc trang sách (4)</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#cbd5e1;fontColor=#1b1340;fontSize=11;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"180\" y=\"500\" width=\"180\" height=\"32\" as=\"geometry\" /></mxCell>");

        // Thumbs strip (from 03_detail.png)
        sb.AppendLine($"        <mxCell id=\"wf03_thumbs_lbl\" value=\"{Val("<b>📷 Ảnh bìa, Mục lục &amp; Trang đọc thử (4 ảnh):</b>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontSize=11;color=#334155;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"70\" y=\"550\" width=\"300\" height=\"25\" as=\"geometry\" /></mxCell>");
        for (int i = 0; i < 4; i++)
        {
            int tx = 70 + (i % 3) * 95;
            int ty = 580 + (i / 3) * 115;
            sb.AppendLine($"        <mxCell id=\"wf03_th_{i}\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#cbd5e1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"{tx}\" y=\"{ty}\" width=\"80\" height=\"105\" as=\"geometry\" /></mxCell>");
        }

        // Right Column: Book Metadata (Exact from 03_detail.png)
        sb.AppendLine($"        <mxCell id=\"wf03_cat_p\" value=\"{Val("<span style=\"color: #64748b; font-size: 11px;\">Văn học &amp; Kỹ năng mềm</span><h1 style=\"margin: 4px 0 6px 0; font-size: 26px; color: #1b1340;\">Tôi Thấy Hoa Vàng Trên Cỏ Xanh</h1><span style=\"color: #475569; font-size: 14px;\">Nguyễn Nhật Ánh</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"420\" y=\"120\" width=\"600\" height=\"80\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf03_avail_p\" value=\"{Val("✓ Còn 1/2 bản sẵn sàng")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ecfdf5;fontColor=#047857;fontSize=12;fontStyle=1;strokeColor=#a7f3d0;arcSize=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"420\" y=\"215\" width=\"180\" height=\"30\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf03_desc\" value=\"{Val("<p style=\"color: #334155; font-size: 14px; margin: 15px 0 10px 0;\">Tác phẩm văn học đoạt giải, bức tranh tuổi thơ mộc mạc làng quê Việt Nam.</p><table width=\"100%\" style=\"font-size: 13px; color: #334155; line-height: 2.2;\"><tr><td width=\"25%\" style=\"color: #64748b;\">Nhà xuất bản</td><td width=\"75%\"><b>NXB Trẻ</b></td></tr><tr><td style=\"color: #64748b;\">Năm xuất bản</td><td><b>2022</b></td></tr><tr><td style=\"color: #64748b;\">Chuyên ngành</td><td><u>Văn học &amp; Kỹ năng mềm</u></td></tr><tr><td style=\"color: #64748b;\">Vị trí kệ chính</td><td><b>KE-VH-01</b></td></tr><tr><td style=\"color: #64748b;\">ISBN</td><td><b>978-6042081234</b></td></tr><tr><td style=\"color: #64748b;\">Ấn bản</td><td>Tái bản lần thứ 45</td></tr><tr><td style=\"color: #64748b;\">Ngôn ngữ</td><td>Tiếng Việt</td></tr><tr><td style=\"color: #64748b;\">Số trang</td><td>378 trang</td></tr><tr><td style=\"color: #64748b;\">Tổng số bản lưu hành</td><td>2 bản</td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"420\" y=\"260\" width=\"800\" height=\"320\" as=\"geometry\" /></mxCell>");

        // Physical Copies Table (from 03_detail.png)
        sb.AppendLine($"        <mxCell id=\"wf03_copies_title\" value=\"{Val("<b style=\"font-size: 15px; color: #1b1340;\">Danh sách bản sao vật lý (2 cuốn)</b>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"420\" y=\"600\" width=\"400\" height=\"30\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf03_c_tbl\" value=\"{Val("<table width=\"100%\" style=\"font-size: 12px; border-collapse: collapse;\"><tr style=\"border-bottom: 1px solid #cbd5e1; color: #64748b; font-weight: bold;\"><td width=\"25%\">Mã vạch (Barcode)</td><td width=\"25%\">Vị trí kệ</td><td width=\"25%\">Khu vực</td><td width=\"25%\" align=\"right\">Trạng thái</td></tr><tr style=\"border-bottom: 1px solid #e2e8f0; height: 40px;\"><td><b>BC-202610-0016</b></td><td>KE-VH-01</td><td>Kệ tầng 2 - Văn học &amp; Kỹ năng sống</td><td align=\"right\"><span style=\"background: #ecfdf5; color: #047857; padding: 2px 8px; border-radius: 8px; font-weight: bold;\">✓ Sẵn sàng</span></td></tr><tr style=\"height: 40px;\"><td><b>BC-202610-0017</b></td><td>KE-VH-01</td><td>Kệ tầng 2 - Văn học &amp; Kỹ năng sống</td><td align=\"right\"><span style=\"background: #eff6ff; color: #1e40af; padding: 2px 8px; border-radius: 8px;\">Đang mượn</span></td></tr></table>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;align=left;spacingLeft=15;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"420\" y=\"640\" width=\"1080\" height=\"120\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 04. ĐĂNG NHẬP (/Account/Login - from 04_login.png)
    private static void AppendWf04Login(StringBuilder sb)
    {
        StartDiagram(sb, "wf_04_login", "04. Wireframe - Đăng nhập (/Account/Login)", "/Account/Login", "Đăng nhập Độc giả &amp; Cán bộ (UTC Library)");

        // Topbar
        sb.AppendLine($"        <mxCell id=\"wf04_hdr\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"65\" width=\"1520\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04_brand\" value=\"{Val("<table cellpadding=\"0\" cellspacing=\"0\"><tr><td><div style=\"width: 32px; height: 32px; background: #eab308; border-radius: 50%; color: #1b1340; text-align: center; line-height: 32px; font-weight: bold; font-size: 11px;\">UTC</div></td><td style=\"padding-left: 10px;\"><b style=\"font-size: 13px; color: #1b1340;\">Thư viện UTC</b><br><span style=\"font-size: 10px; color: #64748b;\">Trường Đại học Giao thông Vận tải</span></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"60\" y=\"70\" width=\"320\" height=\"50\" as=\"geometry\" /></mxCell>");

        // Center Login Card (Exact from 04_login.png)
        sb.AppendLine($"        <mxCell id=\"wf04_card\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;shadow=1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"540\" y=\"160\" width=\"520\" height=\"620\" as=\"geometry\" /></mxCell>");

        // Heading & Step Indicator
        sb.AppendLine($"        <mxCell id=\"wf04_title\" value=\"{Val("<h1 style=\"margin: 0 0 10px 0; font-size: 26px; color: #1b1340;\">Đăng nhập</h1><span style=\"background: #1b1340; color: white; padding: 2px 7px; border-radius: 4px; font-weight: bold; font-size: 11px;\">1</span> &nbsp;<b>Nhập thông tin</b> &nbsp;———&nbsp; <span style=\"border: 1px solid #cbd5e1; padding: 2px 7px; border-radius: 4px; font-size: 11px; color: #64748b;\">2</span> &nbsp;<span style=\"color: #64748b;\">Nhập mã xác thực</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"575\" y=\"195\" width=\"450\" height=\"65\" as=\"geometry\" /></mxCell>");

        // Segmented Buttons: [Độc giả] [Cán bộ]
        sb.AppendLine($"        <mxCell id=\"wf04_seg_bg\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f1f5f9;strokeColor=#cbd5e1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"575\" y=\"275\" width=\"450\" height=\"44\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04_seg_btn1\" value=\"{Val("<b>Độc giả</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#cbd5e1;fontColor=#1b1340;fontStyle=1;arcSize=4;shadow=1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"578\" y=\"278\" width=\"220\" height=\"38\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04_seg_btn2\" value=\"{Val("Cán bộ")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=center;verticalAlign=middle;fontColor=#64748b;fontStyle=1;fontSize=13;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"805\" y=\"278\" width=\"220\" height=\"38\" as=\"geometry\" /></mxCell>");

        // Inputs
        sb.AppendLine($"        <mxCell id=\"wf04_f1\" value=\"{Val("<span style=\"font-size: 12px; font-weight: bold; color: #334155;\">Tên đăng nhập hoặc email</span><br><div style=\"background: #fff; border: 2px solid #1b1340; border-radius: 4px; padding: 10px; margin-top: 5px; font-size: 14px;\">|</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"575\" y=\"335\" width=\"450\" height=\"65\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04_f2\" value=\"{Val("<span style=\"font-size: 12px; font-weight: bold; color: #334155;\">Mật khẩu</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; border-radius: 4px; padding: 10px; margin-top: 5px; font-size: 14px; color: #94a3b8;\">•••••••••••• &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;👁️</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"575\" y=\"415\" width=\"450\" height=\"65\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf04_rem\" value=\"{Val("<label><input type=\"checkbox\"> Ghi nhớ đăng nhập</label>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontSize=12;color=#475569;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"575\" y=\"490\" width=\"300\" height=\"25\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04_submit\" value=\"{Val("<b>Tiếp tục</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;fontSize=14;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"575\" y=\"530\" width=\"450\" height=\"44\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf04_subnote\" value=\"{Val("<span style=\"color: #64748b; font-size: 11px;\">Mã xác thực sẽ gửi tới email của bạn.</span><br><hr style=\"border: none; border-top: 1px solid #f1f5f9; margin: 15px 0;\"><span style=\"font-size: 12px; color: #475569;\">Chưa có thẻ? <a href=\"#\" style=\"color: #1b1340; text-decoration: underline;\">Đăng ký thẻ thư viện</a></span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"575\" y=\"590\" width=\"450\" height=\"70\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 05. ĐĂNG KÝ THẺ (/Account/Register - from 04_register.png)
    private static void AppendWf04Register(StringBuilder sb)
    {
        StartDiagram(sb, "wf_04_register", "05. Wireframe - Đăng ký thẻ (/Account/Register)", "/Account/Register", "Đăng ký thẻ thư viện trực tuyến (UTC Library)");

        sb.AppendLine($"        <mxCell id=\"wf04r_hdr\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"40\" y=\"65\" width=\"1520\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04r_brand\" value=\"{Val("<table cellpadding=\"0\" cellspacing=\"0\"><tr><td><div style=\"width: 32px; height: 32px; background: #eab308; border-radius: 50%; color: #1b1340; text-align: center; line-height: 32px; font-weight: bold; font-size: 11px;\">UTC</div></td><td style=\"padding-left: 10px;\"><b style=\"font-size: 13px; color: #1b1340;\">Thư viện UTC</b><br><span style=\"font-size: 10px; color: #64748b;\">Trường Đại học Giao thông Vận tải</span></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"60\" y=\"70\" width=\"320\" height=\"50\" as=\"geometry\" /></mxCell>");

        // Center Register Card (from 04_register.png)
        sb.AppendLine($"        <mxCell id=\"wf04r_card\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;shadow=1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"420\" y=\"145\" width=\"760\" height=\"790\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf04r_head\" value=\"{Val("<h1 style=\"margin: 0 0 10px 0; font-size: 26px; color: #1b1340;\">Đăng ký thẻ thư viện</h1><span style=\"background: #1b1340; color: white; padding: 2px 7px; border-radius: 4px; font-weight: bold; font-size: 11px;\">1</span> &nbsp;<b>Nhập thông tin</b> &nbsp;———&nbsp; <span style=\"border: 1px solid #cbd5e1; padding: 2px 7px; border-radius: 4px; font-size: 11px; color: #64748b;\">2</span> &nbsp;<span style=\"color: #64748b;\">Nhập mã xác thực</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"455\" y=\"175\" width=\"690\" height=\"65\" as=\"geometry\" /></mxCell>");

        // 2 Columns of Form Fields (from 04_register.png)
        sb.AppendLine($"        <mxCell id=\"wf04r_f1\" value=\"{Val("<b>Họ và tên</b><br><div style=\"background: #fff; border: 2px solid #1b1340; padding: 8px 10px; border-radius: 4px; margin-top: 4px;\">|</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"455\" y=\"250\" width=\"330\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04r_f2\" value=\"{Val("<b>Tên đăng nhập</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px;\"></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"815\" y=\"250\" width=\"330\" height=\"60\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf04r_f3\" value=\"{Val("<b>Email</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px;\"></div><span style=\"color: #64748b; font-size: 10px;\">Mã xác thực sẽ gửi tới email này.</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"455\" y=\"325\" width=\"330\" height=\"75\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04r_f4\" value=\"{Val("<b>Số điện thoại</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px; color: #94a3b8;\">0912345678</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"815\" y=\"325\" width=\"330\" height=\"75\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf04r_f5\" value=\"{Val("<b>Loại độc giả</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px;\">Chọn loại độc giả ▼</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"455\" y=\"410\" width=\"330\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04r_f6\" value=\"{Val("<b>Mã sinh viên (nếu có)</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px;\"></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"815\" y=\"410\" width=\"330\" height=\"60\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf04r_f7\" value=\"{Val("<b>Ngày sinh (không bắt buộc)</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px; color: #94a3b8;\">mm/dd/yyyy &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;📅</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"455\" y=\"485\" width=\"330\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04r_f8\" value=\"{Val("<b>Địa chỉ (không bắt buộc)</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px;\"></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"815\" y=\"485\" width=\"330\" height=\"60\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf04r_f9\" value=\"{Val("<b>Mật khẩu</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px; color: #94a3b8;\">•••••••• &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;👁️</div><span style=\"color: #64748b; font-size: 10px;\">Ít nhất 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"455\" y=\"560\" width=\"330\" height=\"75\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04r_f10\" value=\"{Val("<b>Xác nhận mật khẩu</b><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 8px 10px; border-radius: 4px; margin-top: 4px;\"></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;fontSize=12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"815\" y=\"560\" width=\"330\" height=\"75\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf04r_btn\" value=\"{Val("<b>Tiếp tục</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;fontSize=13;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"455\" y=\"655\" width=\"130\" height=\"40\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf04r_note\" value=\"{Val("<span style=\"color: #64748b; font-size: 11px;\">Bước tiếp theo: nhập mã 6 số gửi tới email của bạn.</span><br><hr style=\"border: none; border-top: 1px solid #f1f5f9; margin: 15px 0;\"><span style=\"font-size: 12px; color: #475569;\">Đã có thẻ? <a href=\"#\" style=\"color: #1b1340; text-decoration: underline;\">Đăng nhập</a></span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"455\" y=\"705\" width=\"690\" height=\"60\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 06. XÁC THỰC OTP (/Account/VerifyOtp)
    private static void AppendWf05Otp(StringBuilder sb)
    {
        StartDiagram(sb, "wf_05_otp", "06. Wireframe - Xác thực OTP (/Account/VerifyOtp)", "/Account/VerifyOtp", "Nhập mã xác thực 6 số (UTC Library Security)");

        sb.AppendLine($"        <mxCell id=\"wf05_card\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;shadow=1;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"520\" y=\"150\" width=\"560\" height=\"580\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf05_head\" value=\"{Val("<h1 style=\"margin: 0 0 10px 0; font-size: 26px; color: #1b1340;\">Nhập mã xác thực</h1><span style=\"border: 1px solid #cbd5e1; padding: 2px 7px; border-radius: 4px; font-size: 11px; color: #64748b;\">1</span> &nbsp;<span style=\"color: #64748b;\">Nhập thông tin</span> &nbsp;———&nbsp; <span style=\"background: #1b1340; color: white; padding: 2px 7px; border-radius: 4px; font-weight: bold; font-size: 11px;\">2</span> &nbsp;<b>Nhập mã xác thực</b>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"555\" y=\"180\" width=\"490\" height=\"65\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf05_txt\" value=\"{Val("<p style=\"margin: 0; color: #334155; font-size: 13px;\">Mã gồm 6 số, đã gửi tới <b>an***@utc.edu.vn</b>.<br><span style=\"color: #64748b;\">Mã hết hạn sau <b style=\"color: #b45309;\">04:45</b>.</span></p>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"555\" y=\"260\" width=\"490\" height=\"45\" as=\"geometry\" /></mxCell>");

        // 6 OTP Boxes
        sb.AppendLine($"        <mxCell id=\"wf05_lbl\" value=\"{Val("<b>Mã xác thực</b>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontSize=12;color=#334155;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"555\" y=\"320\" width=\"200\" height=\"20\" as=\"geometry\" /></mxCell>");

        for (int i = 0; i < 6; i++)
        {
            int bx = 555 + i * 75;
            string valChar = i == 0 ? "8" : i == 1 ? "2" : i == 2 ? "9" : i == 3 ? "1" : i == 4 ? "0" : "4";
            sb.AppendLine($"        <mxCell id=\"wf05_b_{i}\" value=\"{Val($"<b style=\"font-size: 24px; color: #1b1340;\">{valChar}</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#1b1340;strokeWidth=2;align=center;arcSize=8;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"{bx}\" y=\"350\" width=\"60\" height=\"68\" as=\"geometry\" /></mxCell>");
        }

        sb.AppendLine($"        <mxCell id=\"wf05_btn\" value=\"{Val("<b>Xác nhận mã</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;fontSize=14;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"555\" y=\"445\" width=\"490\" height=\"44\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf05_resend\" value=\"{Val("<div style=\"text-align: center; font-size: 12px; color: #64748b;\">Chưa nhận được mã? <a href=\"#\" style=\"color: #1b1340; font-weight: bold;\">Gửi lại mã mới (Chờ 42s)</a></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=center;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"555\" y=\"515\" width=\"490\" height=\"35\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 07. DASHBOARD NHÂN VIÊN (/Admin/Dashboard - from 07_dashboard.png)
    private static void AppendWf07Dashboard(StringBuilder sb)
    {
        StartDiagram(sb, "wf_07_dashboard", "07. Wireframe - Dashboard nhân viên (/Admin/Dashboard)", "/Admin/Dashboard", "Tổng quan vận hành thư viện (UTC Library)");

        AddStaffShell(sb, "wf07", "Tổng quan", "Quản trị thư viện &nbsp;&gt;&nbsp; <b>Tổng quan vận hành</b>", "Tổng quan vận hành", "Không gian làm việc", "Theo dõi lưu thông tài liệu và những việc cần xử lý tại thư viện.");

        // Top right button
        sb.AppendLine($"        <mxCell id=\"wf07_btn_new\" value=\"{Val("+ Lập phiếu mượn")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1370\" y=\"145\" width=\"150\" height=\"38\" as=\"geometry\" /></mxCell>");

        // 4 KPI Metric Cards (from 07_dashboard.png)
        string[] labels = ["Phiếu đang mở", "Phiếu quá hạn", "Thẻ sắp hết hạn", "Bản sách sẵn sàng"];
        string[] nums = ["7", "2", "2", "13"];
        string[] notes = ["Theo dõi hạn trả ↗", "Cần nhắc trả tài liệu ↗", "Trong 30 ngày tới ↗", "Trong 17 bản sách ↗"];
        string[] icons = ["📋", "⚠️", "🪪", "📖"];

        for (int i = 0; i < 4; i++)
        {
            int cx = 310 + i * 305;
            sb.AppendLine($"        <mxCell id=\"wf07_kpi_{i}\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"{cx}\" y=\"225\" width=\"290\" height=\"120\" as=\"geometry\" /></mxCell>");
            sb.AppendLine($"        <mxCell id=\"wf07_kpit_{i}\" value=\"{Val($"<table width=\"100%\"><tr><td style=\"font-size: 11px; color: #64748b;\">{labels[i]}</td><td align=\"right\" style=\"font-size: 14px;\">{icons[i]}</td></tr></table><b style=\"font-size: 32px; color: #1b1340; line-height: 1.5;\">{nums[i]}</b><br><span style=\"color: #64748b; font-size: 11px;\">{notes[i]}</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"{cx + 15}\" y=\"235\" width=\"260\" height=\"100\" as=\"geometry\" /></mxCell>");
        }

        // Left Panel: Ưu tiên xử lý - Tài liệu đang quá hạn 2 (from 07_dashboard.png)
        sb.AppendLine($"        <mxCell id=\"wf07_p1\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"365\" width=\"780\" height=\"350\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf07_p1_hdr\" value=\"{Val("<table width=\"100%\"><tr><td><span style=\"color: #b45309; font-size: 10px; font-weight: bold;\">Ưu tiên xử lý</span><br><b style=\"font-size: 15px; color: #1b1340;\">Tài liệu đang quá hạn</b> &nbsp;<span style=\"background: #fee2e2; color: #dc2626; padding: 2px 7px; border-radius: 4px; font-weight: bold; font-size: 11px;\">2</span></td><td align=\"right\"><a href=\"#\" style=\"color: #1b1340; font-size: 11px;\">Xem tất cả →</a></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"375\" width=\"740\" height=\"45\" as=\"geometry\" /></mxCell>");

        // 2 Overdue Cards with Red Date Boxes (from 07_dashboard.png)
        sb.AppendLine($"        <mxCell id=\"wf07_od1\" value=\"{Val("<table width=\"100%\"><tr><td width=\"12%\"><div style=\"border: 1px solid #dc2626; border-radius: 4px; padding: 6px; text-align: center; color: #dc2626;\"><b style=\"font-size: 16px;\">04</b><br><span style=\"font-size: 10px;\">11/2026</span></div></td><td width=\"68%\" style=\"padding-left: 12px;\"><b style=\"font-size: 14px; color: #1b1340;\">Cơ học đất</b><br><span style=\"color: #64748b; font-size: 11px;\">Lê Minh Châu &nbsp;2021000123</span><br><span style=\"color: #dc2626; font-size: 11px; font-weight: bold;\">Quá hạn 3 ngày</span></td><td width=\"20%\" align=\"right\"><a href=\"#\" style=\"color: #1b1340; font-size: 12px; font-weight: bold;\">Nhận trả →</a></td></tr></table>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#f1f5f9;align=left;spacingLeft=15;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"435\" width=\"740\" height=\"85\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf07_od2\" value=\"{Val("<table width=\"100%\"><tr><td width=\"12%\"><div style=\"border: 1px solid #dc2626; border-radius: 4px; padding: 6px; text-align: center; color: #dc2626;\"><b style=\"font-size: 16px;\">06</b><br><span style=\"font-size: 10px;\">11/2026</span></div></td><td width=\"68%\" style=\"padding-left: 12px;\"><b style=\"font-size: 14px; color: #1b1340;\">Quản trị chuỗi cung ứng và logistics</b><br><span style=\"color: #64748b; font-size: 11px;\">Phạm Ngọc Anh &nbsp;2023000234</span><br><span style=\"color: #dc2626; font-size: 11px; font-weight: bold;\">Quá hạn 1 ngày</span></td><td width=\"20%\" align=\"right\"><a href=\"#\" style=\"color: #1b1340; font-size: 12px; font-weight: bold;\">Nhận trả →</a></td></tr></table>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#f1f5f9;align=left;spacingLeft=15;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"535\" width=\"740\" height=\"85\" as=\"geometry\" /></mxCell>");

        // Right Panel: Việc cần làm (from 07_dashboard.png)
        sb.AppendLine($"        <mxCell id=\"wf07_p2\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1110\" y=\"365\" width=\"410\" height=\"350\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf07_p2_hdr\" value=\"{Val("<b style=\"font-size: 15px; color: #1b1340;\">Việc cần làm</b>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1130\" y=\"375\" width=\"300\" height=\"35\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf07_task1\" value=\"{Val("<table width=\"100%\"><tr><td width=\"15%\">🪪</td><td width=\"75%\"><b>Thẻ sắp hết hạn</b><br><span style=\"color: #64748b; font-size: 11px;\">2 thẻ trong 30 ngày tới</span></td><td width=\"10%\" align=\"right\">&gt;</td></tr></table>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1130\" y=\"425\" width=\"370\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf07_task2\" value=\"{Val("<table width=\"100%\"><tr><td width=\"15%\">💰</td><td width=\"75%\"><b>Kiểm tra khoản phạt</b><br><span style=\"color: #64748b; font-size: 11px;\">Đối chiếu tài liệu trả trễ</span></td><td width=\"10%\" align=\"right\">&gt;</td></tr></table>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1130\" y=\"490\" width=\"370\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf07_task3\" value=\"{Val("<table width=\"100%\"><tr><td width=\"15%\">📅</td><td width=\"75%\"><b>Phiếu sắp đến hạn</b><br><span style=\"color: #64748b; font-size: 11px;\">Chuẩn bị nhắc bạn đọc trả sách</span></td><td width=\"10%\" align=\"right\">&gt;</td></tr></table>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1130\" y=\"555\" width=\"370\" height=\"55\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf07_p2_tip\" value=\"{Val("☑️ <span style=\"color: #64748b; font-size: 11px;\">Kiểm tra thẻ và tình trạng bản sao trước khi lập phiếu mượn.</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1130\" y=\"625\" width=\"370\" height=\"35\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 08. QUẢN LÝ ĐẦU SÁCH (/Admin/Books - from 08_admin_books.png)
    private static void AppendWf08AdminBooks(StringBuilder sb)
    {
        StartDiagram(sb, "wf_08_adminbooks", "08. Wireframe - Quản lý đầu sách (/Admin/Books)", "/Admin/Books", "Quản lý danh mục đầu sách &amp; Bản sao (UTC Library)");

        AddStaffShell(sb, "wf08", "Đầu sách", "Quản trị thư viện &nbsp;&gt;&nbsp; <b>Quản lý đầu sách</b>", "Quản lý đầu sách", "Quản lý thư viện", "Quản lý thông tin thư mục, chuyên ngành và số lượng bản sao của từng đầu sách.");

        // Button right
        sb.AppendLine($"        <mxCell id=\"wf08_btn_add\" value=\"{Val("+ Thêm đầu sách")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1380\" y=\"145\" width=\"140\" height=\"38\" as=\"geometry\" /></mxCell>");

        // Filter Card (from 08_admin_books.png)
        sb.AppendLine($"        <mxCell id=\"wf08_fbox\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"225\" width=\"1210\" height=\"85\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf08_f1\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Tìm kiếm</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px; color: #94a3b8;\">🔍 Nhập tên, mã hoặc thông tin cần tìm</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"235\" width=\"400\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf08_f2\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Nhóm danh mục</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px;\">Tất cả nhóm ▼</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"750\" y=\"235\" width=\"200\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf08_f3\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Trạng thái</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px;\">Tất cả trạng thái ▼</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"970\" y=\"235\" width=\"200\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf08_fbtn\" value=\"{Val("T <b>Lọc dữ liệu</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1190\" y=\"253\" width=\"110\" height=\"36\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf08_freset\" value=\"{Val("Đặt lại")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;fontColor=#334155;strokeColor=#cbd5e1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1315\" y=\"253\" width=\"80\" height=\"36\" as=\"geometry\" /></mxCell>");

        // Results Count & Table Header (from 08_admin_books.png)
        sb.AppendLine($"        <mxCell id=\"wf08_rcount\" value=\"{Val("<b>7 kết quả</b> trong 7 bản ghi &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<span style=\"border: 1px solid #cbd5e1; padding: 2px 6px; font-size: 10px;\">F2</span> <span style=\"font-size: 11px;\">Tìm nhanh</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;color=#64748b;font-size:12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"320\" width=\"1210\" height=\"30\" as=\"geometry\" /></mxCell>");

        // Table Header
        sb.AppendLine($"        <mxCell id=\"wf08_th\" value=\"{Val("<table width=\"100%\" style=\"font-size: 11px; color: #64748b; font-weight: bold;\"><tr><td width=\"12%\">&nbsp;&nbsp;Ký hiệu</td><td width=\"32%\">Tên sách / tác giả</td><td width=\"24%\">Chuyên ngành</td><td width=\"8%\">Năm</td><td width=\"10%\">Sẵn sàng / tổng</td><td width=\"8%\" align=\"center\">Trạng thái</td><td width=\"6%\" align=\"right\">Thao tác&nbsp;&nbsp;</td></tr></table>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"355\" width=\"1210\" height=\"35\" as=\"geometry\" /></mxCell>");

        // 3 Data Rows (from 08_admin_books.png)
        string[] bCodes = ["KE-VH-01", "KE-GT-01", "KE-IT-02"];
        string[] bTitles = ["Tôi Thấy Hoa Vàng Trên Cỏ Xanh", "Quy hoạch và Tổ chức Giao thông Đô thị thông minh", "Giáo trình Mạng máy tính căn bản & nâng cao"];
        string[] bAuths = ["Nguyễn Nhật Ánh", "PGS.TS. Trần Đình Quế", "Andrew S. Tanenbaum"];
        string[] bCats = ["Văn học & Kỹ năng mềm", "Kỹ thuật Giao thông & Cầu đường", "Công nghệ thông tin & Lập trình"];
        string[] bYears = ["2022", "2024", "2022"];
        string[] bCounts = ["1/2", "2/2", "2/2"];

        for (int i = 0; i < 3; i++)
        {
            int ry = 390 + i * 65;
            sb.AppendLine($"        <mxCell id=\"wf08_r_{i}\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#f1f5f9;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"{ry}\" width=\"1210\" height=\"65\" as=\"geometry\" /></mxCell>");
            sb.AppendLine($"        <mxCell id=\"wf08_rt_{i}\" value=\"{Val($"<table width=\"100%\" style=\"font-size: 12px;\"><tr><td width=\"12%\">&nbsp;&nbsp;<b>{bCodes[i]}</b></td><td width=\"32%\"><b>{bTitles[i]}</b><br><span style=\"color: #64748b; font-size: 11px;\">{bAuths[i]}</span></td><td width=\"24%\">{bCats[i]}</td><td width=\"8%\">{bYears[i]}</td><td width=\"10%\"><b>{bCounts[i]}</b></td><td width=\"8%\" align=\"center\"><span style=\"background: #ecfdf5; color: #047857; padding: 2px 7px; border-radius: 10px; font-weight: bold; font-size: 10px;\">✓ Sẵn sàng</span></td><td width=\"6%\" align=\"right\"><span style=\"cursor: pointer;\">✏️</span>&nbsp;&nbsp;</td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"{ry}\" width=\"1210\" height=\"65\" as=\"geometry\" /></mxCell>");
        }

        EndDiagram(sb);
    }

    // 09. QUẦY MƯỢN SÁCH (/Circulation/Borrow - from 09_borrow_card.png)
    private static void AppendWf09Borrow(StringBuilder sb)
    {
        StartDiagram(sb, "wf_09_borrow", "09. Wireframe - Quầy mượn sách (/Circulation/Borrow)", "/Circulation/Borrow", "Lập phiếu mượn tài liệu tại quầy (UTC Library)");

        AddStaffShell(sb, "wf09", "Mượn sách", "Quản trị thư viện &nbsp;&gt;&nbsp; <b>Lập phiếu mượn</b>", "Lập phiếu mượn", "Nghiệp vụ tại quầy", "Kiểm tra thẻ độc giả trước khi quét mã bản sao vào phiếu.");

        // Top right button (from 09_borrow_card.png)
        sb.AppendLine($"        <mxCell id=\"wf09_btn_switch\" value=\"{Val("↙️ Chuyển sang trả sách")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;fontColor=#334155;strokeColor=#cbd5e1;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1350\" y=\"145\" width=\"170\" height=\"38\" as=\"geometry\" /></mxCell>");

        // 2 Columns (from 09_borrow_card.png)
        // Card 1: Kiểm tra độc giả (F2)
        sb.AppendLine($"        <mxCell id=\"wf09_c1\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"225\" width=\"340\" height=\"480\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf09_c1_hdr\" value=\"{Val("<table width=\"100%\"><tr><td><span style=\"background: #f1f5f9; padding: 2px 7px; border-radius: 4px; font-weight: bold; font-size: 11px;\">1</span> &nbsp;<b>Kiểm tra độc giả</b></td><td align=\"right\"><span style=\"border: 1px solid #cbd5e1; padding: 2px 6px; font-size: 10px;\">F2</span></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"235\" width=\"300\" height=\"35\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf09_c1_in\" value=\"{Val("<b>Mã thẻ độc giả</b><br><div style=\"background: #fff; border: 2px solid #1b1340; border-radius: 4px; padding: 8px 10px; margin-top: 4px; font-size: 14px; font-weight: bold;\">2022000456 &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;✕</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"285\" width=\"300\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf09_c1_btn\" value=\"{Val("<b>Tìm độc giả</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"355\" width=\"300\" height=\"38\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf09_c1_reader\" value=\"{Val("<div style=\"border-top: 1px solid #f1f5f9; padding-top: 15px; margin-top: 10px;\"><div style=\"width: 36px; height: 36px; background: #f1f5f9; border-radius: 6px; text-align: center; line-height: 36px; font-weight: bold; font-size: 16px; color: #1b1340; display: inline-block;\">H</div><div style=\"display: inline-block; vertical-align: middle; margin-left: 10px;\"><b style=\"font-size: 15px; color: #1b1340;\">Nguyễn Thu Hà</b><br><span style=\"color: #64748b; font-size: 11px;\">2022000456</span></div><div style=\"margin-top: 10px;\"><span style=\"background: #ecfdf5; color: #047857; padding: 2px 8px; border-radius: 8px; font-weight: bold; font-size: 11px;\">Còn hạn</span></div><table width=\"100%\" style=\"font-size: 12px; margin-top: 12px; line-height: 1.8;\"><tr><td style=\"color: #64748b;\">Loại độc giả</td><td align=\"right\"><b>Sinh viên</b></td></tr><tr><td style=\"color: #64748b;\">Hạn mức mượn</td><td align=\"right\"><b>3 / 5 cuốn</b></td></tr></table></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"410\" width=\"300\" height=\"170\" as=\"geometry\" /></mxCell>");

        // Card 2: Thêm bản sao vào phiếu (from 09_borrow_card.png)
        sb.AppendLine($"        <mxCell id=\"wf09_c2\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"670\" y=\"225\" width=\"850\" height=\"480\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf09_c2_hdr\" value=\"{Val("<table width=\"100%\"><tr><td><span style=\"background: #f1f5f9; padding: 2px 7px; border-radius: 4px; font-weight: bold; font-size: 11px;\">2</span> &nbsp;<b>Thêm bản sao vào phiếu</b></td><td align=\"right\" style=\"color: #64748b; font-size: 11px;\">Giữ focus để quét liên tiếp</td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"690\" y=\"235\" width=\"810\" height=\"35\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf09_c2_in\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Mã vạch bản sao</span> &nbsp;<span style=\"border: 1px solid #cbd5e1; padding: 1px 4px; font-size: 9px;\">F4</span><br><table width=\"100%\" style=\"margin-top: 4px;\"><tr><td width=\"75%\"><div style=\"background: #f8fafc; border: 1px solid #cbd5e1; padding: 8px 12px; border-radius: 4px; font-size: 13px; color: #94a3b8;\">Quét mã hoặc chọn bản sao mẫu</div></td><td width=\"25%\" align=\"right\"><button style=\"background: #f1f5f9; border: 1px solid #cbd5e1; color: #94a3b8; padding: 8px 16px; border-radius: 4px; font-size: 12px;\">+ Thêm vào phiếu</button></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"690\" y=\"285\" width=\"810\" height=\"65\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf09_c2_th\" value=\"{Val("<table width=\"100%\" style=\"font-size: 11px; color: #64748b; font-weight: bold;\"><tr><td width=\"25%\">Mã vạch</td><td width=\"50%\">Tài liệu</td><td width=\"25%\" align=\"right\">Hạn trả dự kiến</td></tr></table>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"690\" y=\"370\" width=\"810\" height=\"32\" as=\"geometry\" /></mxCell>");

        // Empty state illustration (from 09_borrow_card.png)
        sb.AppendLine($"        <mxCell id=\"wf09_c2_empty\" value=\"{Val("<div style=\"text-align: center; padding: 30px 0;\"><span style=\"font-size: 36px;\">📷</span><br><b style=\"font-size: 15px; color: #1b1340;\">Phiếu chưa có tài liệu</b><br><span style=\"color: #64748b; font-size: 12px;\">Quét mã bản sao để thêm sách và xem hạn trả dự kiến.</span></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=center;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"690\" y=\"420\" width=\"810\" height=\"180\" as=\"geometry\" /></mxCell>");

        // Bottom Sticky Bar (from 09_borrow_card.png)
        sb.AppendLine($"        <mxCell id=\"wf09_bbar\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"280\" y=\"945\" width=\"1280\" height=\"75\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf09_bbar_t\" value=\"{Val("<span style=\"font-size: 11px; color: #64748b;\">Tài liệu trong phiếu</span><br><b style=\"font-size: 18px; color: #1b1340;\">0 cuốn</b> &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<span style=\"color: #64748b; font-size: 12px;\">Phiếu xem thử; chưa ghi nhận mượn sách.</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"950\" width=\"600\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf09_bbar_btn\" value=\"{Val("Kiểm tra phiếu mượn")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f1f5f9;fontColor=#94a3b8;strokeColor=#e2e8f0;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1350\" y=\"960\" width=\"170\" height=\"40\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 10. QUẦY TRẢ SÁCH (/Circulation/Return - from 10_return.png)
    private static void AppendWf10Return(StringBuilder sb)
    {
        StartDiagram(sb, "wf_10_return", "10. Wireframe - Quầy trả sách (/Circulation/Return)", "/Circulation/Return", "Nhận trả sách &amp; Thẩm định hư hại (UTC Library)");

        AddStaffShell(sb, "wf10", "Trả sách", "Quản trị thư viện &nbsp;&gt;&nbsp; <b>Trả sách</b>", "Trả sách", "Nghiệp vụ tại quầy", "Tìm độc giả, đối chiếu tài liệu và kiểm tra khoản phạt dự kiến.");

        // Top right button (from 10_return.png)
        sb.AppendLine($"        <mxCell id=\"wf10_btn_switch\" value=\"{Val("↗️ Chuyển sang mượn sách")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;fontColor=#334155;strokeColor=#cbd5e1;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1350\" y=\"145\" width=\"170\" height=\"38\" as=\"geometry\" /></mxCell>");

        // Left Col (from 10_return.png)
        sb.AppendLine($"        <mxCell id=\"wf10_c1\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"225\" width=\"340\" height=\"480\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf10_c1_hdr\" value=\"{Val("<table width=\"100%\"><tr><td><span style=\"background: #f1f5f9; padding: 2px 7px; border-radius: 4px; font-weight: bold; font-size: 11px;\">1</span> &nbsp;<b>Kiểm tra độc giả</b></td><td align=\"right\"><span style=\"border: 1px solid #cbd5e1; padding: 2px 6px; font-size: 10px;\">F2</span></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"235\" width=\"300\" height=\"35\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf10_c1_in\" value=\"{Val("<b>Mã thẻ độc giả</b><br><div style=\"background: #fff; border: 2px solid #1b1340; border-radius: 4px; padding: 8px 10px; margin-top: 4px; font-size: 14px; font-weight: bold;\">2022000456 &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;✕</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"285\" width=\"300\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf10_c1_btn\" value=\"{Val("<b>Tìm độc giả</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"355\" width=\"300\" height=\"38\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf10_c1_reader\" value=\"{Val("<div style=\"border-top: 1px solid #f1f5f9; padding-top: 15px; margin-top: 10px;\"><div style=\"width: 36px; height: 36px; background: #f1f5f9; border-radius: 6px; text-align: center; line-height: 36px; font-weight: bold; font-size: 16px; color: #1b1340; display: inline-block;\">H</div><div style=\"display: inline-block; vertical-align: middle; margin-left: 10px;\"><b style=\"font-size: 15px; color: #1b1340;\">Nguyễn Thu Hà</b><br><span style=\"color: #64748b; font-size: 11px;\">2022000456</span></div><div style=\"margin-top: 10px;\"><span style=\"background: #ecfdf5; color: #047857; padding: 2px 8px; border-radius: 8px; font-weight: bold; font-size: 11px;\">Còn hạn</span></div><table width=\"100%\" style=\"font-size: 12px; margin-top: 12px; line-height: 1.8;\"><tr><td style=\"color: #64748b;\">Loại độc giả</td><td align=\"right\"><b>Sinh viên</b></td></tr><tr><td style=\"color: #64748b;\">Hạn thẻ</td><td align=\"right\"><b>20/11/2026</b></td></tr></table></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"410\" width=\"300\" height=\"170\" as=\"geometry\" /></mxCell>");

        // Right Col: Đối chiếu sách trả (from 10_return.png)
        sb.AppendLine($"        <mxCell id=\"wf10_c2\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"670\" y=\"225\" width=\"850\" height=\"480\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf10_c2_hdr\" value=\"{Val("<table width=\"100%\"><tr><td><span style=\"background: #f1f5f9; padding: 2px 7px; border-radius: 4px; font-weight: bold; font-size: 11px;\">2</span> &nbsp;<b>Đối chiếu sách trả</b></td><td align=\"right\" style=\"color: #64748b; font-size: 11px;\">Chọn tài liệu nhận trả</td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"690\" y=\"235\" width=\"810\" height=\"35\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf10_c2_th\" value=\"{Val("<table width=\"100%\" style=\"font-size: 11px; color: #64748b; font-weight: bold;\"><tr><td width=\"5%\"><input type=\"checkbox\"></td><td width=\"35%\">Tài liệu / mã vạch</td><td width=\"25%\">Hạn trả</td><td width=\"20%\">Tình trạng nhận</td><td width=\"15%\" align=\"right\">Phạt trả trễ</td></tr></table>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"690\" y=\"285\" width=\"810\" height=\"32\" as=\"geometry\" /></mxCell>");

        // Book Row with Orange Date Box (from 10_return.png)
        sb.AppendLine($"        <mxCell id=\"wf10_c2_row\" value=\"{Val("<table width=\"100%\" style=\"font-size: 12px;\"><tr><td width=\"5%\"><input type=\"checkbox\"></td><td width=\"35%\"><b>Trắc địa đại cương</b><br><span style=\"color: #64748b; font-size: 11px;\">Chưa cập nhật</span></td><td width=\"25%\"><div style=\"border: 1px solid #d97706; border-radius: 4px; padding: 4px 8px; text-align: center; color: #d97706; display: inline-block; vertical-align: middle;\"><b style=\"font-size: 14px;\">09</b><br><span style=\"font-size: 9px;\">11/2026</span></div>&nbsp;&nbsp;<span style=\"color: #d97706; font-size: 11px;\">Còn 2 ngày</span></td><td width=\"20%\"><span style=\"border: 1px solid #cbd5e1; padding: 4px 8px; border-radius: 4px; font-size: 11px;\">Bình thường ▼</span></td><td width=\"15%\" align=\"right\"><b>0 ₫</b></td></tr></table>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#f1f5f9;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"690\" y=\"330\" width=\"810\" height=\"75\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf10_c2_note\" value=\"{Val("<span style=\"color: #64748b; font-size: 11px;\">Phạt trả trễ dự kiến: 2.000 ₫/bản/ngày. Phạt hỏng hoặc mất cần được xác nhận riêng.</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"690\" y=\"420\" width=\"810\" height=\"25\" as=\"geometry\" /></mxCell>");

        // Bottom Sticky Bar (from 10_return.png)
        sb.AppendLine($"        <mxCell id=\"wf10_bbar\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"280\" y=\"945\" width=\"1280\" height=\"75\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf10_bbar_t\" value=\"{Val("<table cellpadding=\"0\" cellspacing=\"0\"><tr><td><span style=\"font-size: 11px; color: #64748b;\">Tài liệu nhận trả</span><br><b style=\"font-size: 16px; color: #1b1340;\">0 cuốn</b></td><td style=\"padding-left: 30px;\"><span style=\"font-size: 11px; color: #64748b;\">Phạt trả trễ dự kiến</span><br><b style=\"font-size: 16px; color: #1b1340;\">0 ₫</b></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"950\" width=\"600\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf10_bbar_btn\" value=\"{Val("Kiểm tra trả sách")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#f1f5f9;fontColor=#94a3b8;strokeColor=#e2e8f0;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1350\" y=\"960\" width=\"170\" height=\"40\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 11. THU PHẠT (/Fines - from 11_fines.png)
    private static void AppendWf11Fines(StringBuilder sb)
    {
        StartDiagram(sb, "wf_11_fines", "11. Wireframe - Thu phạt vi phạm (/Fines)", "/Fines", "Quản lý tiền phạt &amp; Biên lai thu tiền (UTC Library)");

        AddStaffShell(sb, "wf11", "Thu phạt", "Quản trị thư viện &nbsp;&gt;&nbsp; <b>Quản lý tiền phạt</b>", "Quản lý tiền phạt", "Quản lý thư viện", "Kiểm tra các khoản phạt dự kiến do trả trễ. Số tiền thực tế cần được xác nhận tại quầy.");

        // Top right button
        sb.AppendLine($"        <mxCell id=\"wf11_btn_add\" value=\"{Val("+ Lập phiếu thu")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1380\" y=\"145\" width=\"140\" height=\"38\" as=\"geometry\" /></mxCell>");

        // Filter Card
        sb.AppendLine($"        <mxCell id=\"wf11_fbox\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"225\" width=\"1210\" height=\"85\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf11_f1\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Tìm kiếm</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px; color: #94a3b8;\">🔍 Nhập tên, mã hoặc thông tin cần tìm</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"235\" width=\"500\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf11_f2\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Trạng thái</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px;\">Tất cả trạng thái ▼</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"860\" y=\"235\" width=\"220\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf11_btn_f\" value=\"{Val("T <b>Lọc dữ liệu</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1100\" y=\"253\" width=\"110\" height=\"36\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf11_btn_r\" value=\"{Val("Đặt lại")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;fontColor=#334155;strokeColor=#cbd5e1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1225\" y=\"253\" width=\"80\" height=\"36\" as=\"geometry\" /></mxCell>");

        // Table Header & Rows (from 11_fines.png)
        sb.AppendLine($"        <mxCell id=\"wf11_rcount\" value=\"{Val("<b>2 kết quả</b> trong 2 bản ghi &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<span style=\"border: 1px solid #cbd5e1; padding: 2px 6px; font-size: 10px;\">F2</span> <span style=\"font-size: 11px;\">Tìm nhanh</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;color=#64748b;font-size:12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"320\" width=\"1210\" height=\"30\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf11_th\" value=\"{Val("<table width=\"100%\" style=\"font-size: 11px; color: #64748b; font-weight: bold;\"><tr><td width=\"15%\">&nbsp;&nbsp;Mã phiếu</td><td width=\"20%\">Độc giả</td><td width=\"25%\">Tài liệu quá hạn</td><td width=\"12%\">Trễ hạn</td><td width=\"12%\">Phạt dự kiến</td><td width=\"10%\" align=\"center\">Trạng thái</td><td width=\"6%\" align=\"right\">Thao tác&nbsp;&nbsp;</td></tr></table>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"355\" width=\"1210\" height=\"35\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf11_r1\" value=\"{Val("<table width=\"100%\" style=\"font-size: 12px;\"><tr><td width=\"15%\">&nbsp;&nbsp;<b>PM-2026-0001</b></td><td width=\"20%\">Lê Minh Châu</td><td width=\"25%\">Cơ học đất</td><td width=\"12%\">3 ngày</td><td width=\"12%\"><b>6.000 ₫</b></td><td width=\"10%\" align=\"center\"><span style=\"border: 1px solid #dc2626; color: #dc2626; padding: 2px 8px; border-radius: 8px; font-size: 10px; font-weight: bold;\">Chưa nộp</span></td><td width=\"6%\" align=\"right\"><span style=\"cursor: pointer;\">🪙</span>&nbsp;&nbsp;</td></tr></table>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#f1f5f9;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"390\" width=\"1210\" height=\"60\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf11_r2\" value=\"{Val("<table width=\"100%\" style=\"font-size: 12px;\"><tr><td width=\"15%\">&nbsp;&nbsp;<b>PM-2026-0005</b></td><td width=\"20%\">Phạm Ngọc Anh</td><td width=\"25%\">Quản trị chuỗi cung ứng và logistics</td><td width=\"12%\">1 ngày</td><td width=\"12%\"><b>2.000 ₫</b></td><td width=\"10%\" align=\"center\"><span style=\"border: 1px solid #dc2626; color: #dc2626; padding: 2px 8px; border-radius: 8px; font-size: 10px; font-weight: bold;\">Chưa nộp</span></td><td width=\"6%\" align=\"right\"><span style=\"cursor: pointer;\">🪙</span>&nbsp;&nbsp;</td></tr></table>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#f1f5f9;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"450\" width=\"1210\" height=\"60\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 12. CẤU HÌNH HỆ THỐNG (/System/Settings - from 12_settings.png)
    private static void AppendWf12Settings(StringBuilder sb)
    {
        StartDiagram(sb, "wf_12_settings", "12. Wireframe - Cấu hình hệ thống (/System/Settings)", "/System/Settings", "Quy định mượn trả &amp; Mức phạt vi phạm (UTC Library)");

        AddStaffShell(sb, "wf12", "Quy định mượn trả", "Quản trị thư viện &nbsp;&gt;&nbsp; <b>Quy định mượn trả</b>", "Quy định mượn trả", "Quản trị hệ thống", "Thiết lập hạn mức cho bạn đọc và mức phạt khi tài liệu được trả trễ.");

        // Card 1: Hạn mức theo loại độc giả (Exact from 12_settings.png)
        sb.AppendLine($"        <mxCell id=\"wf12_c1\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"225\" width=\"1210\" height=\"390\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf12_c1_hdr\" value=\"{Val("<table width=\"100%\"><tr><td><b style=\"font-size: 15px; color: #1b1340;\">Hạn mức theo loại độc giả</b><br><span style=\"color: #64748b; font-size: 11px;\">Số đầu sách tối đa được mượn cùng lúc và thời gian mượn tương ứng.</span></td><td align=\"right\">🪪</td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"335\" y=\"235\" width=\"1160\" height=\"45\" as=\"geometry\" /></mxCell>");

        // 3 Reader types rows (from 12_settings.png)
        string[] rNames = ["Giảng viên", "Sinh viên", "Khách ngoài"];
        string[] rMax = ["10", "5", "2"];
        string[] rDays = ["30", "14", "7"];

        for (int i = 0; i < 3; i++)
        {
            int rty = 295 + i * 95;
            sb.AppendLine($"        <mxCell id=\"wf12_r_{i}\" value=\"{Val($"<table width=\"100%\" style=\"font-size: 12px;\"><tr><td width=\"35%\"><b style=\"font-size: 14px; color: #1b1340;\">{rNames[i]}</b><br><span style=\"color: #64748b; font-size: 11px;\">Áp dụng cho thẻ thư viện còn hạn.</span></td><td width=\"30%\"><span style=\"color: #64748b; font-size: 11px;\">Số sách tối đa</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; border-radius: 4px; padding: 6px 10px; margin-top: 3px; width: 220px;\"><b>{rMax[i]}</b> &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<span style=\"color: #64748b;\">cuốn</span></div></td><td width=\"35%\"><span style=\"color: #64748b; font-size: 11px;\">Thời hạn mượn</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; border-radius: 4px; padding: 6px 10px; margin-top: 3px; width: 220px;\"><b>{rDays[i]}</b> &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<span style=\"color: #64748b;\">ngày</span></div></td></tr></table>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#f1f5f9;align=left;spacingLeft=15;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"335\" y=\"{rty}\" width=\"1160\" height=\"85\" as=\"geometry\" /></mxCell>");
        }

        // Card 2: Phạt trả quá hạn (from 12_settings.png)
        sb.AppendLine($"        <mxCell id=\"wf12_c2\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"630\" width=\"1210\" height=\"160\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf12_c2_hdr\" value=\"{Val("<table width=\"100%\"><tr><td><b style=\"font-size: 15px; color: #1b1340;\">Phạt trả quá hạn</b><br><span style=\"color: #64748b; font-size: 11px;\">Mức phạt dự kiến được tính trên mỗi bản sách và số ngày trả trễ.</span></td><td align=\"right\">💵</td></tr></table><div style=\"margin-top: 15px; font-size: 12px;\"><span style=\"color: #64748b;\">Tiền phạt mỗi ngày:</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; border-radius: 4px; padding: 6px 10px; margin-top: 3px; width: 220px;\"><b>2.000</b> &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<span style=\"color: #64748b;\">₫ / ngày</span></div></div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"335\" y=\"645\" width=\"1160\" height=\"135\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }

    // 13. QUẢN LÝ PHIẾU MƯỢN (/Circulation/History - from 13_history.png)
    private static void AppendWf13History(StringBuilder sb)
    {
        StartDiagram(sb, "wf_13_history", "13. Wireframe - Quản lý phiếu mượn (/Circulation/History)", "/Circulation/History", "Quản lý phiếu mượn &amp; Lịch sử lưu thông (UTC Library)");

        AddStaffShell(sb, "wf13", "Phiếu mượn", "Quản trị thư viện &nbsp;&gt;&nbsp; <b>Phiếu mượn</b>", "Phiếu mượn", "Quản lý thư viện", "Tra cứu lịch sử lưu thông và xử lý các phiếu sắp đến hạn, quá hạn.");

        // Filter Card (from 13_history.png)
        sb.AppendLine($"        <mxCell id=\"wf13_fbox\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"225\" width=\"1210\" height=\"85\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf13_f1\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Tìm kiếm</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px; color: #94a3b8;\">🔍 Nhập tên, mã hoặc thông tin cần tìm</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"330\" y=\"235\" width=\"520\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf13_f2\" value=\"{Val("<span style=\"font-size: 11px; font-weight: bold; color: #334155;\">Trạng thái</span><br><div style=\"background: #fff; border: 1px solid #cbd5e1; padding: 7px 10px; border-radius: 4px; margin-top: 4px; font-size: 12px;\">Tất cả trạng thái ▼</div>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"870\" y=\"235\" width=\"210\" height=\"60\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf13_btn_f\" value=\"{Val("T <b>Lọc dữ liệu</b>")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;fontColor=#ffffff;strokeColor=#0f0b24;fontStyle=1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1100\" y=\"253\" width=\"110\" height=\"36\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf13_btn_r\" value=\"{Val("Đặt lại")}\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;fontColor=#334155;strokeColor=#cbd5e1;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1225\" y=\"253\" width=\"80\" height=\"36\" as=\"geometry\" /></mxCell>");

        // Table Header & Rows (from 13_history.png)
        sb.AppendLine($"        <mxCell id=\"wf13_rcount\" value=\"{Val("<b>8 kết quả</b> trong 8 bản ghi &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<span style=\"border: 1px solid #cbd5e1; padding: 2px 6px; font-size: 10px;\">F2</span> <span style=\"font-size: 11px;\">Tìm nhanh</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;color=#64748b;font-size:12;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"320\" width=\"1210\" height=\"30\" as=\"geometry\" /></mxCell>");

        sb.AppendLine($"        <mxCell id=\"wf13_th\" value=\"{Val("<table width=\"100%\" style=\"font-size: 11px; color: #64748b; font-weight: bold;\"><tr><td width=\"14%\">&nbsp;&nbsp;Mã phiếu</td><td width=\"26%\">Tài liệu</td><td width=\"18%\">Độc giả</td><td width=\"12%\">Ngày mượn</td><td width=\"12%\">Hạn trả</td><td width=\"12%\" align=\"center\">Trạng thái</td><td width=\"6%\" align=\"right\">Thao tác&nbsp;&nbsp;</td></tr></table>")}\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#f8fafc;strokeColor=#e2e8f0;align=left;spacingLeft=10;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"355\" width=\"1210\" height=\"35\" as=\"geometry\" /></mxCell>");

        string[] pSlips = ["PM-2026-0001", "PM-2026-0002", "PM-2026-0003", "PM-2026-0006"];
        string[] pBooks = ["Cơ học đất", "Trắc địa đại cương", "Cấu trúc dữ liệu và giải thuật", "Giải tích, tập 1"];
        string[] pReaders = ["Lê Minh Châu", "Nguyễn Thu Hà", "Trần Quốc Minh", "Vũ Hoàng Nam"];
        string[] pBdates = ["22/10/2026", "23/10/2026", "24/10/2026", "27/10/2026"];
        string[] pDdates = ["04/11/2026", "09/11/2026", "21/11/2026", "28/10/2026"];
        string[] pStatus = [
            "<span style=\"border: 1px solid #dc2626; color: #dc2626; padding: 2px 7px; border-radius: 8px; font-size: 10px; font-weight: bold;\">(!) Quá hạn</span>",
            "<span style=\"border: 1px solid #d97706; color: #d97706; padding: 2px 7px; border-radius: 8px; font-size: 10px; font-weight: bold;\">⏱ Sắp đến hạn</span>",
            "<span style=\"border: 1px solid #cbd5e1; color: #334155; padding: 2px 7px; border-radius: 8px; font-size: 10px;\">Đang mượn</span>",
            "<span style=\"border: 1px solid #047857; color: #047857; padding: 2px 7px; border-radius: 8px; font-size: 10px; font-weight: bold;\">✓ Đã trả</span>"
        ];

        for (int i = 0; i < 4; i++)
        {
            int ry = 390 + i * 58;
            sb.AppendLine($"        <mxCell id=\"wf13_r_{i}\" value=\"\" style=\"rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#f1f5f9;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"{ry}\" width=\"1210\" height=\"58\" as=\"geometry\" /></mxCell>");
            sb.AppendLine($"        <mxCell id=\"wf13_rt_{i}\" value=\"{Val($"<table width=\"100%\" style=\"font-size: 12px;\"><tr><td width=\"14%\">&nbsp;&nbsp;<b>{pSlips[i]}</b></td><td width=\"26%\"><b>{pBooks[i]}</b></td><td width=\"18%\">{pReaders[i]}</td><td width=\"12%\">{pBdates[i]}</td><td width=\"12%\">{pDdates[i]}</td><td width=\"12%\" align=\"center\">{pStatus[i]}</td><td width=\"6%\" align=\"right\"><span style=\"cursor: pointer;\">👁️</span>&nbsp;&nbsp;</td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"{ry}\" width=\"1210\" height=\"58\" as=\"geometry\" /></mxCell>");
        }

        EndDiagram(sb);
    }

    // 14. BÁO CÁO THỐNG KÊ (/Reports - from 14_reports.png)
    private static void AppendWf14Reports(StringBuilder sb)
    {
        StartDiagram(sb, "wf_14_reports", "14. Wireframe - Báo cáo & Thống kê (/Reports)", "/Reports", "Báo cáo thống kê hoạt động thư viện (UTC Library)");

        AddStaffShell(sb, "wf14", "Báo cáo", "Quản trị thư viện &nbsp;&gt;&nbsp; <b>Báo cáo thư viện</b>", "Báo cáo thư viện", "Phân tích thư viện", "Tổng hợp dữ liệu hiện có để theo dõi kho sách và tình hình lưu thông.");

        // Top date
        sb.AppendLine($"        <mxCell id=\"wf14_tdate\" value=\"{Val("<span style=\"color: #64748b; font-size: 12px;\">📅 Tại ngày 07/11/2026</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=right;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1320\" y=\"145\" width=\"200\" height=\"35\" as=\"geometry\" /></mxCell>");

        // Dark Navy Summary Bar (from 14_reports.png)
        sb.AppendLine($"        <mxCell id=\"wf14_sbar\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#1b1340;strokeColor=#0f0b24;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"225\" width=\"1210\" height=\"110\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf14_sbar_t\" value=\"{Val("<table width=\"100%\" style=\"text-align: left; color: white;\"><tr><td width=\"25%\" style=\"padding-left: 20px;\"><span style=\"font-size: 12px; color: #cbd5e1;\">Đầu sách</span><br><b style=\"font-size: 34px;\">7</b></td><td width=\"25%\" style=\"border-left: 1px solid #332669; padding-left: 20px;\"><span style=\"font-size: 12px; color: #cbd5e1;\">Tổng số bản</span><br><b style=\"font-size: 34px;\">17</b></td><td width=\"25%\" style=\"border-left: 1px solid #332669; padding-left: 20px;\"><span style=\"font-size: 12px; color: #cbd5e1;\">Bản sẵn sàng</span><br><b style=\"font-size: 34px;\">13</b></td><td width=\"25%\" style=\"border-left: 1px solid #332669; padding-left: 20px;\"><span style=\"font-size: 12px; color: #cbd5e1;\">Phiếu quá hạn</span><br><b style=\"font-size: 34px;\">2</b></td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"320\" y=\"230\" width=\"1190\" height=\"100\" as=\"geometry\" /></mxCell>");

        // Left Card: Cơ cấu tài liệu theo chuyên ngành (from 14_reports.png)
        sb.AppendLine($"        <mxCell id=\"wf14_c1\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"310\" y=\"365\" width=\"780\" height=\"340\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf14_c1_hdr\" value=\"{Val("<b style=\"font-size: 15px; color: #1b1340;\">Cơ cấu tài liệu theo chuyên ngành</b><br><span style=\"color: #64748b; font-size: 11px;\">Số đầu sách phân loại trong danh mục hiện tại.</span>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"335\" y=\"375\" width=\"730\" height=\"40\" as=\"geometry\" /></mxCell>");

        string[] catLabels = ["Công nghệ thông tin & Lập trình", "Văn học & Kỹ năng mềm", "Kỹ thuật Giao thông & Cầu đường", "Cơ sở dữ liệu & Hệ thống thông tin"];
        string[] catCounts = ["4 đầu sách", "1 đầu sách", "1 đầu sách", "1 đầu sách"];
        int[] catWidths = [45, 12, 12, 12];

        for (int i = 0; i < 4; i++)
        {
            int cy = 440 + i * 55;
            sb.AppendLine($"        <mxCell id=\"wf14_prog_{i}\" value=\"{Val($"<table width=\"100%\" style=\"font-size: 12px;\"><tr><td width=\"32%\">{catLabels[i]}</td><td width=\"56%\"><div style=\"background: #f1f5f9; height: 8px; border-radius: 4px;\"><div style=\"background: #1b1340; width: {catWidths[i]}%; height: 8px; border-radius: 4px;\"></div></div></td><td width=\"12%\" align=\"right\" style=\"color: #64748b; font-size: 11px;\">{catCounts[i]}</td></tr></table>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"335\" y=\"{cy}\" width=\"730\" height=\"35\" as=\"geometry\" /></mxCell>");
        }

        // Right Card: Tình trạng bản sao (from 14_reports.png)
        sb.AppendLine($"        <mxCell id=\"wf14_c2\" value=\"\" style=\"rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#e2e8f0;arcSize=4;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1110\" y=\"365\" width=\"410\" height=\"340\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf14_c2_hdr\" value=\"{Val("<b style=\"font-size: 15px; color: #1b1340;\">Tình trạng bản sao</b>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1135\" y=\"375\" width=\"360\" height=\"35\" as=\"geometry\" /></mxCell>");
        sb.AppendLine($"        <mxCell id=\"wf14_c2_body\" value=\"{Val("<b style=\"font-size: 42px; color: #1b1340;\">76</b><span style=\"font-size: 20px; color: #64748b;\">%</span><br><span style=\"color: #64748b; font-size: 12px;\">Bản sao sẵn sàng phục vụ</span><div style=\"background: #f1f5f9; height: 8px; border-radius: 4px; margin: 15px 0 20px 0;\"><div style=\"background: #1b1340; width: 76%; height: 8px; border-radius: 4px;\"></div></div><table width=\"100%\" style=\"font-size: 13px; line-height: 2;\"><tr><td>Sẵn sàng</td><td align=\"right\"><b>13 bản</b></td></tr><tr><td>Chưa sẵn sàng</td><td align=\"right\"><b>4 bản</b></td></tr></table><br><a href=\"#\" style=\"color: #1b1340; font-size: 11px; text-decoration: none;\">Kiểm tra từng bản sao →</a>")}\" style=\"text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=top;\" vertex=\"1\" parent=\"1\"><mxGeometry x=\"1135\" y=\"420\" width=\"360\" height=\"270\" as=\"geometry\" /></mxCell>");

        EndDiagram(sb);
    }
}
