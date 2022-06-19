namespace LarkTG.Source.BotOn;

static partial class BotOnReceived
{
    internal static async Task BotOnPersonMessageReceived(ITelegramBotClient botClient, Message message, MagicBox mb)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, $"Yo dude!\nI`m work only in groups (with permissions)!\nUse `/go` in group.\nPrivat commands not available now.");
        return;
    }

    internal static async Task BotOnPersonCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery, MagicBox mb)
    {
        mb.throwIfGroupNull();
    }
}
