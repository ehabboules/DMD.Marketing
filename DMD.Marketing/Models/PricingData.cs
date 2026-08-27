namespace DMD.Marketing.Models;

public static class PricingData
{
    public record Feature(string Text, bool Included = true);

    public record Plan(
        PlanSlug Slug,
        string Name,
        string Description,
        string PreviewTagline,
        decimal MonthlyPrice,
        decimal AnnualPrice,
        bool CustomPrice,
        bool Featured,
        string CtaLabel,
        List<string> Highlights,
        List<Feature> Features);

    public static readonly IReadOnlyList<Plan> Plans = new List<Plan>
    {
        new(PlanSlug.Starter,
            "Starter",
            "One location, getting off the ground.",
            "1 register · 1 warehouse",
            MonthlyPrice: 54.99m,
            AnnualPrice:  49.99m,
            CustomPrice: false,
            Featured: false,
            CtaLabel: "Start free trial",
            Highlights: new()
            {
                "1 register",
                "1 warehouse · unlimited products",
                "Unlimited users",
                "Sales & inventory reports",
            },
            Features: new()
            {
                new("POS terminal (1 register)"),
                new("Cash session management"),
                new("1 warehouse · unlimited products"),
                new("Unlimited users"),
                new("Purchase orders & receiving"),
                new("Customers & sales orders"),
                new("Sales & inventory reports"),
                new("Returns & refunds"),
                new("Specials & promotions"),
                new("Financial dashboard"),
                new("Stripe Terminal"),
                new("Role-based access control"),
                new("Follow up sales through your phone", false),
                new("Multi-language (FR / AR)", false),
            }),

        new(PlanSlug.Growth,
            "Growth",
            "Scaling retail with full POS and reporting.",
            "4 registers · 2 warehouses",
            MonthlyPrice: 99.99m,
            AnnualPrice:  95.99m,
            CustomPrice: false,
            Featured: true,
            CtaLabel: "Start free trial",
            Highlights: new()
            {
                "4 registers",
                "2 warehouses · unlimited products",
                "Unlimited users",
                "Multi-language (FR / AR)",
            },
            Features: new()
            {
                new("POS terminal (4 registers)"),
                new("Cash session management"),
                new("2 warehouses · unlimited products"),
                new("Purchase orders & receiving"),
                new("Returns & refunds"),
                new("Specials & promotions wizard"),
                new("Full reports + financial dashboard"),
                new("Expenses & categories"),
                new("Stripe Terminal support"),
                new("Unlimited users"),
                new("Role-based access control"),
                new("Follow up sales through your phone"),
                new("Multi-language (FR / AR)"),
            }),

        new(PlanSlug.Pro,
            "Pro",
            "Multi-location with full control and localization.",
            "6 registers · 4 warehouses",
            MonthlyPrice: 199.99m,
            AnnualPrice:  190.99m,
            CustomPrice: false,
            Featured: false,
            CtaLabel: "Start free trial",
            Highlights: new()
            {
                "6 registers",
                "4 warehouses · unlimited products",
                "Unlimited users",
                "Multi-language (EN / FR / AR)",
            },
            Features: new()
            {
                new("POS terminal (6 registers)"),
                new("Cash session management"),
                new("4 warehouses · unlimited products"),
                new("Purchase orders & receiving"),
                new("Returns & refunds"),
                new("Specials & promotions wizard"),
                new("Full reports + financial dashboard"),
                new("Expenses & categories"),
                new("Stripe Terminal support"),
                new("Unlimited users"),
                new("Role-based access control"),
                new("Follow up sales through your phone"),
                new("Multi-language (EN / FR / AR)"),
            }),

        new(PlanSlug.Enterprise,
            "Enterprise",
            "Custom deployments, SLAs, and dedicated support.",
            "Custom deployment",
            MonthlyPrice: 0m,
            AnnualPrice:  0m,
            CustomPrice: true,
            Featured: false,
            CtaLabel: "Contact us →",
            Highlights: new()
            {
                "Everything in Pro",
                "Dedicated infrastructure",
                "Custom integrations",
                "SLA & priority support",
            },
            Features: new()
            {
                new("Everything in Pro"),
                new("Dedicated tenant infrastructure"),
                new("Custom onboarding & data migration"),
                new("SLA & priority support"),
                new("Custom integrations (Moneris, ERP, etc.)"),
                new("Custom reporting"),
            }),
    };
}
