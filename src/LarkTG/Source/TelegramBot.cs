using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using LarkTG.Source.DataBase.Controller;

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
                    UpdateType.CallbackQuery => BotOnCallbackReceived(botClient, update.CallbackQuery),
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
            MagicBox mb = new MagicBox(message);

            await (message.Chat.Type switch
            {
                ChatType.Group => LarkTG.Source.BotOn.BotOnReceived.BotOnGroupMessageReceived(botClient, message, mb),
                ChatType.Supergroup => LarkTG.Source.BotOn.BotOnReceived.BotOnGroupMessageReceived(botClient, message, mb),
                ChatType.Channel => Task.CompletedTask,
                ChatType.Sender => LarkTG.Source.BotOn.BotOnReceived.BotOnPersonMessageReceived(botClient, message, mb),
                ChatType.Private => LarkTG.Source.BotOn.BotOnReceived.BotOnPersonMessageReceived(botClient, message, mb)
            });
        }

        private async Task BotOnCallbackReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            if (callbackQuery.Message is null)
            {
                return;
            }

            MagicBox mb = new MagicBox(callbackQuery);

            await (callbackQuery.Message.Chat.Type switch
            {
                ChatType.Group => LarkTG.Source.BotOn.BotOnReceived.BotOnGroupCallbackQueryReceived(botClient, callbackQuery, mb),
                ChatType.Supergroup => LarkTG.Source.BotOn.BotOnReceived.BotOnGroupCallbackQueryReceived(botClient, callbackQuery, mb),
                ChatType.Channel => Task.CompletedTask,
                ChatType.Sender => LarkTG.Source.BotOn.BotOnReceived.BotOnPersonCallbackQueryReceived(botClient, callbackQuery, mb),
                ChatType.Private => LarkTG.Source.BotOn.BotOnReceived.BotOnPersonCallbackQueryReceived(botClient, callbackQuery, mb)
            });
        }


        private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Console.WriteLine($"Unsupported update type: {update.Type}");
            return Task.CompletedTask;
        }
    }
}
