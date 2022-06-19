using LarkTG.Source.Details;
using LarkTG.Source.Exceptions;

namespace LarkTG.Source.BotOn;

static partial class BotOnReceived
{
    internal static async Task BotOnGroupMessageReceived(ITelegramBotClient botClient, Message message, MagicBox mb)
    {
        mb.throwIfGroupNull();

        #region group commands (#TODO OUT)
        if (message?.Text?.Contains("/go") ?? false)
        {
            try
            {
                var egs = await mb.EmojiGameSessionController.StartNewSessionAsync(mb);
                await botClient.SendTextMessageAsync(message.Chat.Id,
                                                    GameD.GetPreMessage(egs.SessionState) + $"\n{mb.User.ToString()}",
                                                    replyMarkup: GameD.GetInlineKeyboardMarkup(egs.SessionState));
            }
            catch (GameException e)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, e.Message);
            }
            return;
        }
        else if (message?.Text?.Contains("/stop") ?? false)
        {
            if (await mb.EmojiGameSessionController.UpdateSessionStateAsync(mb, GameD.Constants.SessionState.ForceEnd))
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Game session closed.");
            }
            else
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Not found active sessions!");
            }
            return;
        }
        else if (message?.Text?.Contains("/dices") ?? false)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id,
                                                $"Avaliable Dices:\n{string.Join(", ", DiceD.DiceNames.Select(dn => dn.Key))}.");
            return;
        }
        #endregion

        if (message?.Dice is not null)
        {
            if (mb.ActiveSession is null)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, $"Get {message.Dice.Value} score!", replyToMessageId: message.MessageId);
            }
            else
            {
                await ProcessingActiveSessionLogic(botClient, message, mb);
            }
        }
    }

    private static async Task SendWrongMessage(ITelegramBotClient botClient, Message message, string text, int deleyToDelete = 2000)
    {
        var sendMessage = await botClient.SendTextMessageAsync(message.Chat.Id, text, replyToMessageId: message.MessageId);
        await Task.Run(async () =>
        {
            await Task.Delay(deleyToDelete);
            await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId);
            await botClient.DeleteMessageAsync(sendMessage.Chat.Id, sendMessage.MessageId);
        });
        return;
    }

    private static async Task ProcessingActiveSessionLogic(ITelegramBotClient botClient, Message message, MagicBox mb)
    {
        var activeRound = mb.ActiveSession.Rounds.FirstOrDefault(r => r.Active == true);
        if (activeRound is null)
        {
            //game over!!!
            await botClient.SendTextMessageAsync(message.Chat.Id, $"Something wrong, enter /stop", replyToMessageId: message.MessageId);
            return;
        }

        if (activeRound.DiceMode != message.Dice.Emoji)
        {
            await SendWrongMessage(botClient, message, "Dude, incorrect dice!");
            return;
        }

        try
        {
            if (await mb.EmojiGameSessionController.UserPlayRoundAsyc(mb, activeRound, message.Dice))
            {
                if (activeRound.Played)
                {
                    var nextActiveRound = mb.ActiveSession.Rounds.FirstOrDefault(r => r.Played == false);
                    if (nextActiveRound is null)
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, $"Get {message.Dice.Value} score!", replyToMessageId: message.MessageId);
                        await mb.EmojiGameSessionController.UpdateSessionStateAsync(mb, GameD.Constants.SessionState.End);
                        var bestUser = await mb.EmojiGameSessionController.GetBestUser(mb);
                        string scoreUsers = await mb.EmojiGameSessionController.GetUsersScoreToString(mb);
                        await botClient.SendTextMessageAsync(message.Chat.Id, $"Game end!\nWin: {bestUser.ToString()}\n{scoreUsers}");
                    }
                    else
                    {
                        await mb.EmojiGameSessionController.SetActiveRound(mb, nextActiveRound);
                        var botmsg = await botClient.SendTextMessageAsync(message.Chat.Id, $"Get {message.Dice.Value} score!\n\nNext {nextActiveRound.DiceMode} round!", replyToMessageId: message.MessageId);
                        await botClient.SendDiceAsync(message.Chat.Id, DiceD.GetTelegramDice(nextActiveRound.DiceMode), replyToMessageId: botmsg.MessageId);
                    }
                }else{
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Get {message.Dice.Value} score!", replyToMessageId: message.MessageId);
                }
            }
            else
            {
                await SendWrongMessage(botClient, message, "Dude, you already played thid round!");
            }
        }
        catch (NullReferenceException e)
        {
            await SendWrongMessage(botClient, message, e.Message);
        }
    }


    #region Group CallbackQuery
    internal static async Task BotOnGroupCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery, MagicBox mb)
    {
        mb.throwIfGroupNull();
        await ProcessingActiveStatesCallbackQuery(botClient, callbackQuery, mb);
    }

    private static async Task ProcessingActiveStatesCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, MagicBox mb)
    {
        if (mb.ActiveSession is null || mb.ActiveSession.InGameUsers.Count == 0)
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Game isn't active.\nMessage will been deleted.\nUse `/go` for start new game.", true);
            await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
            return;
        }

        await (mb.ActiveSession.SessionState switch
        {
            GameD.Constants.SessionState.Registration => ProcessingRegistrartionStateCallbackQuery(botClient, callbackQuery, mb),
            GameD.Constants.SessionState.SelectModes => ProcessingSelectModesStateCallbackQuery(botClient, callbackQuery, mb),
            GameD.Constants.SessionState.Active => ProcessingActiveStateCallbackQuery(botClient, callbackQuery, mb)
        });
    }

    private static async Task ProcessingRegistrartionStateCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, MagicBox mb)
    {
        if (callbackQuery.Data == GameD.Constants.GetQuery(GameD.Constants.SessionState.Registration))
        {
            if (await mb.EmojiGameSessionController.SwitchRegistrationUserInSessionAsync(mb))
            {
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "You join to the game!", true);
            }
            else
            {
                if (mb.ActiveSession.InGameUsers.Count == 0)
                {
                    await mb.EmojiGameSessionController.UpdateSessionStateAsync(mb, GameD.Constants.SessionState.ForceEnd);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Game with 0 players not supported.\nMessage will been deleted.\nUse `/go` for start new game.", true);
                    await botClient.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                    return;
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "You left to the game!", true);
                }
            }

            await botClient.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId,
                                                    $"{GameD.GetPreMessage(mb.ActiveSession.SessionState)}\n{string.Join(", ", mb.ActiveSession.InGameUsers)}",
                                                    replyMarkup: GameD.GetInlineKeyboardMarkup(mb.ActiveSession.SessionState));

        }
        else if (callbackQuery.Data == GameD.Constants.GetQuery(GameD.Constants.SessionState.Next))
        {
            if (await mb.EmojiGameSessionController.UpdateSessionStateAsync(mb, GameD.Constants.SessionState.SelectModes))
            {
                await botClient.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId,
                                                    GameD.GetPreMessage(mb.ActiveSession.SessionState),
                                                    replyMarkup: GameD.GetInlineKeyboardMarkup(mb.ActiveSession.SessionState));
            }
        }
    }
    private static async Task ProcessingSelectModesStateCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, MagicBox mb)
    {
        if (callbackQuery.Data == GameD.Constants.GetQuery(GameD.Constants.GameMode.Default))
        {
            await mb.EmojiGameSessionController.StartDefaultGameAsync(mb);
            var activeRound = mb.ActiveSession.Rounds.FirstOrDefault(r => r.Active == true);
            var botmsg = await botClient.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId,
                                                $"Game started!\nPlay in {activeRound.DiceMode}!",
                                                replyMarkup: null);

            await botClient.SendDiceAsync(callbackQuery.Message.Chat.Id, DiceD.GetTelegramDice(activeRound.DiceMode), replyToMessageId: botmsg.MessageId);
        }
    }

    private static async Task ProcessingActiveStateCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, MagicBox mb)
    {

    }
    #endregion
}
