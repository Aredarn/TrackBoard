using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TrackBoard.Tests.Integration;

/// <summary>
/// SQLite has no native <see cref="DateTimeOffset"/>, and EF refuses to translate an
/// <c>ORDER BY</c> or comparison on one. This stores them as a sortable integer, for the test
/// database only — production PostgreSQL keeps <c>timestamp with time zone</c>.
/// </summary>
/// <remarks>
/// The binary form sorts by local ticks rather than UTC, so ordering is only exact when all
/// values share an offset. Every test timestamp is UTC, so that holds here.
/// </remarks>
public sealed class SqliteDateTimeOffsetModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                }
            }
        }
    }
}
