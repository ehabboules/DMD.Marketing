using DMD.Marketing.Services;
using DMD.Marketing.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Stripe;

namespace DMD.Marketing.Tests.Controllers;

/// <summary>
/// Tests for StripeWebhookController internal handler methods.
/// We bypass EventUtility.ParseEvent (which requires a full Stripe JSON envelope)
/// by constructing Event objects directly and injecting typed Stripe objects.
/// </summary>
public class StripeWebhookControllerTests
{
    private static StripeWebhookController CreateController(ApplicationDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"]     = "sk_test_fake",
                ["Stripe:WebhookSecret"] = "whsec_fake",
            })
            .Build();
        // ProvisioningService is best-effort; supply a no-op instance (no config → all calls return false)
        var httpFactory = new Mock<IHttpClientFactory>().Object;
        var provisioning = new ProvisioningService(httpFactory, config, NullLogger<ProvisioningService>.Instance);
        return new StripeWebhookController(config, db, provisioning, NullLogger<StripeWebhookController>.Instance);
    }

    // ── Stripe object factory helpers ────────────────────────────────────────

    private static Stripe.Checkout.Session MakeSession(
        string userId,
        string customerId = "cus_test",
        string subscriptionId = "sub_test",
        long amountTotal = 5499,
        string? paymentIntentId = "pi_test",
        string? invoiceId = "in_test")
    {
        var json = $@"{{
            ""id"": ""cs_test"",
            ""object"": ""checkout.session"",
            ""customer"": ""{customerId}"",
            ""subscription"": ""{subscriptionId}"",
            ""amount_total"": {amountTotal},
            ""payment_intent"": ""{paymentIntentId}"",
            ""invoice"": ""{invoiceId}"",
            ""metadata"": {{ ""UserId"": ""{userId}"" }}
        }}";
        return JsonConvert.DeserializeObject<Stripe.Checkout.Session>(json)!;
    }

    private static Stripe.Checkout.Session MakeSessionNoMetadata(
        string customerId = "cus_test", long amountTotal = 0) =>
        JsonConvert.DeserializeObject<Stripe.Checkout.Session>(
            $@"{{
                ""id"": ""cs_test"",
                ""object"": ""checkout.session"",
                ""customer"": ""{customerId}"",
                ""amount_total"": {amountTotal},
                ""metadata"": {{}}
            }}")!;

    private static Stripe.Invoice MakeInvoice(
        string customerId, long amountPaid = 9999,
        string invoiceId = "in_001", string? paymentIntentId = "pi_inv_001")
    {
        var json = $@"{{
            ""id"": ""{invoiceId}"",
            ""object"": ""invoice"",
            ""customer"": ""{customerId}"",
            ""amount_paid"": {amountPaid},
            ""payment_intent"": ""{paymentIntentId}""
        }}";
        return JsonConvert.DeserializeObject<Stripe.Invoice>(json)!;
    }

    private static Stripe.Subscription MakeSubscription(string subscriptionId) =>
        JsonConvert.DeserializeObject<Stripe.Subscription>(
            $@"{{""id"": ""{subscriptionId}"", ""object"": ""subscription"", ""status"": ""canceled""}}")!;

    private static Event CheckoutEvent(Stripe.Checkout.Session session) => new Event
    {
        Type = EventTypes.CheckoutSessionCompleted,
        Data = new EventData { Object = session },
    };

    private static Event InvoiceEvent(Stripe.Invoice invoice) => new Event
    {
        Type = EventTypes.InvoicePaid,
        Data = new EventData { Object = invoice },
    };

    private static Event SubscriptionDeletedEvent(Stripe.Subscription sub) => new Event
    {
        Type = EventTypes.CustomerSubscriptionDeleted,
        Data = new EventData { Object = sub },
    };

    // ── HandleCheckoutCompleted ──────────────────────────────────────────────

    [Fact]
    public async Task HandleCheckoutCompleted_ActivatesUser_SetsStripeIds_CreatesPaymentHistory()
    {
        using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        user.SelectedPlan = PlanSlug.Starter;
        user.BillingCycle = BillingCycle.Monthly;
        await db.SaveChangesAsync();

        var ctrl = CreateController(db);
        var session = MakeSession(user.Id.ToString(), "cus_abc", "sub_xyz",
            5499, "pi_abc", "in_abc");

        await ctrl.HandleCheckoutCompleted(CheckoutEvent(session));

        await db.Entry(user).ReloadAsync();
        Assert.Equal(ActivationStatus.Active, user.ActivationStatus);
        Assert.Equal("cus_abc", user.StripeCustomerId);
        Assert.Equal("sub_xyz", user.StripeSubscriptionId);
        Assert.NotNull(user.SubscriptionExpiresAt);

        var ph = await db.PaymentHistory.SingleAsync();
        Assert.Equal(user.Id, ph.UserId);
        Assert.Equal(54.99m, ph.Amount);
        Assert.Equal("Paid", ph.Status);
        Assert.Equal("pi_abc", ph.StripePaymentIntentId);
        Assert.Equal("in_abc", ph.StripeInvoiceId);
    }

    [Fact]
    public async Task HandleCheckoutCompleted_AnnualCycle_SetsOneYearExpiry()
    {
        using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        user.BillingCycle = BillingCycle.Annual;
        await db.SaveChangesAsync();

        var ctrl = CreateController(db);
        var before = DateTime.UtcNow;

        await ctrl.HandleCheckoutCompleted(CheckoutEvent(MakeSession(user.Id.ToString())));

        await db.Entry(user).ReloadAsync();
        Assert.True(user.SubscriptionExpiresAt >= before.AddYears(1));
    }

    [Fact]
    public async Task HandleCheckoutCompleted_MonthlyCycle_SetsOneMonthExpiry()
    {
        using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        user.BillingCycle = BillingCycle.Monthly;
        await db.SaveChangesAsync();

        var ctrl = CreateController(db);
        var before = DateTime.UtcNow;

        await ctrl.HandleCheckoutCompleted(CheckoutEvent(MakeSession(user.Id.ToString())));

        await db.Entry(user).ReloadAsync();
        Assert.True(user.SubscriptionExpiresAt >= before.AddMonths(1));
        Assert.True(user.SubscriptionExpiresAt < before.AddYears(1));
    }

    [Fact]
    public async Task HandleCheckoutCompleted_NoUserIdInMetadata_ReturnsWithoutError()
    {
        using var db = DbHelper.CreateContext();
        var ctrl = CreateController(db);
        var session = MakeSessionNoMetadata();

        await ctrl.HandleCheckoutCompleted(CheckoutEvent(session));

        Assert.Empty(await db.PaymentHistory.ToListAsync());
    }

    [Fact]
    public async Task HandleCheckoutCompleted_NonIntegerUserId_ReturnsWithoutError()
    {
        using var db = DbHelper.CreateContext();
        var ctrl = CreateController(db);
        // "not-a-number" → int.TryParse returns false → userId == 0 → early return
        var session = MakeSession("not-a-number");

        await ctrl.HandleCheckoutCompleted(CheckoutEvent(session));

        Assert.Empty(await db.PaymentHistory.ToListAsync());
    }

    [Fact]
    public async Task HandleCheckoutCompleted_UnknownUserId_ReturnsWithoutError()
    {
        using var db = DbHelper.CreateContext();
        var ctrl = CreateController(db);
        var session = MakeSession("99999"); // user 99999 doesn't exist

        await ctrl.HandleCheckoutCompleted(CheckoutEvent(session));

        Assert.Empty(await db.PaymentHistory.ToListAsync());
    }

    // ── HandleInvoicePaid ────────────────────────────────────────────────────

    [Fact]
    public async Task HandleInvoicePaid_RenewsSubscription_CreatesPaymentHistory()
    {
        using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        user.StripeCustomerId = "cus_renew";
        user.BillingCycle = BillingCycle.Monthly;
        user.SelectedPlan = PlanSlug.Growth;
        user.ActivationStatus = ActivationStatus.Pending;
        await db.SaveChangesAsync();

        var ctrl = CreateController(db);
        var invoice = MakeInvoice("cus_renew", 9999, "in_001", "pi_inv_001");

        await ctrl.HandleInvoicePaid(InvoiceEvent(invoice));

        await db.Entry(user).ReloadAsync();
        Assert.Equal(ActivationStatus.Active, user.ActivationStatus);
        Assert.NotNull(user.SubscriptionExpiresAt);

        var ph = await db.PaymentHistory.SingleAsync();
        Assert.Equal(user.Id, ph.UserId);
        Assert.Equal(99.99m, ph.Amount);
        Assert.Equal("Paid", ph.Status);
        Assert.Equal("in_001", ph.StripeInvoiceId);
        Assert.Equal("pi_inv_001", ph.StripePaymentIntentId);
        Assert.Null(ph.PaymentMethodLast4); // no charge in this event
    }

    [Fact]
    public async Task HandleInvoicePaid_UnknownCustomer_ReturnsWithoutError()
    {
        using var db = DbHelper.CreateContext();
        var ctrl = CreateController(db);
        var invoice = MakeInvoice("cus_unknown", 5000);

        await ctrl.HandleInvoicePaid(InvoiceEvent(invoice));

        Assert.Empty(await db.PaymentHistory.ToListAsync());
    }

    // ── HandleSubscriptionDeleted ────────────────────────────────────────────

    [Fact]
    public async Task HandleSubscriptionDeleted_SetsPendingStatus_ClearsSubscriptionId()
    {
        using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        user.StripeSubscriptionId = "sub_cancel";
        user.ActivationStatus = ActivationStatus.Active;
        await db.SaveChangesAsync();

        var ctrl = CreateController(db);

        await ctrl.HandleSubscriptionDeleted(SubscriptionDeletedEvent(MakeSubscription("sub_cancel")));

        await db.Entry(user).ReloadAsync();
        Assert.Equal(ActivationStatus.Pending, user.ActivationStatus);
        Assert.Null(user.StripeSubscriptionId);
    }

    [Fact]
    public async Task HandleSubscriptionDeleted_UnknownSubscription_ReturnsWithoutError()
    {
        using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db);
        var ctrl = CreateController(db);

        // Should not throw when subscription doesn't match any user
        await ctrl.HandleSubscriptionDeleted(
            SubscriptionDeletedEvent(MakeSubscription("sub_nonexistent")));
    }
}
