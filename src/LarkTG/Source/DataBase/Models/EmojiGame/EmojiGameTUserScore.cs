using System.ComponentModel.DataAnnotations;

namespace LarkTG.Source.DataBase.Models.EmojiGame;

//looks like strange extension table (super Relationship Staging Table)
public class EmojiGameTUserScore
{
    [Key]
    public long TUserID { get; set; }
    //public int State { get; set; }
    public bool Played { get; set; } = false;

    //Navigations
    public List<EmojiGameSessionRound> Rounds { get; set; } = new();
}
