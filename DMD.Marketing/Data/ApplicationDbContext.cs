using Microsoft.EntityFrameworkCore;

namespace DMD.Marketing.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<User>     Users     => Set<User>();
    public DbSet<Role>     Roles     => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Users ─────────────────────────────────────────────────
        builder.Entity<User>(e =>
        {
            e.Property(u => u.Id).UseIdentityColumn();
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ── Roles ─────────────────────────────────────────────────
        builder.Entity<Role>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
        });

        // ── UserRoles ─────────────────────────────────────────────
        builder.Entity<UserRole>(e =>
        {
            e.HasKey(ur => new { ur.UserId, ur.RoleId });

            e.HasIndex(ur => ur.RoleId)
             .HasDatabaseName("IX_UserRoles_RoleId");

            e.HasOne(ur => ur.User)
             .WithMany(u => u.UserRoles)
             .HasForeignKey(ur => ur.UserId);

            e.HasOne(ur => ur.Role)
             .WithMany(r => r.UserRoles)
             .HasForeignKey(ur => ur.RoleId);
        });

        // ── OpenIddict entity sets ─────────────────────────────────
        builder.UseOpenIddict();
    }
}
