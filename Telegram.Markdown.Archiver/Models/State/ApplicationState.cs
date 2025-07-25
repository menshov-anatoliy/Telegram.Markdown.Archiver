namespace Telegram.Markdown.Archiver.Models.State;

/// <summary>
/// Состояние приложения для отслеживания последних обработанных сообщений
/// </summary>
public class ApplicationState
{
    /// <summary>
    /// ID последнего обработанного обновления
    /// </summary>
    public int LastProcessedUpdateId { get; set; }

    /// <summary>
    /// Время последнего обновления состояния
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}