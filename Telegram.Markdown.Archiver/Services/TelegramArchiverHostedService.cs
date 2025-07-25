using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Markdown.Archiver.Models.Configuration;
using Telegram.Markdown.Archiver.Models.State;

namespace Telegram.Markdown.Archiver.Services;

/// <summary>
/// Фоновый сервис для обработки сообщений Telegram
/// </summary>
public class TelegramArchiverHostedService : BackgroundService
{
	private readonly IStateService _stateService;
	private readonly ITelegramService _telegramService;
	private readonly IFileSystemService _fileSystemService;
	private readonly IMarkdownService _markdownService;
	private readonly IWhisperService _whisperService;
	private readonly TelegramConfiguration _telegramConfiguration;
	private readonly ILogger<TelegramArchiverHostedService> _logger;

	private const int PollingDelayMs = 1000; // Задержка между опросами в миллисекундах
	private const int RetryDelayMs = 5000; // Задержка при ошибках

	public TelegramArchiverHostedService(
		IStateService stateService,
		ITelegramService telegramService,
		IFileSystemService fileSystemService,
		IMarkdownService markdownService,
		IWhisperService whisperService,
		IOptions<TelegramConfiguration> telegramConfiguration,
		ILogger<TelegramArchiverHostedService> logger)
	{
		_stateService = stateService;
		_telegramService = telegramService;
		_fileSystemService = fileSystemService;
		_markdownService = markdownService;
		_whisperService = whisperService;
		_telegramConfiguration = telegramConfiguration.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Сервис архивирования Telegram запущен");

		// Обеспечить существование необходимых директорий
		await _fileSystemService.EnsureDirectoriesExistAsync();

		// Загрузить состояние
		var state = await _stateService.GetStateAsync();
		var currentOffset = state.LastProcessedUpdateId + 1;

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var updates = await _telegramService.GetUpdatesAsync(currentOffset);

				foreach (var update in updates)
				{
					if (update.Message != null)
					{
						await ProcessMessageAsync(update.Message);
					}

					// Обновить offset
					currentOffset = update.Id + 1;
					state.LastProcessedUpdateId = update.Id;
				}

				// Сохранить состояние, если были обновления
				if (updates.Length > 0)
				{
					await _stateService.SaveStateAsync(state);
				}

				// Небольшая задержка между опросами
				await Task.Delay(PollingDelayMs, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				// Нормальное завершение работы
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка при обработке обновлений");
				await Task.Delay(RetryDelayMs, stoppingToken);
			}
		}

		_logger.LogInformation("Сервис архивирования Telegram остановлен");
	}

	private async Task ProcessMessageAsync(Message message)
	{
		try
		{
			_logger.LogInformation("Обработка сообщения {MessageId} типа {MessageType}",
				message.MessageId, message.Type);

			string? mediaFileName = null;
			string? transcription = null;

			// Обработка медиафайлов
			if (IsMediaMessage(message))
			{
				mediaFileName = await ProcessMediaAsync(message);
			}

			// Обработка голосовых сообщений
			if (message.Type == MessageType.Voice && message.Voice != null)
			{
				transcription = await ProcessVoiceMessageAsync(message.Voice, mediaFileName);

				// Отправить транскрипцию обратно в чат
				if (!string.IsNullOrEmpty(transcription) && transcription != "[Ошибка транскрипции]")
				{
					await _telegramService.SendTextMessageAsync(message.Chat.Id, transcription);
				}
			}

			// Получить информацию о сообщении, на которое отвечаем (если есть)
			Message? replyToMessage = null;
			if (message.ReplyToMessage != null)
			{
				replyToMessage = message.ReplyToMessage;
			}

			// Форматировать сообщение в Markdown
			var markdownContent = _markdownService.FormatMessage(message, mediaFileName, transcription, replyToMessage);

			// Сохранить в файл заметок
			var notesFilePath = _fileSystemService.GetNotesFilePath(message.Date.ToLocalTime());
			await _markdownService.AppendToNotesFileAsync(notesFilePath, markdownContent);

			_logger.LogInformation("Сообщение {MessageId} успешно обработано и сохранено", message.MessageId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Ошибка при обработке сообщения {MessageId}", message.MessageId);
		}
	}

	private static bool IsMediaMessage(Message message)
	{
		return message.Type switch
		{
			MessageType.Photo => true,
			MessageType.Video => true,
			MessageType.Document => true,
			MessageType.Audio => true,
			MessageType.Voice => true,
			_ => false
		};
	}

	private async Task<string?> ProcessMediaAsync(Message message)
	{
		try
		{
			string? fileId = message.Type switch
			{
				MessageType.Photo => message.Photo?.LastOrDefault()?.FileId,
				MessageType.Video => message.Video?.FileId,
				MessageType.Document => message.Document?.FileId,
				MessageType.Audio => message.Audio?.FileId,
				MessageType.Voice => message.Voice?.FileId,
				_ => null
			};

			if (string.IsNullOrEmpty(fileId))
			{
				_logger.LogWarning("Не удалось получить file_id для сообщения {MessageId}", message.MessageId);
				return null;
			}

			// Получить информацию о файле
			var fileInfo = await _telegramService.GetFileAsync(fileId);
			var originalFileName = GetOriginalFileName(message, fileInfo);

			// Скачать файл
			var fileData = await _telegramService.DownloadFileAsync(fileId);
			if (fileData == null)
			{
				_logger.LogWarning("Не удалось скачать файл {FileId}", fileId);
				return null;
			}

			// Сохранить файл
			var savedFileName = await _fileSystemService.SaveMediaFileAsync(fileData, originalFileName);
			return savedFileName;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Ошибка при обработке медиафайла в сообщении {MessageId}", message.MessageId);
			return null;
		}
	}

	private async Task<string?> ProcessVoiceMessageAsync(Voice voice, string? audioFileName)
	{
		try
		{
			if (string.IsNullOrEmpty(audioFileName))
			{
				_logger.LogWarning("Имя аудиофайла не указано для транскрипции");
				return null;
			}

			var mediaDirectory = _fileSystemService.GetMediaDirectoryPath();
			var audioFilePath = Path.Combine(mediaDirectory, audioFileName);

			if (!System.IO.File.Exists(audioFilePath))
			{
				_logger.LogWarning("Аудиофайл не найден: {AudioFilePath}", audioFilePath);
				return null;
			}

			return await _whisperService.TranscribeAsync(audioFilePath);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Ошибка при транскрипции голосового сообщения");
			return "[Ошибка транскрипции]";
		}
	}

	private static string GetOriginalFileName(Message message, object? fileInfo)
	{
		var extension = message.Type switch
		{
			MessageType.Photo => ".jpg",
			MessageType.Video => ".mp4",
			MessageType.Voice => ".ogg",
			MessageType.Audio => ".mp3",
			MessageType.Document => Path.GetExtension(message.Document?.FileName ?? ""),
			_ => ""
		};

		// Попытаться получить оригинальное имя файла
		var fileName = message.Type switch
		{
			MessageType.Document => message.Document?.FileName,
			MessageType.Audio => message.Audio?.FileName,
			_ => null
		};

		if (!string.IsNullOrEmpty(fileName))
		{
			return fileName;
		}

		// Генерировать имя на основе типа и времени
		var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		var baseName = message.Type switch
		{
			MessageType.Photo => "photo",
			MessageType.Video => "video",
			MessageType.Voice => "voice",
			MessageType.Audio => "audio",
			MessageType.Document => "document",
			_ => "file"
		};

		return $"{baseName}_{timestamp}{extension}";
	}
}