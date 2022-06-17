using LarkTG.Source.DataBase.Context;

namespace LarkTG.Source.DataBase.Controller;

internal class DBController
{
    private static readonly MainCTX _mainCTX = new();

    internal static TelegramEntitiesController TelegramEntitiesController { get; } = new(_mainCTX);
    internal static EmojiGameSessionController EmojiGameSessionController { get; } = new(_mainCTX);
}
