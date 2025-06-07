public class TGroup
{
    [Key]
    public long ID { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public List<GameSession> GameSessions { get; set; } = new();

    public override string ToString() => Title;
}