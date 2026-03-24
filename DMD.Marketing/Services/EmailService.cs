using DMD.Marketing.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace DMD.Marketing.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendDemoRequestAsync(ContactFormModel model)
    {
        try
        {
            var apiKey = _config["SendGrid:ApiKey"];
            var toEmail = _config["SendGrid:ToEmail"];
            var fromEmail = _config["SendGrid:FromEmail"];
            var fromName = _config["SendGrid:FromName"];

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(toEmail))
            {
                _logger.LogWarning("SendGrid configuration is missing.");
                return false;
            }

            var client = new SendGridClient(apiKey);

            var msg = new SendGridMessage
            {
                From = new EmailAddress(fromEmail, fromName),
                Subject = $"🛒 Demo Request — {model.Company}",
                PlainTextContent = BuildPlainText(model),
                HtmlContent = BuildHtmlBody(model)
            };

            msg.AddTo(new EmailAddress(toEmail));

            // Reply-To the prospect directly
            msg.ReplyTo = new EmailAddress(model.Email, model.Name);

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
            {
                _logger.LogInformation("Demo request email sent for {Company}", model.Company);
                return true;
            }

            _logger.LogWarning("SendGrid returned {StatusCode}", response.StatusCode);
            return false;
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
            var apiKey    = _config["SendGrid:ApiKey"];
            var fromEmail = _config["SendGrid:FromEmail"];
            var fromName  = _config["SendGrid:FromName"];

            if (string.IsNullOrEmpty(apiKey)) { _logger.LogWarning("SendGrid API key missing."); return false; }

            var client = new SendGridClient(apiKey);
            var msg = new SendGridMessage
            {
                From             = new EmailAddress(fromEmail, fromName),
                Subject          = "Reset your DMD Inventory password",
                PlainTextContent = $"Hi {firstName},\n\nClick the link below to reset your password (expires in 1 hour):\n{resetUrl}\n\nIf you didn't request this, ignore this email.\n\n— DMD Tech",
                HtmlContent      = $"""
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
                    """
            };
            msg.AddTo(new EmailAddress(toEmail));

            var response = await client.SendEmailAsync(msg);
            return (int)response.StatusCode is >= 200 and < 300;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            return false;
        }
    }

    private static string BuildPlainText(ContactFormModel model) =>
        $"""
        New Demo Request — DMD Inventory

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
            <p style="color:#90CAF9;margin:4px 0 0;">DMD Inventory — Marketing Site</p>
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
            <p style="color:#999;font-size:12px;margin:0;">DMD Inventory · SaaS Platform · Reply directly to this email to respond to {model.Name}</p>
          </div>
        </div>
        """;
}
