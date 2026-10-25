using Microsoft.Extensions.DependencyInjection;
using Yello.Application.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure;

/// <summary>
/// Registers NFR-8's presence bound and the refusal record's retention purge (story 1.6), and the
/// pipeline's creation-bound count and idempotency record (story 2.1).
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

        // Story 2.1: the creation bound's count and the Idempotency-Key record - the pipeline's two
        // other ports, on the request's own unit of work, so scoped with it.
        services.AddScoped<IBoundCounter, BoundCounter>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddSingleton<RefusalPurgeSchedule>();
        services.AddSingleton<IRefusalRetention, AccessRefusalPurge>();

        return services;
    }
}
