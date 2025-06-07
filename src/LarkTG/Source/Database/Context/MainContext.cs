using System.Text.Json;

namespace LarkTG.Source.Database.Context;

public class MainContext : DbContext
{
    private readonly string _connectionString;

    public MainContext()
    {
        _connectionString = Environment.GetEnvironmentVariable("SQLite_MainCTX_Name") ?? "game_bot.db";
        Database.EnsureDeleted();
        Database.EnsureCreated();
    }

    public DbSet<TUser> Users { get; set; }
    public DbSet<TGroup> Groups { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<GameRound> GameRounds { get; set; }
    public DbSet<PlayerRoundResult> PlayerRoundResults { get; set; }
    public DbSet<PlayerScore> PlayerScores { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_connectionString}");
        optionsBuilder.LogTo(Console.WriteLine, LogLevel.Warning);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TUser
        modelBuilder.Entity<TUser>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Username).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            entity.HasIndex(e => e.Username);
        });

        // Configure TGroup
        modelBuilder.Entity<TGroup>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // Configure GameSession
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.Property(e => e.State).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            // JSON serialization for Configuration
            entity.Property(e => e.Configuration)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<GameConfiguration>(v, (JsonSerializerOptions?)null)
                         ?? new GameConfiguration());

            // Foreign Key relationships
            entity.HasOne(e => e.Group)
                .WithMany(g => g.GameSessions)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Many-to-many relationship for players
            entity.HasMany(e => e.Players)
                .WithMany(u => u.GameSessions)
                .UsingEntity("GameSessionPlayers");
        });

        // Configure GameRound
        modelBuilder.Entity<GameRound>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.Property(e => e.DiceEmoji).IsRequired().HasMaxLength(10);
            entity.Property(e => e.RoundNumber).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Rounds)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SessionId, e.RoundNumber }).IsUnique();
        });

        // Configure PlayerRoundResult
        modelBuilder.Entity<PlayerRoundResult>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.Property(e => e.PlayerId).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Round)
                .WithMany(r => r.Results)
                .HasForeignKey(e => e.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.RoundId, e.PlayerId }).IsUnique();
        });

        // Configure PlayerScore
        modelBuilder.Entity<PlayerScore>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.Property(e => e.PlayerId).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Scores)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SessionId, e.PlayerId }).IsUnique();
        });
    }
}