namespace LarkTG.Source.Utils;

public static class DiceHelper
{
    private static readonly Dictionary<string, DiceInfo> _diceInfo = new()
    {
        { "🎲", new DiceInfo("Classic Dice", DiceEmoji.Dice, 6, "🎲", "The traditional six-sided dice", DiceDifficulty.Easy) },
        { "🎯", new DiceInfo("Darts", DiceEmoji.Darts, 6, "🎯", "Aim for the bullseye!", DiceDifficulty.Easy) },
        { "🏀", new DiceInfo("Basketball", DiceEmoji.Basketball, 5, "🏀", "Shoot for the perfect score", DiceDifficulty.Medium) },
        { "⚽", new DiceInfo("Football", DiceEmoji.Football, 5, "⚽", "Score the winning goal", DiceDifficulty.Medium) },
        { "🎳", new DiceInfo("Bowling", DiceEmoji.Bowling, 6, "🎳", "Strike! All pins down", DiceDifficulty.Medium) },
        { "🎰", new DiceInfo("Slot Machine", DiceEmoji.SlotMachine, 64, "🎰", "Hit the jackpot!", DiceDifficulty.Hard) }
    };

    private static readonly Dictionary<DiceDifficulty, string[]> _difficultyGroups = new()
    {
        { DiceDifficulty.Easy, ["🎲", "🎯"] },
        { DiceDifficulty.Medium, ["🏀", "⚽", "🎳"] },
        { DiceDifficulty.Hard, ["🎰"] }
    };

    public enum DiceDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    private record DiceInfo(
        string Name,
        string TelegramEmoji,
        int MaxScore,
        string Emoji,
        string Description,
        DiceDifficulty Difficulty);

    public static List<string> GetAllDiceEmojis() => [.. _diceInfo.Keys];

    public static int GetMaxScore(string diceEmoji) =>
        _diceInfo.TryGetValue(diceEmoji, out var info) ? info.MaxScore : 6;

    public static string GetDiceName(string diceEmoji) =>
        _diceInfo.TryGetValue(diceEmoji, out var info) ? info.Name : "Unknown Dice";

    public static string GetDiceDescription(string diceEmoji) =>
        _diceInfo.TryGetValue(diceEmoji, out var info) ? info.Description : "Unknown dice type";

    public static DiceDifficulty GetDiceDifficulty(string diceEmoji) =>
        _diceInfo.TryGetValue(diceEmoji, out var info) ? info.Difficulty : DiceDifficulty.Easy;

    public static string GetTelegramDiceEmoji(string diceEmoji) =>
        _diceInfo.TryGetValue(diceEmoji, out var info) ? info.TelegramEmoji : DiceEmoji.Dice;

    public static bool IsValidDice(string diceEmoji) => _diceInfo.ContainsKey(diceEmoji);

    public static List<string> GetDiceByDifficulty(DiceDifficulty difficulty) =>
        _difficultyGroups.TryGetValue(difficulty, out var dice) ? [.. dice] : [];

    public static List<string> GetEasyDice() => GetDiceByDifficulty(DiceDifficulty.Easy);
    public static List<string> GetMediumDice() => GetDiceByDifficulty(DiceDifficulty.Medium);
    public static List<string> GetHardDice() => GetDiceByDifficulty(DiceDifficulty.Hard);

    public static List<string> GetCoreDice() => ["🎲", "🎯", "🏀", "⚽"];
    public static List<string> GetFunDice() => ["🎳", "🎰"];
    public static List<string> GetBalancedDice() => ["🎲", "🎯", "🎳"];

    public static string GetScoreDisplay(string diceEmoji, int score)
    {
        var maxScore = GetMaxScore(diceEmoji);
        var percentage = (double)score / maxScore * 100;

        return percentage switch
        {
            100 => "🏆 PERFECT!",
            >= 95 => "⭐ AMAZING!",
            >= 90 => "🥇 Excellent!",
            >= 80 => "🥈 Very Good!",
            >= 70 => "🥉 Good!",
            >= 60 => "👍 Not Bad!",
            >= 50 => "😐 Average",
            >= 40 => "😕 Below Average",
            >= 25 => "😢 Poor",
            _ => "💔 Unlucky"
        };
    }

    public static string GetDetailedScoreAnalysis(string diceEmoji, int score)
    {
        var maxScore = GetMaxScore(diceEmoji);
        var percentage = (double)score / maxScore * 100;
        var diceName = GetDiceName(diceEmoji);

        var analysis = $"🎯 **{diceName} Result:**\n";
        analysis += $"📊 Score: **{score}/{maxScore}** ({percentage:F1}%)\n";
        analysis += $"🎭 Rating: {GetScoreDisplay(diceEmoji, score)}\n";

        var difficulty = GetDiceDifficulty(diceEmoji);
        analysis += $"⚡ Difficulty: {GetDifficultyName(difficulty)}\n";

        if (percentage >= 90)
        {
            analysis += "🌟 Outstanding performance!";
        }
        else if (percentage >= 75)
        {
            analysis += "💪 Strong result!";
        }
        else if (percentage >= 50)
        {
            analysis += "👌 Solid attempt!";
        }
        else if (percentage >= 25)
        {
            analysis += "🍀 Better luck next time!";
        }
        else
        {
            analysis += "🎭 Sometimes the dice just don't cooperate!";
        }

        return analysis;
    }

    public static List<string> GetRandomDiceSelection(int count, bool allowDuplicates = false)
    {
        var allDice = GetAllDiceEmojis();
        var random = new Random();
        var selection = new List<string>();

        if (allowDuplicates)
        {
            for (int i = 0; i < count; i++)
            {
                selection.Add(allDice[random.Next(allDice.Count)]);
            }
        }
        else
        {
            var shuffled = allDice.OrderBy(x => random.Next()).ToList();
            selection = shuffled.Take(Math.Min(count, allDice.Count)).ToList();
        }

        return selection;
    }

    public static List<string> GetBalancedDiceSelection(int count)
    {
        if (count <= 0) return [];
        if (count >= GetAllDiceEmojis().Count) return GetAllDiceEmojis();

        var selection = new List<string>();
        var difficulties = Enum.GetValues<DiceDifficulty>().ToList();
        var random = new Random();

        for (int i = 0; i < count; i++)
        {
            var targetDifficulty = difficulties[i % difficulties.Count];
            var availableDice = GetDiceByDifficulty(targetDifficulty)
                .Where(d => !selection.Contains(d))
                .ToList();

            if (availableDice.Any())
            {
                selection.Add(availableDice[random.Next(availableDice.Count)]);
            }
            else
            {
                var remainingDice = GetAllDiceEmojis()
                    .Where(d => !selection.Contains(d))
                    .ToList();

                if (remainingDice.Any())
                {
                    selection.Add(remainingDice[random.Next(remainingDice.Count)]);
                }
            }
        }

        return selection;
    }

    public static List<string> GetPresetDice(string presetName) => presetName.ToLower() switch
    {
        "casual" => ["🎲", "🎯", "🏀"],
        "competitive" => GetAllDiceEmojis(),
        "quick" => ["🎲"],
        "fun" => ["🎳", "🎰"],
        "balanced" => ["🎲", "🎯", "🏀", "⚽"],
        "expert" => ["🎳", "🎰"],
        "core" => GetCoreDice(),
        "easy" => GetEasyDice(),
        "medium" => GetMediumDice(),
        "hard" => GetHardDice(),
        _ => []
    };

    public static string GetDiceList(bool includeDetails = false)
    {
        if (!includeDetails)
        {
            return string.Join("\n", _diceInfo.Select(kvp =>
                $"{kvp.Key} **{kvp.Value.Name}** - Max: {kvp.Value.MaxScore} points"));
        }

        var result = new StringBuilder();
        result.AppendLine("🎲 **Available Dice Types:**\n");

        foreach (var group in _difficultyGroups)
        {
            result.AppendLine($"**{GetDifficultyName(group.Key)} Dice:**");

            foreach (var diceEmoji in group.Value)
            {
                var info = _diceInfo[diceEmoji];
                result.AppendLine($"  {diceEmoji} **{info.Name}** - Max: {info.MaxScore} points");
                result.AppendLine($"     _{info.Description}_");
            }
            result.AppendLine();
        }

        return result.ToString();
    }

    public static string GetPresetsList()
    {
        return """
            🎯 **Available Presets:**
            
            **Beginner Friendly:**
            • `casual` - Easy dice for casual play (🎲🎯🏀)
            • `easy` - Only easy difficulty dice (🎲🎯)
            • `quick` - Single dice for fast games (🎲)
            
            **Balanced Play:**
            • `balanced` - Well-rounded selection (🎲🎯🏀⚽)
            • `core` - Core dice types (🎲🎯🏀⚽)
            • `medium` - Medium difficulty dice (🏀⚽🎳)
            
            **Advanced:**
            • `competitive` - All dice types for tournaments
            • `expert` - High-scoring challenging dice (🎳🎰)
            • `hard` - Only hard difficulty dice (🎰)
            • `fun` - Entertainment-focused dice (🎳🎰)
            """;
    }

    public static string GetDifficultyExplanation()
    {
        return """
            ⚡ **Dice Difficulty Levels:**
            
            🟢 **Easy** - Consistent results, good for beginners
            • 🎲 Classic Dice - Traditional and reliable
            • 🎯 Darts - Steady scoring potential
            
            🟡 **Medium** - Balanced risk/reward
            • 🏀 Basketball - Moderate scoring range
            • ⚽ Football - Similar to basketball
            • 🎳 Bowling - Good points, slight variability
            
            🔴 **Hard** - High risk, high reward
            • 🎰 Slot Machine - Massive points but unpredictable
            
            **Tip:** Mix difficulties for the most exciting games!
            """;
    }

    public static double CalculateExpectedValue(List<string> diceTypes)
    {
        if (diceTypes.Count == 0) return 0;

        var totalExpectedValue = diceTypes.Sum(dice =>
        {
            var maxScore = GetMaxScore(dice);
            return maxScore / 2.0;
        });

        return totalExpectedValue / diceTypes.Count;
    }

    public static int CalculateTotalMaxScore(List<string> diceTypes)
    {
        return diceTypes.Sum(GetMaxScore);
    }

    public static string GetGameDifficultyEstimate(List<string> diceTypes)
    {
        if (!diceTypes.Any()) return "Unknown";

        var difficulties = diceTypes.Select(GetDiceDifficulty).ToList();
        var easyCount = difficulties.Count(d => d == DiceDifficulty.Easy);
        var mediumCount = difficulties.Count(d => d == DiceDifficulty.Medium);
        var hardCount = difficulties.Count(d => d == DiceDifficulty.Hard);

        var easyPercent = (double)easyCount / difficulties.Count * 100;
        var hardPercent = (double)hardCount / difficulties.Count * 100;

        return (easyPercent, hardPercent) switch
        {
            ( >= 80, _) => "🟢 Very Easy",
            ( >= 60, < 20) => "🟢 Easy",
            ( >= 40, < 30) => "🟡 Moderate",
            (_, >= 50) => "🔴 Very Hard",
            (_, >= 30) => "🔴 Hard",
            _ => "🟡 Balanced"
        };
    }

    private static string GetDifficultyName(DiceDifficulty difficulty) => difficulty switch
    {
        DiceDifficulty.Easy => "Easy",
        DiceDifficulty.Medium => "Medium",
        DiceDifficulty.Hard => "Hard",
        _ => "Unknown"
    };

    public static bool IsDiceEmoji(string text)
    {
        return _diceInfo.ContainsKey(text);
    }

    public static string GetRandomDice()
    {
        var allDice = GetAllDiceEmojis();
        var random = new Random();
        return allDice[random.Next(allDice.Count)];
    }

    public static List<string> SortDiceByDifficulty(List<string> diceTypes)
    {
        return [.. diceTypes
            .Where(IsValidDice)
            .OrderBy(GetDiceDifficulty)
            .ThenBy(GetMaxScore)];
    }

    public static List<string> SortDiceByMaxScore(List<string> diceTypes, bool ascending = true)
    {
        var query = diceTypes.Where(IsValidDice).AsEnumerable();
        return ascending
            ? query.OrderBy(GetMaxScore).ToList()
            : query.OrderByDescending(GetMaxScore).ToList();
    }
}