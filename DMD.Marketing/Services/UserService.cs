using DMD.Marketing.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DMD.Marketing.Services;

public class UserService
{
    private readonly ApplicationDbContext    _db;
    private readonly IPasswordHasher<User>   _hasher;

    public UserService(ApplicationDbContext db, IPasswordHasher<User> hasher)
    {
        _db     = db;
        _hasher = hasher;
    }

    // ── Lookup ─────────────────────────────────────────────────────────
    public Task<User?> FindByEmailAsync(string email) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower());

    public Task<User?> FindByIdAsync(int id) =>
        _db.Users.FindAsync(id).AsTask();

    // ── Validate credentials ──────────────────────────────────────────
    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        var user = await FindByEmailAsync(email);
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }

    // ── Register ──────────────────────────────────────────────────────
    public async Task<(User? User, string? Error)> CreateAsync(
        string email, string firstName, string lastName, string password)
    {
        if (await FindByEmailAsync(email) is not null)
            return (null, "An account with this email already exists.");

        var user = new User
        {
            Email         = email.Trim().ToLower(),
            FirstName     = firstName.Trim(),
            LastName      = lastName.Trim(),
            IsActive      = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt     = DateTime.UtcNow,
        };
        user.PasswordHash = _hasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Assign default "User" role
        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "User");
        if (userRole is not null)
        {
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();
        }

        return (user, null);
    }

    // ── Change password (requires current password) ────────────────────
    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        User user, string currentPassword, string newPassword)
    {
        var check = _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (check == PasswordVerificationResult.Failed)
            return (false, "Current password is incorrect.");

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ModifiedAt    = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    // ── Forgot password: generate & store token ────────────────────────
    public async Task<(User? User, string? Token)> GeneratePasswordResetTokenAsync(string email)
    {
        var user = await FindByEmailAsync(email);
        if (user is null) return (null, null);   // don't reveal non-existence to callers

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); // 64 hex chars
        user.PasswordResetToken       = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        user.ModifiedAt               = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (user, token);
    }

    // ── Reset password using token ─────────────────────────────────────
    public async Task<(bool Success, string? Error)> ResetPasswordAsync(
        string email, string token, string newPassword)
    {
        var user = await FindByEmailAsync(email);
        if (user is null || user.PasswordResetToken != token)
            return (false, "Invalid or expired reset link.");

        if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
            return (false, "This reset link has expired. Please request a new one.");

        user.PasswordHash             = _hasher.HashPassword(user, newPassword);
        user.PasswordResetToken       = null;
        user.PasswordResetTokenExpiry = null;
        user.SecurityStamp            = Guid.NewGuid().ToString("N");
        user.MustChangePassword       = false;
        user.ModifiedAt               = DateTime.UtcNow;

        await _db.SaveChangesAsync();
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
        }
    }

    public async Task RemoveRoleAsync(int userId, Guid roleId)
    {
        var ur = await _db.UserRoles.FindAsync(userId, roleId);
        if (ur is not null)
        {
            _db.UserRoles.Remove(ur);
            await _db.SaveChangesAsync();
        }
    }
}
