using System.ComponentModel.DataAnnotations;

namespace LarkTG.Source.DataBase.Models;

public class BaseModel
{
    [Key]
    public Guid ID { get; set; }
}
