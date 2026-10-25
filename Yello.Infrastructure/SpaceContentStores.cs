using Microsoft.Extensions.DependencyInjection;
using Yello.Domain.Projects;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure;

/// <summary>
/// Registers the stores behind a Space's own content. Story 2.1 onwards.
/// </summary>
internal static class SpaceContentStores
{
    /// <summary>
    /// Adds the Project store.
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

        return services;
    }
}
