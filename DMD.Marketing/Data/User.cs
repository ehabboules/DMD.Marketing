using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DMD.Marketing.Models;

namespace DMD.Marketing.Data;

[Table("Users", Schema = "public")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Email { get; set; } = "";

    [Required]
    public string PasswordHash { get; set; } = "";

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(256)]
    public string? SecurityStamp { get; set; }

    public string?   CreatedBy  { get; set; }
    public DateTime  CreatedAt  { get; set; } = DateTime.UtcNow;
    public string?   ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public bool MustChangePassword { get; set; } = false;

    [MaxLength(256)]
    public string?   PasswordResetToken       { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    // ── Plan selection ────────────────────────────────────────────
    public PlanSlug     SelectedPlan   { get; set; } = PlanSlug.None;
    public BillingCycle BillingCycle   { get; set; } = BillingCycle.Monthly;
    public DateTime?    PlanSelectedAt { get; set; }

    // ── Store setup ───────────────────────────────────────────────
    public string?          StoreName        { get; set; }
    public string?          StorePhone       { get; set; }
    public string?          StoreTimezone    { get; set; }
    public string?          BusinessType     { get; set; }  // "Retail" | "Grocery" | "Restaurant" | "Other"
    public ActivationStatus ActivationStatus { get; set; } = ActivationStatus.None;

    // ── Store defaults ────────────────────────────────────────────
    public string?   Currency           { get; set; } = "CAD";
    public decimal   FederalTaxRate     { get; set; } = 5m;
    public decimal   ProvincialTaxRate  { get; set; } = 0m;
    public bool      TaxInclusive       { get; set; } = false;

    // ── Subscription expiry ───────────────────────────────────────
    public DateTime? SubscriptionExpiresAt { get; set; }

    // ── Stripe ───────────────────────────────────────────────────────
    public string? StripeCustomerId     { get; set; }
    public string? StripeSubscriptionId { get; set; }

    // ── Navigation ────────────────────────────────────────────────
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
