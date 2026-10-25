using Microsoft.Extensions.DependencyInjection;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Registers <see cref="IKeyRingStore"/>.
/// </summary>
public static class KeyRingStoreServices
{
    /// <summary>
    /// Adds the key-ring store. The Host calls this when it persists its Data Protection keys.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddKeyRingStore(this IServiceCollection services)
    {
        services.AddSingleton<IKeyRingStore, KeyRingStore>();

        return services;
    }
}
