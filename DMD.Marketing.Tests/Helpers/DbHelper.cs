using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DMD.Marketing.Tests.Helpers;

/// <summary>Creates a fresh InMemory ApplicationDbContext per test.</summary>
public static class DbHelper
{
    public static ApplicationDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>Seeds a "User" role and returns its ID.</summary>
    public static async Task<Guid> SeedUserRoleAsync(ApplicationDbContext db)
    {
        var role = new Role
        {
            Id        = Guid.NewGuid(),
            Name      = "User",
            CreatedAt = DateTime.UtcNow,
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    /// <summary>Creates a user directly in the DB (bypassing UserService).</summary>
    public static async Task<User> SeedUserAsync(ApplicationDbContext db,
        string email = "test@example.com",
        string? passwordHash = null,
        ActivationStatus status = ActivationStatus.None)
    {
        var hasher = new PasswordHasher<User>();
        var user = new User
        {
            Email            = email,
            FirstName        = "Test",
            LastName         = "User",
            IsActive         = true,
            SecurityStamp    = Guid.NewGuid().ToString("N"),
            CreatedAt        = DateTime.UtcNow,
            ActivationStatus = status,
        };
        user.PasswordHash = passwordHash ?? hasher.HashPassword(user, "Test@1234");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Returns a mock IHttpContextAccessor that returns the given IP.</summary>
    public static Mock<IHttpContextAccessor> MockHttpContext(string ip = "127.0.0.1")
    {
        var connMock = new Mock<ConnectionInfo>();
        connMock.Setup(c => c.RemoteIpAddress)
                .Returns(System.Net.IPAddress.Parse(ip));

        var httpCtxMock = new Mock<HttpContext>();
        httpCtxMock.Setup(h => h.Connection).Returns(connMock.Object);

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpCtxMock.Object);
        return accessorMock;
    }

    /// <summary>Builds a UserService backed by the given context.</summary>
    public static UserService CreateUserService(ApplicationDbContext db,
        Mock<IHttpContextAccessor>? httpAccessor = null)
    {
        return new UserService(
            db,
            new PasswordHasher<User>(),
            (httpAccessor ?? MockHttpContext()).Object,
            NullLogger<UserService>.Instance);
    }
}
