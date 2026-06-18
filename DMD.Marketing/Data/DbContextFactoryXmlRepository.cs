using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DMD.Marketing.Data;

/// <summary>
/// DataProtection IXmlRepository backed by IDbContextFactory so DataProtection
/// keys survive container restarts without coupling to a scoped DbContext lifetime.
/// </summary>
internal sealed class DbContextFactoryXmlRepository : IXmlRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DbContextFactoryXmlRepository(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory  = scope.ServiceProvider
                              .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        using var db   = dbFactory.CreateDbContext();
        return db.DataProtectionKeys
                 .AsNoTracking()
                 .Where(k => k.Xml != null)
                 .Select(k => k.Xml!)
                 .ToList()
                 .ConvertAll(XElement.Parse);
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory  = scope.ServiceProvider
                              .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        using var db   = dbFactory.CreateDbContext();
        db.DataProtectionKeys.Add(new DataProtectionKey
        {
            FriendlyName = friendlyName,
            Xml          = element.ToString(SaveOptions.DisableFormatting)
        });
        db.SaveChanges();
    }
}
