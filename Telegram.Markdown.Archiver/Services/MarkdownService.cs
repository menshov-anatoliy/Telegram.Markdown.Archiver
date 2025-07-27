using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Markdown.Archiver.Models.Configuration;

namespace Telegram.Markdown.Archiver.Services;

/// <summary>
/// Интерфейс для форматирования сообщений в Markdown
/// </summary>
public interface IMarkdownService
{
	/// <summary>
	/// Форматировать сообщение в Markdown
	/// </summary>
	string FormatMessage(Message message, string? mediaFileName = null, string? transcription = null, Message? replyToMessage = null);

	/// <summary>
	/// Добавить сообщение в файл заметок
	/// </summary>
	Task AppendToNotesFileAsync(string filePath, string content);
}

/// <summary>
/// Сервис для форматирования сообщений в Markdown
/// </summary>
public class MarkdownService : IMarkdownService
{
	private readonly ILogger<MarkdownService> _logger;
	private readonly IErrorLoggingService _errorLoggingService;
	private readonly PathsConfiguration _pathsConfiguration;
	private static readonly CultureInfo RussianCulture = new("ru-RU");

	public MarkdownService(ILogger<MarkdownService> logger, IErrorLoggingService errorLoggingService, IOptions<PathsConfiguration> pathsConfiguration)
	{
		_logger = logger;
		_errorLoggingService = errorLoggingService;
		_pathsConfiguration = pathsConfiguration.Value;
	}

	public string FormatMessage(Message message, string? mediaFileName = null, string? transcription = null, Message? replyToMessage = null)
	{
		var messageTime = message.Date.ToLocalTime();
		var dayOfWeek = RussianCulture.DateTimeFormat.GetAbbreviatedDayName(messageTime.DayOfWeek);

		var sb = new StringBuilder();

		// Заголовок сообщения
		sb.AppendLine($"### [[{messageTime:yyyy-MM-dd} {dayOfWeek}]] {messageTime:HH:mm:ss}");
		sb.AppendLine();

		// Если это ответ на сообщение, добавляем цитату
		if (replyToMessage != null)
		{
			var replyText = GetMessageText(replyToMessage);
			if (!string.IsNullOrEmpty(replyText))
			{
				var quotedReply = string.Join("\n", replyText.Split('\n').Select(line => $"> {line}"));
				sb.AppendLine(quotedReply);
				sb.AppendLine();
			}
		}

		// Основной контент в зависимости от типа сообщения
		switch (message.Type)
		{
			case MessageType.Text:
				sb.AppendLine(message.Text ?? "");
				break;

			case MessageType.Photo:
				if (!string.IsNullOrEmpty(mediaFileName))
				{
					sb.AppendLine($"![](./{_pathsConfiguration.MediaDirectoryName}/{mediaFileName})");
				}
				if (!string.IsNullOrEmpty(message.Caption))
				{
					sb.AppendLine();
					sb.AppendLine(message.Caption);
				}
				break;

			case MessageType.Video:
			case MessageType.Document:
			case MessageType.Audio:
				if (!string.IsNullOrEmpty(mediaFileName))
				{
					sb.AppendLine($"[{mediaFileName}](./{_pathsConfiguration.MediaDirectoryName}/{mediaFileName})");
				}
				if (!string.IsNullOrEmpty(message.Caption))
				{
					sb.AppendLine();
					sb.AppendLine(message.Caption);
				}
				break;

			case MessageType.Voice:
				if (!string.IsNullOrEmpty(mediaFileName))
				{
					sb.AppendLine($"[{mediaFileName}](./{_pathsConfiguration.MediaDirectoryName}/{mediaFileName})");
					sb.AppendLine();
				}
				if (!string.IsNullOrEmpty(transcription))
				{
					var quotedTranscription = string.Join("\n", transcription.Split('\n').Select(line => $"> {line}"));
					sb.AppendLine(quotedTranscription);
				}
				break;

			case MessageType.Sticker:
				sb.AppendLine("[Стикер]");
				if (!string.IsNullOrEmpty(message.Sticker?.Emoji))
				{
					sb.AppendLine($"Эмодзи: {message.Sticker.Emoji}");
				}
				break;

			case MessageType.Animation:
				sb.AppendLine("[GIF-анимация]");
				if (!string.IsNullOrEmpty(message.Caption))
				{
					sb.AppendLine();
					sb.AppendLine(message.Caption);
				}
				break;

			case MessageType.Location:
				if (message.Location != null)
				{
					sb.AppendLine($"[Геолокация] Широта: {message.Location.Latitude}, Долгота: {message.Location.Longitude}");
				}
				break;

			case MessageType.Poll:
				sb.AppendLine("[Опрос]");
				if (message.Poll != null)
				{
					sb.AppendLine($"Вопрос: {message.Poll.Question}");
				}
				break;

			case MessageType.Contact:
				sb.AppendLine("[Контакт]");
				if (message.Contact != null)
				{
					sb.AppendLine($"Имя: {message.Contact.FirstName} {message.Contact.LastName}");
					sb.AppendLine($"Телефон: {message.Contact.PhoneNumber}");
				}
				break;

			default:
				sb.AppendLine($"[Неподдерживаемый тип сообщения: {message.Type}]");
				break;
		}

		sb.AppendLine();
		sb.AppendLine("---");
		sb.AppendLine();

		return sb.ToString();
	}

	public async Task AppendToNotesFileAsync(string filePath, string content)
	{
		try
		{
			// Создать директорию, если она не существует
			var directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			await File.AppendAllTextAsync(filePath, content, Encoding.UTF8);
			_logger.LogInformation("Контент добавлен в файл заметок: {FilePath}", filePath);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Ошибка при добавлении контента в файл заметок {FilePath}", filePath);
			await _errorLoggingService.LogErrorAsync(ex, $"Ошибка при записи в файл заметок {filePath}", "MarkdownService.AppendToNotesFileAsync");
			throw;
		}
	}

	private static string GetMessageText(Message message)
	{
		return message.Type switch
		{
			MessageType.Text => message.Text ?? "",
			MessageType.Photo => message.Caption ?? "[Фото]",
			MessageType.Video => message.Caption ?? "[Видео]",
			MessageType.Document => message.Caption ?? "[Документ]",
			MessageType.Audio => message.Caption ?? "[Аудио]",
			MessageType.Voice => "[Голосовое сообщение]",
			MessageType.Sticker => $"[Стикер {message.Sticker?.Emoji}]",
			MessageType.Animation => message.Caption ?? "[GIF]",
			MessageType.Location => "[Геолокация]",
			MessageType.Poll => $"[Опрос: {message.Poll?.Question}]",
			MessageType.Contact => $"[Контакт: {message.Contact?.FirstName} {message.Contact?.LastName}]",
			_ => $"[{message.Type}]"
		};
	}
}