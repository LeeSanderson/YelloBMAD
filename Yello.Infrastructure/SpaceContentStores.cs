using Microsoft.Extensions.DependencyInjection;
using Yello.Domain.Boards;
using Yello.Domain.Labels;
using Yello.Domain.Lists;
using Yello.Domain.Projects;
using Yello.Domain.Tasks;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure;

/// <summary>
/// Registers the stores behind a Space's own content. Story 2.1 onwards.
/// </summary>
internal static class SpaceContentStores
{
    /// <summary>
    /// Adds the Project, Task and Label stores, the Board reader and the List reader (story 2.10).
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Scoped with the request's unit of work: every Space-scoped store runs in the transaction the
    /// pipeline opened, under the Space it resolved.
    /// </remarks>
    internal static IServiceCollection AddSpaceContentStores(this IServiceCollection services)
    {
        services.AddScoped<IProjectStore, ProjectStore>();
        services.AddScoped<ITaskStore, TaskStore>();
        services.AddScoped<IBoardReader, BoardReader>();
        services.AddScoped<IListReader, ListReader>();
        services.AddScoped<ILabelStore, LabelStore>();

        return services;
    }
}
