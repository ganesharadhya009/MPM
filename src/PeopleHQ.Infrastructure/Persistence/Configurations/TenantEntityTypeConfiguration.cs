using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Common;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

/// <summary>
/// Applies the tenant global query filter to EVERY entity implementing
/// ITenantOwned, automatically, via reflection — the safety net described in
/// 00-overview.md §4: "a missing WHERE clause can never leak data across
/// tenants". Entity-specific config classes (indexes, precision, unique
/// constraints — see the per-module *Configuration.cs files) never need to
/// touch the filter themselves; this guarantees it even for an entity that
/// has no dedicated configuration class at all. Called once from
/// AppDbContext.OnModelCreating, after ApplyConfigurationsFromAssembly.
/// </summary>
public static class TenantQueryFilterApplier
{
    private static readonly MethodInfo ApplyFilterMethod =
        typeof(TenantQueryFilterApplier).GetMethod(nameof(ApplyFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static void ApplyToAllTenantOwnedEntities(ModelBuilder modelBuilder, ITenantContext tenantContext)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.GetQueryFilter() is not null) continue; // an explicit config already set one — respect it

            ApplyFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(null, new object[] { modelBuilder, tenantContext });
        }
    }

    private static void ApplyFilter<TEntity>(ModelBuilder modelBuilder, ITenantContext tenantContext)
        where TEntity : class, ITenantOwned
    {
        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                e.TenantId == tenantContext.TenantId &&
                EF.Property<bool>(e, nameof(ISoftDeletable.IsDeleted)) == false);
        }
        else
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        }
    }
}
