using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMD.Marketing.Data;

[Table("PaymentHistory", Schema = "public")]
public class PaymentHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int UserId { get; set; }

    [MaxLength(50)]
    public string PlanName { get; set; } = "";

    [MaxLength(20)]
    public string BillingCycle { get; set; } = "";

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Paid";

    [MaxLength(256)]
    public string? StripePaymentIntentId { get; set; }

    [MaxLength(256)]
    public string? StripeInvoiceId { get; set; }

    [MaxLength(4)]
    public string? PaymentMethodLast4 { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
