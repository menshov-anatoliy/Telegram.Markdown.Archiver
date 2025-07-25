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

	/// <summary>
	/// Тип модели Whisper для загрузки (например, "Base", "Small", "Medium", "LargeV2").
	/// Используется для автоматической загрузки модели, если она отсутствует.
	/// </summary>
	public string ModelType { get; set; } = string.Empty;
}