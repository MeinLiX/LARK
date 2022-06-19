using LarkTG.Source.DataBase.Context;
using LarkTG.Source.DataBase.Models.Telegram;
using LarkTG.Source.Exceptions;
using Telegram.Bot.Types.Enums;

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

    public async Task<TUser?> GetOrCreateUser(Telegram.Bot.Types.User? user)
    {
        if (user is null)
            return null;

        TUser? tUser = await _mainCTX.Users.FirstOrDefaultAsync(u => u.ID == user.Id);
        if (tUser is null)
        {
            tUser = new()
            {
                ID = user.Id,
                FirstName= user.FirstName,
                Username = user.Username //?? throw new UsernameException() 
            };
            _mainCTX.Users.Add(tUser);
            await _mainCTX.SaveChangesAsync();
        }

        return tUser;
    }

    public async Task<TGroup?> GetOrCreateGroup(Telegram.Bot.Types.Chat? chat)
    {
        if (chat is null || (chat.Type != ChatType.Group && chat.Type != ChatType.Supergroup) || string.IsNullOrEmpty(chat.Title))
        {
            return null;
        }

        TGroup? tGroup = await _mainCTX.Groups.FirstOrDefaultAsync(u => u.ID == chat.Id);
        if (tGroup is null)
        {
            tGroup = new()
            {
                ID = chat.Id,
                Title = chat.Title
            };
            _mainCTX.Groups.Add(tGroup);
            await _mainCTX.SaveChangesAsync();
        }

        return tGroup;
    }
}
