using DMD.Marketing.Data;
using DMD.Marketing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DMD.Marketing.Services;

public record AdminSummary(int Total, int Active, int Pending, int ExpiringSoon);

public class CreateUserModel
{
    public string    Email              { get; set; } = "";
    public string    Password           { get; set; } = "";
    public string    FirstName          { get; set; } = "";
    public string    LastName           { get; set; } = "";
    public string?   StoreName          { get; set; }
    public string?   StorePhone         { get; set; }
    public string?   StoreTimezone      { get; set; }
    public string?   BusinessType       { get; set; }
    public PlanSlug  SelectedPlan       { get; set; } = PlanSlug.Starter;
    public BillingCycle BillingCycle    { get; set; } = BillingCycle.Monthly;
    public string    Currency           { get; set; } = "CAD";
    public decimal   FederalTaxRate     { get; set; } = 5m;
    public decimal   ProvincialTaxRate  { get; set; } = 0m;
    public bool      TaxInclusive       { get; set; } = false;
    public ActivationStatus ActivationStatus { get; set; } = ActivationStatus.Active;
    public string?   AppUrl             { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
}

public class AdminClientService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ILogger<AdminClientService> _logger;

    public AdminClientService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IPasswordHasher<User> hasher,
        ILogger<AdminClientService> logger)
    {
        _dbFactory = dbFactory;
        _hasher    = hasher;
        _logger    = logger;
    }

    // ── Summary ──────────────────────────────────────────────────────
    public async Task<AdminSummary> GetSummaryAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var users = await db.Users.ToListAsync();

        return new AdminSummary(
            Total:       users.Count,
            Active:      users.Count(u => u.ActivationStatus == ActivationStatus.Active),
            Pending:     users.Count(u => u.ActivationStatus == ActivationStatus.Pending
                                      || (u.ActivationStatus == ActivationStatus.None && u.SelectedPlan != PlanSlug.None)),
            ExpiringSoon: users.Count(u => u.SubscriptionExpiresAt.HasValue
                                       && u.SubscriptionExpiresAt.Value > now
                                       && u.SubscriptionExpiresAt.Value <= now.AddDays(7))
        );
    }

    // ── Tables ───────────────────────────────────────────────────────
    public async Task<List<User>> GetPendingUsersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ActivationStatus == ActivationStatus.Pending
                     || (u.ActivationStatus == ActivationStatus.None && u.SelectedPlan != PlanSlug.None))
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<User>> GetActiveUsersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ActivationStatus == ActivationStatus.Active)
            .OrderBy(u => u.SubscriptionExpiresAt)
            .ToListAsync();
    }

    public async Task<List<User>> SearchUsersAsync(string query)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var q = query.Trim().ToLower();
        return await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.Email.ToLower().Contains(q)
                     || (u.FirstName != null && u.FirstName.ToLower().Contains(q))
                     || (u.LastName  != null && u.LastName.ToLower().Contains(q))
                     || (u.StoreName != null && u.StoreName.ToLower().Contains(q)))
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    // ── Detail tab data ──────────────────────────────────────────────
    public async Task<List<PaymentHistory>> GetPaymentHistoryAsync(int userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.PaymentHistory
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetUserAuditLogsAsync(int userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToListAsync();
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Roles.OrderBy(r => r.Name).ToListAsync();
    }

    // ── Write ─────────────────────────────────────────────────────────
    public async Task<bool> ActivateUserAsync(int userId, string appUrl, DateTime? expiresAt)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var user = await db.Users.FindAsync(userId);
            if (user is null) return false;

            user.ActivationStatus     = ActivationStatus.Active;
            user.AppUrl               = appUrl;
            user.SubscriptionExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(14);
            user.ModifiedAt           = DateTime.UtcNow;

            db.AuditLogs.Add(new AuditLog
            {
                UserId     = userId,
                Action     = "UserActivated",
                EntityType = "User",
                EntityId   = userId.ToString(),
                Detail     = $"appUrl={appUrl} expiresAt={user.SubscriptionExpiresAt:O}",
                CreatedAt  = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating user {UserId}", userId);
            return false;
        }
    }

    public async Task<(User? user, string? error)> CreateUserAsync(CreateUserModel model)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var existing = await db.Users.AnyAsync(u => u.Email == model.Email.Trim().ToLower());
            if (existing)
                return (null, "An account with this email already exists.");

            var user = new User
            {
                Email              = model.Email.Trim().ToLower(),
                FirstName          = model.FirstName.Trim(),
                LastName           = model.LastName.Trim(),
                StoreName          = model.StoreName?.Trim(),
                StorePhone         = model.StorePhone?.Trim(),
                StoreTimezone      = model.StoreTimezone,
                BusinessType       = model.BusinessType,
                SelectedPlan       = model.SelectedPlan,
                BillingCycle       = model.BillingCycle,
                Currency           = model.Currency,
                FederalTaxRate     = model.FederalTaxRate,
                ProvincialTaxRate  = model.ProvincialTaxRate,
                TaxInclusive       = model.TaxInclusive,
                ActivationStatus   = model.ActivationStatus,
                AppUrl             = model.AppUrl,
                SubscriptionExpiresAt = model.SubscriptionExpiresAt ?? DateTime.UtcNow.AddDays(14),
                IsActive           = true,
                SecurityStamp      = Guid.NewGuid().ToString("N"),
                CreatedAt          = DateTime.UtcNow,
                PlanSelectedAt     = DateTime.UtcNow,
                MustChangePassword = true,
            };
            user.PasswordHash = _hasher.HashPassword(user, model.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Assign "User" role
            var userRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole is not null)
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id, CreatedAt = DateTime.UtcNow });

            db.AuditLogs.Add(new AuditLog
            {
                UserId     = user.Id,
                Action     = "UserCreatedByAdmin",
                EntityType = "User",
                EntityId   = user.Id.ToString(),
                Detail     = $"email={user.Email} plan={user.SelectedPlan} status={user.ActivationStatus}",
                CreatedAt  = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
            _logger.LogInformation("AdminClientService: created user {UserId} ({Email})", user.Id, user.Email);
            return (user, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email}", model.Email);
            return (null, ex.Message);
        }
    }

    public async Task<bool> ExtendSubscriptionAsync(int userId, DateTime newExpiry)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var user = await db.Users.FindAsync(userId);
            if (user is null) return false;

            var old = user.SubscriptionExpiresAt;
            user.SubscriptionExpiresAt = newExpiry;
            user.ModifiedAt            = DateTime.UtcNow;

            db.AuditLogs.Add(new AuditLog
            {
                UserId     = userId,
                Action     = "SubscriptionExtended",
                EntityType = "User",
                EntityId   = userId.ToString(),
                Detail     = $"from={old:O} to={newExpiry:O}",
                CreatedAt  = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending subscription for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> AssignRoleAsync(int userId, Guid roleId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var exists = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            if (exists) return true;

            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId}", roleId, userId);
            return false;
        }
    }

    public async Task<bool> RemoveRoleAsync(int userId, Guid roleId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var ur = await db.UserRoles.FindAsync(userId, roleId);
            if (ur is null) return true;

            db.UserRoles.Remove(ur);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", roleId, userId);
            return false;
        }
    }
}
