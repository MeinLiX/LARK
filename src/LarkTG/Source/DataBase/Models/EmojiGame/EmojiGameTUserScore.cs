using System.ComponentModel.DataAnnotations;

namespace LarkTG.Source.DataBase.Models.EmojiGame;

//looks like strange extension table (super Relationship Staging Table)
public class EmojiGameTUserScore : BaseModel
{
    public long TUserID { get; set; }

    [Required]
    public bool Played { get; set; } = false;

    public int? Score { get; set; }

    //Navigations
    public List<EmojiGameSessionRound> Rounds { get; set; } = new();
}
