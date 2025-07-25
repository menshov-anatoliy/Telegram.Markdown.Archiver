namespace Telegram.Markdown.Archiver.Models.Configuration;

/// <summary>
/// Конфигурация для логирования ошибок в Telegram
/// </summary>
public class ErrorLoggingConfiguration
{
    /// <summary>
    /// Включить логирование ошибок в Telegram
    /// </summary>
    public bool EnableTelegramLogging { get; set; } = true;

    /// <summary>
    /// Максимальная длина сообщения об ошибке в Telegram
    /// </summary>
    public int MaxMessageLength { get; set; } = 4000;

    /// <summary>
    /// Максимальное количество попыток отправки сообщения
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Задержка между попытками отправки в миллисекундах
    /// </summary>
    public int RetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// Максимальный размер очереди неотправленных сообщений
    /// </summary>
    public int MaxQueueSize { get; set; } = 100;
}