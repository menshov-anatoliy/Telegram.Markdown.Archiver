using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Markdown.Archiver.Models.Configuration;
using Whisper.net;

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
public class WhisperService : IWhisperService
{
    private readonly WhisperConfiguration _whisperConfiguration;
    private readonly ILogger<WhisperService> _logger;
    private WhisperProcessor? _processor;

    public WhisperService(IOptions<WhisperConfiguration> whisperConfiguration, ILogger<WhisperService> logger)
    {
        _whisperConfiguration = whisperConfiguration.Value;
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(string audioFilePath)
    {
        try
        {
            await EnsureProcessorInitializedAsync();

            if (_processor == null)
            {
                _logger.LogWarning("Процессор Whisper не инициализирован. Возвращается плейсхолдер.");
                return "[Транскрипция недоступна: модель Whisper не загружена]";
            }

            _logger.LogInformation("Начало транскрипции файла: {AudioFilePath}", audioFilePath);

            using var fileStream = File.OpenRead(audioFilePath);
            await foreach (var result in _processor.ProcessAsync(fileStream))
            {
                var transcription = result.Text.Trim();
                if (!string.IsNullOrEmpty(transcription))
                {
                    _logger.LogInformation("Транскрипция завершена. Длина текста: {Length} символов", transcription.Length);
                    return transcription;
                }
            }

            return "[Транскрипция не удалась: не удалось извлечь текст]";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при транскрипции файла {AudioFilePath}", audioFilePath);
            return "[Ошибка транскрипции]";
        }
    }

    private async Task EnsureProcessorInitializedAsync()
    {
        if (_processor != null)
            return;

        try
        {
            if (string.IsNullOrEmpty(_whisperConfiguration.ModelPath) || !File.Exists(_whisperConfiguration.ModelPath))
            {
                _logger.LogWarning("Модель Whisper не найдена по пути: {ModelPath}", _whisperConfiguration.ModelPath);
                return;
            }

            _logger.LogInformation("Загрузка модели Whisper: {ModelPath}", _whisperConfiguration.ModelPath);
            
            var factory = WhisperFactory.FromPath(_whisperConfiguration.ModelPath);
            _processor = factory.CreateBuilder()
                .WithLanguage("ru") // Устанавливаем русский язык
                .WithProbabilities()
                .Build();

            _logger.LogInformation("Модель Whisper успешно загружена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации Whisper с моделью {ModelPath}", _whisperConfiguration.ModelPath);
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _processor?.Dispose();
    }
}