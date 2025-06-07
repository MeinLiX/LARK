using LarkTG.Source.Validation;

namespace LarkTG.Source.Handlers;

public class GameCommandHandler
{
    private readonly IGameSessionService _gameService;
    private readonly GameValidationService _validationService;
    private readonly ILogger<GameCommandHandler> _logger;

    public GameCommandHandler(IGameSessionService gameService, GameValidationService validationService, ILogger<GameCommandHandler> logger)
    {
        _gameService = gameService;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task HandleStartGameCommand(ITelegramBotClient botClient, Message message,
        long groupId, long userId, string[]? args = default)
    {
        try
        {
            var existingSession = await _gameService.GetActiveSessionAsync(groupId);
            if (existingSession != null)
            {
                var sessionInfo = GameMessageBuilder.BuildSessionInfo(existingSession);
                var kb = CreateMainSessionKeyboard(existingSession);

                await botClient.SendMessage(
                    message.Chat.Id,
                    "🎮 **Game Already Active!**\n\n" + sessionInfo +
                    "\n\nUse the buttons below to join or manage the game, or use `/stop` to end it.",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: kb,
                    replyParameters: message.MessageId);
                return;
            }

            var session = await _gameService.CreateSessionAsync(groupId, userId);

            if (args?.Length > 0)
            {
                ApplyStartGameArguments(session, args);
                await _gameService.ConfigureSessionAsync(session.ID, session.Configuration);
            }

            var keyboard = CreateMainSessionKeyboard(session);
            var messageText = GameMessageBuilder.BuildSessionInfo(session);

            var sentMessage = await botClient.SendMessage(
                message.Chat.Id,
                "🎮 **New Game Created!**\n\n" + messageText +
                "\n\n**Next steps:**\n" +
                "• Other players can join using the 'Join Game' button\n" +
                "• Configure game settings using 'Settings'\n" +
                "• Start when ready with 'Start Game'",
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard);

            _logger.LogInformation("Game created by user {UserId} in group {GroupId}, session {SessionId}",
                userId, groupId, session.ID);

            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                try
                {
                    await botClient.DeleteMessage(message.Chat.Id, message.MessageId);
                }
                catch
                {
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling start game command in group {GroupId} by user {UserId}", groupId, userId);
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ **Error Creating Game**\n\n" +
                "Failed to create the game. This might be due to:\n" +
                "• Database connectivity issues\n" +
                "• Insufficient permissions\n" +
                "• Temporary server problems\n\n" +
                "Please try again in a moment. If the problem persists, contact support.",
                parseMode: ParseMode.Markdown,
                replyParameters: message.MessageId);
        }
    }

    private void ApplyStartGameArguments(GameSession session, string[] args)
    {
        foreach (var arg in args)
        {
            var argLower = arg.ToLower();

            switch (argLower)
            {
                case "quick":
                    session.Configuration.Mode = GameMode.Quick;
                    break;
                case "classic":
                    session.Configuration.Mode = GameMode.Classic;
                    break;
                case "custom":
                    session.Configuration.Mode = GameMode.Custom;
                    break;
                case "tournament":
                    session.Configuration.Mode = GameMode.Tournament;
                    break;
                case "survival":
                    session.Configuration.Mode = GameMode.Survival;
                    break;
                default:
                    if (int.TryParse(arg, out var maxPlayers) && maxPlayers >= 2 && maxPlayers <= 50)
                    {
                        session.Configuration.MaxPlayers = maxPlayers;
                    }
                    break;
            }
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
                    "❌ **No Active Game**\n\n" +
                    "There is no active game to stop in this group.\n" +
                    "Use `/start_game` to create a new game.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
                return;
            }

            var validation = _validationService.ValidateSessionAction(session, userId, "stop");
            if (!validation.IsValid)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    $"❌ **Cannot Stop Game**\n\n{validation.Errors.First()}",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
                return;
            }

            var success = await _gameService.CancelSessionAsync(session.ID);

            if (success)
            {
                var finalMessage = "🛑 **Game Stopped**\n\n" +
                    $"The game has been stopped by {message.From?.FirstName ?? "the creator"}.\n";

                if (session.State == SessionState.Active && session.Scores.Any())
                {
                    var leaderboard = await _gameService.GetLeaderboardAsync(session.ID);
                    if (leaderboard.Any())
                    {
                        finalMessage += "\n📊 **Final Standings:**\n";
                        var topPlayers = leaderboard.Take(3).ToList();
                        for (int i = 0; i < topPlayers.Count; i++)
                        {
                            var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => "🏅" };
                            finalMessage += $"{medal} {topPlayers[i].Player} - {topPlayers[i].TotalScore} pts\n";
                        }
                    }
                }

                finalMessage += "\nThanks for playing! Use `/start_game` to play again.";

                await botClient.SendMessage(
                    message.Chat.Id,
                    finalMessage,
                    parseMode: ParseMode.Markdown);

                _logger.LogInformation("Game stopped by user {UserId} in group {GroupId}, session {SessionId}",
                    userId, groupId, session.ID);
            }
            else
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ **Failed to Stop Game**\n\n" +
                    "An error occurred while stopping the game. Please try again.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling stop game command in group {GroupId} by user {UserId}", groupId, userId);
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ **Error Stopping Game**\n\n" +
                "An unexpected error occurred. Please try again.",
                parseMode: ParseMode.Markdown,
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
                    "❌ **No Active Game**\n\n" +
                    "There is no active game to join in this group.\n" +
                    "Use `/start_game` to create a new game.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
                return;
            }

            var validation = _validationService.ValidatePlayerJoin(session, userId);
            if (!validation.IsValid)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    $"❌ **Cannot Join Game**\n\n{validation.Errors.First()}",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
                return;
            }

            var success = await _gameService.JoinSessionAsync(session.ID, userId);

            if (success)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    $"✅ **{message.From?.FirstName ?? "Player"} Joined!**\n\n" +
                    $"You successfully joined the game!\n" +
                    $"Players in game: **{session.Players.Count + 1}/{session.Configuration.MaxPlayers}**\n\n" +
                    "Wait for the game creator to start the game.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);

                _logger.LogInformation("User {UserId} joined game session {SessionId} in group {GroupId}",
                    userId, session.ID, groupId);
            }
            else
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ **Failed to Join**\n\n" +
                    "Unable to join the game. You might already be in it, or the game might be full.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling join command in group {GroupId} by user {UserId}", groupId, userId);
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ **Error Joining Game**\n\n" +
                "An error occurred while joining. Please try again.",
                parseMode: ParseMode.Markdown,
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
                    "❌ **No Active Game**\n\n" +
                    "There is no active game to leave.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
                return;
            }

            var validation = _validationService.ValidatePlayerLeave(session, userId);
            if (!validation.IsValid)
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    $"❌ **Cannot Leave Game**\n\n{validation.Errors.First()}",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
                return;
            }

            var success = await _gameService.LeaveSessionAsync(session.ID, userId);

            if (success)
            {
                var responseMessage = $"👋 **{message.From?.FirstName ?? "Player"} Left**\n\n" +
                    "You have successfully left the game.";

                var updatedSession = await _gameService.GetActiveSessionAsync(groupId);
                if (updatedSession?.CreatorId != userId && updatedSession?.Creator != null)
                {
                    responseMessage += $"\n\n👑 **New game creator:** {updatedSession.Creator.FirstName}";
                }

                await botClient.SendMessage(
                    message.Chat.Id,
                    responseMessage,
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);

                _logger.LogInformation("User {UserId} left game session {SessionId} in group {GroupId}",
                    userId, session.ID, groupId);
            }
            else
            {
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ **Failed to Leave**\n\n" +
                    "Unable to leave the game. Please try again.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling leave command in group {GroupId} by user {UserId}", groupId, userId);
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ **Error Leaving Game**\n\n" +
                "An error occurred while leaving. Please try again.",
                parseMode: ParseMode.Markdown,
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
                    "❌ **No Active Game**\n\n" +
                    "There is no active game in this group.\n" +
                    "Use `/start_game` to create a new game.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
                return;
            }

            string messageText;
            InlineKeyboardMarkup? keyboard = null;

            switch (session.State)
            {
                case SessionState.Registration:
                    messageText = "📝 **Game Registration**\n\n" + GameMessageBuilder.BuildSessionInfo(session);
                    keyboard = CreateMainSessionKeyboard(session);
                    break;

                case SessionState.Active:
                    var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);
                    if (activeRound is not null)
                    {
                        messageText = "🎮 **Game In Progress**\n\n" + GameMessageBuilder.BuildRoundInfo(activeRound);

                        var completedRounds = session.Rounds.Count(r => r.IsCompleted);
                        var totalRounds = session.Rounds.Count;
                        messageText += $"\n\n📊 **Overall Progress:** {completedRounds}/{totalRounds} rounds completed";

                        if (session.Scores.Any())
                        {
                            var leaderboard = session.Scores.OrderByDescending(s => s.TotalScore).Take(3).ToList();
                            messageText += "\n\n🏆 **Current Leaders:**";
                            for (int i = 0; i < leaderboard.Count; i++)
                            {
                                var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => "🏅" };
                                messageText += $"\n{medal} {leaderboard[i].Player} - {leaderboard[i].TotalScore} pts";
                            }
                        }
                    }
                    else
                    {
                        messageText = "🎮 **Game Active**\n\n" + GameMessageBuilder.BuildSessionInfo(session);
                    }
                    break;

                case SessionState.Finished:
                    messageText = "🏁 **Game Finished**\n\n";
                    var finalLeaderboard = await _gameService.GetLeaderboardAsync(session.ID);
                    if (finalLeaderboard.Any())
                    {
                        messageText += GameMessageBuilder.BuildLeaderboard(finalLeaderboard);
                    }
                    break;

                default:
                    messageText = GameMessageBuilder.BuildSessionInfo(session);
                    break;
            }

            await botClient.SendMessage(
                message.Chat.Id,
                messageText,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                replyParameters: message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling game info command in group {GroupId}", groupId);
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ **Error Getting Game Info**\n\n" +
                "Unable to retrieve game information. Please try again.",
                parseMode: ParseMode.Markdown,
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
            if (activeRound is null) return;

            var playerResult = activeRound.Results.FirstOrDefault(r => r.PlayerId == message.From!.Id);
            var scoreValue = message.Dice!.Value;
            var scoreDisplay = DiceHelper.GetScoreDisplay(activeRound.DiceEmoji, scoreValue);

            if (activeRound.Results.All(r => r.HasPlayed))
            {
                var roundResultMessage = $"🎯 **You scored {scoreValue}!** {scoreDisplay}\n\n";

                var sortedResults = activeRound.Results
                    .OrderByDescending(r => r.Score)
                    .ToList();

                var maxScore = sortedResults.First().Score!.Value;
                var winners = sortedResults.Where(r => r.Score == maxScore).ToList();

                if (winners.Count == 1)
                {
                    roundResultMessage += $"🏆 **Round {activeRound.RoundNumber} Winner:** {winners.First().Player}\n";
                }
                else
                {
                    roundResultMessage += $"🤝 **Round {activeRound.RoundNumber} Tie:** {winners.Count} players tied!\n";
                }

                roundResultMessage += "\n📋 **Round Results:**\n";
                for (int i = 0; i < Math.Min(sortedResults.Count, 5); i++)
                {
                    var result = sortedResults[i];
                    var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => "  " };
                    var isWinner = winners.Contains(result) ? " 🏆" : "";
                    roundResultMessage += $"{medal} {result.Player} - **{result.Score}**{isWinner}\n";
                }

                if (sortedResults.Count > 5)
                {
                    roundResultMessage += $"... and {sortedResults.Count - 5} more players\n";
                }

                await botClient.SendMessage(
                    message.Chat.Id,
                    roundResultMessage,
                    parseMode: ParseMode.Markdown);

                var nextRound = session.Rounds.FirstOrDefault(r => r.IsActive && r.ID != activeRound.ID);
                if (nextRound is not null)
                {
                    await Task.Delay(3000);

                    var nextRoundInfo = GameMessageBuilder.BuildRoundInfo(nextRound);
                    var nextRoundMessage = await botClient.SendMessage(
                        message.Chat.Id,
                        "🎲 **Next Round Starting!**\n\n" + nextRoundInfo,
                        parseMode: ParseMode.Markdown);

                    await Task.Delay(1000);

                    await botClient.SendDice(
                        message.Chat.Id,
                        DiceHelper.GetTelegramDiceEmoji(nextRound.DiceEmoji),
                        replyParameters: nextRoundMessage.MessageId);
                }
                else if (session.State == SessionState.Finished)
                {
                    await Task.Delay(3000);

                    var leaderboard = await _gameService.GetLeaderboardAsync(session.ID);
                    var finalResults = GameMessageBuilder.BuildLeaderboard(leaderboard);

                    await botClient.SendMessage(
                        message.Chat.Id,
                        "🏁 **GAME OVER!**\n\n" + finalResults,
                        parseMode: ParseMode.Markdown);

                    _logger.LogInformation("Game completed in group {GroupId}, session {SessionId}",
                        message.Chat.Id, session.ID);
                }
            }
            else
            {
                var waitingCount = activeRound.Results.Count(r => !r.HasPlayed);
                var personalMessage = $"🎯 **You scored {scoreValue}!** {scoreDisplay}\n\n" +
                    $"⏳ Waiting for {waitingCount} more player(s) to play...";

                await botClient.SendMessage(
                    message.Chat.Id,
                    personalMessage,
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling successful play for user {UserId} in group {GroupId}",
                message.From?.Id, message.Chat.Id);
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
                    "❌ **No Active Round**\n\n" +
                    "There is currently no active round to play.\n" +
                    "Wait for the game to start or the next round to begin.",
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);
                return;
            }

            var validation = _validationService.ValidateDicePlay(session, activeRound, message.From!.Id, message.Dice!.Emoji);

            if (!validation.IsValid)
            {
                var errorMessage = $"❌ **Invalid Play**\n\n{validation.Errors.First()}";

                if (activeRound.DiceEmoji != message.Dice.Emoji)
                {
                    errorMessage += $"\n\n🎯 **Current round:** {activeRound.DiceEmoji} {DiceHelper.GetDiceName(activeRound.DiceEmoji)}";
                    errorMessage += $"\n📱 **Your dice:** {message.Dice.Emoji}";
                }

                var errorMsg = await botClient.SendMessage(
                    message.Chat.Id,
                    errorMessage,
                    parseMode: ParseMode.Markdown,
                    replyParameters: message.MessageId);

                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    try
                    {
                        await botClient.DeleteMessage(message.Chat.Id, errorMsg.MessageId);
                        await botClient.DeleteMessage(message.Chat.Id, message.MessageId);
                    }
                    catch
                    {
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling failed play for user {UserId} in group {GroupId}",
                message.From?.Id, message.Chat.Id);
        }
    }

    private InlineKeyboardMarkup CreateMainSessionKeyboard(GameSession session)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        if (session.State == SessionState.Registration)
        {
            buttons.Add(
            [
                InlineKeyboardButton.WithCallbackData("🎯 Join Game", $"join_{session.ID}"),
                InlineKeyboardButton.WithCallbackData("🚪 Leave Game", $"leave_{session.ID}")
            ]);

            buttons.Add(
            [
                InlineKeyboardButton.WithCallbackData("⚙️ Settings", $"config_{session.ID}"),
                InlineKeyboardButton.WithCallbackData("▶️ Start Game", $"start_{session.ID}")
            ]);

            buttons.Add(
            [
                InlineKeyboardButton.WithCallbackData("❌ Cancel", $"cancel_{session.ID}")
            ]);
        }

        return new InlineKeyboardMarkup(buttons);
    }
}