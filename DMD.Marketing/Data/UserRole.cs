using System.ComponentModel.DataAnnotations.Schema;

namespace DMD.Marketing.Data;

[Table("UserRoles", Schema = "public")]
public class UserRole
{
    public int  UserId { get; set; }
    public Guid RoleId { get; set; }

    public DateTime  CreatedAt  { get; set; } = DateTime.UtcNow;
    public string?   CreatedBy  { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string?   ModifiedBy { get; set; }

    // ── Navigation ────────────────────────────────────────────────
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
