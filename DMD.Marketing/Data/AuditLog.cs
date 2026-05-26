using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMD.Marketing.Data;

[Table("AuditLogs", Schema = "public")]
public class AuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>Null for unauthenticated/system actions.</summary>
    public int? UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = "";

    /// <summary>Entity type affected (e.g. "User", "Plan").</summary>
    [MaxLength(100)]
    public string? EntityType { get; set; }

    /// <summary>String representation of the entity PK.</summary>
    [MaxLength(256)]
    public string? EntityId { get; set; }

    /// <summary>Free-form detail (JSON, message, etc.).</summary>
    [MaxLength(2000)]
    public string? Detail { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
