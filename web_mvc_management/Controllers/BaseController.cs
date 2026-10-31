using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using web_mvc_management.Common;
using web_mvc_management.Constant;

namespace web_mvc_management.Controllers;

/// <summary>
/// Controller cơ sở cung cấp các tiện ích xử lý thông báo Flash Message (TempData),
/// kiểm tra request jQuery AJAX và chuẩn hóa phản hồi từ tầng Service về Razor View.
/// </summary>
public abstract class BaseController : Controller
{
    /// <summary>
    /// Kiểm tra xem request hiện tại có phải là jQuery AJAX (X-Requested-With: XMLHttpRequest) hay không.
    /// </summary>
    protected bool IsAjaxRequest =>
        Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
        Request.Headers.Accept.ToString().Contains("application/json");

    /// <summary>
    /// Đặt thông báo thành công vào TempData để Razor View tự động hiển thị alert.
    /// </summary>
    protected void SetSuccessMessage(string message) =>
        TempData[AppConstants.TempDataKeys.SuccessMessage] = message;

    /// <summary>
    /// Đặt thông báo lỗi vào TempData.
    /// </summary>
    protected void SetErrorMessage(string message) =>
        TempData[AppConstants.TempDataKeys.ErrorMessage] = message;

    /// <summary>
    /// Đặt thông báo cảnh báo vào TempData.
    /// </summary>
    protected void SetWarningMessage(string message) =>
        TempData[AppConstants.TempDataKeys.WarningMessage] = message;

    /// <summary>
    /// Đặt thông báo thông tin vào TempData.
    /// </summary>
    protected void SetInfoMessage(string message) =>
        TempData[AppConstants.TempDataKeys.InfoMessage] = message;

    /// <summary>
    /// Trả về JSON thành công chuẩn cho jQuery AJAX handler khi cần.
    /// </summary>
    protected JsonResult AjaxSuccess(string message = "Thao tác thành công.", object? data = null) =>
        Json(new { success = true, message, data });

    /// <summary>
    /// Trả về JSON thất bại chuẩn cho jQuery AJAX handler khi cần.
    /// </summary>
    protected JsonResult AjaxError(string message, IEnumerable<string>? errors = null) =>
        Json(new { success = false, message, errors = errors ?? [message] });

    /// <summary>
    /// Điều hướng kết quả từ ServiceResult sang View/Redirect một cách tường minh.
    /// </summary>
    protected IActionResult HandleServiceResult(
        ServiceResult result,
        Func<IActionResult> onSuccess,
        Func<IActionResult>? onFailure = null)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                SetSuccessMessage(result.Message);
            }
            return onSuccess();
        }

        if (result.Errors.Count > 0)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }
        else if (!string.IsNullOrWhiteSpace(result.Message))
        {
            ModelState.AddModelError(string.Empty, result.Message);
        }

        SetErrorMessage(result.Message);
        return onFailure != null ? onFailure() : View();
    }
}
