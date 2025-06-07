namespace LarkTG.Source.Handlers;

public class CallbackQueryHandler
{
    private readonly IGameSessionService _gameService;
    private readonly ILogger<CallbackQueryHandler> _logger;

    public CallbackQueryHandler(IGameSessionService gameService, ILogger<CallbackQueryHandler> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    public async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null) return;

        var parts = callbackQuery.Data.Split('_');
        if (parts.Length < 2) return;

        var action = parts[0];
        var sessionIdStr = parts[1];

        if (!Guid.TryParse(sessionIdStr, out var sessionId))
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Invalid session");
            return;
        }

        try
        {
            await (action switch
            {
                "join" => HandleJoinCallbackAsync(botClient, callbackQuery, sessionId),
                "leave" => HandleLeaveCallbackAsync(botClient, callbackQuery, sessionId),
                "config" => HandleConfigCallbackAsync(botClient, callbackQuery, sessionId),
                "start" => HandleStartCallbackAsync(botClient, callbackQuery, sessionId),
                "cancel" => HandleCancelCallbackAsync(botClient, callbackQuery, sessionId),
                "mode" => HandleModeCallbackAsync(botClient, callbackQuery, sessionId, parts),
                "dices" => HandleDicesConfigCallbackAsync(botClient, callbackQuery, sessionId),
                "toggle" => HandleToggleDiceCallbackAsync(botClient, callbackQuery, sessionId, parts),
                "all" => HandleAllDicesCallbackAsync(botClient, callbackQuery, sessionId),
                "no" => HandleNoDicesCallbackAsync(botClient, callbackQuery, sessionId),
                "back" => HandleBackCallbackAsync(botClient, callbackQuery, sessionId, parts),
                _ => HandleUnknownCallbackAsync(botClient, callbackQuery)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback query {Data}", callbackQuery.Data);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ An error occurred");
        }
    }

    private async Task HandleJoinCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var success = await _gameService.JoinSessionAsync(sessionId, callbackQuery.From.Id);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "✅ You joined the game!");
            await UpdateSessionMessage(botClient, callbackQuery, callbackQuery.Message!.Chat.Id);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Failed to join game");
        }
    }

    private async Task HandleLeaveCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var success = await _gameService.LeaveSessionAsync(sessionId, callbackQuery.From.Id);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "👋 You left the game");
            await UpdateSessionMessage(botClient, callbackQuery, callbackQuery.Message!.Chat.Id);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Failed to leave game");
        }
    }

    private async Task HandleStartCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null || session.CreatorId != callbackQuery.From.Id)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Only creator can start the game");
            return;
        }

        if (session.Players.Count < 2)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Need at least 2 players to start");
            return;
        }

        var success = await _gameService.StartSessionAsync(sessionId);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "🎮 Game started!");

            session = await _gameService.GetActiveSessionAsync(callbackQuery.Message.Chat.Id);
            if (session is not null)
            {
                var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);
                if (activeRound is not null)
                {
                    var roundInfo = GameMessageBuilder.BuildRoundInfo(activeRound);

                    await botClient.EditMessageText(
                        callbackQuery.Message.Chat.Id,
                        callbackQuery.Message.MessageId,
                        roundInfo,
                        parseMode: ParseMode.Markdown);

                    await botClient.SendDice(
                        callbackQuery.Message.Chat.Id,
                        DiceHelper.GetTelegramDiceEmoji(activeRound.DiceEmoji));
                }
            }
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Failed to start game");
        }
    }

    private async Task HandleConfigCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null || session.CreatorId != callbackQuery.From.Id)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Only creator can configure");
            return;
        }

        var keyboard = CreateConfigurationKeyboard(sessionId);

        await botClient.EditMessageText(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "⚙️ **Game Settings**\n\nChoose what to configure:",
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard);

        await botClient.AnswerCallbackQuery(callbackQuery.Id);
    }

    private async Task HandleModeCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        Guid sessionId, string[] parts)
    {
        if (parts.Length < 3) return;

        var modeStr = parts[2];
        if (!Enum.TryParse<GameMode>(modeStr, true, out var mode))
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Invalid game mode");
            return;
        }

        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null || session.CreatorId != callbackQuery.From.Id)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Access denied");
            return;
        }

        session.Configuration.Mode = mode;

        switch (mode)
        {
            case GameMode.Classic:
                session.Configuration.EnabledDices = DiceHelper.GetAllDiceEmojis();
                session.Configuration.RoundsCount = 1;
                break;
            case GameMode.Quick:
                session.Configuration.EnabledDices = [];
                session.Configuration.RoundsCount = 1;
                break;
            case GameMode.Custom:
                break;
        }

        var success = await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, $"✅ Mode changed to {GetGameModeName(mode)}");
            await UpdateSessionMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Failed to change mode");
        }
    }

    private async Task HandleDicesConfigCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null || session.CreatorId != callbackQuery.From.Id)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Access denied");
            return;
        }

        var keyboard = CreateDiceConfigurationKeyboard(sessionId, session.Configuration.EnabledDices);

        await botClient.EditMessageText(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "🎲 **Dice Configuration**\n\n" +
            "Select which dices to use in the game:\n" +
            "✅ - enabled, ❌ - disabled\n" +
            "Numbers in brackets show max score",
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard);

        await botClient.AnswerCallbackQuery(callbackQuery.Id);
    }

    private async Task HandleToggleDiceCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        Guid sessionId, string[] parts)
    {
        if (parts.Length < 4) return;

        var diceEmoji = parts[3];
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);

        if (session is null || session.CreatorId != callbackQuery.From.Id)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Access denied");
            return;
        }

        if (session.Configuration.EnabledDices.Contains(diceEmoji))
        {
            session.Configuration.EnabledDices.Remove(diceEmoji);
        }
        else
        {
            session.Configuration.EnabledDices.Add(diceEmoji);
        }

        var success = await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);
            await UpdateDiceConfigurationMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Error");
        }
    }

    private async Task HandleAllDicesCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null || session.CreatorId != callbackQuery.From.Id)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Access denied");
            return;
        }

        session.Configuration.EnabledDices = DiceHelper.GetAllDiceEmojis();
        var success = await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "✅ All dices enabled");
            await UpdateDiceConfigurationMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Error");
        }
    }

    private async Task HandleNoDicesCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null || session.CreatorId != callbackQuery.From.Id)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Access denied");
            return;
        }

        session.Configuration.EnabledDices.Clear();
        var success = await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ All dices disabled");
            await UpdateDiceConfigurationMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Error");
        }
    }

    private async Task HandleBackCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        Guid sessionId, string[] parts)
    {
        if (parts.Length < 3) return;

        var destination = parts[2];

        switch (destination)
        {
            case "main":
                await UpdateSessionMessage(botClient, callbackQuery, callbackQuery.Message!.Chat.Id);
                break;
            case "config":
                await HandleConfigCallbackAsync(botClient, callbackQuery, sessionId);
                break;
        }

        await botClient.AnswerCallbackQuery(callbackQuery.Id);
    }

    private async Task HandleCancelCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null || session.CreatorId != callbackQuery.From.Id)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Only creator can cancel");
            return;
        }

        var success = await _gameService.CancelSessionAsync(sessionId);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game cancelled");
            await botClient.EditMessageText(
                callbackQuery.Message.Chat.Id,
                callbackQuery.Message.MessageId,
                "❌ **Game Cancelled**\n\nThe game has been cancelled by the creator.");
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Failed to cancel");
        }
    }

    private async Task UpdateSessionMessage(ITelegramBotClient botClient, CallbackQuery callbackQuery, long chatId)
    {
        var session = await _gameService.GetActiveSessionAsync(chatId);
        if (session is null) return;

        var messageText = GameMessageBuilder.BuildSessionInfo(session);
        var keyboard = CreateMainSessionKeyboard(session);

        await botClient.EditMessageText(
            callbackQuery.Message!.Chat.Id,
            callbackQuery.Message.MessageId,
            messageText,
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard);
    }

    private async Task UpdateDiceConfigurationMessage(ITelegramBotClient botClient, CallbackQuery callbackQuery, long chatId)
    {
        var session = await _gameService.GetActiveSessionAsync(chatId);
        if (session is null) return;

        var keyboard = CreateDiceConfigurationKeyboard(session.ID, session.Configuration.EnabledDices);

        await botClient.EditMessageReplyMarkup(
            callbackQuery.Message!.Chat.Id,
            callbackQuery.Message.MessageId,
            replyMarkup: keyboard);
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

    private InlineKeyboardMarkup CreateConfigurationKeyboard(Guid sessionId)
    {
        return new InlineKeyboardMarkup(
        [
            [
                InlineKeyboardButton.WithCallbackData("🎲 Classic Mode", $"mode_{sessionId}_classic"),
                InlineKeyboardButton.WithCallbackData("⚡ Quick Mode", $"mode_{sessionId}_quick")
            ],
            [
                InlineKeyboardButton.WithCallbackData("🔧 Configure Dices", $"dices_{sessionId}"),
                InlineKeyboardButton.WithCallbackData("🔧 Custom Mode", $"mode_{sessionId}_custom")
            ],
            [
                InlineKeyboardButton.WithCallbackData("◀️ Back", $"back_{sessionId}_main")
            ]
        ]);
    }

    private InlineKeyboardMarkup CreateDiceConfigurationKeyboard(Guid sessionId, List<string> enabledDices)
    {
        var allDices = DiceHelper.GetAllDiceEmojis();
        var buttons = new List<InlineKeyboardButton[]>();

        for (int i = 0; i < allDices.Count; i += 2)
        {
            var row = new List<InlineKeyboardButton>();

            var dice1 = allDices[i];
            var isEnabled1 = enabledDices.Contains(dice1);
            var maxScore1 = DiceHelper.GetMaxScore(dice1);
            row.Add(InlineKeyboardButton.WithCallbackData(
                $"{dice1} {(isEnabled1 ? "✅" : "❌")} ({maxScore1})",
                $"toggle_{sessionId}_dice_{dice1}"));

            if (i + 1 < allDices.Count)
            {
                var dice2 = allDices[i + 1];
                var isEnabled2 = enabledDices.Contains(dice2);
                var maxScore2 = DiceHelper.GetMaxScore(dice2);
                row.Add(InlineKeyboardButton.WithCallbackData(
                    $"{dice2} {(isEnabled2 ? "✅" : "❌")} ({maxScore2})",
                    $"toggle_{sessionId}_dice_{dice2}"));
            }

            buttons.Add([.. row]);
        }

        buttons.Add(
        [
            InlineKeyboardButton.WithCallbackData("✅ Enable All", $"all_{sessionId}_dices"),
            InlineKeyboardButton.WithCallbackData("❌ Disable All", $"no_{sessionId}_dices")
        ]);

        buttons.Add(
        [
            InlineKeyboardButton.WithCallbackData("◀️ Back", $"back_{sessionId}_config")
        ]);

        return new InlineKeyboardMarkup(buttons);
    }

    private async Task HandleUnknownCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Unknown command");
    }

    private static string GetGameModeName(GameMode mode) => mode switch
    {
        GameMode.Classic => "Classic",
        GameMode.Quick => "Quick",
        GameMode.Custom => "Custom",
        GameMode.Tournament => "Tournament",
        GameMode.Survival => "Survival",
        _ => "Unknown"
    };
}