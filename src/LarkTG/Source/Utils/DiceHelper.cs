namespace LarkTG.Source.Utils;

public static class DiceHelper
{
    private static readonly Dictionary<string, DiceInfo> _diceInfo = new()
    {
        { "🎲", new DiceInfo("Dice", DiceEmoji.Dice, 6, "🎲") },
        { "🎯", new DiceInfo("Darts", DiceEmoji.Darts, 6, "🎯") },
        { "🏀", new DiceInfo("Basketball", DiceEmoji.Basketball, 5, "🏀") },
        { "⚽", new DiceInfo("Football", DiceEmoji.Football, 5, "⚽") },
        { "🎳", new DiceInfo("Bowling", DiceEmoji.Bowling, 6, "🎳") },
        { "🎰", new DiceInfo("Slot Machine", DiceEmoji.SlotMachine, 64, "🎰") }
    };

    public static List<string> GetAllDiceEmojis() => [.. _diceInfo.Keys];

    public static int GetMaxScore(string diceEmoji) =>
        _diceInfo.TryGetValue(diceEmoji, out var info) ? info.MaxScore : 6;

    public static string GetDiceName(string diceEmoji) =>
        _diceInfo.TryGetValue(diceEmoji, out var info) ? info.Name : "Unknown";

    public static string GetTelegramDiceEmoji(string diceEmoji) =>
        _diceInfo.TryGetValue(diceEmoji, out var info) ? info.TelegramEmoji : DiceEmoji.Dice;

    public static bool IsValidDice(string diceEmoji) => _diceInfo.ContainsKey(diceEmoji);

    public static string GetScoreDisplay(string diceEmoji, int score)
    {
        var maxScore = GetMaxScore(diceEmoji);
        var percentage = (double)score / maxScore * 100;

        return percentage switch
        {
            100 => "🏆 PERFECT!",
            >= 90 => "🥇 Excellent!",
            >= 75 => "🥈 Very good!",
            >= 50 => "🥉 Not bad!",
            >= 25 => "😐 Could be better",
            _ => "😢 Unlucky"
        };
    }

    public static string GetDiceList()
    {
        return string.Join("\n", _diceInfo.Select(kvp =>
            $"{kvp.Key} **{kvp.Value.Name}** - Max: {kvp.Value.MaxScore} points"));
    }

    private record DiceInfo(string Name, string TelegramEmoji, int MaxScore, string Emoji);
}