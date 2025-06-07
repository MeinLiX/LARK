namespace LarkTG.Source.MessageBuilder;

public static class GameMessageBuilder
{
    public static string BuildSessionInfo(GameSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🎮 **Game Information**");
        sb.AppendLine();

        sb.AppendLine($"📊 **Status:** {GetSessionStateEmoji(session.State)} {GetSessionStateName(session.State)}");
        sb.AppendLine($"🎯 **Mode:** {GetGameModeName(session.Configuration.Mode)}");
        sb.AppendLine($"👥 **Players:** {session.Players.Count}/{session.Configuration.MaxPlayers}");

        if (session.Configuration.EnabledDices.Any())
        {
            sb.AppendLine($"🎲 **Dices:** {string.Join(" ", session.Configuration.EnabledDices)}");
        }

        if (session.Configuration.TimeLimit.HasValue)
        {
            sb.AppendLine($"⏱️ **Time Limit:** {session.Configuration.TimeLimit.Value.TotalMinutes:F0} min");
        }

        sb.AppendLine();
        sb.AppendLine("👥 **Participants:**");
        foreach (var player in session.Players)
        {
            var isCreator = player.ID == session.Creator.ID;
            sb.AppendLine($"  {(isCreator ? "👑" : "👤")} {player}");
        }

        if (session.State == SessionState.Registration)
        {
            sb.AppendLine();
            sb.AppendLine("⏱️ **Registration Time:** 5 minutes");
            sb.AppendLine("📝 **Minimum Players:** 2");
        }

        return sb.ToString();
    }

    public static string BuildRoundInfo(GameRound round)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🎲 **Round {round.RoundNumber}**");
        sb.AppendLine();
        sb.AppendLine($"🎯 **Dice:** {round.DiceEmoji} {DiceHelper.GetDiceName(round.DiceEmoji)}");
        sb.AppendLine($"🏆 **Max Score:** {DiceHelper.GetMaxScore(round.DiceEmoji)}");

        var playedCount = round.Results.Count(r => r.HasPlayed);
        var totalCount = round.Results.Count;

        sb.AppendLine($"📊 **Progress:** {playedCount}/{totalCount}");

        if (playedCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("📋 **Current Results:**");

            var sortedResults = round.Results
                .Where(r => r.HasPlayed)
                .OrderByDescending(r => r.Score)
                .ToList();

            for (int i = 0; i < sortedResults.Count; i++)
            {
                var result = sortedResults[i];
                var medal = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => "  "
                };

                sb.AppendLine($"  {medal} {result.Player} - **{result.Score}** {DiceHelper.GetScoreDisplay(round.DiceEmoji, result.Score ?? 0)}");
            }
        }

        if (playedCount < totalCount)
        {
            sb.AppendLine();
            sb.AppendLine("⏳ **Waiting for:**");
            var waitingPlayers = round.Results.Where(r => !r.HasPlayed).Select(r => r.Player);
            foreach (var player in waitingPlayers)
            {
                sb.AppendLine($"  ⏱️ {player}");
            }
        }

        return sb.ToString();
    }

    public static string BuildLeaderboard(List<PlayerScore> scores)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🏆 **Final Results**");
        sb.AppendLine();

        var sortedScores = scores.OrderByDescending(s => s.TotalScore).ToList();

        for (int i = 0; i < sortedScores.Count; i++)
        {
            var score = sortedScores[i];
            var medal = i switch
            {
                0 => "🥇",
                1 => "🥈",
                2 => "🥉",
                _ => $"**{i + 1}.**"
            };

            sb.AppendLine($"{medal} **{score.Player}**");
            sb.AppendLine($"    📊 Total Score: **{score.TotalScore}** points");
            sb.AppendLine($"    🏆 Rounds Won: {score.RoundsWon}/{score.RoundsPlayed}");

            if (score.RoundsPlayed > 0)
            {
                var winRate = (double)score.RoundsWon / score.RoundsPlayed * 100;
                sb.AppendLine($"    📈 Win Rate: {winRate:F1}%");
            }

            sb.AppendLine();
        }

        // Add congratulations for winner
        if (sortedScores.Any())
        {
            var winner = sortedScores.First();
            sb.AppendLine($"🎉 **Congratulations {winner.Player}!**");
            sb.AppendLine("Thanks everyone for playing! 🎮");
        }

        return sb.ToString();
    }

    public static string BuildGameStartMessage(GameSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🎮 **Game Started!**");
        sb.AppendLine();
        sb.AppendLine($"🎯 **Mode:** {GetGameModeName(session.Configuration.Mode)}");
        sb.AppendLine($"👥 **Players:** {session.Players.Count}");
        sb.AppendLine($"🎲 **Total Rounds:** {session.Rounds.Count}");
        sb.AppendLine();
        sb.AppendLine("🎯 **Instructions:**");
        sb.AppendLine("• Wait for your dice to appear");
        sb.AppendLine("• Roll the correct dice emoji");
        sb.AppendLine("• Higher scores are better!");
        sb.AppendLine("• Have fun! 🎉");

        return sb.ToString();
    }

    public static string BuildRoundCompleteMessage(GameRound round)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"✅ **Round {round.RoundNumber} Complete!**");
        sb.AppendLine();
        sb.AppendLine($"🎯 **Dice:** {round.DiceEmoji} {DiceHelper.GetDiceName(round.DiceEmoji)}");
        sb.AppendLine();

        var sortedResults = round.Results
            .OrderByDescending(r => r.Score)
            .ToList();

        var maxScore = sortedResults.First().Score;
        var winners = sortedResults.Where(r => r.Score == maxScore).ToList();

        if (winners.Count == 1)
        {
            sb.AppendLine($"🏆 **Round Winner:** {winners.First().Player} with {maxScore} points!");
        }
        else
        {
            sb.AppendLine($"🤝 **Tie!** {winners.Count} players scored {maxScore} points:");
            foreach (var winner in winners)
            {
                sb.AppendLine($"  🏆 {winner.Player}");
            }
        }

        return sb.ToString();
    }

    public static string BuildGameConfigurationSummary(GameConfiguration config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("⚙️ **Current Configuration**");
        sb.AppendLine();
        sb.AppendLine($"🎯 **Mode:** {GetGameModeName(config.Mode)}");
        sb.AppendLine($"👥 **Max Players:** {config.MaxPlayers}");
        sb.AppendLine($"🔄 **Rounds per Dice:** {config.RoundsCount}");
        sb.AppendLine($"🎲 **Rerolls Allowed:** {(config.AllowRerolls ? "Yes" : "No")}");

        if (config.TimeLimit.HasValue)
        {
            sb.AppendLine($"⏱️ **Time Limit:** {config.TimeLimit.Value.TotalMinutes:F0} minutes");
        }

        if (config.EnabledDices.Any())
        {
            sb.AppendLine();
            sb.AppendLine("🎲 **Enabled Dices:**");
            foreach (var dice in config.EnabledDices)
            {
                sb.AppendLine($"  {dice} {DiceHelper.GetDiceName(dice)} (max: {DiceHelper.GetMaxScore(dice)})");
            }
        }

        return sb.ToString();
    }

    private static string GetSessionStateEmoji(SessionState state) => state switch
    {
        SessionState.Registration => "📝",
        SessionState.Configuration => "⚙️",
        SessionState.Active => "🎮",
        SessionState.Paused => "⏸️",
        SessionState.Finished => "🏁",
        SessionState.Cancelled => "❌",
        _ => "❓"
    };

    private static string GetSessionStateName(SessionState state) => state switch
    {
        SessionState.Registration => "Registration",
        SessionState.Configuration => "Configuration",
        SessionState.Active => "Active",
        SessionState.Paused => "Paused",
        SessionState.Finished => "Finished",
        SessionState.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    private static string GetGameModeName(GameMode mode) => mode switch
    {
        GameMode.Classic => "Classic (all dices)",
        GameMode.Quick => "Quick (random dice)",
        GameMode.Custom => "Custom",
        GameMode.Tournament => "Tournament",
        GameMode.Survival => "Survival",
        _ => "Unknown"
    };
}