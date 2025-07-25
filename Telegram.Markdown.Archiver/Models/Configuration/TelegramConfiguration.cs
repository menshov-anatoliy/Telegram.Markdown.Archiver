namespace Telegram.Markdown.Archiver.Models.Configuration;

/// <summary>
/// Конфигурация для подключения к Telegram Bot API
/// </summary>
public class TelegramConfiguration
{
    /// <summary>
    /// Токен бота
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// ID пользователя (владельца бота)
    /// </summary>
    public long UserId { get; set; }
}