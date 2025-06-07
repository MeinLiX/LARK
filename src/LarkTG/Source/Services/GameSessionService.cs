using LarkTG.Source.Validation;

namespace LarkTG.Source.Services;

public interface IGameSessionService
{
    Task<GameSession?> GetActiveSessionAsync(long groupId);
    Task<GameSession> CreateSessionAsync(long groupId, long creatorId);
    Task<bool> JoinSessionAsync(Guid sessionId, long userId);
    Task<bool> LeaveSessionAsync(Guid sessionId, long userId);
    Task<bool> ConfigureSessionAsync(Guid sessionId, GameConfiguration config);
    Task<bool> StartSessionAsync(Guid sessionId);
    Task<bool> PlayRoundAsync(Guid sessionId, long userId, int diceValue, string diceEmoji);
    Task<GameSession?> FinishSessionAsync(Guid sessionId);
    Task<List<PlayerScore>> GetLeaderboardAsync(Guid sessionId);
    Task<bool> CancelSessionAsync(Guid sessionId);

    // Enhanced methods
    Task<List<GameSession>> GetRecentSessionsAsync(long groupId, int limit = 10);
    Task<bool> PauseSessionAsync(Guid sessionId);
    Task<bool> ResumeSessionAsync(Guid sessionId);
    Task<GameSession?> GetSessionByIdAsync(Guid sessionId);
    Task<bool> IsUserInActiveGameAsync(long userId, long groupId);
    Task<int> GetActiveGameCountAsync();
    Task<Dictionary<string, object>> GetSessionStatisticsAsync(Guid sessionId);
}

public class GameSessionService : IGameSessionService
{
    private readonly MainContext _context;
    private readonly GameValidationService _validationService;
    private readonly ILogger<GameSessionService> _logger;

    public GameSessionService(MainContext context, GameValidationService validationService, ILogger<GameSessionService> logger)
    {
        _context = context;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<GameSession?> GetActiveSessionAsync(long groupId)
    {
        try
        {
            return await _context.GameSessions
                .Include(s => s.Group)
                .Include(s => s.Creator)
                .Include(s => s.Players)
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Results)
                        .ThenInclude(pr => pr.Player)
                .Include(s => s.Scores)
                    .ThenInclude(ps => ps.Player)
                .FirstOrDefaultAsync(s => s.GroupId == groupId &&
                    (s.State == SessionState.Registration ||
                     s.State == SessionState.Configuration ||
                     s.State == SessionState.Active ||
                     s.State == SessionState.Paused));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active session for group {GroupId}", groupId);
            return null;
        }
    }

    public async Task<GameSession?> GetSessionByIdAsync(Guid sessionId)
    {
        try
        {
            return await _context.GameSessions
                .Include(s => s.Group)
                .Include(s => s.Creator)
                .Include(s => s.Players)
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Results)
                        .ThenInclude(pr => pr.Player)
                .Include(s => s.Scores)
                    .ThenInclude(ps => ps.Player)
                .FirstOrDefaultAsync(s => s.ID == sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<GameSession> CreateSessionAsync(long groupId, long creatorId)
    {
        try
        {
            var group = await _context.Groups.FindAsync(groupId);
            var creator = await _context.Users.FindAsync(creatorId);

            if (group == null)
                throw new ArgumentException($"Group {groupId} not found");

            if (creator == null)
                throw new ArgumentException($"User {creatorId} not found");

            var existingSession = await GetActiveSessionAsync(groupId);
            if (existingSession != null)
                throw new InvalidOperationException($"Active session already exists in group {groupId}");

            var session = new GameSession(group, creator);

            await ApplyIntelligentDefaults(session, groupId);

            session.Players.Add(creator);

            _context.GameSessions.Add(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new game session {SessionId} in group {GroupId} by user {UserId}",
                session.ID, groupId, creatorId);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for group {GroupId} by user {UserId}", groupId, creatorId);
            throw;
        }
    }

    private async Task ApplyIntelligentDefaults(GameSession session, long groupId)
    {
        try
        {
            var recentSessions = await GetRecentSessionsAsync(groupId, 5);

            if (recentSessions.Any())
            {
                var avgPlayers = recentSessions.Average(s => s.Players.Count);
                session.Configuration.MaxPlayers = Math.Max(10, (int)Math.Ceiling(avgPlayers * 1.5));

                var mostCommonMode = recentSessions
                    .GroupBy(s => s.Configuration.Mode)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? GameMode.Classic;

                session.Configuration.Mode = mostCommonMode;

                var commonDice = recentSessions
                    .SelectMany(s => s.Configuration.EnabledDices)
                    .GroupBy(d => d)
                    .OrderByDescending(g => g.Count())
                    .Take(4)
                    .Select(g => g.Key)
                    .ToList();

                if (commonDice.Any())
                {
                    session.Configuration.EnabledDices = commonDice;
                }
            }
            else
            {
                session.Configuration.Mode = GameMode.Classic;
                session.Configuration.EnabledDices = DiceHelper.GetCoreDice();
                session.Configuration.MaxPlayers = 10;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply intelligent defaults for group {GroupId}", groupId);
            session.Configuration.Mode = GameMode.Classic;
            session.Configuration.EnabledDices = DiceHelper.GetCoreDice();
            session.Configuration.MaxPlayers = 10;
        }
    }

    public async Task<bool> JoinSessionAsync(Guid sessionId, long userId)
    {
        try
        {
            var session = await _context.GameSessions
                .Include(s => s.Players)
                .FirstOrDefaultAsync(s => s.ID == sessionId);

            var user = await _context.Users.FindAsync(userId);

            if (session == null || user == null)
            {
                _logger.LogWarning("Join failed: Session {SessionId} or User {UserId} not found", sessionId, userId);
                return false;
            }

            var validation = _validationService.ValidatePlayerJoin(session, userId);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Join validation failed for user {UserId} in session {SessionId}: {Errors}",
                    userId, sessionId, string.Join(", ", validation.Errors));
                return false;
            }

            session.Players.Add(user);
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining session {SessionId} for user {UserId}", sessionId, userId);
            return false;
        }
    }

    public async Task<bool> LeaveSessionAsync(Guid sessionId, long userId)
    {
        try
        {
            var session = await _context.GameSessions
                .Include(s => s.Players)
                .Include(s => s.Creator)
                .FirstOrDefaultAsync(s => s.ID == sessionId);

            if (session == null)
            {
                _logger.LogWarning("Leave failed: Session {SessionId} not found", sessionId);
                return false;
            }

            var validation = _validationService.ValidatePlayerLeave(session, userId);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Leave validation failed for user {UserId} in session {SessionId}: {Errors}",
                    userId, sessionId, string.Join(", ", validation.Errors));
                return false;
            }

            var player = session.Players.FirstOrDefault(p => p.ID == userId);
            if (player == null)
            {
                _logger.LogWarning("User {UserId} not found in session {SessionId} players", userId, sessionId);
                return false;
            }

            session.Players.Remove(player);
            session.UpdatedAt = DateTime.UtcNow;

            if (session.CreatorId == userId)
            {
                if (session.Players.Any())
                {
                    var newCreator = session.Players.First();
                    session.Creator = newCreator;
                    session.CreatorId = newCreator.ID;

                    _logger.LogInformation("Transferred session {SessionId} ownership from {OldCreator} to {NewCreator}",
                        sessionId, userId, newCreator.ID);
                }
                else
                {
                    session.State = SessionState.Cancelled;
                    session.FinishedAt = DateTime.UtcNow;

                    _logger.LogInformation("Cancelled empty session {SessionId} after creator left", sessionId);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} left session {SessionId}", userId, sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving session {SessionId} for user {UserId}", sessionId, userId);
            return false;
        }
    }

    public async Task<bool> ConfigureSessionAsync(Guid sessionId, GameConfiguration config)
    {
        try
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("Configure failed: Session {SessionId} not found", sessionId);
                return false;
            }

            var validation = _validationService.ValidateGameConfiguration(config);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Configuration validation failed for session {SessionId}: {Errors}",
                    sessionId, string.Join(", ", validation.Errors));
                return false;
            }

            session.Configuration = config;
            session.State = SessionState.Configuration;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Configured session {SessionId} with mode {Mode} and {DiceCount} dice types",
                sessionId, config.Mode, config.EnabledDices.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<bool> StartSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.GameSessions
                .Include(s => s.Players)
                .Include(s => s.Rounds)
                .Include(s => s.Scores)
                .FirstOrDefaultAsync(s => s.ID == sessionId);

            if (session == null)
            {
                _logger.LogWarning("Start failed: Session {SessionId} not found", sessionId);
                return false;
            }

            var validation = _validationService.ValidateSessionStart(session);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Start validation failed for session {SessionId}: {Errors}",
                    sessionId, string.Join(", ", validation.Errors));
                return false;
            }

            if (session.Rounds.Count != 0)
            {
                _context.GameRounds.RemoveRange(session.Rounds);
                session.Rounds.Clear();
            }

            if (session.Scores.Any())
            {
                _context.PlayerScores.RemoveRange(session.Scores);
                session.Scores.Clear();
            }

            CreateRounds(session);
            CreatePlayerScores(session);

            session.State = SessionState.Active;
            session.StartedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            var firstRound = session.Rounds.OrderBy(r => r.RoundNumber).First();
            firstRound.IsActive = true;
            firstRound.StartedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Started game session {SessionId} with {PlayerCount} players and {RoundCount} rounds",
                sessionId, session.Players.Count, session.Rounds.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<bool> PlayRoundAsync(Guid sessionId, long userId, int diceValue, string diceEmoji)
    {
        try
        {
            var session = await _context.GameSessions
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Results)
                        .ThenInclude(pr => pr.Player)
                .Include(s => s.Players)
                .Include(s => s.Scores)
                .FirstOrDefaultAsync(s => s.ID == sessionId);

            if (session == null)
            {
                _logger.LogWarning("Play failed: Session {SessionId} not found", sessionId);
                return false;
            }

            var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);
            if (activeRound == null)
            {
                _logger.LogWarning("Play failed: No active round in session {SessionId}", sessionId);
                return false;
            }

            var validation = _validationService.ValidateDicePlay(session, activeRound, userId, diceEmoji);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Play validation failed for user {UserId} in session {SessionId}: {Errors}",
                    userId, sessionId, string.Join(", ", validation.Errors));
                return false;
            }

            var playerResult = activeRound.Results.FirstOrDefault(r => r.PlayerId == userId);
            if (playerResult == null)
            {
                _logger.LogError("Player result not found for user {UserId} in round {RoundId}", userId, activeRound.ID);
                return false;
            }

            playerResult.Score = diceValue;
            playerResult.HasPlayed = true;
            playerResult.PlayedAt = DateTime.UtcNow;

            if (activeRound.Results.All(r => r.HasPlayed))
            {
                CompleteRound(activeRound, session);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} played round {RoundNumber} in session {SessionId} with score {Score}",
                userId, activeRound.RoundNumber, sessionId, diceValue);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing round for session {SessionId}, user {UserId}", sessionId, userId);
            return false;
        }
    }

    public async Task<bool> PauseSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.State != SessionState.Active)
                return false;

            session.State = SessionState.Paused;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Paused session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<bool> ResumeSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null || session.State != SessionState.Paused)
                return false;

            session.State = SessionState.Active;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Resumed session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<bool> CancelSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.GameSessions
                .Include(s => s.Rounds)
                .FirstOrDefaultAsync(s => s.ID == sessionId);

            if (session == null)
            {
                _logger.LogWarning("Cancel failed: Session {SessionId} not found", sessionId);
                return false;
            }

            foreach (var round in session.Rounds.Where(r => r.IsActive))
            {
                round.IsActive = false;
                round.CompletedAt = DateTime.UtcNow;
            }

            session.State = SessionState.Cancelled;
            session.FinishedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cancelled session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<List<PlayerScore>> GetLeaderboardAsync(Guid sessionId)
    {
        try
        {
            return await _context.PlayerScores
                .Include(ps => ps.Player)
                .Where(ps => ps.SessionId == sessionId)
                .OrderByDescending(ps => ps.TotalScore)
                .ThenByDescending(ps => ps.RoundsWon)
                .ThenBy(ps => ps.Player.FirstName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leaderboard for session {SessionId}", sessionId);
            return [];
        }
    }

    public async Task<List<GameSession>> GetRecentSessionsAsync(long groupId, int limit = 10)
    {
        try
        {
            return await _context.GameSessions
                .Include(s => s.Creator)
                .Include(s => s.Players)
                .Where(s => s.GroupId == groupId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent sessions for group {GroupId}", groupId);
            return [];
        }
    }

    public async Task<bool> IsUserInActiveGameAsync(long userId, long groupId)
    {
        try
        {
            var activeSession = await GetActiveSessionAsync(groupId);
            return activeSession?.Players.Any(p => p.ID == userId) == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is in active game in group {GroupId}", userId, groupId);
            return false;
        }
    }

    public async Task<int> GetActiveGameCountAsync()
    {
        try
        {
            return await _context.GameSessions
                .CountAsync(s => s.State == SessionState.Active || s.State == SessionState.Registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active game count");
            return 0;
        }
    }

    public async Task<Dictionary<string, object>> GetSessionStatisticsAsync(Guid sessionId)
    {
        try
        {
            var session = await GetSessionByIdAsync(sessionId);
            if (session == null) return new Dictionary<string, object>();

            var stats = new Dictionary<string, object>
            {
                ["SessionId"] = sessionId,
                ["State"] = session.State.ToString(),
                ["Mode"] = session.Configuration.Mode.ToString(),
                ["PlayerCount"] = session.Players.Count,
                ["TotalRounds"] = session.Rounds.Count,
                ["CompletedRounds"] = session.Rounds.Count(r => r.IsCompleted),
                ["CreatedAt"] = session.CreatedAt,
                ["Duration"] = session.FinishedAt?.Subtract(session.StartedAt ?? session.CreatedAt) ??
                             (session.StartedAt.HasValue ? DateTime.UtcNow.Subtract(session.StartedAt.Value) : TimeSpan.Zero)
            };

            if (session.Scores.Any())
            {
                stats["HighestScore"] = session.Scores.Max(s => s.TotalScore);
                stats["LowestScore"] = session.Scores.Min(s => s.TotalScore);
                stats["AverageScore"] = session.Scores.Average(s => s.TotalScore);
                stats["WinnerName"] = session.Scores.OrderByDescending(s => s.TotalScore).First().Player.FirstName;
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statistics for session {SessionId}", sessionId);
            return new Dictionary<string, object>();
        }
    }

    public async Task<GameSession?> FinishSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.GameSessions
                .Include(s => s.Scores)
                    .ThenInclude(ps => ps.Player)
                .Include(s => s.Rounds)
                .FirstOrDefaultAsync(s => s.ID == sessionId);

            if (session == null)
            {
                _logger.LogWarning("Finish failed: Session {SessionId} not found", sessionId);
                return null;
            }

            foreach (var round in session.Rounds.Where(r => r.IsActive))
            {
                round.IsActive = false;
                round.IsCompleted = true;
                round.CompletedAt = DateTime.UtcNow;
            }

            session.State = SessionState.Finished;
            session.FinishedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Finished session {SessionId} with {PlayerCount} players",
                sessionId, session.Players.Count);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finishing session {SessionId}", sessionId);
            return null;
        }
    }

    private void CreateRounds(GameSession session)
    {
        var diceTypes = session.Configuration.EnabledDices.Any()
            ? session.Configuration.EnabledDices
            : DiceHelper.GetAllDiceEmojis();

        int roundNumber = 1;

        switch (session.Configuration.Mode)
        {
            case GameMode.Classic:
                foreach (var dice in diceTypes)
                {
                    for (int i = 0; i < session.Configuration.RoundsCount; i++)
                    {
                        CreateRound(session, dice, roundNumber++);
                    }
                }
                break;

            case GameMode.Quick:
                var randomDice = diceTypes[Random.Shared.Next(diceTypes.Count)];
                CreateRound(session, randomDice, roundNumber);
                break;

            case GameMode.Custom:
                foreach (var dice in diceTypes)
                {
                    for (int i = 0; i < session.Configuration.RoundsCount; i++)
                    {
                        CreateRound(session, dice, roundNumber++);
                    }
                }
                break;

            case GameMode.Tournament:
                var tournamentDice = DiceHelper.SortDiceByDifficulty(diceTypes);
                foreach (var dice in tournamentDice)
                {
                    for (int i = 0; i < Math.Max(1, session.Configuration.RoundsCount); i++)
                    {
                        CreateRound(session, dice, roundNumber++);
                    }
                }
                break;

            case GameMode.Survival:
                var survivalDice = new List<string>();
                survivalDice.AddRange(DiceHelper.GetEasyDice());
                survivalDice.AddRange(DiceHelper.GetMediumDice());
                survivalDice.AddRange(DiceHelper.GetHardDice());

                foreach (var dice in survivalDice)
                {
                    CreateRound(session, dice, roundNumber++);
                }
                break;
        }

        _logger.LogInformation("Created {RoundCount} rounds for session {SessionId} in mode {Mode}",
            session.Rounds.Count, session.ID, session.Configuration.Mode);
    }

    private void CreateRound(GameSession session, string diceEmoji, int roundNumber)
    {
        var round = new GameRound
        {
            Session = session,
            SessionId = session.ID,
            DiceEmoji = diceEmoji,
            RoundNumber = roundNumber
        };

        foreach (var player in session.Players)
        {
            round.Results.Add(new PlayerRoundResult
            {
                Player = player,
                PlayerId = player.ID,
                Round = round,
                RoundId = round.ID
            });
        }

        session.Rounds.Add(round);
    }

    private void CreatePlayerScores(GameSession session)
    {
        foreach (var player in session.Players)
        {
            session.Scores.Add(new PlayerScore
            {
                Player = player,
                PlayerId = player.ID,
                Session = session,
                SessionId = session.ID
            });
        }

        _logger.LogDebug("Created score tracking for {PlayerCount} players in session {SessionId}",
            session.Players.Count, session.ID);
    }

    private void CompleteRound(GameRound round, GameSession session)
    {
        round.IsCompleted = true;
        round.IsActive = false;
        round.CompletedAt = DateTime.UtcNow;

        var maxScore = round.Results.Where(r => r.Score.HasValue).Max(r => r.Score!.Value);
        var winners = round.Results.Where(r => r.Score == maxScore).ToList();

        foreach (var result in round.Results.Where(r => r.Score.HasValue))
        {
            var playerScore = session.Scores.FirstOrDefault(ps => ps.PlayerId == result.PlayerId);
            if (playerScore != null)
            {
                playerScore.TotalScore += result.Score!.Value;
                playerScore.RoundsPlayed++;

                if (winners.Contains(result))
                {
                    playerScore.RoundsWon++;
                }
            }
        }

        var nextRound = session.Rounds
            .Where(r => !r.IsCompleted && r.RoundNumber > round.RoundNumber)
            .OrderBy(r => r.RoundNumber)
            .FirstOrDefault();

        if (nextRound != null)
        {
            nextRound.IsActive = true;
            nextRound.StartedAt = DateTime.UtcNow;

            _logger.LogDebug("Activated round {RoundNumber} in session {SessionId}",
                nextRound.RoundNumber, session.ID);
        }
        else
        {
            session.State = SessionState.Finished;
            session.FinishedAt = DateTime.UtcNow;

            _logger.LogInformation("All rounds completed in session {SessionId}, game finished", session.ID);
        }

        _logger.LogInformation("Completed round {RoundNumber} in session {SessionId}, {WinnerCount} winner(s) with score {MaxScore}",
            round.RoundNumber, session.ID, winners.Count, maxScore);
    }
}