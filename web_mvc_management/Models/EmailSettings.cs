namespace web_mvc_management.Models;

public class EmailSettings
{
    public const string SectionName = "MailSettings";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Protocol { get; set; } = "smtp";
    public bool Auth { get; set; } = true;
    public bool StartTls { get; set; } = true;
    public bool StartTlsRequired { get; set; } = true;
    public string SenderDisplayName { get; set; } = "Thư Viện UTC";
}
