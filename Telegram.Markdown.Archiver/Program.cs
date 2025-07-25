using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Markdown.Archiver.Models.Configuration;
using Telegram.Markdown.Archiver.Services;

namespace Telegram.Markdown.Archiver;

internal class Program
{
	static async Task Main(string[] args)
	{
		var host = CreateHostBuilder(args).Build();

		var logger = host.Services.GetRequiredService<ILogger<Program>>();
		var errorLoggingService = host.Services.GetRequiredService<IErrorLoggingService>();
		
		logger.LogInformation("Запуск приложения Telegram Markdown Archiver");

		try
		{
			await host.RunAsync();
		}
		catch (Exception ex)
		{
			logger.LogCritical(ex, "Критическая ошибка при выполнении приложения");
			await errorLoggingService.LogCriticalErrorAsync(ex, "Критическая ошибка при выполнении приложения", "Program.Main");
			throw;
		}
		finally
		{
			logger.LogInformation("Приложение завершено");
		}
	}

	static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.ConfigureAppConfiguration((context, config) =>
			{
				config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
				config.AddEnvironmentVariables();
				config.AddCommandLine(args);
			})
			.ConfigureServices((context, services) =>
			{
				// Конфигурация
				services.Configure<TelegramConfiguration>(
					context.Configuration.GetSection("Telegram"));
				services.Configure<PathsConfiguration>(
					context.Configuration.GetSection("Paths"));
				services.Configure<WhisperConfiguration>(
					context.Configuration.GetSection("Whisper"));
				services.Configure<ErrorLoggingConfiguration>(
					context.Configuration.GetSection("ErrorLogging"));

				// HTTP клиент
				services.AddHttpClient();

				// Сервисы
				services.AddSingleton<IStateService, StateService>();
				services.AddSingleton<IFileSystemService, FileSystemService>();
				services.AddSingleton<IMarkdownService, MarkdownService>();
				services.AddSingleton<IWhisperService, WhisperService>();
				services.AddSingleton<ITelegramService, TelegramService>();
				services.AddSingleton<IErrorLoggingService, ErrorLoggingService>();

				// Фоновый сервис
				services.AddHostedService<TelegramArchiverHostedService>();
			})
			.ConfigureLogging((context, logging) =>
			{
				logging.ClearProviders();
				logging.AddConsole();
				logging.AddConfiguration(context.Configuration.GetSection("Logging"));
			});
}
