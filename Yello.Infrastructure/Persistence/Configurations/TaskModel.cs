using Microsoft.EntityFrameworkCore;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Tasks in the model: the Task table, the Labels and their applications, and each Task's description log. Named one
/// by one, for the reason <see cref="YelloDbContext"/> gives, and split out only to keep that class's dependencies within
/// S1200's limit.
/// </summary>
internal static class TaskModel
{
    /// <summary>
    /// Applies the configurations.
    /// </summary>
    /// <param name="modelBuilder">The model.</param>
    /// <param name="context">The context whose request-scoped Space the query filters close over.</param>
    internal static void Apply(ModelBuilder modelBuilder, YelloDbContext context)
    {
        modelBuilder.ApplyConfiguration(new TaskConfiguration(context));
        modelBuilder.ApplyConfiguration(new LabelConfiguration(context));
        modelBuilder.ApplyConfiguration(new TaskLabelConfiguration(context));
        modelBuilder.ApplyConfiguration(new TaskDescriptionChangeConfiguration(context));
    }
}