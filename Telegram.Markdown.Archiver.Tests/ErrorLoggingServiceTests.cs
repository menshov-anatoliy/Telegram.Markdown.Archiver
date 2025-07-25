using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Markdown.Archiver.Models.Configuration;
using Telegram.Markdown.Archiver.Services;

namespace Telegram.Markdown.Archiver.Tests;

[TestClass]
public sealed class ErrorLoggingServiceTests
{
    private ErrorLoggingService _errorLoggingService = null!;
    private Mock<ITelegramService> _telegramServiceMock = null!;
    private Mock<ILogger<ErrorLoggingService>> _loggerMock = null!;
    private TelegramConfiguration _telegramConfiguration = null!;
    private ErrorLoggingConfiguration _errorLoggingConfiguration = null!;

    [TestInitialize]
    public void Setup()
    {
        _telegramServiceMock = new Mock<ITelegramService>();
        _loggerMock = new Mock<ILogger<ErrorLoggingService>>();
        
        _telegramConfiguration = new TelegramConfiguration
        {
            BotToken = "test_token",
            UserId = 123456789
        };

        _errorLoggingConfiguration = new ErrorLoggingConfiguration
        {
            EnableTelegramLogging = true,
            MaxMessageLength = 4000,
            MaxRetryAttempts = 3,
            RetryDelayMs = 1000, // Сократим для тестов
            MaxQueueSize = 100
        };

        var telegramOptionsMock = new Mock<IOptions<TelegramConfiguration>>();
        telegramOptionsMock.Setup(x => x.Value).Returns(_telegramConfiguration);

        var errorLoggingOptionsMock = new Mock<IOptions<ErrorLoggingConfiguration>>();
        errorLoggingOptionsMock.Setup(x => x.Value).Returns(_errorLoggingConfiguration);

        _errorLoggingService = new ErrorLoggingService(
            _telegramServiceMock.Object,
            telegramOptionsMock.Object,
            errorLoggingOptionsMock.Object,
            _loggerMock.Object);
    }

    [TestMethod]
    public async Task LogErrorAsync_CallsTelegramService()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var message = "Test error message";
        var context = "Test context";

        // Act
        await _errorLoggingService.LogErrorAsync(exception, message, context);

        // Assert
        _telegramServiceMock.Verify(
            x => x.SendTextMessageAsync(_telegramConfiguration.UserId, It.IsAny<string>()),
            Times.Once);
    }

    [TestMethod]
    public async Task LogCriticalErrorAsync_CallsTelegramService()
    {
        // Arrange
        var exception = new InvalidOperationException("Test critical exception");
        var message = "Test critical error message";
        var context = "Test critical context";

        // Act
        await _errorLoggingService.LogCriticalErrorAsync(exception, message, context);

        // Assert
        _telegramServiceMock.Verify(
            x => x.SendTextMessageAsync(_telegramConfiguration.UserId, It.IsAny<string>()),
            Times.Once);
    }

    [TestMethod]
    public async Task LogErrorAsync_WhenTelegramLoggingDisabled_DoesNotCallTelegramService()
    {
        // Arrange
        _errorLoggingConfiguration.EnableTelegramLogging = false;
        var exception = new InvalidOperationException("Test exception");

        // Act
        await _errorLoggingService.LogErrorAsync(exception);

        // Assert
        _telegramServiceMock.Verify(
            x => x.SendTextMessageAsync(It.IsAny<long>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public void StartBackgroundProcessing_DoesNotThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act & Assert - если метод не выбрасывает исключение, тест пройдет
        _errorLoggingService.StartBackgroundProcessing(cts.Token);
        
        // Проверим, что ничего не сломалось
        Assert.IsNotNull(_errorLoggingService);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _errorLoggingService?.Dispose();
    }
}