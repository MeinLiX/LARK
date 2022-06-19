using System.ComponentModel.DataAnnotations;
using LarkTG.Source.DataBase.Models.Telegram;

namespace LarkTG.Source.DataBase.Models.EmojiGame;

public class EmojiGameSessionRound : BaseModel
{
    public EmojiGameSessionRound() { }

    public EmojiGameSessionRound(List<TUser> tUsers)
    {
        tUsers.ForEach(tUser =>
        {
            UsersScore.Add(new EmojiGameTUserScore()
            {
                TUserID = tUser.ID,
                Played = false,
                Score = null
            });
        });
    }

    [Required]
    public bool Played { get; set; } = false;

    public bool Active { get; set; } = false;

    [Required]
    public string DiceMode { get; set; }

    //Navigations
    public EmojiGameSession EmojiGameSession { get; set; }
    public List<EmojiGameTUserScore> UsersScore { get; set; } = new();
}
