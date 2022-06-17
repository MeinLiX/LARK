using LarkTG.Source.DataBase.Context.Interface;
using LarkTG.Source.DataBase.Models.EmojiGame;
using LarkTG.Source.DataBase.Models.Telegram;

namespace LarkTG.Source.DataBase.Context;

public class MainCTX : DbContext, IMainCTX
{
    protected string SQLite_MainCTX_Name = Environment.GetEnvironmentVariable("SQLite_MainCTX_Name") ?? "MainCTX.db";
    public MainCTX()
    {
        //Database.EnsureDeleted();
        Database.EnsureCreated();
    }
    public DbSet<TGroup> Groups { get; set; }
    public DbSet<TUser> Users { get; set; }
    public DbSet<EmojiGameSession> EmojiGameSessions { get; set; }
    public DbSet<EmojiGameSessionRound> EmojiGameSessionRounds { get; set; }
    public DbSet<EmojiGameTUserScore> EmojiGameTUsersScore { get; set; }

    public async Task<int> SaveChangesAsync() => await base.SaveChangesAsync();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(@$"Filename={@$"{SQLite_MainCTX_Name}"}");
    }
}
