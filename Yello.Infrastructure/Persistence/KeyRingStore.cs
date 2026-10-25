using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yello.Domain;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// <see cref="IKeyRingStore"/> over <see cref="YelloDbContext"/>, a context of its own per call.
/// </summary>
/// <remarks>
/// <b>Synchronous, because the framework's contract is.</b> It is called on the first protect or
/// unprotect of a process and when a key is created or the ring is refreshed - on a request, never on a
/// timer (AD-10). A singleton, so each call builds its own scope rather than borrowing a request's.
/// </remarks>
/// <param name="scopes">Creates the scope each call's context lives in.</param>
/// <param name="identifiers">The application-side key generator every table here uses.</param>
internal sealed class KeyRingStore(IServiceScopeFactory scopes, IIdentifierGenerator identifiers) : IKeyRingStore
{
    /// <inheritdoc />
    public IReadOnlyList<string> ReadAll()
    {
        using var scope = scopes.CreateScope();
        using var context = new YelloDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<YelloDbContext>>());

        return context.Set<DataProtectionKey>()
            .AsNoTracking()
            .Select(key => key.Xml)
            .ToList();
    }

    /// <inheritdoc />
    public void Add(string? friendlyName, string xml)
    {
        using var scope = scopes.CreateScope();
        using var context = new YelloDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<YelloDbContext>>());

        context.Set<DataProtectionKey>().Add(new DataProtectionKey
        {
            Id = identifiers.Next(),
            FriendlyName = friendlyName,
            Xml = xml,
        });

        context.SaveChanges();
    }
}
