namespace LarkTG.Source.Models;

public class BaseModel
{
    [Key]
    public Guid ID { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
