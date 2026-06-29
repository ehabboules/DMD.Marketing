using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DMD.Marketing.Models;

namespace DMD.Marketing.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<EmailService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ── Core send helper ────────────────────────────────────────────────────

    private async Task<bool> SendAsync(
        string to, string? toName,
        string subject, string html, string text,
        string? replyToEmail = null, string? replyToName = null)
    {
        var apiKey    = _config["Resend:ApiKey"];
        var fromEmail = _config["Resend:FromEmail"];
        var fromName  = _config["Resend:FromName"];

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Resend API key missing.");
            return false;
        }

        var toAddress = string.IsNullOrEmpty(toName) ? to : $"{toName} <{to}>";

        var payload = new Dictionary<string, object>
        {
            ["from"]    = $"{fromName} <{fromEmail}>",
            ["to"]      = new[] { toAddress },
            ["subject"] = subject,
            ["html"]    = html,
            ["text"]    = text
        };

        if (!string.IsNullOrEmpty(replyToEmail))
        {
            var replyTo = string.IsNullOrEmpty(replyToName) ? replyToEmail : $"{replyToName} <{replyToEmail}>";
            payload["reply_to"] = replyTo;
        }

        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var content  = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await http.PostAsync("https://api.resend.com/emails", content);

        if (response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadAsStringAsync();
        _logger.LogWarning("Resend returned {Status} for email to {Email}: {Body}",
            (int)response.StatusCode, to, body);
        return false;
    }

    // ── Public methods ───────────────────────────────────────────────────────

    public async Task<bool> SendDemoRequestAsync(ContactFormModel model)
    {
        try
        {
            var toEmail = _config["Resend:ToEmail"];
            if (string.IsNullOrEmpty(toEmail)) { _logger.LogWarning("Resend ToEmail not configured."); return false; }

            var ok = await SendAsync(
                to: toEmail, toName: null,
                subject: $"🛒 Demo Request — {model.Company}",
                html: BuildHtmlBody(model),
                text: BuildPlainText(model),
                replyToEmail: model.Email, replyToName: model.Name);

            if (ok) _logger.LogInformation("Demo request email sent for {Company}", model.Company);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send demo request email.");
            return false;
        }
    }

    public async Task<bool> SendPasswordResetAsync(string toEmail, string firstName, string resetUrl)
    {
        try
        {
            var html = $"""
                <div style="font-family:sans-serif;max-width:560px;margin:auto;">
                  <div style="background:#1A237E;padding:24px 32px;border-radius:12px 12px 0 0;">
                    <h2 style="color:#fff;margin:0;font-size:1.2rem;">Reset your password</h2>
                  </div>
                  <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                    <p style="color:#374151;margin:0 0 16px;">Hi {firstName},</p>
                    <p style="color:#374151;margin:0 0 24px;">Click the button below to set a new password. This link expires in <strong>1 hour</strong>.</p>
                    <a href="{resetUrl}" style="display:inline-block;background:#00BFA5;color:#fff;font-weight:700;padding:12px 28px;border-radius:8px;text-decoration:none;font-size:0.95rem;">Reset my password</a>
                    <p style="color:#9ca3af;font-size:0.8rem;margin:24px 0 0;">If you didn't request a password reset, you can safely ignore this email.</p>
                  </div>
                </div>
                """;

            var text = $"Hi {firstName},\n\nClick the link below to reset your password (expires in 1 hour):\n{resetUrl}\n\nIf you didn't request this, ignore this email.\n\n— Cloud Mobil POS";

            var ok = await SendAsync(toEmail, firstName, "Reset your Cloud Mobil POS password", html, text);
            if (!ok) _logger.LogWarning("Failed to send password reset email to {Email}", toEmail);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendTrialExpiryReminderAsync(string toEmail, string firstName, string paymentUrl, DateTime expiresAt)
    {
        try
        {
            var daysLeft = (int)Math.Ceiling((expiresAt - DateTime.UtcNow).TotalDays);
            var urgency  = daysLeft <= 0
                ? "Your free trial has expired"
                : $"Your free trial expires in {daysLeft} day{(daysLeft == 1 ? "" : "s")}";

            var html = $"""
                <div style="font-family:sans-serif;max-width:560px;margin:auto;">
                  <div style="background:#1A237E;padding:24px 32px;border-radius:12px 12px 0 0;">
                    <h2 style="color:#fff;margin:0;font-size:1.2rem;">{urgency}</h2>
                  </div>
                  <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                    <p style="color:#374151;margin:0 0 16px;">Hi {firstName},</p>
                    <p style="color:#374151;margin:0 0 24px;">Your 14-day free trial is coming to an end. Subscribe now to keep your store running smoothly with Cloud Mobil POS.</p>
                    <a href="{paymentUrl}" style="display:inline-block;background:#00BFA5;color:#fff;font-weight:700;padding:12px 28px;border-radius:8px;text-decoration:none;font-size:0.95rem;">Subscribe Now</a>
                    <p style="color:#9ca3af;font-size:0.8rem;margin:24px 0 0;">If you have questions, reply to this email or contact our support team.</p>
                  </div>
                </div>
                """;

            var text = $"Hi {firstName},\n\n{urgency}. To continue using Cloud Mobil POS, please subscribe:\n{paymentUrl}\n\n— Cloud Mobil POS";

            var ok = await SendAsync(toEmail, firstName, $"{urgency} — Cloud Mobil POS", html, text);
            if (ok) _logger.LogInformation("Trial expiry reminder sent to {Email}", toEmail);
            else    _logger.LogWarning("Failed to send trial expiry reminder to {Email}", toEmail);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send trial expiry reminder to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendAdminNotificationAsync(
        string toEmail, string firstName, string subject, string messageBody)
    {
        try
        {
            var html = $"""
                <div style="font-family:sans-serif;max-width:560px;margin:auto;">
                  <div style="background:#1A237E;padding:24px 32px;border-radius:12px 12px 0 0;">
                    <h2 style="color:#fff;margin:0;font-size:1.2rem;">{subject}</h2>
                    <p style="color:#90CAF9;margin:4px 0 0;font-size:0.85rem;">Cloud Mobil POS</p>
                  </div>
                  <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                    <p style="color:#374151;margin:0 0 16px;">Hi {firstName},</p>
                    <div style="color:#374151;line-height:1.65;white-space:pre-wrap;">{messageBody}</div>
                    <p style="color:#9ca3af;font-size:0.8rem;margin:24px 0 0;">— Cloud Mobil POS Team</p>
                  </div>
                </div>
                """;

            var text = $"Hi {firstName},\n\n{messageBody}\n\n— Cloud Mobil POS Team";

            var ok = await SendAsync(toEmail, firstName, subject, html, text);
            if (ok) _logger.LogInformation("Admin notification '{Subject}' sent to {Email}", subject, toEmail);
            else    _logger.LogWarning("Failed to send admin notification to {Email}", toEmail);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendRegistrationWelcomeAsync(string toEmail, string firstName, string baseUrl)
    {
        try
        {
            var profileUrl = $"{baseUrl.TrimEnd('/')}/profile";

            var html = $"""
                <div style="font-family:sans-serif;max-width:560px;margin:auto;">
                  <div style="background:#1A237E;padding:24px 32px;border-radius:12px 12px 0 0;">
                    <h2 style="color:#fff;margin:0;font-size:1.2rem;">Welcome to Cloud Mobil POS!</h2>
                    <p style="color:#90CAF9;margin:4px 0 0;font-size:0.85rem;">Your 14-day free trial has started</p>
                  </div>
                  <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                    <p style="color:#374151;margin:0 0 16px;">Hi {firstName},</p>
                    <p style="color:#374151;margin:0 0 8px;">Thanks for signing up! Your account is ready and your <strong>14-day free trial</strong> has started.</p>
                    <p style="color:#374151;margin:0 0 24px;">Head to your profile to pick a plan and set up your store details.</p>
                    <a href="{profileUrl}"
                       style="display:inline-block;background:#00BFA5;color:#fff;font-weight:700;padding:12px 28px;border-radius:8px;text-decoration:none;font-size:0.95rem;">
                      Set up my store →
                    </a>
                    <hr style="border:none;border-top:1px solid #e5e7eb;margin:28px 0;" />
                    <p style="color:#6b7280;font-size:0.85rem;margin:0 0 4px;"><strong>What happens next?</strong></p>
                    <p style="color:#6b7280;font-size:0.85rem;margin:0 0 4px;">1. Choose a plan on your profile page</p>
                    <p style="color:#6b7280;font-size:0.85rem;margin:0 0 4px;">2. Our team will activate your POS store within 24 hours</p>
                    <p style="color:#6b7280;font-size:0.85rem;margin:0;">3. You'll receive a second email with your store link</p>
                    <p style="color:#9ca3af;font-size:0.8rem;margin:24px 0 0;">Questions? Reply to this email — we're here to help.</p>
                  </div>
                </div>
                """;

            var text = $"Hi {firstName},\n\nWelcome to Cloud Mobil POS! Your 14-day free trial has started.\n\nLog in to your account and set up your store:\n{profileUrl}\n\nIf you have any questions, just reply to this email — we're happy to help.\n\n— The Cloud Mobil POS Team";

            var ok = await SendAsync(toEmail, firstName, $"Welcome to Cloud Mobil POS, {firstName}!", html, text);
            if (ok) _logger.LogInformation("Registration welcome email sent to {Email}", toEmail);
            else    _logger.LogWarning("Failed to send registration welcome email to {Email}", toEmail);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration welcome email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(DMD.Marketing.Data.User user, string appUrl)
    {
        try
        {
            var firstName = user.FirstName ?? user.Email;
            var planName  = user.SelectedPlan.ToString();
            var loginUrl  = $"https://{appUrl}";

            var html = $"""
                <div style="font-family:sans-serif;max-width:560px;margin:auto;">
                  <div style="background:#1A237E;padding:24px 32px;border-radius:12px 12px 0 0;">
                    <h2 style="color:#fff;margin:0;font-size:1.2rem;">Your store is ready!</h2>
                    <p style="color:#90CAF9;margin:4px 0 0;font-size:0.85rem;">Cloud Mobil POS</p>
                  </div>
                  <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                    <p style="color:#374151;margin:0 0 16px;">Hi {firstName},</p>
                    <p style="color:#374151;margin:0 0 8px;">Your <strong>{user.StoreName}</strong> store is now live on Cloud Mobil POS.</p>
                    <p style="color:#374151;margin:0 0 24px;">Plan: <strong>{planName}</strong> · {user.BillingCycle}</p>
                    <a href="{loginUrl}" style="display:inline-block;background:#00BFA5;color:#fff;font-weight:700;padding:12px 28px;border-radius:8px;text-decoration:none;font-size:0.95rem;">Open my store →</a>
                    <p style="color:#9ca3af;font-size:0.8rem;margin:24px 0 0;">If you have questions, reply to this email or contact our support team.</p>
                  </div>
                </div>
                """;

            var text = $"Hi {firstName},\n\nYour Cloud Mobil POS store is ready! Log in at: {loginUrl}\n\nPlan: {planName}\n\n— Cloud Mobil POS";

            var ok = await SendAsync(user.Email, firstName, $"Your Cloud Mobil POS store is ready — {user.StoreName}", html, text);
            if (ok) _logger.LogInformation("Welcome email sent to {Email}", user.Email);
            else    _logger.LogWarning("Failed to send welcome email to {Email}", user.Email);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", user.Email);
            return false;
        }
    }

    // ── Contact form helpers ─────────────────────────────────────────────────

    private static string BuildPlainText(ContactFormModel model) =>
        $"""
        New Demo Request — Cloud Mobil POS

        Name:          {model.Name}
        Email:         {model.Email}
        Company:       {model.Company}
        Phone:         {model.Phone ?? "N/A"}
        Business Type: {model.BusinessType}

        Message:
        {model.Message}
        """;

    private static string BuildHtmlBody(ContactFormModel model) =>
        $"""
        <div style="font-family:sans-serif;max-width:600px;margin:auto;border:1px solid #e0e0e0;border-radius:8px;overflow:hidden;">
          <div style="background:#1A237E;padding:24px 32px;">
            <h2 style="color:#fff;margin:0;">New Demo Request</h2>
            <p style="color:#90CAF9;margin:4px 0 0;">Cloud Mobil POS — Marketing Site</p>
          </div>
          <div style="padding:32px;">
            <table style="width:100%;border-collapse:collapse;">
              <tr><td style="padding:8px 0;color:#666;width:140px;">Name</td><td style="padding:8px 0;font-weight:600;">{model.Name}</td></tr>
              <tr><td style="padding:8px 0;color:#666;">Email</td><td style="padding:8px 0;font-weight:600;"><a href="mailto:{model.Email}">{model.Email}</a></td></tr>
              <tr><td style="padding:8px 0;color:#666;">Company</td><td style="padding:8px 0;font-weight:600;">{model.Company}</td></tr>
              <tr><td style="padding:8px 0;color:#666;">Phone</td><td style="padding:8px 0;">{model.Phone ?? "N/A"}</td></tr>
              <tr><td style="padding:8px 0;color:#666;">Business Type</td><td style="padding:8px 0;">{model.BusinessType}</td></tr>
            </table>
            <hr style="margin:24px 0;border:none;border-top:1px solid #eee;" />
            <p style="color:#666;margin:0 0 8px;">Message</p>
            <p style="background:#F5F7FA;padding:16px;border-radius:6px;margin:0;">{model.Message}</p>
          </div>
          <div style="background:#F5F7FA;padding:16px 32px;text-align:center;">
            <p style="color:#999;font-size:12px;margin:0;">Cloud Mobil POS · SaaS Platform · Reply directly to this email to respond to {model.Name}</p>
          </div>
        </div>
        """;
}
