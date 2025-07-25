using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Markdown.Archiver.Services;

namespace Telegram.Markdown.Archiver.Tests;

[TestClass]
public sealed class MarkdownServiceTests
{
    private MarkdownService _markdownService = null!;
    private Mock<ILogger<MarkdownService>> _loggerMock = null!;
    private Mock<IErrorLoggingService> _errorLoggingServiceMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MarkdownService>>();
        _errorLoggingServiceMock = new Mock<IErrorLoggingService>();
        _markdownService = new MarkdownService(_loggerMock.Object, _errorLoggingServiceMock.Object);
    }

    [TestMethod]
    public void MarkdownService_Constructor_InitializesCorrectly()
    {
        // Assert
        Assert.IsNotNull(_markdownService);
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