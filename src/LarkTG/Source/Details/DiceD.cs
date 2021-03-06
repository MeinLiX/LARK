using System.Reflection;

namespace LarkTG.Source.Details;

public static class DiceD
{
    public static class Constants
    {
        public const string BASKETBALL = "🏀";
        public const string BOWLING = "🎳";
        public const string DARTS = "🎯";
        public const string DICE = "🎲";
        public const string FOOTBALL = "⚽";
        public const string SLOT_MACHINE = "🎰";
    }

    public static Dictionary<string, string> DiceNames = typeof(Constants)
        .GetFields(BindingFlags.Static | BindingFlags.Public).ToDictionary(
                prop => (string)(prop.GetValue(null) ?? throw new NullReferenceException("Dice constants")),
                prop => prop.Name
            );

    public static Telegram.Bot.Types.Enums.Emoji GetTelegramDice(string diceName)=>diceName switch{
        Constants.DICE => Telegram.Bot.Types.Enums.Emoji.Dice,
        Constants.DARTS => Telegram.Bot.Types.Enums.Emoji.Darts,
        Constants.BASKETBALL => Telegram.Bot.Types.Enums.Emoji.Basketball,
        Constants.FOOTBALL => Telegram.Bot.Types.Enums.Emoji.Football,
        Constants.SLOT_MACHINE => Telegram.Bot.Types.Enums.Emoji.SlotMachine,
        Constants.BOWLING => Telegram.Bot.Types.Enums.Emoji.Bowling,
    };

    public static string GetName(string diceEmoji) => DiceNames.FirstOrDefault(x => x.Key == diceEmoji).Value;

    public static string GetEmoji(string diceName) => DiceNames.FirstOrDefault(x => x.Value == diceName).Key;

    public static int GetBestValue(string diceEmoji) => diceEmoji switch
    {
        Constants.DICE => 6,
        Constants.DARTS => 6,
        Constants.BASKETBALL => 5,
        Constants.FOOTBALL => 5,
        Constants.SLOT_MACHINE => 64,
        Constants.BOWLING => 6,
        _ => throw new Exception("Dice not found.")
    };
}
