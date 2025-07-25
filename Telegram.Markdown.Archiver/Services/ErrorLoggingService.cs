using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using Telegram.Markdown.Archiver.Models.Configuration;

namespace Telegram.Markdown.Archiver.Services;

/// <summary>
/// Интерфейс для логирования ошибок в Telegram
/// </summary>
public interface IErrorLoggingService
{
    /// <summary>
    /// Логировать ошибку в консоль и Telegram
    /// </summary>
    /// <param name="exception">Исключение</param>
    /// <param name="message">Дополнительное сообщение</param>
    /// <param name="context">Контекст ошибки</param>
    Task LogErrorAsync(Exception exception, string? message = null, string? context = null);

    /// <summary>
    /// Логировать критическую ошибку в консоль и Telegram
    /// </summary>
    /// <param name="exception">Исключение</param>
    /// <param name="message">Дополнительное сообщение</param>
    /// <param name="context">Контекст ошибки</param>
    Task LogCriticalErrorAsync(Exception exception, string? message = null, string? context = null);

    /// <summary>
    /// Запустить фоновую обработку очереди сообщений
    /// </summary>
    void StartBackgroundProcessing(CancellationToken cancellationToken);
}

/// <summary>
/// Модель сообщения об ошибке для очереди
/// </summary>
public record ErrorMessage(
    DateTime Timestamp,
    string Level,
    string Message,
    string? StackTrace,
    string? Context,
    int Attempts = 0);

/// <summary>
/// Сервис для логирования ошибок в консоль и Telegram
/// </summary>
public class ErrorLoggingService : IErrorLoggingService, IDisposable
{
    private readonly ITelegramService _telegramService;
    private readonly TelegramConfiguration _telegramConfiguration;
    private readonly ErrorLoggingConfiguration _errorLoggingConfiguration;
    private readonly ILogger<ErrorLoggingService> _logger;

    private readonly ConcurrentQueue<ErrorMessage> _messageQueue = new();
    private readonly Timer _retryTimer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    public ErrorLoggingService(
        ITelegramService telegramService,
        IOptions<TelegramConfiguration> telegramConfiguration,
        IOptions<ErrorLoggingConfiguration> errorLoggingConfiguration,
        ILogger<ErrorLoggingService> logger)
    {
        _telegramService = telegramService;
        _telegramConfiguration = telegramConfiguration.Value;
        _errorLoggingConfiguration = errorLoggingConfiguration.Value;
        _logger = logger;

        // Таймер для повторных попыток отправки
        _retryTimer = new Timer(ProcessMessageQueue, null, TimeSpan.FromSeconds(10), 
            TimeSpan.FromMilliseconds(_errorLoggingConfiguration.RetryDelayMs));
    }

    public async Task LogErrorAsync(Exception exception, string? message = null, string? context = null)
    {
        await LogErrorInternalAsync(exception, "ERROR", message, context);
    }

    public async Task LogCriticalErrorAsync(Exception exception, string? message = null, string? context = null)
    {
        await LogErrorInternalAsync(exception, "CRITICAL", message, context);
    }

    public void StartBackgroundProcessing(CancellationToken cancellationToken)
    {
        // Фоновая обработка уже запущена через таймер
        _logger.LogInformation("Фоновая обработка очереди сообщений об ошибках запущена");
    }

    private async Task LogErrorInternalAsync(Exception exception, string level, string? message, string? context)
    {
        try
        {
            // Всегда логируем в консоль
            if (level == "CRITICAL")
            {
                _logger.LogCritical(exception, "{Message} | Контекст: {Context}", message ?? exception.Message, context ?? "Неизвестно");
            }
            else
            {
                _logger.LogError(exception, "{Message} | Контекст: {Context}", message ?? exception.Message, context ?? "Неизвестно");
            }

            // Если Telegram логирование отключено, выходим
            if (!_errorLoggingConfiguration.EnableTelegramLogging)
            {
                return;
            }

            // Создаем сообщение для очереди
            var errorMessage = new ErrorMessage(
                DateTime.Now,
                level,
                message ?? exception.Message,
                exception.ToString(),
                context
            );

            // Добавляем в очередь
            if (_messageQueue.Count < _errorLoggingConfiguration.MaxQueueSize)
            {
                _messageQueue.Enqueue(errorMessage);
                _logger.LogDebug("Сообщение об ошибке добавлено в очередь для отправки в Telegram");

                // Попытаться отправить сразу
                await TrySendToTelegramAsync(errorMessage);
            }
            else
            {
                _logger.LogWarning("Очередь сообщений об ошибках переполнена, сообщение не будет отправлено в Telegram");
            }
        }
        catch (Exception ex)
        {
            // Ошибка в системе логирования не должна влиять на основное приложение
            _logger.LogWarning(ex, "Ошибка при логировании сообщения об ошибке");
        }
    }

    private async Task TrySendToTelegramAsync(ErrorMessage errorMessage)
    {
        try
        {
            await _semaphore.WaitAsync();

            var telegramMessage = FormatTelegramMessage(errorMessage);
            await _telegramService.SendTextMessageAsync(_telegramConfiguration.UserId, telegramMessage);

            _logger.LogDebug("Сообщение об ошибке успешно отправлено в Telegram");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось отправить сообщение об ошибке в Telegram, останется в очереди для повторной попытки");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ProcessMessageQueue(object? state)
    {
        if (_disposed) return;

        Task.Run(async () =>
        {
            var processedMessages = new List<ErrorMessage>();

            while (_messageQueue.TryDequeue(out var errorMessage))
            {
                try
                {
                    if (errorMessage.Attempts >= _errorLoggingConfiguration.MaxRetryAttempts)
                    {
                        _logger.LogWarning("Превышено максимальное количество попыток отправки сообщения об ошибке в Telegram");
                        continue;
                    }

                    await TrySendToTelegramAsync(errorMessage);
                    
                    // Если дошли сюда без исключения, сообщение отправлено успешно
                    processedMessages.Add(errorMessage);
                }
                catch
                {
                    // Увеличиваем счетчик попыток и возвращаем в очередь
                    var updatedMessage = errorMessage with { Attempts = errorMessage.Attempts + 1 };
                    if (updatedMessage.Attempts < _errorLoggingConfiguration.MaxRetryAttempts)
                    {
                        _messageQueue.Enqueue(updatedMessage);
                    }
                }
            }
        });
    }

    private string FormatTelegramMessage(ErrorMessage errorMessage)
    {
        var sb = new StringBuilder();
        
        // Заголовок с эмоджи
        var emoji = errorMessage.Level == "CRITICAL" ? "🚨" : "❌";
        sb.AppendLine($"{emoji} **{errorMessage.Level}** | {errorMessage.Timestamp:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine();

        // Контекст, если есть
        if (!string.IsNullOrEmpty(errorMessage.Context))
        {
            sb.AppendLine($"**Контекст:** {errorMessage.Context}");
            sb.AppendLine();
        }

        // Сообщение об ошибке
        sb.AppendLine($"**Ошибка:** {errorMessage.Message}");
        sb.AppendLine();

        // Стек-трейс (сокращенный)
        if (!string.IsNullOrEmpty(errorMessage.StackTrace))
        {
            sb.AppendLine("**Стек-трейс:**");
            var stackTrace = errorMessage.StackTrace;
            
            // Ограничиваем длину стек-трейса
            var maxStackTraceLength = _errorLoggingConfiguration.MaxMessageLength - sb.Length - 100;
            if (stackTrace.Length > maxStackTraceLength)
            {
                stackTrace = stackTrace.Substring(0, maxStackTraceLength) + "...";
            }
            
            sb.AppendLine($"```");
            sb.AppendLine(stackTrace);
            sb.AppendLine("```");
        }

        var result = sb.ToString();
        
        // Убеждаемся, что сообщение не превышает лимит Telegram
        if (result.Length > _errorLoggingConfiguration.MaxMessageLength)
        {
            result = result.Substring(0, _errorLoggingConfiguration.MaxMessageLength - 3) + "...";
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _retryTimer?.Dispose();
        _semaphore?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}