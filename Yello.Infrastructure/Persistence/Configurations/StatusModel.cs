using Microsoft.EntityFrameworkCore;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Statuses in the model: the definitions, defaults and Project-owned alike, and every Project's delta. Named one by
/// one, for the reason <see cref="YelloDbContext"/> gives.
/// </summary>
internal static class StatusModel
{
    /// <summary>
    /// Applies the configurations.
    /// </summary>
    /// <param name="modelBuilder">The model.</param>
    /// <param name="context">The context whose request-scoped Space the query filters close over.</param>
    internal static void Apply(ModelBuilder modelBuilder, YelloDbContext context)
    {
        modelBuilder.ApplyConfiguration(new StatusDefinitionConfiguration(context));
        modelBuilder.ApplyConfiguration(new StatusDeltaOperationConfiguration(context));
    }
}
