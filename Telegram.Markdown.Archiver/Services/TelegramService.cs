using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Markdown.Archiver.Models.Configuration;

namespace Telegram.Markdown.Archiver.Services;

/// <summary>
/// Интерфейс для работы с Telegram Bot API
/// </summary>
public interface ITelegramService
{
    /// <summary>
    /// Получить обновления с указанного offset
    /// </summary>
    Task<Update[]> GetUpdatesAsync(int offset = 0);

    /// <summary>
    /// Скачать файл по его ID
    /// </summary>
    Task<byte[]?> DownloadFileAsync(string fileId);

    /// <summary>
    /// Отправить текстовое сообщение в чат
    /// </summary>
    Task SendTextMessageAsync(long chatId, string text);

    /// <summary>
    /// Получить информацию о файле
    /// </summary>
    Task<object?> GetFileAsync(string fileId);
}

/// <summary>
/// Сервис для работы с Telegram Bot API
/// </summary>
public class TelegramService : ITelegramService
{
    private readonly TelegramBotClient _botClient;
    private readonly TelegramConfiguration _telegramConfiguration;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(IOptions<TelegramConfiguration> telegramConfiguration, ILogger<TelegramService> logger)
    {
        _telegramConfiguration = telegramConfiguration.Value;
        _logger = logger;
        _botClient = new TelegramBotClient(_telegramConfiguration.BotToken);
    }

    public async Task<Update[]> GetUpdatesAsync(int offset = 0)
    {
        try
        {
            _logger.LogDebug("Получение обновлений с offset: {Offset}", offset);
            
            var updates = await _botClient.GetUpdates(
                offset: offset,
                limit: 100,
                timeout: 10,
                allowedUpdates: new[] { UpdateType.Message }
            );

            _logger.LogInformation("Получено {Count} обновлений", updates.Length);
            return updates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении обновлений");
            return Array.Empty<Update>();
        }
    }

    public async Task<byte[]?> DownloadFileAsync(string fileId)
    {
        try
        {
            var file = await _botClient.GetFile(fileId);
            if (file?.FilePath == null)
            {
                _logger.LogWarning("Не удалось получить информацию о файле {FileId}", fileId);
                return null;
            }

            using var stream = new MemoryStream();
            await _botClient.DownloadFile(file.FilePath, stream);
            
            _logger.LogInformation("Файл {FileId} загружен, размер: {Size} байт", fileId, stream.Length);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при скачивании файла {FileId}", fileId);
            return null;
        }
    }

    public async Task SendTextMessageAsync(long chatId, string text)
    {
        try
        {
            await _botClient.SendMessage(chatId, text);
            _logger.LogInformation("Сообщение отправлено в чат {ChatId}: {Text}", chatId, text.Substring(0, Math.Min(text.Length, 50)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке сообщения в чат {ChatId}", chatId);
        }
    }

    public async Task<object?> GetFileAsync(string fileId)
    {
        try
        {
            var file = await _botClient.GetFile(fileId);
            _logger.LogDebug("Получена информация о файле {FileId}: {FilePath}", fileId, file.FilePath);
            return file;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении информации о файле {FileId}", fileId);
            return null;
        }
    }
}