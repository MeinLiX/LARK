using LarkTG.Source.Details;
namespace LarkTG.Source.BotOnReceived;

static partial class BotOnReceived
{
    internal static async Task BotOnPersonMessageReceived(ITelegramBotClient botClient, Message message)
    {
        //test
        await LarkTG.Source.DataBase.Controller.DBController.TelegramEntitiesController.Trigger();
        return;
        if (message.Dice?.Emoji is not null)
            await botClient.SendTextMessageAsync(message.Chat.Id, $"Your {message.Dice?.Emoji} score: {message.Dice?.Value} (max {DiceD.GetName(message.Dice.Emoji)} {DiceD.GetBestValue(message.Dice.Emoji)})");
        else await botClient.SendTextMessageAsync(message.Chat.Id, $"Yo dude!");

    }
}