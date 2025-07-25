namespace Telegram.Markdown.Archiver.Models.Configuration;

/// <summary>
/// Конфигурация путей для файлов и директорий
/// </summary>
public class PathsConfiguration
{
    /// <summary>
    /// Корневая директория для заметок
    /// </summary>
    public string NotesRoot { get; set; } = string.Empty;

    /// <summary>
    /// Имя поддиректории для медиафайлов
    /// </summary>
    public string MediaDirectoryName { get; set; } = "media";

    /// <summary>
    /// Путь к файлу состояния
    /// </summary>
    public string StateFile { get; set; } = string.Empty;
}