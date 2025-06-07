namespace LarkTG.Source.Handlers;

public class GameCommandHandler
{
    private readonly IGameSessionService _gameService;
    private readonly ILogger<GameCommandHandler> _logger;

    public GameCommandHandler(IGameSessionService gameService, ILogger<GameCommandHandler> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    public async Task HandleStartGameCommand(ITelegramBotClient botClient, Message message,
        long groupId, long userId)
    {
        try
        {
            var existingSession = await _gameService.GetActiveSessionAsync(groupId);
            if (existingSession != null)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "🎮 Game is already active! Use /join to join or /stop to end current game.",
                    replyParameters: message.MessageId);
                return;
            }

            var session = await _gameService.CreateSessionAsync(groupId, userId);

            var keyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("🎯 Join Game", $"join_{session.ID}"),
                    InlineKeyboardButton.WithCallbackData("🚪 Leave Game", $"leave_{session.ID}")
                ],
                [
                    InlineKeyboardButton.WithCallbackData("⚙️ Settings", $"config_{session.ID}"),
                    InlineKeyboardButton.WithCallbackData("▶️ Start Game", $"start_{session.ID}")
                ],
                [
                    InlineKeyboardButton.WithCallbackData("❌ Cancel", $"cancel_{session.ID}")
                ]
            ]);

            var messageText = GameMessageBuilder.BuildSessionInfo(session);

            await botClient.SendMessage(
                message.Chat.Id,
                messageText,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling start game command");
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Error creating game. Please try again.",
                replyParameters: message.MessageId);
        }
    }

    public async Task HandleStopGameCommand(ITelegramBotClient botClient, Message message,
        long groupId, long userId)
    {
        try
        {
            var session = await _gameService.GetActiveSessionAsync(groupId);
            if (session is null)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ No active game to stop.",
                    replyParameters: message.MessageId);
                return;
            }

            if (session.Creator.ID != userId)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ Only the game creator can stop the game.",
                    replyParameters: message.MessageId);
                return;
            }

            var success = await _gameService.CancelSessionAsync(session.ID);

            if (success)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "🛑 Game has been stopped.",
                    replyParameters: message.MessageId);
            }
            else
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ Failed to stop the game.",
                    replyParameters: message.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling stop game command");
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Error stopping game.",
                replyParameters: message.MessageId);
        }
    }

    public async Task HandleJoinCommand(ITelegramBotClient botClient, Message message,
        long groupId, long userId)
    {
        try
        {
            var session = await _gameService.GetActiveSessionAsync(groupId);
            if (session is null)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ No active game to join. Use /start_game to create one.",
                    replyParameters: message.MessageId);
                return;
            }

            if (session.State != SessionState.Registration)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ Cannot join game at this stage.",
                    replyParameters: message.MessageId);
                return;
            }

            var success = await _gameService.JoinSessionAsync(session.ID, userId);

            if (success)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "✅ You joined the game!",
                    replyParameters: message.MessageId);
            }
            else
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ Failed to join the game. You might already be in it or it's full.",
                    replyParameters: message.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling join command");
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Error joining game.",
                replyParameters: message.MessageId);
        }
    }

    public async Task HandleLeaveCommand(ITelegramBotClient botClient, Message message,
        long groupId, long userId)
    {
        try
        {
            var session = await _gameService.GetActiveSessionAsync(groupId);
            if (session is null)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ No active game to leave.",
                    replyParameters: message.MessageId);
                return;
            }

            if (session.State != SessionState.Registration)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ Cannot leave game at this stage.",
                    replyParameters: message.MessageId);
                return;
            }

            var success = await _gameService.LeaveSessionAsync(session.ID, userId);

            if (success)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "👋 You left the game.",
                    replyParameters: message.MessageId);
            }
            else
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ Failed to leave the game.",
                    replyParameters: message.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling leave command");
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Error leaving game.",
                replyParameters: message.MessageId);
        }
    }

    public async Task HandleGameInfoCommand(ITelegramBotClient botClient, Message message, long groupId)
    {
        try
        {
            var session = await _gameService.GetActiveSessionAsync(groupId);
            if (session is null)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ No active game.",
                    replyParameters: message.MessageId);
                return;
            }

            string messageText;

            if (session.State == SessionState.Active)
            {
                var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);
                if (activeRound is not null)
                {
                    messageText = GameMessageBuilder.BuildRoundInfo(activeRound);
                }
                else
                {
                    messageText = GameMessageBuilder.BuildSessionInfo(session);
                }
            }
            else
            {
                messageText = GameMessageBuilder.BuildSessionInfo(session);
            }

            await botClient.SendMessage(
                message.Chat.Id,
                messageText,
                parseMode: ParseMode.Markdown,
                replyParameters: message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling game info command");
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Error getting game information.",
                replyParameters: message.MessageId);
        }
    }

    public async Task HandleSuccessfulPlay(ITelegramBotClient botClient, Message message, GameSession? session)
    {
        try
        {
            session = await _gameService.GetActiveSessionAsync(message.Chat.Id);
            if (session is null) return;

            var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);

            if (activeRound is not null)
            {
                if (activeRound.Results.All(r => r.HasPlayed))
                {
                    var roundInfo = GameMessageBuilder.BuildRoundInfo(activeRound);

                    await botClient.SendMessage(
                        message.Chat.Id,
                        $"🎯 You scored {message.Dice!.Value}!\n\n" + roundInfo,
                        parseMode: ParseMode.Markdown);

                    var nextRound = session.Rounds.FirstOrDefault(r => r.IsActive && r.ID != activeRound.ID);
                    if (nextRound is not null)
                    {
                        await Task.Delay(2000);

                        var nextRoundInfo = GameMessageBuilder.BuildRoundInfo(nextRound);
                        var nextRoundMessage = await botClient.SendMessage(
                            message.Chat.Id,
                            nextRoundInfo,
                            parseMode: ParseMode.Markdown);

                        await botClient.SendDice(
                            message.Chat.Id,
                            DiceHelper.GetTelegramDiceEmoji(nextRound.DiceEmoji),
                            replyParameters: nextRoundMessage.MessageId);
                    }
                    else if (session.State == SessionState.Finished)
                    {
                        await Task.Delay(2000);
                        var leaderboard = await _gameService.GetLeaderboardAsync(session.ID);
                        var finalResults = GameMessageBuilder.BuildLeaderboard(leaderboard);

                        await botClient.SendMessage(
                            message.Chat.Id,
                            finalResults,
                            parseMode: ParseMode.Markdown);
                    }
                }
                else
                {
                    await botClient.SendMessage(
                        message.Chat.Id,
                        $"🎯 You scored {message.Dice!.Value}! {DiceHelper.GetScoreDisplay(activeRound.DiceEmoji, message.Dice.Value)}",
                        replyParameters: message.MessageId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling successful play");
        }
    }

    public async Task HandleFailedPlay(ITelegramBotClient botClient, Message message, GameSession session)
    {
        try
        {
            var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);

            if (activeRound is null)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ No active round.",
                    replyParameters: message.MessageId);
                return;
            }

            if (activeRound.DiceEmoji != message.Dice!.Emoji)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    $"❌ Wrong dice! Current round: {activeRound.DiceEmoji} {DiceHelper.GetDiceName(activeRound.DiceEmoji)}",
                    replyParameters: message.MessageId);

                await Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    try
                    {
                        await botClient.DeleteMessage(message.Chat.Id, message.MessageId);
                    }
                    catch { }
                });
                return;
            }

            var playerResult = activeRound.Results.FirstOrDefault(r => r.PlayerId == message.From!.Id);
            if (playerResult?.HasPlayed == true)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ You already played this round!",
                    replyParameters: message.MessageId);

                await Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    try
                    {
                        await botClient.DeleteMessage(message.Chat.Id, message.MessageId);
                    }
                    catch { }
                });
                return;
            }

            if (playerResult is null)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ You're not registered in this game!",
                    replyParameters: message.MessageId);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling failed play");
        }
    }
}