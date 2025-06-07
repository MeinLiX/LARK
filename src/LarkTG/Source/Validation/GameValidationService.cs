namespace LarkTG.Source.Validation;

public class GameValidationService
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Failure(params string[] errors) =>
            new() { IsValid = false, Errors = errors.ToList() };
        public static ValidationResult Warning(string warning) =>
            new() { IsValid = true, Warnings = { warning } };
    }

    public ValidationResult ValidateGameConfiguration(GameConfiguration config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!Enum.IsDefined(config.Mode))
        {
            errors.Add("❌ Invalid game mode");
        }

        // Validate dices
        if (config.EnabledDices.Any())
        {
            var invalidDices = config.EnabledDices.Where(d => !DiceHelper.IsValidDice(d)).ToList();
            if (invalidDices.Any())
            {
                errors.Add($"❌ Invalid dices: {string.Join(", ", invalidDices)}");
            }

            if (config.EnabledDices.Count > 6)
            {
                warnings.Add("⚠️ Many dices selected - game might take long");
            }
        }
        else if (config.Mode == GameMode.Custom)
        {
            errors.Add("❌ Custom mode requires at least one dice to be selected");
        }

        if (config.RoundsCount < 1 || config.RoundsCount > 10)
        {
            errors.Add("❌ Rounds count must be between 1 and 10");
        }

        if (config.RoundsCount > 3)
        {
            warnings.Add("⚠️ Many rounds selected - game might take long");
        }

        if (config.MaxPlayers < 2 || config.MaxPlayers > 50)
        {
            errors.Add("❌ Max players must be between 2 and 50");
        }

        if (config.MaxPlayers > 20)
        {
            warnings.Add("⚠️ Many players allowed - rounds might take long");
        }

        if (config.TimeLimit.HasValue)
        {
            if (config.TimeLimit.Value.TotalMinutes < 1 || config.TimeLimit.Value.TotalHours > 24)
            {
                errors.Add("❌ Time limit must be between 1 minute and 24 hours");
            }
            else if (config.TimeLimit.Value.TotalMinutes < 5)
            {
                warnings.Add("⚠️ Short time limit - players might not have enough time");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    public ValidationResult ValidateSessionStart(GameSession session)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (session.State != SessionState.Registration && session.State != SessionState.Configuration)
        {
            errors.Add("❌ Can only start game from Registration or Configuration state");
        }

        if (session.Players.Count < 2)
        {
            errors.Add("❌ Need at least 2 players to start the game");
        }

        var configValidation = ValidateGameConfiguration(session.Configuration);
        if (!configValidation.IsValid)
        {
            errors.AddRange(configValidation.Errors);
        }
        warnings.AddRange(configValidation.Warnings);

        switch (session.Configuration.Mode)
        {
            case GameMode.Custom:
                if (session.Configuration.EnabledDices.Count == 0)
                {
                    errors.Add("❌ Custom mode requires at least one dice to be selected");
                }
                break;
        }

        var estimatedRounds = session.Configuration.EnabledDices.Count * session.Configuration.RoundsCount;
        if (estimatedRounds > 10)
        {
            warnings.Add($"⚠️ Game will have {estimatedRounds} rounds - might take long time");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    public ValidationResult ValidatePlayerJoin(GameSession session, long userId)
    {
        var errors = new List<string>();

        if (session.State != SessionState.Registration)
        {
            errors.Add("❌ Can only join during registration phase");
        }

        if (session.Players.Any(p => p.ID == userId))
        {
            errors.Add("❌ You are already in this game");
        }

        if (session.Players.Count >= session.Configuration.MaxPlayers)
        {
            errors.Add("❌ Game is full");
        }

        return errors.Any() ? ValidationResult.Failure([.. errors]) : ValidationResult.Success();
    }

    public ValidationResult ValidatePlayerLeave(GameSession session, long userId)
    {
        var errors = new List<string>();

        if (session.State != SessionState.Registration)
        {
            errors.Add("❌ Can only leave during registration phase");
        }

        if (!session.Players.Any(p => p.ID == userId))
        {
            errors.Add("❌ You are not in this game");
        }

        return errors.Count != 0 ? ValidationResult.Failure([.. errors]) : ValidationResult.Success();
    }

    public ValidationResult ValidateDicePlay(GameSession session, GameRound round, long userId, string diceEmoji)
    {
        var errors = new List<string>();

        if (session.State != SessionState.Active)
        {
            errors.Add("❌ Game is not active");
        }

        if (!round.IsActive)
        {
            errors.Add("❌ This round is not active");
        }

        if (round.DiceEmoji != diceEmoji)
        {
            errors.Add($"❌ Wrong dice! Current round: {round.DiceEmoji} {DiceHelper.GetDiceName(round.DiceEmoji)}");
        }

        if (!session.Players.Any(p => p.ID == userId))
        {
            errors.Add("❌ You are not registered in this game");
        }

        var playerResult = round.Results.FirstOrDefault(r => r.PlayerId == userId);
        if (playerResult?.HasPlayed == true)
        {
            errors.Add("❌ You already played this round");
        }

        if (playerResult == null)
        {
            errors.Add("❌ Player result not found for this round");
        }

        return errors.Any() ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success();
    }

    public ValidationResult ValidateSessionConfiguration(GameSession session, long userId)
    {
        var errors = new List<string>();

        if (session.Creator.ID != userId)
        {
            errors.Add("❌ Only the game creator can change settings");
        }

        if (session.State != SessionState.Registration)
        {
            errors.Add("❌ Cannot change settings after registration phase");
        }

        return errors.Any() ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success();
    }

    public ValidationResult ValidateSessionAction(GameSession session, long userId, string action)
    {
        var errors = new List<string>();

        switch (action.ToLower())
        {
            case "start":
                if (session.Creator.ID != userId)
                {
                    errors.Add("❌ Only the creator can start the game");
                }
                break;

            case "cancel":
            case "stop":
                if (session.Creator.ID != userId)
                {
                    errors.Add("❌ Only the creator can cancel/stop the game");
                }
                break;

            case "pause":
                if (session.Creator.ID != userId)
                {
                    errors.Add("❌ Only the creator can pause the game");
                }
                if (session.State != SessionState.Active)
                {
                    errors.Add("❌ Can only pause an active game");
                }
                break;

            case "resume":
                if (session.Creator.ID != userId)
                {
                    errors.Add("❌ Only the creator can resume the game");
                }
                if (session.State != SessionState.Paused)
                {
                    errors.Add("❌ Can only resume a paused game");
                }
                break;
        }

        return errors.Any() ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success();
    }

    public ValidationResult ValidateTimeLimit(GameSession session)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (session.Configuration.TimeLimit.HasValue && session.StartedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - session.StartedAt.Value;
            var timeLimit = session.Configuration.TimeLimit.Value;

            if (elapsed > timeLimit)
            {
                errors.Add("⏰ Game time limit exceeded");
            }
            else if (elapsed > timeLimit.Subtract(TimeSpan.FromMinutes(2)))
            {
                warnings.Add("⏰ Game time limit approaching");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
}