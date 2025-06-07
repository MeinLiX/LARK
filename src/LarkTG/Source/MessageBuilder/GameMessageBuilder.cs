namespace LarkTG.Source.MessageBuilder;

public static class GameMessageBuilder
{
    public static string BuildSessionInfo(GameSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🎮 **Game Information**");
        sb.AppendLine();

        sb.AppendLine($"📊 **Status:** {GetSessionStateEmoji(session.State)} {GetSessionStateName(session.State)}");
        sb.AppendLine($"🎯 **Mode:** {GetGameModeDescription(session.Configuration.Mode)}");
        sb.AppendLine($"👥 **Players:** {session.Players.Count}/{session.Configuration.MaxPlayers}");

        if (session.Configuration.Mode != GameMode.Quick && session.Configuration.EnabledDices.Any())
        {
            sb.AppendLine($"🎲 **Dice Types:** {string.Join(" ", session.Configuration.EnabledDices)} ({session.Configuration.EnabledDices.Count})");
        }

        if (session.Configuration.RoundsCount > 1)
        {
            sb.AppendLine($"🔄 **Rounds per Dice:** {session.Configuration.RoundsCount}");
        }

        if (session.Configuration.AllowRerolls)
        {
            sb.AppendLine("🔁 **Rerolls:** Enabled");
        }

        if (session.Configuration.TimeLimit.HasValue)
        {
            var timeLimit = session.Configuration.TimeLimit.Value;
            sb.AppendLine($"⏱️ **Time Limit:** {FormatTimeSpan(timeLimit)}");
        }

        sb.AppendLine();
        sb.AppendLine($"👑 **Creator:** {session.Creator}");

        sb.AppendLine();
        sb.AppendLine("👥 **Participants:**");

        if (session.Players.Any())
        {
            foreach (var player in session.Players.OrderBy(p => p.ID == session.Creator.ID ? 0 : 1))
            {
                var isCreator = player.ID == session.Creator.ID;
                var icon = isCreator ? "👑" : "👤";
                sb.AppendLine($"  {icon} {player}");
            }
        }
        else
        {
            sb.AppendLine("  _(No players yet)_");
        }

        switch (session.State)
        {
            case SessionState.Registration:
                sb.AppendLine();
                sb.AppendLine("⏱️ **Registration Phase**");
                sb.AppendLine($"📝 **Minimum Players:** 2");
                sb.AppendLine($"👥 **Available Slots:** {session.Configuration.MaxPlayers - session.Players.Count}");
                if (session.Players.Count >= 2)
                {
                    sb.AppendLine("✅ **Ready to start!**");
                }
                break;

            case SessionState.Active:
                sb.AppendLine();
                var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);
                if (activeRound != null)
                {
                    sb.AppendLine($"🎯 **Current Round:** {activeRound.RoundNumber}");
                    sb.AppendLine($"🎲 **Current Dice:** {activeRound.DiceEmoji} {DiceHelper.GetDiceName(activeRound.DiceEmoji)}");
                }

                var completedRounds = session.Rounds.Count(r => r.IsCompleted);
                var totalRounds = session.Rounds.Count;
                sb.AppendLine($"📊 **Progress:** {completedRounds}/{totalRounds} rounds");
                break;

            case SessionState.Finished:
                sb.AppendLine();
                sb.AppendLine("🏁 **Game completed!**");
                if (session.FinishedAt.HasValue)
                {
                    var duration = session.FinishedAt.Value - session.StartedAt!.Value;
                    sb.AppendLine($"⏱️ **Duration:** {FormatTimeSpan(duration)}");
                }
                break;
        }

        if (session.State == SessionState.Registration && session.Configuration.EnabledDices.Any())
        {
            sb.AppendLine();
            sb.AppendLine("🎮 **Game Preview:**");

            var estimatedRounds = session.Configuration.EnabledDices.Count * session.Configuration.RoundsCount;
            sb.AppendLine($"📊 **Total Rounds:** ~{estimatedRounds}");

            if (session.Configuration.TimeLimit.HasValue)
            {
                var estimatedTime = TimeSpan.FromMinutes(estimatedRounds * 2); // Rough estimate
                sb.AppendLine($"⏱️ **Estimated Duration:** {FormatTimeSpan(estimatedTime)}");
            }
        }

        return sb.ToString();
    }

    public static string BuildRoundInfo(GameRound round)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🎲 **Round {round.RoundNumber}**");
        sb.AppendLine();

        sb.AppendLine($"🎯 **Dice:** {round.DiceEmoji} {DiceHelper.GetDiceName(round.DiceEmoji)}");
        sb.AppendLine($"🏆 **Max Score:** {DiceHelper.GetMaxScore(round.DiceEmoji)} points");

        var playedCount = round.Results.Count(r => r.HasPlayed);
        var totalCount = round.Results.Count;
        var progressPercent = totalCount > 0 ? (double)playedCount / totalCount * 100 : 0;

        sb.AppendLine($"📊 **Progress:** {playedCount}/{totalCount} ({progressPercent:F0}%)");

        var progressBar = CreateProgressBar(playedCount, totalCount);
        sb.AppendLine($"▓{progressBar}▓");

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

                var scoreEmoji = GetScoreEmoji(round.DiceEmoji, result.Score ?? 0);
                sb.AppendLine($"  {medal} {result.Player} - **{result.Score}** {scoreEmoji}");
            }

            if (sortedResults.Any())
            {
                var bestScore = sortedResults.First().Score!.Value;
                var maxPossible = DiceHelper.GetMaxScore(round.DiceEmoji);
                var percentage = (double)bestScore / maxPossible * 100;

                sb.AppendLine();
                sb.AppendLine($"🎯 **Best Score:** {bestScore}/{maxPossible} ({percentage:F1}%)");
            }
        }

        if (playedCount < totalCount)
        {
            sb.AppendLine();
            sb.AppendLine("⏳ **Waiting for:**");
            var waitingPlayers = round.Results.Where(r => !r.HasPlayed).Select(r => r.Player);

            var waitingList = waitingPlayers.Take(5).ToList();
            foreach (var player in waitingList)
            {
                sb.AppendLine($"  ⏱️ {player}");
            }

            if (waitingPlayers.Count() > 5)
            {
                sb.AppendLine($"  ... and {waitingPlayers.Count() - 5} more");
            }

            sb.AppendLine();
            sb.AppendLine($"🎯 **To play:** Send the {round.DiceEmoji} dice!");
        }

        if (round.StartedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - round.StartedAt.Value;
            if (elapsed.TotalMinutes >= 1)
            {
                sb.AppendLine();
                sb.AppendLine($"⏱️ **Round time:** {FormatTimeSpan(elapsed)}");
            }
        }

        return sb.ToString();
    }

    public static string BuildLeaderboard(List<PlayerScore> scores)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🏆 **Final Results**");
        sb.AppendLine();

        if (!scores.Any())
        {
            sb.AppendLine("No scores recorded.");
            return sb.ToString();
        }

        var sortedScores = scores.OrderByDescending(s => s.TotalScore).ToList();

        var winner = sortedScores.First();
        sb.AppendLine($"🎉 **WINNER: {winner.Player}!** 🎉");
        sb.AppendLine($"🏆 **Champion Score:** {winner.TotalScore} points");
        sb.AppendLine();

        sb.AppendLine("📊 **Final Standings:**");
        sb.AppendLine();

        for (int i = 0; i < sortedScores.Count; i++)
        {
            var score = sortedScores[i];
            var position = i + 1;

            var medal = i switch
            {
                0 => "🥇",
                1 => "🥈",
                2 => "🥉",
                _ => $"**{position}.**"
            };

            sb.AppendLine($"{medal} **{score.Player}**");
            sb.AppendLine($"    📊 Total Score: **{score.TotalScore}** points");

            if (score.RoundsPlayed > 0)
            {
                sb.AppendLine($"    🎯 Rounds Won: **{score.RoundsWon}**/{score.RoundsPlayed}");

                var winRate = (double)score.RoundsWon / score.RoundsPlayed * 100;
                var avgScore = (double)score.TotalScore / score.RoundsPlayed;

                sb.AppendLine($"    📈 Win Rate: **{winRate:F1}%**");
                sb.AppendLine($"    📊 Avg Score: **{avgScore:F1}** per round");
            }

            sb.AppendLine();
        }

        if (scores.Count >= 2)
        {
            var totalPoints = sortedScores.Sum(s => s.TotalScore);
            var avgPoints = totalPoints / (double)scores.Count;
            var highestScore = sortedScores.Max(s => s.TotalScore);
            var totalRounds = sortedScores.Max(s => s.RoundsPlayed);

            sb.AppendLine("📈 **Game Statistics:**");
            sb.AppendLine($"• **Total Points Scored:** {totalPoints:N0}");
            sb.AppendLine($"• **Average Score:** {avgPoints:F1} points");
            sb.AppendLine($"• **Highest Score:** {highestScore:N0} points");
            sb.AppendLine($"• **Total Rounds:** {totalRounds}");
            sb.AppendLine();
        }

        sb.AppendLine("🎮 **Thanks for playing!**");
        sb.AppendLine("Use `/start_game` to play again!");

        return sb.ToString();
    }

    public static string BuildGameStartMessage(GameSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🎮 **Game Started!**");
        sb.AppendLine();

        sb.AppendLine($"🎯 **Mode:** {GetGameModeDescription(session.Configuration.Mode)}");
        sb.AppendLine($"👥 **Players:** {session.Players.Count}");

        var totalRounds = session.Rounds.Count;
        sb.AppendLine($"🎲 **Total Rounds:** {totalRounds}");

        if (session.Configuration.TimeLimit.HasValue)
        {
            sb.AppendLine($"⏱️ **Time Limit:** {FormatTimeSpan(session.Configuration.TimeLimit.Value)}");
        }

        sb.AppendLine();
        sb.AppendLine("🎯 **How to Play:**");
        sb.AppendLine("• Wait for the round announcement");
        sb.AppendLine("• Send the correct dice type when prompted");
        sb.AppendLine("• Higher scores win each round");
        sb.AppendLine("• Most round wins = champion!");

        if (session.Configuration.AllowRerolls)
        {
            sb.AppendLine("• Rerolls are allowed in this game");
        }

        sb.AppendLine();
        sb.AppendLine("🍀 **Good luck to all players!**");

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

        var maxScore = sortedResults.First().Score!.Value;
        var winners = sortedResults.Where(r => r.Score == maxScore).ToList();

        if (winners.Count == 1)
        {
            sb.AppendLine($"🏆 **Round Winner:** {winners.First().Player}");
            sb.AppendLine($"🎯 **Winning Score:** {maxScore} points {GetScoreEmoji(round.DiceEmoji, maxScore)}");
        }
        else
        {
            sb.AppendLine($"🤝 **Round Tie!** {winners.Count} players scored {maxScore} points:");
            foreach (var winner in winners)
            {
                sb.AppendLine($"  🏆 {winner.Player}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("📊 **All Results:**");
        for (int i = 0; i < sortedResults.Count; i++)
        {
            var result = sortedResults[i];
            var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => "  " };
            var scoreEmoji = GetScoreEmoji(round.DiceEmoji, result.Score!.Value);
            sb.AppendLine($"{medal} {result.Player} - **{result.Score}** {scoreEmoji}");
        }

        return sb.ToString();
    }

    public static string BuildGameConfigurationSummary(GameConfiguration config)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"🎯 **Mode:** {GetGameModeDescription(config.Mode)}");
        sb.AppendLine($"👥 **Max Players:** {config.MaxPlayers}");

        if (config.RoundsCount > 1)
        {
            sb.AppendLine($"🔄 **Rounds per Dice:** {config.RoundsCount}");
        }

        sb.AppendLine($"🔁 **Rerolls:** {(config.AllowRerolls ? "Enabled" : "Disabled")}");

        if (config.TimeLimit.HasValue)
        {
            sb.AppendLine($"⏱️ **Time Limit:** {FormatTimeSpan(config.TimeLimit.Value)}");
        }
        else
        {
            sb.AppendLine("⏱️ **Time Limit:** None");
        }

        if (config.Mode != GameMode.Quick)
        {
            sb.AppendLine();
            if (config.EnabledDices.Any())
            {
                sb.AppendLine($"🎲 **Enabled Dice ({config.EnabledDices.Count}):**");
                foreach (var dice in config.EnabledDices)
                {
                    var maxScore = DiceHelper.GetMaxScore(dice);
                    sb.AppendLine($"  {dice} {DiceHelper.GetDiceName(dice)} (max: {maxScore})");
                }
            }
            else
            {
                sb.AppendLine("🎲 **Dice Types:** _(None selected)_");
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("🎲 **Dice Types:** Random selection");
        }

        var estimatedRounds = config.EnabledDices.Count * config.RoundsCount;
        if (estimatedRounds > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"📊 **Estimated Rounds:** {estimatedRounds}");

            var estimatedTime = TimeSpan.FromMinutes(estimatedRounds * 2); // Rough estimate
            sb.AppendLine($"⏱️ **Estimated Duration:** {FormatTimeSpan(estimatedTime)}");
        }

        return sb.ToString();
    }

    // Helper methods
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
        SessionState.Registration => "Registration Open",
        SessionState.Configuration => "Configuring",
        SessionState.Active => "Game Active",
        SessionState.Paused => "Paused",
        SessionState.Finished => "Finished",
        SessionState.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    private static string GetGameModeDescription(GameMode mode) => mode switch
    {
        GameMode.Classic => "Classic (all dice types)",
        GameMode.Quick => "Quick (random dice)",
        GameMode.Custom => "Custom (selected dice)",
        GameMode.Tournament => "Tournament (competitive)",
        GameMode.Survival => "Survival (elimination)",
        _ => "Unknown"
    };

    private static string CreateProgressBar(int current, int total, int width = 10)
    {
        if (total == 0) return new string('░', width);

        var filled = (int)Math.Round((double)current / total * width);
        var empty = width - filled;

        return new string('█', filled) + new string('░', empty);
    }

    private static string GetScoreEmoji(string diceEmoji, int score)
    {
        var maxScore = DiceHelper.GetMaxScore(diceEmoji);
        var percentage = (double)score / maxScore * 100;

        return percentage switch
        {
            100 => "🏆",
            >= 99 => "🥇",
            >= 80 => "🥈",
            >= 75 => "🥉",
            >= 60 => "👍",
            _ => "😢"
        };
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        }
        else if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        }
        else if (timeSpan.TotalMinutes >= 1)
        {
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }
        else
        {
            return $"{timeSpan.Seconds}s";
        }
    }
}