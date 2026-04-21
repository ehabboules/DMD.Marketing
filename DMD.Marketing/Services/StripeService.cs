using DMD.Marketing.Data;
using DMD.Marketing.Models;
using Stripe;
using Stripe.Checkout;

namespace DMD.Marketing.Services;

public class StripeService
{
    private readonly IConfiguration _config;
    private readonly ILogger<StripeService> _logger;

    public StripeService(IConfiguration config, ILogger<StripeService> logger)
    {
        _config = config;
        _logger = logger;
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
    }

    public async Task<string> CreateCheckoutSessionAsync(User user, string baseUrl)
    {
        var priceId = GetPriceId(user.SelectedPlan, user.BillingCycle);
        if (priceId is null)
            throw new InvalidOperationException($"No Stripe price configured for {user.SelectedPlan}/{user.BillingCycle}");

        // Create or reuse Stripe customer
        var customerId = user.StripeCustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = user.Email,
                Name = $"{user.FirstName} {user.LastName}".Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["UserId"] = user.Id.ToString()
                }
            });
            customerId = customer.Id;
        }

        var successUrl = $"{baseUrl.TrimEnd('/')}{_config["Stripe:SuccessUrl"]}?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl.TrimEnd('/')}{_config["Stripe:CancelUrl"]}";

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["UserId"] = user.Id.ToString()
            }
        });

        _logger.LogInformation("Created Stripe Checkout Session {SessionId} for user {UserId}", session.Id, user.Id);
        return session.Url;
    }

    public string? GetNewCustomerId(User user) => user.StripeCustomerId;

    private string? GetPriceId(PlanSlug plan, BillingCycle cycle)
    {
        var key = plan switch
        {
            PlanSlug.Starter => cycle == BillingCycle.Annual ? "StarterAnnual" : "StarterMonthly",
            PlanSlug.Growth => cycle == BillingCycle.Annual ? "GrowthAnnual" : "GrowthMonthly",
            PlanSlug.Pro => cycle == BillingCycle.Annual ? "ProAnnual" : "ProMonthly",
            _ => null
        };

        if (key is null) return null;
        return _config[$"Stripe:PriceIds:{key}"];
    }
}
