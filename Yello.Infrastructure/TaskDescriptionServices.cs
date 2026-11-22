using Microsoft.Extensions.DependencyInjection;
using Yello.Domain.Tasks;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure;

/// <summary>
/// Registers the merge adapter behind a Task's description.
/// </summary>
internal static class TaskDescriptionServices
{
    /// <summary>
    /// Adds the description store, on the request's unit of work like every Space-scoped store. It consumes the merge port
    /// the Host registers and registers none of its own.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddTaskDescriptionLog(this IServiceCollection services) =>
        services.AddScoped<ITaskDescriptionLog, TaskDescriptionStore>();
}
