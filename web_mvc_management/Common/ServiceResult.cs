namespace web_mvc_management.Common;

/// <summary>
/// Chuẩn hóa kết quả trả về từ tầng Service về Controller.
/// </summary>
public class ServiceResult
{
    public bool Succeeded { get; protected set; }
    public string Message { get; protected set; } = string.Empty;
    public List<string> Errors { get; protected set; } = [];

    public static ServiceResult Success(string message = "Thao tác thành công.") =>
        new() { Succeeded = true, Message = message };

    public static ServiceResult Failure(string error) =>
        new() { Succeeded = false, Message = error, Errors = [error] };

    public static ServiceResult Failure(string message, IEnumerable<string> errors) =>
        new() { Succeeded = false, Message = message, Errors = errors.ToList() };
}

/// <summary>
/// Chuẩn hóa kết quả trả về kèm dữ liệu.
/// </summary>
public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private init; }

    public static ServiceResult<T> Success(T data, string message = "Thao tác thành công.") =>
        new() { Succeeded = true, Message = message, Data = data };

    public new static ServiceResult<T> Failure(string error) =>
        new() { Succeeded = false, Message = error, Errors = [error] };

    public new static ServiceResult<T> Failure(string message, IEnumerable<string> errors) =>
        new() { Succeeded = false, Message = message, Errors = errors.ToList() };
}

