namespace LarkTG.Source.BotOnReceived;

static partial class BotOnReceived
{
    internal static async Task BotOnGroupMessageReceived(ITelegramBotClient botClient, Message message)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, $"qq `{message.Chat.Title}` group");
    }
}