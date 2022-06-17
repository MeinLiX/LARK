using System.ComponentModel.DataAnnotations;
using LarkTG.Source.DataBase.Models.EmojiGame;

namespace LarkTG.Source.DataBase.Models.Telegram;

public class TUser
{
    [Key]
    public long ID { get; set; }

    //public int State { get; set; }
    public string NickName { get; set; }

    //Navigations
    public List<EmojiGameSession> EmojiGameSessions { get; set; } = new();
}
