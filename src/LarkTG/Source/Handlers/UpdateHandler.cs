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
        var handler = update.Type switch
        {
            UpdateType.Message => HandleMessageAsync(botClient, update.Message!),
            UpdateType.CallbackQuery => HandleCallbackQueryAsync(botClient, update.CallbackQuery!),
            UpdateType.MyChatMember => HandleMyChatMemberAsync(botClient, update.MyChatMember!),
            _ => HandleUnknownUpdateAsync(update)
        };

        await handler;
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message)
    {
        if (message.From is null) return;

        var user = await _userService.GetOrCreateUserAsync(message.From);
        if (user is null)
        {
            _logger.LogWarning("Failed to create user {UserId}", message.From.Id);
            return;
        }

        if (message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup)
        {
            var group = await _groupService.GetOrCreateGroupAsync(message.Chat);
            if (group is null)
            {
                _logger.LogWarning("Failed to create group {ChatId}", message.Chat.Id);
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
            var command = message.Text.Split(' ')[0].ToLower();

            switch (command)
            {
                case "/start_game" or "/go":
                    await _gameCommandHandler.HandleStartGameCommand(botClient, message, groupId, userId);
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

    private async Task HandlePrivateMessageAsync(ITelegramBotClient botClient, Message message, long userId)
    {
        await botClient.SendMessage(
            message.Chat.Id,
            "👋 Hello! I'm a dice game bot for group chats.\n\n" +
            "Add me to a group and use /start_game to begin playing!\n\n" +
            "**Available commands:**\n" +
            "🎮 `/start_game` - Start new game\n" +
            "🛑 `/stop_game` - Stop current game\n" +
            "🎯 `/join` - Join game\n" +
            "🚪 `/leave` - Leave game\n" +
            "ℹ️ `/info` - Game information\n" +
            "🎲 `/dices` - Available dices\n" +
            "❓ `/help` - Help",
            parseMode: ParseMode.Markdown);
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        if (callbackQuery.From is null || callbackQuery.Data is null) return;

        await _callbackHandler.HandleCallbackQueryAsync(botClient, callbackQuery);
    }

    private async Task HandleMyChatMemberAsync(ITelegramBotClient botClient, ChatMemberUpdated myChatMember)
    {
        if (myChatMember.NewChatMember.Status == ChatMemberStatus.Member ||
            myChatMember.NewChatMember.Status == ChatMemberStatus.Administrator)
        {
            await botClient.SendMessage(
                myChatMember.Chat.Id,
                "👋 Hello! Thanks for adding me to the group!\n\n" +
                "🎮 Use `/start_game` to begin playing dice games!\n" +
                "❓ `/help` for more information");
        }
    }

    private async Task HandleHelpCommand(ITelegramBotClient botClient, Message message)
    {
        var helpText = """
            🎮 **Dice Game Bot**
            
            **Commands:**
            🎯 `/start_game` or `/go` - Start new game
            🛑 `/stop_game` or `/stop` - Stop current game
            🎯 `/join` - Join game
            🚪 `/leave` - Leave game
            ℹ️ `/info` - Game information
            🎲 `/dices` - Available dices
            ❓ `/help` - This help
            
            **How to play:**
            1. Create game with `/start_game`
            2. Configure game settings
            3. Other players join with "Join" button
            4. Start game with "Start Game" button
            5. Roll dices when it's your turn!
            
            **Game modes:**
            🎲 **Classic** - All dices in sequence
            ⚡ **Quick** - Only one random dice
            🔧 **Custom** - Choose which dices to play
            
            **Dices and max scores:**
            🎲 Dice - 6 points
            🎯 Darts - 6 points
            🏀 Basketball - 5 points
            ⚽ Football - 5 points
            🎳 Bowling - 6 points
            🎰 Slot Machine - 64 points
            """;

        await botClient.SendMessage(
            message.Chat.Id,
            helpText,
            parseMode: ParseMode.Markdown);
    }

    private async Task HandleDicesCommand(ITelegramBotClient botClient, Message message)
    {
        var dicesText = """
            🎲 **Available Dices**
            
            🎲 **Dice** - Max: 6 points
            Classic six-sided dice
            
            🎯 **Darts** - Max: 6 points
            Hit the bullseye!
            
            🏀 **Basketball** - Max: 5 points
            Score a basket!
            
            ⚽ **Football** - Max: 5 points
            Goal!
            
            🎳 **Bowling** - Max: 6 points
            Strike!
            
            🎰 **Slot Machine** - Max: 64 points
            Jackpot!
            
            Use `/start_game` to begin playing!
            """;

        await botClient.SendMessage(
            message.Chat.Id,
            dicesText,
            parseMode: ParseMode.Markdown);
    }

    private Task HandleUnknownUpdateAsync(Update update)
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }
}