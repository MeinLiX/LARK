using System.ComponentModel.DataAnnotations;
using LarkTG.Source.DataBase.Models.EmojiGame;

namespace LarkTG.Source.DataBase.Models.Telegram;

public class TUser
{
    [Key]
    public long ID { get; set; }

    [Required]
    public string FirstName { get; set; }

    public string? Username { get; set; }

    //Navigations
    public List<EmojiGameSession> EmojiGameSessions { get; set; } = new();

    public override string ToString()
    {
        if (Username is not null)
        {
            return $"{FirstName}[@{Username}]";
        }
        else
        {
            return $"{FirstName}";
        }
    }
}
