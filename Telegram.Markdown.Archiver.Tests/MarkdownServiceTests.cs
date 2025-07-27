using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Telegram.Bot.Types;
using Telegram.Markdown.Archiver.Models.Configuration;
using Telegram.Markdown.Archiver.Services;

namespace Telegram.Markdown.Archiver.Tests;

[TestClass]
public sealed class MarkdownServiceTests
{
    private MarkdownService _markdownService = null!;
    private Mock<ILogger<MarkdownService>> _loggerMock = null!;
    private Mock<IErrorLoggingService> _errorLoggingServiceMock = null!;
    private Mock<IOptions<PathsConfiguration>> _pathsOptionsMock = null!;
    private PathsConfiguration _pathsConfiguration = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MarkdownService>>();
        _errorLoggingServiceMock = new Mock<IErrorLoggingService>();
        
        _pathsConfiguration = new PathsConfiguration { MediaDirectoryName = "test_media" };
        _pathsOptionsMock = new Mock<IOptions<PathsConfiguration>>();
        _pathsOptionsMock.Setup(x => x.Value).Returns(_pathsConfiguration);
        
        _markdownService = new MarkdownService(_loggerMock.Object, _errorLoggingServiceMock.Object, _pathsOptionsMock.Object);
    }

    [TestMethod]
    public void MarkdownService_Constructor_InitializesCorrectly()
    {
        // Assert
        Assert.IsNotNull(_markdownService);
    }

    [TestMethod]
    public void FormatMessage_WithPhoto_IncludesCorrectMarkdown()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var caption = "Test caption";
        var mediaFileName = "photo.jpg";
        
        var messageJson = $$"""
        {
            "message_id": 1,
            "date": {{new DateTimeOffset(date).ToUnixTimeSeconds()}},
            "chat": {"id": 123, "type": "private"},
            "photo": [],
            "caption": "{{caption}}"
        }
        """;

        var message = JsonSerializer.Deserialize<Message>(messageJson)!;
        
        // Act
        var result = _markdownService.FormatMessage(message, mediaFileName: mediaFileName);

        // Assert
        Assert.IsTrue(result.Contains($"![](./{_pathsConfiguration.MediaDirectoryName}/{mediaFileName})"));
        Assert.IsTrue(result.Contains(caption));
    }
    
    [TestMethod]
    public async Task AppendToNotesFileAsync_CreatesFileWithContent()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        var testContent = "Test content";

        try
        {
            // Act
            await _markdownService.AppendToNotesFileAsync(tempFilePath, testContent);

            // Assert
            var fileContent = await File.ReadAllTextAsync(tempFilePath);
            Assert.AreEqual(testContent, fileContent);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [TestMethod]
    public async Task AppendToNotesFileAsync_AppendsToExistingFile()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        var initialContent = "Initial content\n";
        var additionalContent = "Additional content\n";

        try
        {
            await File.WriteAllTextAsync(tempFilePath, initialContent);

            // Act
            await _markdownService.AppendToNotesFileAsync(tempFilePath, additionalContent);

            // Assert
            var fileContent = await File.ReadAllTextAsync(tempFilePath);
            Assert.AreEqual(initialContent + additionalContent, fileContent);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}