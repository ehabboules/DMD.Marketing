using DMD.Marketing.Tests.Helpers;

namespace DMD.Marketing.Tests.Services;

public class UserServiceTests
{
    // ── FindByEmailAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task FindByEmailAsync_ReturnsUser_WhenEmailExists()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "alice@example.com");
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.FindByEmailAsync("alice@example.com");

        Assert.NotNull(result);
        Assert.Equal("alice@example.com", result.Email);
    }

    [Fact]
    public async Task FindByEmailAsync_ReturnsNull_WhenEmailNotFound()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.FindByEmailAsync("nobody@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByEmailAsync_IsCaseInsensitive()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "alice@example.com");
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.FindByEmailAsync("ALICE@EXAMPLE.COM");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindByEmailAsync_TrimsWhitespace()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "alice@example.com");
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.FindByEmailAsync("  alice@example.com  ");

        Assert.NotNull(result);
    }

    // ── FindByIdAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task FindByIdAsync_ReturnsUser_WhenIdExists()
    {
        await using var db = DbHelper.CreateContext();
        var seeded = await DbHelper.SeedUserAsync(db);
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.FindByIdAsync(seeded.Id);

        Assert.NotNull(result);
        Assert.Equal(seeded.Id, result.Id);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenIdNotFound()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.FindByIdAsync(9999);

        Assert.Null(result);
    }

    // ── ValidateCredentialsAsync ────────────────────────────────────────────

    [Fact]
    public async Task ValidateCredentials_ReturnsUser_WithCorrectPassword()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "bob@example.com"); // default password: Test@1234
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.ValidateCredentialsAsync("bob@example.com", "Test@1234");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ValidateCredentials_ReturnsNull_WithWrongPassword()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "bob@example.com");
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.ValidateCredentialsAsync("bob@example.com", "WrongPassword!");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentials_ReturnsNull_WhenUserNotFound()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var result = await svc.ValidateCredentialsAsync("ghost@example.com", "any");

        Assert.Null(result);
    }

    // ── CreateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_CreatesUser_WithHashedPassword()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var (user, error) = await svc.CreateAsync("new@example.com", "Jane", "Doe", "Pass@1234");

        Assert.Null(error);
        Assert.NotNull(user);
        Assert.Equal("new@example.com", user.Email);
        Assert.NotEqual("Pass@1234", user.PasswordHash); // must be hashed
    }

    [Fact]
    public async Task CreateAsync_NormalizesEmail_ToLowercase()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var (user, _) = await svc.CreateAsync("JANE@Example.COM", "Jane", "Doe", "Pass@1234");

        Assert.Equal("jane@example.com", user!.Email);
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_OnDuplicateEmail()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "dup@example.com");
        var svc = DbHelper.CreateUserService(db);

        var (user, error) = await svc.CreateAsync("dup@example.com", "A", "B", "Pass@1234");

        Assert.Null(user);
        Assert.NotNull(error);
        Assert.Contains("already exists", error);
    }

    [Fact]
    public async Task CreateAsync_AssignsUserRole_WhenRoleExists()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserRoleAsync(db);
        var svc = DbHelper.CreateUserService(db);

        var (user, _) = await svc.CreateAsync("role@example.com", "A", "B", "Pass@1234");

        var userRoles = await db.UserRoles.Where(ur => ur.UserId == user!.Id).ToListAsync();
        Assert.Single(userRoles);
    }

    [Fact]
    public async Task CreateAsync_DoesNotFail_WhenNoUserRoleSeeded()
    {
        await using var db = DbHelper.CreateContext(); // no roles seeded
        var svc = DbHelper.CreateUserService(db);

        var (user, error) = await svc.CreateAsync("norole@example.com", "A", "B", "Pass@1234");

        Assert.Null(error);
        Assert.NotNull(user);
        Assert.Empty(await db.UserRoles.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_SetsTermsFields_WhenVersionProvided()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var (user, _) = await svc.CreateAsync("terms@example.com", "A", "B", "Pass@1234", "2026-05-25");

        Assert.Equal("2026-05-25", user!.TermsVersion);
        Assert.NotNull(user.TermsAcceptedAt);
    }

    [Fact]
    public async Task CreateAsync_LeavesTermsNull_WhenNoVersionProvided()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var (user, _) = await svc.CreateAsync("noterms@example.com", "A", "B", "Pass@1234");

        Assert.Null(user!.TermsVersion);
        Assert.Null(user.TermsAcceptedAt);
    }

    [Fact]
    public async Task CreateAsync_WritesAuditLog()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var (user, _) = await svc.CreateAsync("audit@example.com", "A", "B", "Pass@1234");

        var log = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "UserRegistered");
        Assert.NotNull(log);
        Assert.Equal(user!.Id, log.UserId);
    }

    [Fact]
    public async Task CreateAsync_CapturesClientIp_InAuditLog()
    {
        await using var db = DbHelper.CreateContext();
        var http = DbHelper.MockHttpContext("192.168.1.50");
        var svc = DbHelper.CreateUserService(db, http);

        await svc.CreateAsync("ip@example.com", "A", "B", "Pass@1234");

        var log = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "UserRegistered");
        Assert.Equal("192.168.1.50", log!.IpAddress);
    }

    // ── ChangePasswordAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_Succeeds_WithCorrectCurrentPassword()
    {
        await using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        var svc = DbHelper.CreateUserService(db);

        var (success, error) = await svc.ChangePasswordAsync(user, "Test@1234", "NewPass@5678");

        Assert.True(success);
        Assert.Null(error);
    }

    [Fact]
    public async Task ChangePassword_Fails_WithWrongCurrentPassword()
    {
        await using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        var svc = DbHelper.CreateUserService(db);

        var (success, error) = await svc.ChangePasswordAsync(user, "WrongPassword!", "NewPass@5678");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ChangePassword_UpdatesPasswordHash()
    {
        await using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        var oldHash = user.PasswordHash;
        var svc = DbHelper.CreateUserService(db);

        await svc.ChangePasswordAsync(user, "Test@1234", "NewPass@5678");

        Assert.NotEqual(oldHash, user.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_RotatesSecurityStamp()
    {
        await using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        var oldStamp = user.SecurityStamp;
        var svc = DbHelper.CreateUserService(db);

        await svc.ChangePasswordAsync(user, "Test@1234", "NewPass@5678");

        Assert.NotEqual(oldStamp, user.SecurityStamp);
    }

    [Fact]
    public async Task ChangePassword_WritesAuditLog()
    {
        await using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db);
        var svc = DbHelper.CreateUserService(db);

        await svc.ChangePasswordAsync(user, "Test@1234", "NewPass@5678");

        var log = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PasswordChanged");
        Assert.NotNull(log);
        Assert.Equal(user.Id, log.UserId);
    }

    // ── GeneratePasswordResetTokenAsync ────────────────────────────────────

    [Fact]
    public async Task GeneratePasswordResetToken_ReturnsToken_ForExistingUser()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "reset@example.com");
        var svc = DbHelper.CreateUserService(db);

        var (user, token) = await svc.GeneratePasswordResetTokenAsync("reset@example.com");

        Assert.NotNull(user);
        Assert.NotNull(token);
        Assert.Equal(64, token.Length);
    }

    [Fact]
    public async Task GeneratePasswordResetToken_ReturnsNulls_ForUnknownEmail()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        var (user, token) = await svc.GeneratePasswordResetTokenAsync("ghost@example.com");

        Assert.Null(user);
        Assert.Null(token);
    }

    [Fact]
    public async Task GeneratePasswordResetToken_SetsExpiry_OneHourFromNow()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "expiry@example.com");
        var svc = DbHelper.CreateUserService(db);

        var before = DateTime.UtcNow;
        var (user, _) = await svc.GeneratePasswordResetTokenAsync("expiry@example.com");
        var after = DateTime.UtcNow;

        Assert.NotNull(user!.PasswordResetTokenExpiry);
        Assert.InRange(user.PasswordResetTokenExpiry!.Value,
            before.AddMinutes(59),
            after.AddHours(1).AddSeconds(1));
    }

    [Fact]
    public async Task GeneratePasswordResetToken_WritesAuditLog()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "auditpw@example.com");
        var svc = DbHelper.CreateUserService(db);

        await svc.GeneratePasswordResetTokenAsync("auditpw@example.com");

        var log = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PasswordResetRequested");
        Assert.NotNull(log);
    }

    // ── ResetPasswordAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_Succeeds_WithValidToken()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "valid@example.com");
        var svc = DbHelper.CreateUserService(db);
        var (_, token) = await svc.GeneratePasswordResetTokenAsync("valid@example.com");

        var (success, error) = await svc.ResetPasswordAsync("valid@example.com", token!, "NewPass@99");

        Assert.True(success);
        Assert.Null(error);
    }

    [Fact]
    public async Task ResetPassword_Fails_WithWrongToken()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "wrongtoken@example.com");
        var svc = DbHelper.CreateUserService(db);
        await svc.GeneratePasswordResetTokenAsync("wrongtoken@example.com");

        var (success, error) = await svc.ResetPasswordAsync("wrongtoken@example.com", "not-the-right-token", "NewPass@99");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ResetPassword_Fails_WithExpiredToken()
    {
        await using var db = DbHelper.CreateContext();
        var user = await DbHelper.SeedUserAsync(db, "expired@example.com");
        var svc = DbHelper.CreateUserService(db);

        // Set token manually with past expiry
        user.PasswordResetToken       = "validtokenformat00000000000000000000000000000000000000000000000000";
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1); // expired 1 hour ago
        await db.SaveChangesAsync();

        var (success, error) = await svc.ResetPasswordAsync(
            "expired@example.com",
            "validtokenformat00000000000000000000000000000000000000000000000000",
            "NewPass@99");

        Assert.False(success);
        Assert.Contains("expired", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPassword_ClearsToken_AfterSuccess()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "clear@example.com");
        var svc = DbHelper.CreateUserService(db);
        var (_, token) = await svc.GeneratePasswordResetTokenAsync("clear@example.com");

        await svc.ResetPasswordAsync("clear@example.com", token!, "NewPass@99");

        var user = await svc.FindByEmailAsync("clear@example.com");
        Assert.Null(user!.PasswordResetToken);
        Assert.Null(user.PasswordResetTokenExpiry);
    }

    [Fact]
    public async Task ResetPassword_AllowsLoginWithNewPassword()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "newpw@example.com");
        var svc = DbHelper.CreateUserService(db);
        var (_, token) = await svc.GeneratePasswordResetTokenAsync("newpw@example.com");

        await svc.ResetPasswordAsync("newpw@example.com", token!, "BrandNew@99");

        var user = await svc.ValidateCredentialsAsync("newpw@example.com", "BrandNew@99");
        Assert.NotNull(user);
    }

    [Fact]
    public async Task ResetPassword_WritesAuditLog()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "auditres@example.com");
        var svc = DbHelper.CreateUserService(db);
        var (_, token) = await svc.GeneratePasswordResetTokenAsync("auditres@example.com");

        await svc.ResetPasswordAsync("auditres@example.com", token!, "NewPass@99");

        var log = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PasswordReset");
        Assert.NotNull(log);
    }

    // ── Role management ─────────────────────────────────────────────────────

    [Fact]
    public async Task AssignRole_AddsRole_WhenNotAlreadyAssigned()
    {
        await using var db = DbHelper.CreateContext();
        var roleId = await DbHelper.SeedUserRoleAsync(db);
        var user   = await DbHelper.SeedUserAsync(db);
        var svc    = DbHelper.CreateUserService(db);

        await svc.AssignRoleAsync(user.Id, roleId);

        var assigned = await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == roleId);
        Assert.True(assigned);
    }

    [Fact]
    public async Task AssignRole_IsIdempotent_WhenCalledTwice()
    {
        await using var db = DbHelper.CreateContext();
        var roleId = await DbHelper.SeedUserRoleAsync(db);
        var user   = await DbHelper.SeedUserAsync(db);
        var svc    = DbHelper.CreateUserService(db);

        await svc.AssignRoleAsync(user.Id, roleId);
        await svc.AssignRoleAsync(user.Id, roleId); // second call must not throw

        var count = await db.UserRoles.CountAsync(ur => ur.UserId == user.Id && ur.RoleId == roleId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RemoveRole_RemovesRole_WhenAssigned()
    {
        await using var db = DbHelper.CreateContext();
        var roleId = await DbHelper.SeedUserRoleAsync(db);
        var user   = await DbHelper.SeedUserAsync(db);
        var svc    = DbHelper.CreateUserService(db);

        await svc.AssignRoleAsync(user.Id, roleId);
        await svc.RemoveRoleAsync(user.Id, roleId);

        var assigned = await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == roleId);
        Assert.False(assigned);
    }

    [Fact]
    public async Task RemoveRole_DoesNotThrow_WhenRoleNotAssigned()
    {
        await using var db = DbHelper.CreateContext();
        var svc = DbHelper.CreateUserService(db);

        // Should complete without exception
        await svc.RemoveRoleAsync(999, Guid.NewGuid());
    }

    [Fact]
    public async Task GetAllRolesAsync_ReturnsOrderedByName()
    {
        await using var db = DbHelper.CreateContext();
        db.Roles.AddRange(
            new Role { Id = Guid.NewGuid(), Name = "Zebra",  CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.NewGuid(), Name = "Admin",  CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.NewGuid(), Name = "Manager",CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var svc = DbHelper.CreateUserService(db);

        var roles = await svc.GetAllRolesAsync();

        Assert.Equal(new[] { "Admin", "Manager", "Zebra" }, roles.Select(r => r.Name));
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsOnlyUserRoles()
    {
        await using var db = DbHelper.CreateContext();
        var roleId = await DbHelper.SeedUserRoleAsync(db);
        var user1  = await DbHelper.SeedUserAsync(db, "u1@example.com");
        var user2  = await DbHelper.SeedUserAsync(db, "u2@example.com");
        var svc    = DbHelper.CreateUserService(db);

        await svc.AssignRoleAsync(user1.Id, roleId);

        var roles = await svc.GetUserRolesAsync(user1.Id);
        Assert.Single(roles);

        var noRoles = await svc.GetUserRolesAsync(user2.Id);
        Assert.Empty(noRoles);
    }

    [Fact]
    public async Task GetAllUsersWithRolesAsync_ReturnsOrderedByEmail()
    {
        await using var db = DbHelper.CreateContext();
        await DbHelper.SeedUserAsync(db, "zzz@example.com");
        await DbHelper.SeedUserAsync(db, "aaa@example.com");
        var svc = DbHelper.CreateUserService(db);

        var users = await svc.GetAllUsersWithRolesAsync();

        Assert.Equal("aaa@example.com", users[0].Email);
        Assert.Equal("zzz@example.com", users[1].Email);
    }
}
