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
		logger.LogInformation("Запуск приложения Telegram Markdown Archiver");

		try
		{
			await host.RunAsync();
		}
		catch (Exception ex)
		{
			logger.LogCritical(ex, "Критическая ошибка при выполнении приложения");
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

				// HTTP клиент
				services.AddHttpClient();

				// Сервисы
				services.AddSingleton<IStateService, StateService>();
				services.AddSingleton<IFileSystemService, FileSystemService>();
				services.AddSingleton<IMarkdownService, MarkdownService>();
				services.AddSingleton<IWhisperService, WhisperService>();
				services.AddSingleton<ITelegramService, TelegramService>();

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
