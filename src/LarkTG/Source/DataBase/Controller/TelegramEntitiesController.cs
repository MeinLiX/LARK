using LarkTG.Source.DataBase.Context;

namespace LarkTG.Source.DataBase.Controller;

internal class TelegramEntitiesController
{
    private readonly MainCTX _mainCTX = new();
    public TelegramEntitiesController(MainCTX mainCTX)
    {
        _mainCTX = mainCTX;
    }
    public async Task Trigger()
    {
        await _mainCTX.Users.FirstOrDefaultAsync();
    }
}
