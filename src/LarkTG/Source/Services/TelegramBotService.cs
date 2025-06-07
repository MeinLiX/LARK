using LarkTG.Source.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;

namespace LarkTG.Source.Services;

public interface ITelegramBotService
{
    Task StartAsync();
    Task StopAsync();
}

public class TelegramBotService : ITelegramBotService
{
    private readonly string _botToken;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBotService> _logger;
    private ITelegramBotClient? _botClient;
    private CancellationTokenSource? _cancellationTokenSource;

    public TelegramBotService(IServiceProvider serviceProvider, ILogger<TelegramBotService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN not found");
    }

    public async Task StartAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _botClient = new TelegramBotClient(_botToken);

        var me = await _botClient.GetMe();
        _logger.LogInformation("Bot started: @{Username}", me.Username);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates =
            [
                UpdateType.Message,
                UpdateType.CallbackQuery,
                UpdateType.MyChatMember
            ]
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            _cancellationTokenSource.Token);

        _logger.LogInformation("Bot is listening for updates...");
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        _logger.LogInformation("Bot stopped");
        await Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var updateHandler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();

        try
        {
            await updateHandler.HandleUpdateAsync(botClient, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error [{apiRequestException.ErrorCode}]: {apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, "Telegram API Error: {ErrorMessage}", errorMessage);
        return Task.CompletedTask;
    }
}