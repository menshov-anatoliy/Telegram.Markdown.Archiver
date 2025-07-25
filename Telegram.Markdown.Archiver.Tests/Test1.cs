using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Markdown.Archiver.Models.Configuration;
using Telegram.Markdown.Archiver.Services;

namespace Telegram.Markdown.Archiver.Tests;

[TestClass]
public sealed class FileSystemServiceTests
{
	private FileSystemService _fileSystemService = null!;
	private Mock<ILogger<FileSystemService>> _loggerMock = null!;
	private Mock<IErrorLoggingService> _errorLoggingServiceMock = null!;
	private PathsConfiguration _pathsConfiguration = null!;

	[TestInitialize]
	public void Setup()
	{
		_loggerMock = new Mock<ILogger<FileSystemService>>();
		_errorLoggingServiceMock = new Mock<IErrorLoggingService>();
		_pathsConfiguration = new PathsConfiguration
		{
			NotesRoot = "/tmp/test_notes",
			MediaDirectoryName = "media",
			StateFile = "/tmp/test_state.json"
		};

		var optionsMock = new Mock<IOptions<PathsConfiguration>>();
		optionsMock.Setup(x => x.Value).Returns(_pathsConfiguration);

		_fileSystemService = new FileSystemService(optionsMock.Object, _loggerMock.Object, _errorLoggingServiceMock.Object);
	}

	[TestMethod]
	public void GetNotesFilePath_ReturnsCorrectFormat()
	{
		// Arrange
		var testDate = new DateTime(2023, 12, 25);

		// Act
		var result = _fileSystemService.GetNotesFilePath(testDate);

		// Assert
		var expected = Path.Combine(_pathsConfiguration.NotesRoot, "2023-12-25_Notes.md");
		Assert.AreEqual(expected, result);
	}

	[TestMethod]
	public void GetMediaDirectoryPath_ReturnsCorrectPath()
	{
		// Act
		var result = _fileSystemService.GetMediaDirectoryPath();

		// Assert
		var expected = Path.Combine(_pathsConfiguration.NotesRoot, _pathsConfiguration.MediaDirectoryName);
		Assert.AreEqual(expected, result);
	}

	[TestMethod]
	public void GetUniqueMediaFileName_WhenFileNotExists_ReturnsOriginalName()
	{
		// Arrange
		var fileName = "test_unique_file.jpg";

		// Act
		var result = _fileSystemService.GetUniqueMediaFileName(fileName);

		// Assert
		Assert.AreEqual(fileName, result);
	}

	[TestCleanup]
	public void Cleanup()
	{
		// Очистить временные файлы
		if (Directory.Exists(_pathsConfiguration.NotesRoot))
		{
			Directory.Delete(_pathsConfiguration.NotesRoot, true);
		}
	}
}
