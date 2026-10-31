using System;
using System.Collections.Generic;

namespace web_mvc_management.Common;

/// <summary>
/// Giao diện phi generic cho kết quả phân trang, cho phép Razor Partial View (_Pagination.cshtml)
/// hiển thị giao diện phân trang dùng chung cho mọi loại thực thể và ViewModel.
/// </summary>
public interface IPagedResult
{
    /// <summary>
    /// Số thứ tự trang hiện tại (1-based, chuẩn hóa).
    /// </summary>
    int PageIndex { get; }

    /// <summary>
    /// Số thứ tự trang hiện tại (alias của PageIndex).
    /// </summary>
    int PageNo { get; }

    /// <summary>
    /// Số lượng bản ghi trên một trang (mặc định 10).
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// Số lượng bản ghi trên một trang (alias của PageSize).
    /// </summary>
    int PageItemSize { get; }

    /// <summary>
    /// Tổng số lượng bản ghi thỏa mãn điều kiện trong CSDL.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// Tổng số trang.
    /// </summary>
    int TotalPages { get; }

    /// <summary>
    /// Có trang trước hay không.
    /// </summary>
    bool HasPreviousPage { get; }

    /// <summary>
    /// Có trang kế tiếp hay không.
    /// </summary>
    bool HasNextPage { get; }

    /// <summary>
    /// Chỉ số bản ghi bắt đầu hiển thị trên trang hiện tại (1-based).
    /// </summary>
    int StartItemIndex { get; }

    /// <summary>
    /// Chỉ số bản ghi kết thúc hiển thị trên trang hiện tại.
    /// </summary>
    int EndItemIndex { get; }

    /// <summary>
    /// Danh sách các số trang lân cận để hiển thị trên thanh điều hướng UI.
    /// </summary>
    IReadOnlyList<int> GetPageNumbers(int maxDisplayPages = 5);
}

