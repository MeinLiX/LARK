using LarkTG.Source.Validation;

namespace LarkTG.Source.Handlers;

public class CallbackQueryHandler
{
    private readonly IGameSessionService _gameService;
    private readonly GameValidationService _validationService;
    private readonly ILogger<CallbackQueryHandler> _logger;

    public CallbackQueryHandler(IGameSessionService gameService, GameValidationService validationService, ILogger<CallbackQueryHandler> logger)
    {
        _gameService = gameService;
        _validationService = validationService;
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
                "set" => HandleSetConfigValueAsync(botClient, callbackQuery, sessionId, parts),
                "adjust" => HandleAdjustConfigValueAsync(botClient, callbackQuery, sessionId, parts),
                "preset" => HandlePresetConfigAsync(botClient, callbackQuery, sessionId, parts),

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
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidatePlayerJoin(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        var success = await _gameService.JoinSessionAsync(sessionId, callbackQuery.From.Id);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "✅ You joined the game!");
            await UpdateSessionMessage(botClient, callbackQuery, callbackQuery.Message!.Chat.Id);

            if (validation.Warnings.Count != 0)
            {
                await botClient.SendMessage(
                    callbackQuery.Message.Chat.Id,
                    string.Join("\n", validation.Warnings),
                    replyParameters: callbackQuery.Message.MessageId);
            }
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Failed to join game");
        }
    }

    private async Task HandleLeaveCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidatePlayerLeave(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

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
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionStart(session);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        var actionValidation = _validationService.ValidateSessionAction(session, callbackQuery.From.Id, "start");
        if (!actionValidation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, actionValidation.Errors.First());
            return;
        }

        var success = await _gameService.StartSessionAsync(sessionId);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "🎮 Game started!");

            session = await _gameService.GetActiveSessionAsync(callbackQuery.Message.Chat.Id);
            if (session is not null)
            {
                var startMessage = GameMessageBuilder.BuildGameStartMessage(session);
                await botClient.EditMessageText(
                    callbackQuery.Message.Chat.Id,
                    callbackQuery.Message.MessageId,
                    startMessage,
                    parseMode: ParseMode.Markdown);

                await Task.Delay(1000);

                var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);
                if (activeRound is not null)
                {
                    var roundInfo = GameMessageBuilder.BuildRoundInfo(activeRound);

                    var roundMessage = await botClient.SendMessage(
                        callbackQuery.Message.Chat.Id,
                        roundInfo,
                        parseMode: ParseMode.Markdown);

                    await botClient.SendDice(
                        callbackQuery.Message.Chat.Id,
                        DiceHelper.GetTelegramDiceEmoji(activeRound.DiceEmoji),
                        replyParameters: roundMessage.MessageId);
                }
            }

            if (validation.Warnings.Any())
            {
                await Task.Delay(2000);
                await botClient.SendMessage(
                    callbackQuery.Message.Chat.Id,
                    "⚠️ **Warnings:**\n" + string.Join("\n", validation.Warnings),
                    parseMode: ParseMode.Markdown);
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
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionConfiguration(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        var keyboard = CreateConfigurationKeyboard(sessionId);
        var configText = GameMessageBuilder.BuildGameConfigurationSummary(session.Configuration);

        await botClient.EditMessageText(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "⚙️ **Game Configuration**\n\n" + configText + "\n\nChoose what to configure:",
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
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionConfiguration(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        var previousMode = session.Configuration.Mode;
        session.Configuration.Mode = mode;

        // Apply mode-specific defaults
        switch (mode)
        {
            case GameMode.Classic:
                session.Configuration.EnabledDices = DiceHelper.GetAllDiceEmojis();
                session.Configuration.RoundsCount = 1;
                session.Configuration.AllowRerolls = false;
                break;
            case GameMode.Quick:
                session.Configuration.EnabledDices = [];
                session.Configuration.RoundsCount = 1;
                session.Configuration.AllowRerolls = false;
                break;
            case GameMode.Custom:
                // Keep current settings for custom mode
                break;
            case GameMode.Tournament:
                session.Configuration.EnabledDices = DiceHelper.GetAllDiceEmojis();
                session.Configuration.RoundsCount = 2;
                session.Configuration.AllowRerolls = false;
                session.Configuration.MaxPlayers = 16;
                break;
            case GameMode.Survival:
                session.Configuration.EnabledDices = DiceHelper.GetAllDiceEmojis();
                session.Configuration.RoundsCount = 1;
                session.Configuration.AllowRerolls = true;
                session.Configuration.MaxPlayers = 20;
                break;
        }

        var configValidation = _validationService.ValidateGameConfiguration(session.Configuration);

        var success = await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

        if (success)
        {
            var modeChangedText = $"✅ Mode changed to {GetGameModeName(mode)}";
            if (configValidation.Warnings.Any())
            {
                modeChangedText += "\n⚠️ " + string.Join(", ", configValidation.Warnings);
            }

            await botClient.AnswerCallbackQuery(callbackQuery.Id, modeChangedText);
            await UpdateSessionMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
        else
        {
            session.Configuration.Mode = previousMode;
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Failed to change mode");
        }
    }

    private async Task HandleSetConfigValueAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        Guid sessionId, string[] parts)
    {
        if (parts.Length < 4) return;

        var configType = parts[2];
        var value = parts[3];

        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionConfiguration(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        var success = false;
        var feedbackMessage = "";

        switch (configType)
        {
            case "rounds":
                if (int.TryParse(value, out var rounds) && rounds >= 1 && rounds <= 10)
                {
                    session.Configuration.RoundsCount = rounds;
                    success = true;
                    feedbackMessage = $"✅ Rounds set to {rounds}";
                }
                else
                {
                    feedbackMessage = "❌ Invalid rounds count (1-10)";
                }
                break;

            case "players":
                if (int.TryParse(value, out var maxPlayers) && maxPlayers >= 2 && maxPlayers <= 50)
                {
                    session.Configuration.MaxPlayers = maxPlayers;
                    success = true;
                    feedbackMessage = $"✅ Max players set to {maxPlayers}";
                }
                else
                {
                    feedbackMessage = "❌ Invalid player count (2-50)";
                }
                break;

            case "time":
                if (value == "none")
                {
                    session.Configuration.TimeLimit = null;
                    success = true;
                    feedbackMessage = "✅ Time limit removed";
                }
                else if (int.TryParse(value, out var minutes) && minutes >= 5 && minutes <= 1440)
                {
                    session.Configuration.TimeLimit = TimeSpan.FromMinutes(minutes);
                    success = true;
                    feedbackMessage = $"✅ Time limit set to {minutes} minutes";
                }
                else
                {
                    feedbackMessage = "❌ Invalid time (5-1440 min)";
                }
                break;

            case "rerolls":
                if (bool.TryParse(value, out var allowRerolls))
                {
                    session.Configuration.AllowRerolls = allowRerolls;
                    success = true;
                    feedbackMessage = allowRerolls ? "✅ Rerolls enabled" : "✅ Rerolls disabled";
                }
                break;
        }

        if (success)
        {
            var configValidation = _validationService.ValidateGameConfiguration(session.Configuration);
            if (configValidation.IsValid)
            {
                await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

                if (configValidation.Warnings.Any())
                {
                    feedbackMessage += "\n⚠️ " + string.Join(", ", configValidation.Warnings);
                }
            }
            else
            {
                feedbackMessage = "❌ " + string.Join(", ", configValidation.Errors);
                success = false;
            }
        }

        await botClient.AnswerCallbackQuery(callbackQuery.Id, feedbackMessage);

        if (success)
        {
            await UpdateConfigurationMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
    }

    private async Task HandleAdjustConfigValueAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        Guid sessionId, string[] parts)
    {
        if (parts.Length < 4) return;

        var configType = parts[2];
        var adjustment = parts[3];

        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null) return;

        switch (configType)
        {
            case "rounds":
                var currentRounds = session.Configuration.RoundsCount;
                var newRounds = adjustment == "+1" ? currentRounds + 1 : currentRounds - 1;
                if (newRounds >= 1 && newRounds <= 10)
                {
                    await HandleSetConfigValueAsync(botClient, callbackQuery, sessionId,
                        ["adjust", sessionId.ToString(), "rounds", newRounds.ToString()]);
                }
                else
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Cannot adjust further");
                }
                break;

            case "players":
                var currentPlayers = session.Configuration.MaxPlayers;
                var newPlayers = adjustment == "+5" ? currentPlayers + 5 : currentPlayers - 5;
                if (newPlayers >= 2 && newPlayers <= 50)
                {
                    await HandleSetConfigValueAsync(botClient, callbackQuery, sessionId,
                        ["adjust", sessionId.ToString(), "players", newPlayers.ToString()]);
                }
                else
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Cannot adjust further");
                }
                break;
        }
    }

    private async Task HandlePresetConfigAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        Guid sessionId, string[] parts)
    {
        if (parts.Length < 3) return;

        var presetName = parts[2];
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null) return;

        switch (presetName)
        {
            case "casual":
                session.Configuration.Mode = GameMode.Custom;
                session.Configuration.EnabledDices = ["🎲", "🎯", "🏀"];
                session.Configuration.RoundsCount = 1;
                session.Configuration.MaxPlayers = 10;
                session.Configuration.AllowRerolls = false;
                session.Configuration.TimeLimit = TimeSpan.FromMinutes(15);
                break;

            case "competitive":
                session.Configuration.Mode = GameMode.Classic;
                session.Configuration.EnabledDices = DiceHelper.GetAllDiceEmojis();
                session.Configuration.RoundsCount = 2;
                session.Configuration.MaxPlayers = 8;
                session.Configuration.AllowRerolls = false;
                session.Configuration.TimeLimit = TimeSpan.FromMinutes(30);
                break;

            case "quick":
                session.Configuration.Mode = GameMode.Quick;
                session.Configuration.EnabledDices = [];
                session.Configuration.RoundsCount = 1;
                session.Configuration.MaxPlayers = 20;
                session.Configuration.AllowRerolls = false;
                session.Configuration.TimeLimit = TimeSpan.FromMinutes(5);
                break;
        }

        await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);
        await botClient.AnswerCallbackQuery(callbackQuery.Id, $"✅ Applied {presetName} preset");
        await UpdateConfigurationMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
    }

    private async Task HandleDicesConfigCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionConfiguration(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        var keyboard = CreateDiceConfigurationKeyboard(sessionId, session.Configuration.EnabledDices);

        await botClient.EditMessageText(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            "🎲 **Dice Configuration**\n\n" +
            "Select which dice types to use in the game:\n" +
            "✅ - enabled, ❌ - disabled\n" +
            "Numbers in brackets show maximum score\n\n" +
            $"**Currently enabled:** {session.Configuration.EnabledDices.Count} dice types\n" +
            $"**Current mode:** {GetGameModeName(session.Configuration.Mode)}",
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

        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionConfiguration(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        var wasEnabled = session.Configuration.EnabledDices.Contains(diceEmoji);

        if (wasEnabled)
        {
            session.Configuration.EnabledDices.Remove(diceEmoji);
        }
        else
        {
            session.Configuration.EnabledDices.Add(diceEmoji);
        }

        var configValidation = _validationService.ValidateGameConfiguration(session.Configuration);
        var success = configValidation.IsValid && await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

        if (success)
        {
            var diceName = DiceHelper.GetDiceName(diceEmoji);
            var feedbackMessage = wasEnabled ? $"❌ {diceName} disabled" : $"✅ {diceName} enabled";

            if (configValidation.Warnings.Any())
            {
                feedbackMessage += " (⚠️ " + configValidation.Warnings.First() + ")";
            }

            await botClient.AnswerCallbackQuery(callbackQuery.Id, feedbackMessage);
            await UpdateDiceConfigurationMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
        else
        {
            if (wasEnabled)
            {
                session.Configuration.EnabledDices.Add(diceEmoji);
            }
            else
            {
                session.Configuration.EnabledDices.Remove(diceEmoji);
            }

            var errorMessage = configValidation.Errors.FirstOrDefault() ?? "❌ Error toggling dice";
            await botClient.AnswerCallbackQuery(callbackQuery.Id, errorMessage);
        }
    }

    private async Task HandleAllDicesCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionConfiguration(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        session.Configuration.EnabledDices = DiceHelper.GetAllDiceEmojis();
        var success = await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "✅ All dice types enabled");
            await UpdateDiceConfigurationMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Error enabling all dice types");
        }
    }

    private async Task HandleNoDicesCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, Guid sessionId)
    {
        var session = await _gameService.GetActiveSessionAsync(callbackQuery.Message!.Chat.Id);
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionConfiguration(session, callbackQuery.From.Id);
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        if (session.Configuration.Mode == GameMode.Classic || session.Configuration.Mode == GameMode.Tournament)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id,
                "❌ Cannot disable all dice in this mode");
            return;
        }

        session.Configuration.EnabledDices.Clear();
        var success = await _gameService.ConfigureSessionAsync(sessionId, session.Configuration);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ All dice types disabled");
            await UpdateDiceConfigurationMessage(botClient, callbackQuery, callbackQuery.Message.Chat.Id);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Error disabling dice types");
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
        if (session is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game not found");
            return;
        }

        var validation = _validationService.ValidateSessionAction(session, callbackQuery.From.Id, "cancel");
        if (!validation.IsValid)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, validation.Errors.First());
            return;
        }

        var success = await _gameService.CancelSessionAsync(sessionId);

        if (success)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Game cancelled");
            await botClient.EditMessageText(
                callbackQuery.Message.Chat.Id,
                callbackQuery.Message.MessageId,
                "❌ **Game Cancelled**\n\n" +
                "The game has been cancelled by the creator.\n" +
                "Use `/start_game` to create a new game.",
                parseMode: ParseMode.Markdown);
        }
        else
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Failed to cancel game");
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

    private async Task UpdateConfigurationMessage(ITelegramBotClient botClient, CallbackQuery callbackQuery, long chatId)
    {
        var session = await _gameService.GetActiveSessionAsync(chatId);
        if (session is null) return;

        var keyboard = CreateConfigurationKeyboard(session.ID);
        var configText = GameMessageBuilder.BuildGameConfigurationSummary(session.Configuration);

        await botClient.EditMessageText(
            callbackQuery.Message!.Chat.Id,
            callbackQuery.Message.MessageId,
            "⚙️ **Game Configuration**\n\n" + configText + "\n\nChoose what to configure:",
            parseMode: ParseMode.Markdown,
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
                InlineKeyboardButton.WithCallbackData("🎯 Game Mode", $"config_mode_{sessionId}"),
                InlineKeyboardButton.WithCallbackData("🎲 Dice Types", $"dices_{sessionId}")
            ],
            [
                InlineKeyboardButton.WithCallbackData("🔄 Rounds", $"config_rounds_{sessionId}"),
                InlineKeyboardButton.WithCallbackData("👥 Max Players", $"config_players_{sessionId}")
            ],
            [
                InlineKeyboardButton.WithCallbackData("⏱️ Time Limit", $"config_time_{sessionId}"),
                InlineKeyboardButton.WithCallbackData("🔁 Rerolls", $"config_rerolls_{sessionId}")
            ],
            [
                InlineKeyboardButton.WithCallbackData("📋 Presets", $"config_presets_{sessionId}")
            ],
            [
                InlineKeyboardButton.WithCallbackData("◀️ Back to Game", $"back_{sessionId}_main")
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

        // Preset buttons
        buttons.Add(
        [
            InlineKeyboardButton.WithCallbackData("🎯 Core Dice", $"preset_{sessionId}_core"),
            InlineKeyboardButton.WithCallbackData("🎰 All Dice", $"preset_{sessionId}_all")
        ]);

        buttons.Add(
        [
            InlineKeyboardButton.WithCallbackData("◀️ Back", $"back_{sessionId}_config")
        ]);

        return new InlineKeyboardMarkup(buttons);
    }

    private async Task HandleUnknownCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        _logger.LogWarning("Unknown callback query: {Data}", callbackQuery.Data);
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