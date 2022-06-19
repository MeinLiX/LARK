using System.ComponentModel.DataAnnotations;
using LarkTG.Source.DataBase.Models.EmojiGame;

namespace LarkTG.Source.DataBase.Models.Telegram;

public class TGroup
{
    [Key]
    public long ID { get; set; }

    [Required]
    public string Title { get; set; }

    //Navigations
    public List<EmojiGameSession> EmojiGameSessions { get; set; } = new();


    public override string ToString() => $"{Title}";
}
