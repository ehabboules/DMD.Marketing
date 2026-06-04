using Microsoft.EntityFrameworkCore;

namespace DMD.Marketing.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<User>           Users          => Set<User>();
    public DbSet<Role>           Roles          => Set<Role>();
    public DbSet<UserRole>       UserRoles      => Set<UserRole>();
    public DbSet<PaymentHistory> PaymentHistory => Set<PaymentHistory>();
    public DbSet<AuditLog>       AuditLogs      => Set<AuditLog>();

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

        // ── PaymentHistory ───────────────────────────────────────────
        builder.Entity<PaymentHistory>(e =>
        {
            e.Property(p => p.Id).UseIdentityColumn();
            e.HasOne(p => p.User)
             .WithMany()
             .HasForeignKey(p => p.UserId);
        });

        // ── AuditLogs ────────────────────────────────────────────────
        builder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Id).UseIdentityColumn();
            e.HasIndex(a => a.UserId).HasDatabaseName("IX_AuditLogs_UserId");
            e.HasIndex(a => a.CreatedAt).HasDatabaseName("IX_AuditLogs_CreatedAt");
            e.HasOne<User>()
             .WithMany()
             .HasForeignKey(a => a.UserId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── OpenIddict entity sets ─────────────────────────────────
        builder.UseOpenIddict();
    }
}
