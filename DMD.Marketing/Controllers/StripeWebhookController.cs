using DMD.Marketing.Data;
using DMD.Marketing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace DMD.Marketing.Controllers;

[ApiController]
[Route("api/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IConfiguration config,
        ApplicationDbContext db,
        ILogger<StripeWebhookController> logger)
    {
        _config = config;
        _db = db;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var webhookSecret = _config["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest("Invalid signature");
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutCompleted(stripeEvent);
                break;

            case EventTypes.InvoicePaid:
                await HandleInvoicePaid(stripeEvent);
                break;

            case EventTypes.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeleted(stripeEvent);
                break;

            default:
                _logger.LogInformation("Unhandled Stripe event type: {Type}", stripeEvent.Type);
                break;
        }

        return Ok();
    }

    private async Task HandleCheckoutCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session is null) return;

        var userId = session.Metadata.TryGetValue("UserId", out var id) ? int.Parse(id) : 0;
        if (userId == 0)
        {
            _logger.LogWarning("Checkout session {SessionId} has no UserId metadata", session.Id);
            return;
        }

        var user = await _db.Users.FindAsync(userId);
        if (user is null) return;

        user.StripeCustomerId = session.CustomerId;
        user.StripeSubscriptionId = session.SubscriptionId;
        user.ActivationStatus = ActivationStatus.Active;

        // Set expiry based on billing cycle
        user.SubscriptionExpiresAt = user.BillingCycle == BillingCycle.Annual
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);

        // Log payment history
        _db.PaymentHistory.Add(new PaymentHistory
        {
            UserId              = user.Id,
            PlanName            = user.SelectedPlan.ToString(),
            BillingCycle        = user.BillingCycle.ToString(),
            Amount              = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : 0m,
            Status              = "Paid",
            StripePaymentIntentId = session.PaymentIntentId,
            StripeInvoiceId     = session.InvoiceId,
            CreatedAt           = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} activated via Stripe checkout", userId);
    }

    private async Task HandleInvoicePaid(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice is null) return;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == invoice.CustomerId);
        if (user is null) return;

        user.ActivationStatus = ActivationStatus.Active;
        user.SubscriptionExpiresAt = user.BillingCycle == BillingCycle.Annual
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);

        // Extract payment method last4
        string? last4 = null;
        if (invoice.ChargeId is not null)
        {
            try
            {
                var chargeService = new ChargeService();
                var charge = await chargeService.GetAsync(invoice.ChargeId);
                last4 = charge.PaymentMethodDetails?.Card?.Last4;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch charge details for last4");
            }
        }

        // Log payment history
        _db.PaymentHistory.Add(new PaymentHistory
        {
            UserId              = user.Id,
            PlanName            = user.SelectedPlan.ToString(),
            BillingCycle        = user.BillingCycle.ToString(),
            Amount              = invoice.AmountPaid / 100m,
            Status              = "Paid",
            StripePaymentIntentId = invoice.PaymentIntentId,
            StripeInvoiceId     = invoice.Id,
            PaymentMethodLast4  = last4,
            CreatedAt           = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("Subscription renewed for user {UserId} via invoice.paid", user.Id);
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.StripeSubscriptionId == subscription.Id);
        if (user is null) return;

        user.ActivationStatus = ActivationStatus.Pending;
        user.StripeSubscriptionId = null;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Subscription cancelled for user {UserId}", user.Id);
    }
}
