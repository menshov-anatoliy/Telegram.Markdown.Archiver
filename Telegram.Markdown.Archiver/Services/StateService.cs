using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Markdown.Archiver.Models.Configuration;
using Telegram.Markdown.Archiver.Models.State;

namespace Telegram.Markdown.Archiver.Services;

/// <summary>
/// Интерфейс для работы с состоянием приложения
/// </summary>
public interface IStateService
{
	/// <summary>
	/// Получить текущее состояние приложения
	/// </summary>
	Task<ApplicationState> GetStateAsync();

	/// <summary>
	/// Сохранить состояние приложения
	/// </summary>
	Task SaveStateAsync(ApplicationState state);
}

/// <summary>
/// Сервис для работы с состоянием приложения
/// </summary>
public class StateService(IOptions<PathsConfiguration> pathsConfiguration, ILogger<StateService> logger)
	: IStateService
{
	private readonly PathsConfiguration _pathsConfiguration = pathsConfiguration.Value;

	public async Task<ApplicationState> GetStateAsync()
	{
		try
		{
			if (!File.Exists(_pathsConfiguration.StateFile))
			{
				logger.LogInformation("Файл состояния не найден. Создается новое состояние.");
				return new ApplicationState();
			}

			var json = await File.ReadAllTextAsync(_pathsConfiguration.StateFile);
			var state = JsonSerializer.Deserialize<ApplicationState>(json);

			logger.LogInformation("Состояние загружено. Последний обработанный update_id: {UpdateId}",
				state?.LastProcessedUpdateId ?? 0);

			return state ?? new ApplicationState();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Ошибка при загрузке состояния из файла {StateFile}", _pathsConfiguration.StateFile);
			return new ApplicationState();
		}
	}

	public async Task SaveStateAsync(ApplicationState state)
	{
		try
		{
			state.LastUpdated = DateTime.UtcNow;

			// Создать директорию, если она не существует
			var directory = Path.GetDirectoryName(_pathsConfiguration.StateFile);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
			await File.WriteAllTextAsync(_pathsConfiguration.StateFile, json);

			logger.LogInformation("Состояние сохранено. Update_id: {UpdateId}", state.LastProcessedUpdateId);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Ошибка при сохранении состояния в файл {StateFile}", _pathsConfiguration.StateFile);
			throw;
		}
	}
}