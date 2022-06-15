using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace LarkTG.Source;

internal class TelegramBot
{
    private readonly string TELEGRAM_BOT_TOKEN;
    private readonly CancellationToken _ct;
    private TelegramBotClient? Bot;

    public TelegramBot(string? TELEGRAM_BOT_TOKEN, CancellationToken ct)
    {
        this.TELEGRAM_BOT_TOKEN = TELEGRAM_BOT_TOKEN ?? throw new ArgumentNullException();
        _ct = ct;
    }

    public async Task StartAsync()
    {
        Bot = new TelegramBotClient(TELEGRAM_BOT_TOKEN);
        User me = await Bot.GetMeAsync();

        Handler handler = new();
        Bot.StartReceiving(new DefaultUpdateHandler(handler.HandleUpdateAsync, Handler.HandleErrorAsync), cancellationToken: _ct);

        Console.WriteLine($"Start listening for @{me.Username}");
    }

    internal class Handler
    {
        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                await (update.Type switch
                {
                    UpdateType.Message => BotOnMessageReceived(botClient, update.Message),
                    UpdateType.EditedMessage => BotOnMessageReceived(botClient, update.EditedMessage),
                    _ => UnknownUpdateHandlerAsync(botClient, update)
                });
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        private async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
        {
            Console.WriteLine($"Receive message type: {message.Type} (chat type {message.Chat.Type}).");
            await (message.Chat.Type switch
            {
                ChatType.Group => LarkTG.Source.BotOnReceived.BotOnReceived.BotOnGroupMessageReceived(botClient,message),
                ChatType.Supergroup => LarkTG.Source.BotOnReceived.BotOnReceived.BotOnGroupMessageReceived(botClient,message),
                ChatType.Channel => LarkTG.Source.BotOnReceived.BotOnReceived.BotOnGroupMessageReceived(botClient,message),
                ChatType.Sender => LarkTG.Source.BotOnReceived.BotOnReceived.BotOnPersonMessageReceived(botClient,message),
                ChatType.Private => LarkTG.Source.BotOnReceived.BotOnReceived.BotOnPersonMessageReceived(botClient,message)
            });
        }

        private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Console.WriteLine($"Unsupported update type: {update.Type}");
            return Task.CompletedTask;
        }
    }
}