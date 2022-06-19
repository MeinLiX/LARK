
using LarkTG.Source.DataBase.Context;
using LarkTG.Source.DataBase.Controller;
using LarkTG.Source.DataBase.Models.EmojiGame;
using LarkTG.Source.DataBase.Models.Telegram;

namespace LarkTG.Source;

class MagicBox
{
    private readonly MainCTX _mainCTX;

    public readonly bool SystemMessage;
    public readonly TUser? User;
    public readonly TGroup? Group;
    public readonly EmojiGameSession? ActiveSession;



    internal readonly TelegramEntitiesController TelegramEntitiesController;
    internal readonly EmojiGameSessionController EmojiGameSessionController;

    public MagicBox()
    {
        _mainCTX = new MainCTX();
        TelegramEntitiesController = new(_mainCTX);
        EmojiGameSessionController = new(_mainCTX);
    }

    public MagicBox(Message message)
            : this()
    {
        User = TelegramEntitiesController.GetOrCreateUser(message.From).GetAwaiter().GetResult();
        Group = TelegramEntitiesController.GetOrCreateGroup(message.Chat).GetAwaiter().GetResult();

        SystemMessage = User is null;

        ActiveSession = EmojiGameSessionController.GetGameNotEndOrDefault(Group).GetAwaiter().GetResult();
    }

    public MagicBox(CallbackQuery callback)
            : this()
    {
        User = TelegramEntitiesController.GetOrCreateUser(callback.From).GetAwaiter().GetResult();
        Group = TelegramEntitiesController.GetOrCreateGroup(callback?.Message?.Chat).GetAwaiter().GetResult();

        SystemMessage = User is null;

        ActiveSession = EmojiGameSessionController.GetGameNotEndOrDefault(Group).GetAwaiter().GetResult();
    }

    public void throwIfGroupNull()
    {
        if (Group is null) throw new Exception("Group not found");
    }
}