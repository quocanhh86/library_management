using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace web_mvc_management.Extensions;

/// <summary>
/// Các phương thức mở rộng cho ISession hỗ trợ lưu trữ đối tượng trạng thái (Stateful MVC).
/// </summary>
public static class SessionExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void SetObject<T>(this ISession session, string key, T value)
    {
        session.SetString(key, JsonSerializer.Serialize(value, JsonOptions));
    }

    public static T? GetObject<T>(this ISession session, string key)
    {
        var value = session.GetString(key);
        return value == null ? default : JsonSerializer.Deserialize<T>(value, JsonOptions);
    }
}

