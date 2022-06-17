using LarkTG.Source.DataBase.Models.Telegram;

namespace LarkTG.Source.DataBase.Models.EmojiGame;

public class EmojiGameSessionRound : BaseModel
{
    public bool Played { get; set; } = false;
    public string DiceMode { get; set; }

    //Navigations
    public EmojiGameSession EmojiGameSession { get; set; }
    public List<TUser> InGameUsers { get; set; } = new();
}
