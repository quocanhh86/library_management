namespace web_mvc_management.Models;

/// <summary>
/// Cấu hình tích hợp dịch vụ gửi email SMTP (bind từ Section MailSettings trong .env).
/// </summary>
public class EmailSettings
{
    public const string SectionName = "MailSettings";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Protocol { get; set; } = "smtp";
    public bool Auth { get; set; } = true;
    public bool StartTls { get; set; } = true;
    public bool StartTlsRequired { get; set; } = true;
    public string SenderDisplayName { get; set; } = "Thư Viện UTC";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !Username.Equals("your_email@gmail.com", StringComparison.OrdinalIgnoreCase) &&
        !Password.Equals("your_app_password", StringComparison.OrdinalIgnoreCase);
}
