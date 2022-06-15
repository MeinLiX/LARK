namespace LarkTG.Source.BotOnReceived;

static partial class BotOnReceived
{
    internal static async Task BotOnPersonMessageReceived(ITelegramBotClient botClient, Message message)
    {
        //test
        if(message.Dice?.Emoji is not null)
            await botClient.SendTextMessageAsync(message.Chat.Id, $"Your {message.Dice?.Emoji} score: {message.Dice?.Value} (max {LarkTG.Source.DiceDetails.GetName(message.Dice.Emoji)} {LarkTG.Source.DiceDetails.GetBestValue(message.Dice.Emoji)})");
        else await botClient.SendTextMessageAsync(message.Chat.Id, $"Yo dude!");
    }
}