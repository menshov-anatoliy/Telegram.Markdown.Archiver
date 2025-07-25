namespace Telegram.Markdown.Archiver.Models.Configuration;

/// <summary>
/// Конфигурация для Whisper (транскрипция голосовых сообщений)
/// </summary>
public class WhisperConfiguration
{
    /// <summary>
    /// Путь к модели Whisper
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;
}