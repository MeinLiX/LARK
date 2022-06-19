using System.ComponentModel.DataAnnotations;
using LarkTG.Source.DataBase.Models.Telegram;
using LarkTG.Source.Details;

namespace LarkTG.Source.DataBase.Models.EmojiGame;

public class EmojiGameSession : BaseModel
{
    public EmojiGameSession() { }
    public EmojiGameSession(TGroup groupOwner)
    {
        GroupOwner = groupOwner;
    }
    [Required]
    public GameD.Constants.SessionState SessionState { get; set; } = GameD.Constants.SessionState.Registration;

    //Navigations
    public TGroup GroupOwner { get; set; }
    public List<TUser> InGameUsers { get; set; } = new();
    public List<EmojiGameSessionRound> Rounds { get; set; } = new();
}
