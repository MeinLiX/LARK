namespace LarkTG.Source.Models;

public enum GameMode
{
    Classic = 0,        // All dices in sequence
    Quick = 1,          // Only one random dice
    Custom = 2,         // Configurable dices
    Tournament = 3,     // Tournament mode
    Survival = 4        // Elimination mode
}

public enum SessionState
{
    Registration = 0,
    Configuration = 1,
    Active = 2,
    Paused = 3,
    Finished = 4,
    Cancelled = 5
}

public class GameConfiguration
{
    public GameMode Mode { get; set; } = GameMode.Classic;
    public List<string> EnabledDices { get; set; } = new();
    public int RoundsCount { get; set; } = 1;
    public bool AllowRerolls { get; set; } = false;
    public int MaxPlayers { get; set; } = 10;
    public TimeSpan? TimeLimit { get; set; }
}

public class GameSession : BaseModel
{
    public GameSession() { }

    public GameSession(TGroup group, TUser creator)
    {
        Group = group;
        GroupId = group.ID;
        Creator = creator;
        CreatorId = creator.ID;
        Configuration = new GameConfiguration();
    }

    [Required]
    public SessionState State { get; set; } = SessionState.Registration;

    [Required]
    public GameConfiguration Configuration { get; set; } = new();

    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    // Foreign Keys
    public long GroupId { get; set; }
    public long CreatorId { get; set; }

    // Navigation properties
    public TGroup Group { get; set; } = null!;
    public TUser Creator { get; set; } = null!;
    public List<TUser> Players { get; set; } = new();
    public List<GameRound> Rounds { get; set; } = new();
    public List<PlayerScore> Scores { get; set; } = new();
}

public class GameRound : BaseModel
{
    [Required]
    public string DiceEmoji { get; set; } = string.Empty;

    [Required]
    public int RoundNumber { get; set; }

    public bool IsActive { get; set; } = false;
    public bool IsCompleted { get; set; } = false;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Foreign Key
    public Guid SessionId { get; set; }

    // Navigation properties
    public GameSession Session { get; set; } = null!;
    public List<PlayerRoundResult> Results { get; set; } = new();
}

public class PlayerRoundResult : BaseModel
{
    public long PlayerId { get; set; }
    public int? Score { get; set; }
    public bool HasPlayed { get; set; } = false;
    public DateTime? PlayedAt { get; set; }
    public int RerollsUsed { get; set; } = 0;

    // Foreign Key
    public Guid RoundId { get; set; }

    // Navigation properties
    public TUser Player { get; set; } = null!;
    public GameRound Round { get; set; } = null!;
}

public class PlayerScore : BaseModel
{
    public long PlayerId { get; set; }
    public int TotalScore { get; set; } = 0;
    public int RoundsWon { get; set; } = 0;
    public int RoundsPlayed { get; set; } = 0;

    // Foreign Key
    public Guid SessionId { get; set; }

    // Navigation properties
    public TUser Player { get; set; } = null!;
    public GameSession Session { get; set; } = null!;
}