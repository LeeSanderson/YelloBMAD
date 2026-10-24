using Microsoft.Extensions.DependencyInjection;
using Yello.Application.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure;

/// <summary>
/// Registers NFR-8's presence bound and the refusal record's retention purge. Story 1.6.
/// </summary>
internal static class SpaceBoundaryLimits
{
    /// <summary>
    /// Adds the presence register and the retention purge.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// The presence register works on the request's own unit of work, in its transaction, so it is
    /// scoped with it. The purge and its schedule are singletons: the Host runs the purge off the
    /// request path, in a scope of its own, and the schedule holds a single instant - see
    /// <see cref="RefusalPurgeSchedule"/> for why that is not a cache.
    /// </remarks>
    internal static IServiceCollection AddSpaceBoundaryLimits(this IServiceCollection services)
    {
        services.AddScoped<ISpacePresenceRegister, SpacePresenceRegister>();
        services.AddSingleton<RefusalPurgeSchedule>();
        services.AddSingleton<IRefusalRetention, AccessRefusalPurge>();

        return services;
    }
}
