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
}

public class GameSessionService : IGameSessionService
{
    private readonly MainContext _context;
    private readonly ILogger<GameSessionService> _logger;

    public GameSessionService(MainContext context, ILogger<GameSessionService> logger)
    {
        _context = context;
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

    public async Task<GameSession> CreateSessionAsync(long groupId, long creatorId)
    {
        try
        {
            var group = await _context.Groups.FindAsync(groupId);
            var creator = await _context.Users.FindAsync(creatorId);

            if (group == null || creator == null)
                throw new ArgumentException("Group or creator not found");

            var existingSession = await GetActiveSessionAsync(groupId);
            if (existingSession != null)
                throw new InvalidOperationException("Active session already exists in this group");

            var session = new GameSession(group, creator);
            session.Players.Add(creator);

            _context.GameSessions.Add(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new game session {SessionId} in group {GroupId}",
                session.ID, groupId);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for group {GroupId}", groupId);
            throw;
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

            if (session == null || user == null || session.State != SessionState.Registration)
                return false;

            if (session.Players.Any(p => p.ID == userId))
                return false;

            if (session.Players.Count >= session.Configuration.MaxPlayers)
                return false;

            session.Players.Add(user);
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

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

            if (session == null || session.State != SessionState.Registration)
                return false;

            var player = session.Players.FirstOrDefault(p => p.ID == userId);
            if (player == null) return false;

            session.Players.Remove(player);
            session.UpdatedAt = DateTime.UtcNow;

            if (session.CreatorId == userId && session.Players.Any())
            {
                var newCreator = session.Players.First();
                session.Creator = newCreator;
                session.CreatorId = newCreator.ID;
            }
            else if (!session.Players.Any())
            {
                session.State = SessionState.Cancelled;
            }

            await _context.SaveChangesAsync();
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
            if (session == null || session.State != SessionState.Registration)
                return false;

            session.Configuration = config;
            session.State = SessionState.Configuration;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
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
                .FirstOrDefaultAsync(s => s.ID == sessionId);

            if (session == null || session.Players.Count < 2)
                return false;

            CreateRounds(session);
            CreatePlayerScores(session);

            session.State = SessionState.Active;
            session.StartedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            var firstRound = session.Rounds.OrderBy(r => r.RoundNumber).First();
            firstRound.IsActive = true;
            firstRound.StartedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Started game session {SessionId}", sessionId);
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

            if (session == null || session.State != SessionState.Active)
                return false;

            var activeRound = session.Rounds.FirstOrDefault(r => r.IsActive);
            if (activeRound == null || activeRound.DiceEmoji != diceEmoji)
                return false;

            var playerResult = activeRound.Results.FirstOrDefault(r => r.PlayerId == userId);
            if (playerResult == null || playerResult.HasPlayed)
                return false;

            playerResult.Score = diceValue;
            playerResult.HasPlayed = true;
            playerResult.PlayedAt = DateTime.UtcNow;

            if (activeRound.Results.All(r => r.HasPlayed))
            {
                CompleteRound(activeRound, session);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing round for session {SessionId}, user {UserId}", sessionId, userId);
            return false;
        }
    }

    public async Task<bool> CancelSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.GameSessions.FindAsync(sessionId);
            if (session == null) return false;

            session.State = SessionState.Cancelled;
            session.FinishedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
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
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leaderboard for session {SessionId}", sessionId);
            return new List<PlayerScore>();
        }
    }

    public async Task<GameSession?> FinishSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.GameSessions
                .Include(s => s.Scores)
                    .ThenInclude(ps => ps.Player)
                .FirstOrDefaultAsync(s => s.ID == sessionId);

            if (session == null) return null;

            session.State = SessionState.Finished;
            session.FinishedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
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
        var dices = session.Configuration.EnabledDices.Count != 0
            ? session.Configuration.EnabledDices
            : DiceHelper.GetAllDiceEmojis();

        int roundNumber = 1;

        switch (session.Configuration.Mode)
        {
            case GameMode.Classic:
                foreach (var dice in dices)
                {
                    for (int i = 0; i < session.Configuration.RoundsCount; i++)
                    {
                        CreateRound(session, dice, roundNumber++);
                    }
                }
                break;

            case GameMode.Quick:
                var randomDice = dices[Random.Shared.Next(dices.Count)];
                CreateRound(session, randomDice, roundNumber);
                break;

            case GameMode.Custom:
                foreach (var dice in dices)
                {
                    CreateRound(session, dice, roundNumber++);
                }
                break;
        }
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
                Round = round
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
                Session = session
            });
        }
    }

    private void CompleteRound(GameRound round, GameSession session)
    {
        round.IsCompleted = true;
        round.IsActive = false;
        round.CompletedAt = DateTime.UtcNow;

        var maxScore = round.Results.Max(r => r.Score ?? 0);
        var winners = round.Results.Where(r => r.Score == maxScore).ToList();

        foreach (var result in round.Results)
        {
            var playerScore = session.Scores.FirstOrDefault(ps => ps.PlayerId == result.PlayerId);
            if (playerScore != null)
            {
                playerScore.TotalScore += result.Score ?? 0;
                playerScore.RoundsPlayed++;

                if (winners.Contains(result))
                {
                    playerScore.RoundsWon++;
                }
            }
        }

        var nextRound = session.Rounds
            .Where(r => !r.IsCompleted)
            .OrderBy(r => r.RoundNumber)
            .FirstOrDefault();

        if (nextRound != null)
        {
            nextRound.IsActive = true;
            nextRound.StartedAt = DateTime.UtcNow;
        }
        else
        {
            session.State = SessionState.Finished;
            session.FinishedAt = DateTime.UtcNow;
        }
    }
}