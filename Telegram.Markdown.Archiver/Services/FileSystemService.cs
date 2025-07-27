using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Markdown.Archiver.Models.Configuration;

namespace Telegram.Markdown.Archiver.Services;

/// <summary>
/// Интерфейс для работы с файловой системой
/// </summary>
public interface IFileSystemService
{
	/// <summary>
	/// Получить путь к файлу заметок для указанной даты
	/// </summary>
	string GetNotesFilePath(DateTime date);

	/// <summary>
	/// Получить путь к директории медиафайлов
	/// </summary>
	string GetMediaDirectoryPath();

	/// <summary>
	/// Получить уникальное имя файла для медиафайла
	/// </summary>
	string GetUniqueMediaFileName(string originalFileName);

	/// <summary>
	/// Обеспечить существование необходимых директорий
	/// </summary>
	Task EnsureDirectoriesExistAsync();

	/// <summary>
	/// Сохранить файл в медиа-директорию
	/// </summary>
	Task<string> SaveMediaFileAsync(byte[] fileData, string fileName);
}

/// <summary>
/// Сервис для работы с файловой системой
/// </summary>
public class FileSystemService(IOptions<PathsConfiguration> pathsConfiguration, ILogger<FileSystemService> logger,
	IErrorLoggingService errorLoggingService)
	: IFileSystemService
{
	private readonly PathsConfiguration _pathsConfiguration = pathsConfiguration.Value;

	public string GetNotesFilePath(DateTime date)
	{
		var fileName = $"Telegram-{date:yyyy-MM-dd}_Notes.md";
		return Path.Combine(_pathsConfiguration.NotesRoot, fileName);
	}

	public string GetMediaDirectoryPath()
	{
		return Path.Combine(_pathsConfiguration.NotesRoot, _pathsConfiguration.MediaDirectoryName);
	}

	public string GetUniqueMediaFileName(string originalFileName)
	{
		var mediaDirectory = GetMediaDirectoryPath();
		var fullPath = Path.Combine(mediaDirectory, originalFileName);

		if (!File.Exists(fullPath))
		{
			return originalFileName;
		}

		// Если файл существует, добавляем суффикс с датой и временем
		var extension = Path.GetExtension(originalFileName);
		var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
		var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

		return $"{nameWithoutExtension}_{timestamp}{extension}";
	}

	public async Task EnsureDirectoriesExistAsync()
	{
		try
		{
			if (!Directory.Exists(_pathsConfiguration.NotesRoot))
			{
				Directory.CreateDirectory(_pathsConfiguration.NotesRoot);
				logger.LogInformation("Создана директория для заметок: {NotesRoot}", _pathsConfiguration.NotesRoot);
			}

			var mediaDirectory = GetMediaDirectoryPath();
			if (!Directory.Exists(mediaDirectory))
			{
				Directory.CreateDirectory(mediaDirectory);
				logger.LogInformation("Создана директория для медиафайлов: {MediaDirectory}", mediaDirectory);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Ошибка при создании директорий");
			await errorLoggingService.LogErrorAsync(ex, "Ошибка при создании необходимых директорий", "FileSystemService.EnsureDirectoriesExistAsync");
			throw;
		}

		await Task.CompletedTask;
	}

	public async Task<string> SaveMediaFileAsync(byte[] fileData, string fileName)
	{
		try
		{
			await EnsureDirectoriesExistAsync();

			var uniqueFileName = GetUniqueMediaFileName(fileName);
			var mediaDirectory = GetMediaDirectoryPath();
			var fullPath = Path.Combine(mediaDirectory, uniqueFileName);

			await File.WriteAllBytesAsync(fullPath, fileData);

			logger.LogInformation("Медиафайл сохранен: {FileName} ({Size} байт)", uniqueFileName, fileData.Length);

			return uniqueFileName;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Ошибка при сохранении медиафайла {FileName}", fileName);
			await errorLoggingService.LogErrorAsync(ex, $"Ошибка при сохранении медиафайла {fileName}", "FileSystemService.SaveMediaFileAsync");
			throw;
		}
	}
}