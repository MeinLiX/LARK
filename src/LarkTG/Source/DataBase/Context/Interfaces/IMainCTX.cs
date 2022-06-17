using LarkTG.Source.DataBase.Models;
using LarkTG.Source.DataBase.Models.EmojiGame;
using LarkTG.Source.DataBase.Models.Telegram;

namespace LarkTG.Source.DataBase.Context.Interface;

public interface IMainCTX
{
    DbSet<TGroup> Groups { get; set; }
    DbSet<TUser> Users { get; set; }
    DbSet<EmojiGameSession> EmojiGameSessions { get; set; }
    DbSet<EmojiGameSessionRound> EmojiGameSessionRounds { get; set; }
    DbSet<EmojiGameTUserScore> EmojiGameTUsersScore { get; set; }

    Task<int> SaveChangesAsync();
}
