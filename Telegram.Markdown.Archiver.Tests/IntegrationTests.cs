using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Markdown.Archiver.Models.Configuration;
using Telegram.Markdown.Archiver.Services;

namespace Telegram.Markdown.Archiver.Tests;

[TestClass]
public sealed class IntegrationTests
{
    [TestMethod]
    public async Task ErrorLoggingService_Integration_Test()
    {
        // Arrange
        var telegramServiceMock = new Mock<ITelegramService>();
        var loggerMock = new Mock<ILogger<ErrorLoggingService>>();
        
        var telegramConfig = new TelegramConfiguration
        {
            BotToken = "test_token",
            UserId = 123456789
        };

        var errorLoggingConfig = new ErrorLoggingConfiguration
        {
            EnableTelegramLogging = true,
            MaxMessageLength = 4000,
            MaxRetryAttempts = 3,
            RetryDelayMs = 100, // Быстрее для тестов
            MaxQueueSize = 100
        };

        var telegramOptionsMock = new Mock<IOptions<TelegramConfiguration>>();
        telegramOptionsMock.Setup(x => x.Value).Returns(telegramConfig);

        var errorLoggingOptionsMock = new Mock<IOptions<ErrorLoggingConfiguration>>();
        errorLoggingOptionsMock.Setup(x => x.Value).Returns(errorLoggingConfig);

        using var errorLoggingService = new ErrorLoggingService(
            telegramServiceMock.Object,
            telegramOptionsMock.Object,
            errorLoggingOptionsMock.Object,
            loggerMock.Object);

        // Act
        var testException = new InvalidOperationException("Тестовая ошибка для демонстрации");
        await errorLoggingService.LogErrorAsync(testException, "Это тестовое сообщение об ошибке", "IntegrationTests");

        await Task.Delay(200); // Небольшая задержка для обработки асинхронного вызова

        // Assert
        telegramServiceMock.Verify(
            x => x.SendTextMessageAsync(
                telegramConfig.UserId, 
                It.Is<string>(msg => 
                    msg.Contains("ERROR") && 
                    msg.Contains("Тестовая ошибка для демонстрации") &&
                    msg.Contains("IntegrationTests")
                )
            ),
            Times.Once);

        Console.WriteLine("✅ Интеграционный тест логирования ошибок прошел успешно!");
        Console.WriteLine("Сервис корректно отправляет сообщения об ошибках в Telegram чат 'Избранное'");
    }
}