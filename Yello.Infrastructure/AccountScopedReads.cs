using Microsoft.Extensions.DependencyInjection;
using Yello.Application.ApiTokens;
using Yello.Application.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure;

/// <summary>
/// Registers story 1.7's reads: the Account-scoped context and the Space switcher's directory
/// (AD-24), and the active Space's name for the context bar.
/// </summary>
internal static class AccountScopedReads
{
    /// <summary>
    /// Adds the Account scope, the directory and the name reader.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Every one is SCOPED, for the reason <see cref="SpaceScope"/> is: the Account the
    /// <c>Membership</c> filter admits must be this request's and no other's, and the directory - the
    /// only thing that narrows it - works on this request's unit of work.
    /// </remarks>
    internal static IServiceCollection AddAccountScopedReads(this IServiceCollection services)
    {
        services.AddScoped<AccountScope>();
        services.AddScoped<AccountSpaceDirectory>();
        services.AddScoped<IAccountSpaceDirectory>(provider => provider.GetRequiredService<AccountSpaceDirectory>());
        services.AddScoped<IAccountApiTokenDirectory>(provider => provider.GetRequiredService<AccountSpaceDirectory>());
        services.AddScoped<IActiveSpaceNameReader, ActiveSpaceNameReader>();

        return services;
    }
}
