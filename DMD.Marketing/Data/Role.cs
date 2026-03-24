using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMD.Marketing.Data;

[Table("Roles", Schema = "public")]
public class Role
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime  CreatedAt  { get; set; } = DateTime.UtcNow;
    public string?   CreatedBy  { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string?   ModifiedBy { get; set; }

    // ── Navigation ────────────────────────────────────────────────
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
