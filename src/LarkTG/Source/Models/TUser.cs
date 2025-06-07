namespace LarkTG.Source.Models;

public class TUser
{
    [Key]
    public long ID { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Username { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public List<GameSession> GameSessions { get; set; } = new();

    public override string ToString()
    {
        return Username is not null ? $"{FirstName} (@{Username})" : FirstName;
    }
}