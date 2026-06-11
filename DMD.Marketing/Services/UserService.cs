using DMD.Marketing.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DMD.Marketing.Services;

public class UserService
{
    private readonly ApplicationDbContext    _db;
    private readonly IPasswordHasher<User>   _hasher;
    private readonly IHttpContextAccessor    _http;
    private readonly ILogger<UserService>    _logger;

    public UserService(ApplicationDbContext db, IPasswordHasher<User> hasher,
                       IHttpContextAccessor http, ILogger<UserService> logger)
    {
        _db     = db;
        _hasher = hasher;
        _http   = http;
        _logger = logger;
    }

    private string? ClientIp =>
        _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private void Audit(string action, int? userId = null,
                       string? entityType = null, string? entityId = null,
                       string? detail = null) =>
        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = userId,
            Action     = action,
            EntityType = entityType,
            EntityId   = entityId,
            Detail     = detail,
            IpAddress  = ClientIp,
            CreatedAt  = DateTime.UtcNow,
        });

    // ── Lookup ─────────────────────────────────────────────────────────
    public Task<User?> FindByEmailAsync(string email) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower());

    public Task<User?> FindByIdAsync(int id) =>
        _db.Users.FindAsync(id).AsTask();

    // ── Validate credentials ──────────────────────────────────────────
    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        _logger.LogDebug("ValidateCredentials: {Email}", email);
        var user = await FindByEmailAsync(email);
        if (user is null)
        {
            _logger.LogWarning("ValidateCredentials: no account found for {Email}", email);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("ValidateCredentials: wrong password for {Email}", email);
            return null;
        }

        _logger.LogInformation("ValidateCredentials: success for user {UserId}", user.Id);
        return user;
    }

    // ── Register ──────────────────────────────────────────────────────
    public async Task<(User? User, string? Error)> CreateAsync(
        string email, string firstName, string lastName, string password,
        string? termsVersion = null)
    {
        _logger.LogInformation("CreateUser: {Email}", email);

        if (await FindByEmailAsync(email) is not null)
        {
            _logger.LogWarning("CreateUser: duplicate email {Email}", email);
            return (null, "An account with this email already exists.");
        }

        var user = new User
        {
            Email            = email.Trim().ToLower(),
            FirstName        = firstName.Trim(),
            LastName         = lastName.Trim(),
            IsActive         = true,
            SecurityStamp    = Guid.NewGuid().ToString("N"),
            CreatedAt        = DateTime.UtcNow,
            TermsAcceptedAt  = termsVersion is not null ? DateTime.UtcNow : null,
            TermsVersion     = termsVersion,
        };
        user.PasswordHash = _hasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Assign default "User" role + audit log in one save
        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "User");
        if (userRole is not null)
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id, CreatedAt = DateTime.UtcNow });

        Audit("UserRegistered", userId: user.Id, entityType: "User", entityId: user.Id.ToString(),
              detail: $"email={user.Email} termsVersion={termsVersion}");
        await _db.SaveChangesAsync();

        _logger.LogInformation("CreateUser: registered user {UserId} ({Email})", user.Id, user.Email);
        return (user, null);
    }

    // ── Change password (requires current password) ────────────────────
    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        User user, string currentPassword, string newPassword)
    {
        _logger.LogInformation("ChangePassword: user {UserId}", user.Id);

        var check = _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (check == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("ChangePassword: wrong current password for user {UserId}", user.Id);
            return (false, "Current password is incorrect.");
        }

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ModifiedAt    = DateTime.UtcNow;

        Audit("PasswordChanged", userId: user.Id, entityType: "User", entityId: user.Id.ToString());
        await _db.SaveChangesAsync();

        _logger.LogInformation("ChangePassword: success for user {UserId}", user.Id);
        return (true, null);
    }

    // ── Forgot password: generate & store token ────────────────────────
    public async Task<(User? User, string? Token)> GeneratePasswordResetTokenAsync(string email)
    {
        _logger.LogInformation("GeneratePasswordResetToken: {Email}", email);

        var user = await FindByEmailAsync(email);
        if (user is null)
        {
            _logger.LogWarning("GeneratePasswordResetToken: no account for {Email}", email);
            return (null, null);   // don't reveal non-existence to callers
        }

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); // 64 hex chars
        user.PasswordResetToken       = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        user.ModifiedAt               = DateTime.UtcNow;

        Audit("PasswordResetRequested", userId: user.Id, entityType: "User", entityId: user.Id.ToString());
        await _db.SaveChangesAsync();

        _logger.LogInformation("GeneratePasswordResetToken: token issued for user {UserId}", user.Id);
        return (user, token);
    }

    // ── Reset password using token ─────────────────────────────────────
    public async Task<(bool Success, string? Error)> ResetPasswordAsync(
        string email, string token, string newPassword)
    {
        _logger.LogInformation("ResetPassword: {Email}", email);

        var user = await FindByEmailAsync(email);
        if (user is null || user.PasswordResetToken != token)
        {
            _logger.LogWarning("ResetPassword: invalid token for {Email}", email);
            return (false, "Invalid or expired reset link.");
        }

        if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("ResetPassword: expired token for user {UserId}", user.Id);
            return (false, "This reset link has expired. Please request a new one.");
        }

        user.PasswordHash             = _hasher.HashPassword(user, newPassword);
        user.PasswordResetToken       = null;
        user.PasswordResetTokenExpiry = null;
        user.SecurityStamp            = Guid.NewGuid().ToString("N");
        user.MustChangePassword       = false;
        user.ModifiedAt               = DateTime.UtcNow;

        Audit("PasswordReset", userId: user.Id, entityType: "User", entityId: user.Id.ToString());
        await _db.SaveChangesAsync();

        _logger.LogInformation("ResetPassword: success for user {UserId}", user.Id);
        return (true, null);
    }

    // ── Role management ───────────────────────────────────────────────
    public Task<List<Role>> GetAllRolesAsync() =>
        _db.Roles.OrderBy(r => r.Name).ToListAsync();

    public Task<List<User>> GetAllUsersWithRolesAsync() =>
        _db.Users
           .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
           .OrderBy(u => u.Email)
           .ToListAsync();

    public Task<List<UserRole>> GetUserRolesAsync(int userId) =>
        _db.UserRoles.Include(ur => ur.Role)
           .Where(ur => ur.UserId == userId)
           .ToListAsync();

    public async Task AssignRoleAsync(int userId, Guid roleId)
    {
        var exists = await _db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (!exists)
        {
            _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();
            _logger.LogInformation("AssignRole: role {RoleId} assigned to user {UserId}", roleId, userId);
        }
    }

    public async Task RemoveRoleAsync(int userId, Guid roleId)
    {
        var ur = await _db.UserRoles.FindAsync(userId, roleId);
        if (ur is not null)
        {
            _db.UserRoles.Remove(ur);
            await _db.SaveChangesAsync();
            _logger.LogInformation("RemoveRole: role {RoleId} removed from user {UserId}", roleId, userId);
        }
    }
}
