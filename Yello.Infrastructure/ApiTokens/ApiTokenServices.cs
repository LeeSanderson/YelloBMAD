using Microsoft.Extensions.DependencyInjection;
using Yello.Domain.ApiTokens;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure.ApiTokens;

/// <summary>
/// Registers the API Token store. Story 1.8.
/// </summary>
internal static class ApiTokenServices
{
    /// <summary>
    /// Adds <see cref="IApiTokenStore"/>. Scoped with the request's unit of work, and never cached: a
    /// Token whose row has gone is refused on the very next request (AC8).
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddApiTokenStore(this IServiceCollection services) =>
        services.AddScoped<IApiTokenStore, ApiTokenStore>();
}
