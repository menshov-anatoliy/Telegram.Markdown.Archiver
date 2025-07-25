using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Markdown.Archiver.Models.Configuration;
using Whisper.net;
using Whisper.net.Ggml;

namespace Telegram.Markdown.Archiver.Services;

/// <summary>
/// Интерфейс для транскрипции голосовых сообщений
/// </summary>
public interface IWhisperService
{
	/// <summary>
	/// Транскрибировать аудиофайл
	/// </summary>
	Task<string> TranscribeAsync(string audioFilePath);
}

/// <summary>
/// Сервис для транскрипции голосовых сообщений с использованием Whisper
/// </summary>
public class WhisperService(
	IOptions<WhisperConfiguration> whisperConfiguration,
	ILogger<WhisperService> logger,
	IHttpClientFactory httpClientFactory,
	IErrorLoggingService errorLoggingService)
	: IWhisperService, IDisposable
{
	private readonly WhisperConfiguration _whisperConfiguration = whisperConfiguration.Value;

	private WhisperProcessor? _processor;

	public async Task<string> TranscribeAsync(string audioFilePath)
	{
		string? tempWavPath = null;
		try
		{
			await EnsureProcessorInitializedAsync();

			if (_processor == null)
			{
				logger.LogWarning("Процессор Whisper не инициализирован. Модель Whisper не загружена.");
				return "[Транскрипция недоступна: модель Whisper не загружена]";
			}

			tempWavPath = await ConvertToWavAsync(audioFilePath);
			if (tempWavPath == null)
			{
				return "[Ошибка транскрипции: не удалось конвертировать файл]";
			}

			logger.LogInformation("Начало транскрипции файла: {AudioFilePath}", tempWavPath);

			await using var fileStream = File.OpenRead(tempWavPath);

			var transcriptionBuilder = new StringBuilder();
			await foreach (var result in _processor.ProcessAsync(fileStream))
			{
				transcriptionBuilder.Append(result.Text);
			}

			var transcription = transcriptionBuilder.ToString().Trim();
			if (!string.IsNullOrEmpty(transcription))
			{
				logger.LogInformation("Транскрипция завершена. Длина текста: {Length} символов", transcription.Length);
				return transcription;
			}

			return "[Транскрипция не удалась: не удалось извлечь текст]";
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Ошибка при транскрипции файла {AudioFilePath}", audioFilePath);
			await errorLoggingService.LogErrorAsync(ex, $"Ошибка при транскрипции аудиофайла {audioFilePath}", "WhisperService.TranscribeAsync");
			return "[Ошибка транскрипции]";
		}
		finally
		{
			if (tempWavPath != null && File.Exists(tempWavPath))
			{
				try
				{
					File.Delete(tempWavPath);
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Не удалось удалить временный WAV файл: {TempWavPath}", tempWavPath);
				}
			}
		}
	}

	private async Task<string?> ConvertToWavAsync(string inputPath)
	{
		var outputPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
		var processStartInfo = new ProcessStartInfo
		{
			FileName = "ffmpeg",
			Arguments = $"-i \"{inputPath}\" -ar 16000 -ac 1 -c:a pcm_s16le \"{outputPath}\"",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = new Process { StartInfo = processStartInfo };

		try
		{
			process.Start();
			var error = await process.StandardError.ReadToEndAsync();
			await process.WaitForExitAsync();

			if (process.ExitCode != 0)
			{
				logger.LogError("Ошибка конвертации файла с помощью FFmpeg. Exit code: {ExitCode}. Error: {Error}", process.ExitCode, error);
				File.Delete(outputPath);
				return null;
			}

			logger.LogInformation("Файл успешно сконвертирован в {OutputPath}", outputPath);
			return outputPath;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Исключение при вызове FFmpeg для конвертации файла {InputPath}", inputPath);
			await errorLoggingService.LogErrorAsync(ex, $"Ошибка конвертации аудиофайла через FFmpeg: {inputPath}", "WhisperService.ConvertToWavAsync");
			if (File.Exists(outputPath))
			{
				File.Delete(outputPath);
			}
			return null;
		}
	}

	private async Task EnsureProcessorInitializedAsync()
	{
		if (_processor != null)
			return;

		try
		{
			// Проверяем, существует ли файл модели
			if (!File.Exists(_whisperConfiguration.ModelPath))
			{
				logger.LogWarning("Модель Whisper не найдена по пути: {ModelPath}", _whisperConfiguration.ModelPath);

				// Если модель не найдена, пытаемся ее скачать, если указан ModelType
				if (string.IsNullOrWhiteSpace(_whisperConfiguration.ModelType))
				{
					logger.LogError("Тип модели (ModelType) не указан в конфигурации. Невозможно скачать модель.");
					return;
				}

				if (!Enum.TryParse<GgmlType>(_whisperConfiguration.ModelType, true, out var ggmlType))
				{
					logger.LogError("Неверный тип модели Whisper: {ModelType}", _whisperConfiguration.ModelType);
					return;
				}

				logger.LogInformation("Начало загрузки модели Whisper типа {GgmlType}", ggmlType);

				// Убедимся, что директория для модели существует
				var modelDirectory = Path.GetDirectoryName(_whisperConfiguration.ModelPath);
				if (modelDirectory != null && !Directory.Exists(modelDirectory))
				{
					Directory.CreateDirectory(modelDirectory);
				}

				var downloader = new WhisperGgmlDownloader(httpClientFactory.CreateClient());

				await using var modelStream = await downloader.GetGgmlModelAsync(ggmlType);

				await using var fileStream = File.Create(_whisperConfiguration.ModelPath);

				await modelStream.CopyToAsync(fileStream);

				logger.LogInformation("Модель Whisper успешно загружена в: {ModelPath}", _whisperConfiguration.ModelPath);
			}


			logger.LogInformation("Загрузка модели Whisper: {ModelPath}", _whisperConfiguration.ModelPath);

			var factory = WhisperFactory.FromPath(_whisperConfiguration.ModelPath);

			_processor = factory.CreateBuilder()
				.WithLanguage("ru") // Устанавливаем русский язык
				.WithProbabilities()
				.Build();

			logger.LogInformation("Модель Whisper успешно загружена");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Ошибка при инициализации Whisper с моделью {ModelPath}", _whisperConfiguration.ModelPath);
			await errorLoggingService.LogErrorAsync(ex, $"Ошибка при инициализации модели Whisper: {_whisperConfiguration.ModelPath}", "WhisperService.EnsureProcessorInitializedAsync");
		}
	}

	public void Dispose()
	{
		_processor?.Dispose();
		GC.SuppressFinalize(this);
	}
}