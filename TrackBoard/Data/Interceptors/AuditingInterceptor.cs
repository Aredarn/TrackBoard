using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TrackBoard.Entities;

namespace TrackBoard.Data.Interceptors;

/// <summary>
/// Stamps <see cref="BaseEntity.CreatedAt"/> and <see cref="BaseEntity.UpdatedAt"/> on save.
/// This is the .NET equivalent of Spring Data's auditing support: callers never set
/// these fields, so they cannot be spoofed from a request body.
/// </summary>
public sealed class AuditingInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    // Guard against a detached-then-attached entity resetting its own origin.
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    break;
            }
        }
    }
}
