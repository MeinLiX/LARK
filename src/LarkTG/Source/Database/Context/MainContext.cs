using System.Text.Json;

namespace LarkTG.Source.Database.Context;

public class MainContext : DbContext
{
    private readonly string _connectionString;
    private readonly ILogger<MainContext>? _logger;

    public MainContext()
    {
        _connectionString = Environment.GetEnvironmentVariable("SQLite_MainCTX_Name") ?? "gamebot.db";
        InitializeDatabase();
    }

    public MainContext(ILogger<MainContext> logger) : this()
    {
        _logger = logger;
    }

    public DbSet<TUser> Users { get; set; } = null!;
    public DbSet<TGroup> Groups { get; set; } = null!;
    public DbSet<GameSession> GameSessions { get; set; } = null!;
    public DbSet<GameRound> GameRounds { get; set; } = null!;
    public DbSet<PlayerRoundResult> PlayerRoundResults { get; set; } = null!;
    public DbSet<PlayerScore> PlayerScores { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = $"Data Source={_connectionString}";

        optionsBuilder.UseSqlite(connectionString, options =>
        {
            options.CommandTimeout(30);
        });

        var isDevelopment = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Development";

        if (isDevelopment)
        {
            optionsBuilder.LogTo(message => _logger?.LogDebug(message), LogLevel.Information);
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
        else
        {
            optionsBuilder.LogTo(message => _logger?.LogWarning(message), LogLevel.Warning);
        }

        optionsBuilder.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUserEntity(modelBuilder);
        ConfigureGroupEntity(modelBuilder);
        ConfigureGameSessionEntity(modelBuilder);
        ConfigureGameRoundEntity(modelBuilder);
        ConfigurePlayerRoundResultEntity(modelBuilder);
        ConfigurePlayerScoreEntity(modelBuilder);

        AddDatabaseConstraints(modelBuilder);
    }

    private void ConfigureUserEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TUser>(entity =>
        {
            entity.HasKey(e => e.ID);

            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("User's first name from Telegram");

            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasComment("User's username from Telegram (optional)");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')")
                .HasComment("When the user was first seen by the bot");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("datetime('now')")
                .HasComment("Last time user information was updated");

            entity.HasIndex(e => e.Username)
                .HasDatabaseName("IX_Users_Username");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Users_CreatedAt");

            entity.ToTable("Users", t => t.HasComment("Telegram users who have interacted with the bot"));
        });
    }

    private void ConfigureGroupEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TGroup>(entity =>
        {
            entity.HasKey(e => e.ID);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200)
                .HasComment("Group/channel title");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')")
                .HasComment("When the bot was first added to the group");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("datetime('now')")
                .HasComment("Last time group information was updated");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Groups_CreatedAt");

            entity.ToTable("Groups", t => t.HasComment("Telegram groups/channels where the bot is active"));
        });
    }

    private void ConfigureGameSessionEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasKey(e => e.ID);

            entity.Property(e => e.State)
                .IsRequired()
                .HasComment("Current state of the game session");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')")
                .HasComment("When the game session was created");

            entity.Property(e => e.StartedAt)
                .HasComment("When the game actually started");

            entity.Property(e => e.FinishedAt)
                .HasComment("When the game finished");

            entity.Property(e => e.Configuration)
                .HasConversion(
                    v => SerializeConfiguration(v),
                    v => DeserializeConfiguration(v))
                .HasComment("Game configuration settings in JSON format");

            entity.HasOne(e => e.Group)
                .WithMany(g => g.GameSessions)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_GameSessions_Groups");

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_GameSessions_Creator");

            entity.HasMany(e => e.Players)
                .WithMany(u => u.GameSessions)
                .UsingEntity<Dictionary<string, object>>(
                    "GameSessionPlayers",
                    j => j.HasOne<TUser>().WithMany().HasForeignKey("PlayerId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<GameSession>().WithMany().HasForeignKey("SessionId").OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("SessionId", "PlayerId");
                        j.ToTable("GameSessionPlayers");
                        j.HasIndex("SessionId").HasDatabaseName("IX_GameSessionPlayers_SessionId");
                        j.HasIndex("PlayerId").HasDatabaseName("IX_GameSessionPlayers_PlayerId");
                    });

            entity.HasIndex(e => new { e.GroupId, e.State })
                .HasDatabaseName("IX_GameSessions_Group_State")
                .HasFilter("State IN (0, 1, 2, 3)");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_GameSessions_CreatedAt");

            entity.HasIndex(e => e.State)
                .HasDatabaseName("IX_GameSessions_State");

            entity.ToTable("GameSessions", t => t.HasComment("Game sessions/matches"));
        });
    }

    private void ConfigureGameRoundEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameRound>(entity =>
        {
            entity.HasKey(e => e.ID);

            entity.Property(e => e.DiceEmoji)
                .IsRequired()
                .HasMaxLength(10)
                .HasComment("Emoji representing the dice type for this round");

            entity.Property(e => e.RoundNumber)
                .IsRequired()
                .HasComment("Sequential round number within the session");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(false)
                .HasComment("Whether this round is currently active");

            entity.Property(e => e.IsCompleted)
                .HasDefaultValue(false)
                .HasComment("Whether this round has been completed");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Rounds)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_GameRounds_Session");

            entity.HasIndex(e => new { e.SessionId, e.RoundNumber })
                .IsUnique()
                .HasDatabaseName("IX_GameRounds_Session_RoundNumber");

            entity.HasIndex(e => new { e.SessionId, e.IsActive })
                .HasDatabaseName("IX_GameRounds_Session_Active")
                .HasFilter("IsActive = 1");

            entity.ToTable("GameRounds", t => t.HasComment("Individual rounds within game sessions"));
        });
    }

    private void ConfigurePlayerRoundResultEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerRoundResult>(entity =>
        {
            entity.HasKey(e => e.ID);

            entity.Property(e => e.PlayerId)
                .IsRequired()
                .HasComment("ID of the player who played this round");

            entity.Property(e => e.Score)
                .HasComment("Score achieved by the player (null if not played yet)");

            entity.Property(e => e.HasPlayed)
                .HasDefaultValue(false)
                .HasComment("Whether the player has played this round");

            entity.Property(e => e.RerollsUsed)
                .HasDefaultValue(0)
                .HasComment("Number of rerolls used by the player");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PlayerRoundResults_Player");

            entity.HasOne(e => e.Round)
                .WithMany(r => r.Results)
                .HasForeignKey(e => e.RoundId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PlayerRoundResults_Round");

            entity.HasIndex(e => new { e.RoundId, e.PlayerId })
                .IsUnique()
                .HasDatabaseName("IX_PlayerRoundResults_Round_Player");

            entity.HasIndex(e => new { e.RoundId, e.HasPlayed })
                .HasDatabaseName("IX_PlayerRoundResults_Round_HasPlayed");

            entity.ToTable("PlayerRoundResults", t => t.HasComment("Individual player results for each round"));
        });
    }

    private void ConfigurePlayerScoreEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerScore>(entity =>
        {
            entity.HasKey(e => e.ID);

            entity.Property(e => e.PlayerId)
                .IsRequired()
                .HasComment("ID of the player");

            entity.Property(e => e.TotalScore)
                .HasDefaultValue(0)
                .HasComment("Total accumulated score across all rounds");

            entity.Property(e => e.RoundsWon)
                .HasDefaultValue(0)
                .HasComment("Number of rounds won by this player");

            entity.Property(e => e.RoundsPlayed)
                .HasDefaultValue(0)
                .HasComment("Number of rounds played by this player");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PlayerScores_Player");

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Scores)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PlayerScores_Session");

            entity.HasIndex(e => new { e.SessionId, e.PlayerId })
                .IsUnique()
                .HasDatabaseName("IX_PlayerScores_Session_Player");

            entity.HasIndex(e => new { e.SessionId, e.TotalScore })
                .HasDatabaseName("IX_PlayerScores_Session_TotalScore");

            entity.ToTable("PlayerScores", t => t.HasComment("Aggregated player scores for each game session"));
        });
    }

    private void AddDatabaseConstraints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameSession>()
            .ToTable(tb => tb.HasCheckConstraint("CK_GameSessions_ValidState",
                "State IN (0, 1, 2, 3, 4, 5)"));

        modelBuilder.Entity<PlayerRoundResult>()
            .ToTable(tb => tb.HasCheckConstraint("CK_PlayerRoundResults_ValidScore",
                "Score IS NULL OR Score >= 0"));

        modelBuilder.Entity<PlayerScore>()
            .ToTable(tb => tb.HasCheckConstraint("CK_PlayerScores_ValidCounts",
                "TotalScore >= 0 AND RoundsWon >= 0 AND RoundsPlayed >= 0 AND RoundsWon <= RoundsPlayed"));

        modelBuilder.Entity<GameRound>()
            .ToTable(tb => tb.HasCheckConstraint("CK_GameRounds_ValidRoundNumber",
                "RoundNumber > 0"));
    }

    private void InitializeDatabase()
    {
        try
        {
            var shouldRecreate = Environment.GetEnvironmentVariable("RECREATE_DATABASE") == "true";

            if (shouldRecreate)
            {
                Database.EnsureDeleted();
                _logger?.LogInformation("Database deleted for recreation");
            }

            var created = Database.EnsureCreated();
            if (created)
            {
                _logger?.LogInformation("Database created successfully at {Path}", _connectionString);
            }
            else
            {
                _logger?.LogDebug("Database already exists at {Path}", _connectionString);
            }

            if (Database.GetPendingMigrations().Any())
            {
                Database.Migrate();
                _logger?.LogInformation("Database migrations applied");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize database at {Path}", _connectionString);
            throw;
        }
    }

    private static string SerializeConfiguration(GameConfiguration config)
    {
        try
        {
            return JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }
        catch (Exception)
        {
            return JsonSerializer.Serialize(new GameConfiguration());
        }
    }

    private static GameConfiguration DeserializeConfiguration(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                return new GameConfiguration();

            return JsonSerializer.Deserialize<GameConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            }) ?? new GameConfiguration();
        }
        catch (Exception)
        {
            return new GameConfiguration();
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving changes to database");
            throw;
        }
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();

        try
        {
            return base.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving changes to database");
            throw;
        }
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is TUser user)
            {
                if (entry.State == EntityState.Added)
                    user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is TGroup group)
            {
                if (entry.State == EntityState.Added)
                    group.CreatedAt = DateTime.UtcNow;
                group.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is GameSession session && entry.State == EntityState.Modified)
            {
                session.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}