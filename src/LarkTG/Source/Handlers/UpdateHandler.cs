namespace LarkTG.Source.Handlers;

public class UpdateHandler
{
    private readonly IUserService _userService;
    private readonly IGroupService _groupService;
    private readonly IGameSessionService _gameService;
    private readonly GameCommandHandler _gameCommandHandler;
    private readonly CallbackQueryHandler _callbackHandler;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        IUserService userService,
        IGroupService groupService,
        IGameSessionService gameService,
        GameCommandHandler gameCommandHandler,
        CallbackQueryHandler callbackHandler,
        ILogger<UpdateHandler> logger)
    {
        _userService = userService;
        _groupService = groupService;
        _gameService = gameService;
        _gameCommandHandler = gameCommandHandler;
        _callbackHandler = callbackHandler;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        await EnsureUserRegistrationAsync(update);

        var handler = update.Type switch
        {
            UpdateType.Message => HandleMessageAsync(botClient, update.Message!),
            UpdateType.CallbackQuery => HandleCallbackQueryAsync(botClient, update.CallbackQuery!),
            UpdateType.MyChatMember => HandleMyChatMemberAsync(botClient, update.MyChatMember!),
            _ => HandleUnknownUpdateAsync(update)
        };

        await handler;
    }

    private async Task<TUser?> EnsureUserRegistrationAsync(Update update)
    {
        User? telegramUser = update.Type switch
        {
            UpdateType.Message => update.Message?.From,
            UpdateType.CallbackQuery => update.CallbackQuery?.From,
            UpdateType.MyChatMember => update.MyChatMember?.From,
            UpdateType.ChatMember => update.ChatMember?.From,
            UpdateType.InlineQuery => update.InlineQuery?.From,
            UpdateType.ChosenInlineResult => update.ChosenInlineResult?.From,
            UpdateType.ChannelPost => update.ChannelPost?.From,
            UpdateType.EditedChannelPost => update.EditedChannelPost?.From,
            UpdateType.EditedMessage => update.EditedMessage?.From,
            _ => null
        };

        if (telegramUser == null) return null;

        try
        {
            var user = await _userService.GetOrCreateUserAsync(telegramUser);
            if (user == null)
            {
                _logger.LogWarning("Failed to register user {UserId} ({FirstName})",
                    telegramUser.Id, telegramUser.FirstName);
            }
            else
            {
                _logger.LogDebug("User {UserId} ({FirstName}) registered/updated",
                    user.ID, user.FirstName);
            }
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user {UserId}", telegramUser.Id);
            return null;
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message)
    {
        if (message.From is null) return;

        var user = await _userService.GetUserAsync(message.From.Id);
        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found after registration attempt", message.From.Id);
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Failed to register user. Please try again.",
                replyParameters: message.MessageId);
            return;
        }

        if (message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup)
        {
            var group = await _groupService.GetOrCreateGroupAsync(message.Chat);
            if (group is null)
            {
                _logger.LogWarning("Failed to create group {ChatId}", message.Chat.Id);
                await botClient.SendMessage(
                    message.Chat.Id,
                    "❌ Failed to register group. Please try again.",
                    replyParameters: message.MessageId);
                return;
            }

            await HandleGroupMessageAsync(botClient, message, group.ID, user.ID);
        }
        else
        {
            await HandlePrivateMessageAsync(botClient, message, user.ID);
        }
    }

    private async Task HandleGroupMessageAsync(ITelegramBotClient botClient, Message message,
        long groupId, long userId)
    {
        if (message.Text?.StartsWith('/') == true)
        {
            var commandParts = message.Text.Split(' ');
            var command = commandParts[0].ToLower();
            var args = commandParts.Length > 1 ? commandParts[1..] : Array.Empty<string>();

            switch (command)
            {
                case "/start_game" or "/go":
                    await _gameCommandHandler.HandleStartGameCommand(botClient, message, groupId, userId, args);
                    break;

                case "/stop_game" or "/stop":
                    await _gameCommandHandler.HandleStopGameCommand(botClient, message, groupId, userId);
                    break;

                case "/join":
                    await _gameCommandHandler.HandleJoinCommand(botClient, message, groupId, userId);
                    break;

                case "/leave":
                    await _gameCommandHandler.HandleLeaveCommand(botClient, message, groupId, userId);
                    break;

                case "/game_info" or "/info":
                    await _gameCommandHandler.HandleGameInfoCommand(botClient, message, groupId);
                    break;

                case "/help":
                    await HandleHelpCommand(botClient, message);
                    break;

                case "/dices":
                    await HandleDicesCommand(botClient, message);
                    break;

                case "/settings" or "/config":
                    await HandleSettingsCommand(botClient, message, groupId, userId);
                    break;

                default:
                    if (command.StartsWith('/'))
                    {
                        await botClient.SendMessage(
                            message.Chat.Id,
                            "❓ Unknown command. Use /help to see available commands.",
                            replyParameters: message.MessageId);
                    }
                    break;
            }
        }
        else if (message.Dice is not null)
        {
            await HandleDiceMessageAsync(botClient, message, groupId, userId);
        }
    }

    private async Task HandleDiceMessageAsync(ITelegramBotClient botClient, Message message,
        long groupId, long userId)
    {
        var session = await _gameService.GetActiveSessionAsync(groupId);
        if (session is null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "🎮 No active game! Use /start_game to begin a new game.",
                replyParameters: message.MessageId);
            return;
        }

        if (session.State != SessionState.Active)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "⏳ Game hasn't started yet or has finished.",
                replyParameters: message.MessageId);
            return;
        }

        var success = message.Dice is not null ? await _gameService.PlayRoundAsync(
            session.ID, userId, message.Dice.Value, message.Dice.Emoji) : false;

        if (success)
        {
            await _gameCommandHandler.HandleSuccessfulPlay(botClient, message, session);
        }
        else
        {
            await _gameCommandHandler.HandleFailedPlay(botClient, message, session);
        }
    }

    private async Task HandleSettingsCommand(ITelegramBotClient botClient, Message message, long groupId, long userId)
    {
        var session = await _gameService.GetActiveSessionAsync(groupId);
        if (session is null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ No active game. Use /start_game to create one.",
                replyParameters: message.MessageId);
            return;
        }

        if (session.CreatorId != userId)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Only the game creator can access settings.",
                replyParameters: message.MessageId);
            return;
        }

        if (session.State != SessionState.Registration)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Settings can only be changed during registration phase.",
                replyParameters: message.MessageId);
            return;
        }

        var keyboard = CreateAdvancedConfigurationKeyboard(session.ID);
        var messageText = GameMessageBuilder.BuildGameConfigurationSummary(session.Configuration);

        await botClient.SendMessage(
            message.Chat.Id,
            messageText,
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard,
            replyParameters: message.MessageId);
    }

    private async Task HandlePrivateMessageAsync(ITelegramBotClient botClient, Message message, long userId)
    {
        if (message.Text?.StartsWith('/') == true)
        {
            var command = message.Text.Split(' ')[0].ToLower();

            switch (command)
            {
                case "/start":
                    await HandleStartCommand(botClient, message);
                    break;
                case "/help":
                    await HandleHelpCommand(botClient, message);
                    break;
                case "/dices":
                    await HandleDicesCommand(botClient, message);
                    break;
                default:
                    await HandleStartCommand(botClient, message);
                    break;
            }
        }
        else
        {
            await HandleStartCommand(botClient, message);
        }
    }

    private async Task HandleStartCommand(ITelegramBotClient botClient, Message message)
    {
        await botClient.SendMessage(
            message.Chat.Id,
            "🎮 **Welcome to Dice Game Bot!**\n\n" +
            "I help organize dice games in group chats.\n\n" +
            "**How to get started:**\n" +
            "1. Add me to your group chat\n" +
            "2. Use `/start_game` to create a new game\n" +
            "3. Configure game settings\n" +
            "4. Invite friends to join\n" +
            "5. Start playing!\n\n" +
            "**Available commands:**\n" +
            "🎮 `/start_game` - Start new game\n" +
            "🛑 `/stop_game` - Stop current game\n" +
            "🎯 `/join` - Join game\n" +
            "🚪 `/leave` - Leave game\n" +
            "ℹ️ `/info` - Game information\n" +
            "⚙️ `/settings` - Game settings (creator only)\n" +
            "🎲 `/dices` - Available dices\n" +
            "❓ `/help` - Help\n\n" +
            "Add me to a group to start playing!",
            parseMode: ParseMode.Markdown);
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        if (callbackQuery.From is null || callbackQuery.Data is null) return;

        var user = await _userService.GetUserAsync(callbackQuery.From.Id);
        if (user is null)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ User registration failed");
            return;
        }

        await _callbackHandler.HandleCallbackQueryAsync(botClient, callbackQuery);
    }

    private async Task HandleMyChatMemberAsync(ITelegramBotClient botClient, ChatMemberUpdated myChatMember)
    {
        if (myChatMember.NewChatMember.Status == ChatMemberStatus.Member ||
            myChatMember.NewChatMember.Status == ChatMemberStatus.Administrator)
        {
            if (myChatMember.Chat.Type == ChatType.Group || myChatMember.Chat.Type == ChatType.Supergroup)
            {
                await _groupService.GetOrCreateGroupAsync(myChatMember.Chat);
            }

            await botClient.SendMessage(
                myChatMember.Chat.Id,
                "🎮 **Welcome to Dice Game Bot!**\n\n" +
                "Thanks for adding me to your group!\n\n" +
                "**Quick start:**\n" +
                "• Use `/start_game` to begin playing\n" +
                "• Use `/help` for more information\n" +
                "• Use `/dices` to see available dice types\n\n" +
                "Let's play! 🎲",
                parseMode: ParseMode.Markdown);
        }
        else if (myChatMember.NewChatMember.Status == ChatMemberStatus.Left ||
                 myChatMember.NewChatMember.Status == ChatMemberStatus.Kicked)
        {
            _logger.LogInformation("Bot removed from chat {ChatId} ({ChatTitle})",
                myChatMember.Chat.Id, myChatMember.Chat.Title);

            var session = await _gameService.GetActiveSessionAsync(myChatMember.Chat.Id);
            if (session != null)
            {
                await _gameService.CancelSessionAsync(session.ID);
                _logger.LogInformation("Cancelled game session {SessionId} due to bot removal", session.ID);
            }
        }
    }

    private async Task HandleHelpCommand(ITelegramBotClient botClient, Message message)
    {
        var helpText = """
            🎮 **Dice Game Bot Help**
            
            **Game Commands:**
            🎯 `/start_game` or `/go` - Start new game
            🛑 `/stop_game` or `/stop` - Stop current game
            🎯 `/join` - Join active game
            🚪 `/leave` - Leave game
            ℹ️ `/info` - Current game information
            ⚙️ `/settings` - Advanced game settings (creator only)
            🎲 `/dices` - Show available dice types
            ❓ `/help` - Show this help message
            
            **How to play:**
            1. **Start a game** with `/start_game`
            2. **Configure settings** using buttons or `/settings`
            3. **Wait for players** to join using "Join" button
            4. **Start the game** with "Start Game" button
            5. **Roll dice** when prompted (send the correct dice emoji)
            6. **Win by scoring highest** in each round!
            
            **Game Modes:**
            🎲 **Classic** - Play all available dice types in sequence
            ⚡ **Quick** - Play only one random dice type
            🔧 **Custom** - Choose specific dice types to play
            🏆 **Tournament** - Elimination-style competition
            💀 **Survival** - Last player standing wins
            
            **Dice Types & Max Scores:**
            🎲 Classic Dice - 6 points
            🎯 Darts - 6 points  
            🏀 Basketball - 5 points
            ⚽ Football - 5 points
            🎳 Bowling - 6 points
            🎰 Slot Machine - 64 points
            
            **Tips:**
            • Higher scores are always better
            • Some dice have different maximum scores
            • Creator can configure game settings before starting
            • Games have automatic time limits for fairness
            
            Have fun playing! 🎉
            """;

        await botClient.SendMessage(
            message.Chat.Id,
            helpText,
            parseMode: ParseMode.Markdown);
    }

    private async Task HandleDicesCommand(ITelegramBotClient botClient, Message message)
    {
        var dicesText = """
            🎲 **Available Dice Types**
            
            🎲 **Classic Dice** - Max: 6 points
            Traditional six-sided dice - the classic choice!
            
            🎯 **Darts** - Max: 6 points
            Aim for the bullseye! Precision matters.
            
            🏀 **Basketball** - Max: 5 points
            Shoot for the hoop! Score that perfect shot.
            
            ⚽ **Football/Soccer** - Max: 5 points
            Kick it into the goal! Show your skills.
            
            🎳 **Bowling** - Max: 6 points
            Roll for a strike! Knock down all the pins.
            
            🎰 **Slot Machine** - Max: 64 points
            Hit the jackpot! The highest scoring dice.
            
            **Scoring System:**
            • 🏆 Perfect Score (100%) - Maximum points possible
            • 🥇 Excellent (90%+) - Almost perfect!
            • 🥈 Very Good (75%+) - Great job!
            • 🥉 Not Bad (50%+) - Decent score
            • 😐 Could be Better (25%+) - Room for improvement
            • 😢 Unlucky (<25%) - Better luck next time!
            
            Use `/start_game` to begin playing with these dice!
            """;

        await botClient.SendMessage(
            message.Chat.Id,
            dicesText,
            parseMode: ParseMode.Markdown);
    }

    private InlineKeyboardMarkup CreateAdvancedConfigurationKeyboard(Guid sessionId)
    {
        return new InlineKeyboardMarkup(
        [
            [
                InlineKeyboardButton.WithCallbackData("🎯 Game Mode", $"config_mode_{sessionId}"),
                InlineKeyboardButton.WithCallbackData("🎲 Dice Types", $"config_dices_{sessionId}")
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
                InlineKeyboardButton.WithCallbackData("📋 Current Settings", $"config_summary_{sessionId}")
            ],
            [
                InlineKeyboardButton.WithCallbackData("◀️ Back to Game", $"back_main_{sessionId}")
            ]
        ]);
    }

    private Task HandleUnknownUpdateAsync(Update update)
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }
}