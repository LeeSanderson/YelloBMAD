using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Spaces;
using Yello.Infrastructure.Identity;
using Yello.Infrastructure.Localisation;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure;

/// <summary>
/// Registers every adapter behind a <c>Yello.Domain</c> port. The composition root calls this
/// once and names no implementation itself.
/// </summary>
/// <remarks>
/// The registrations live here rather than in <c>Yello.Host</c> so the implementations can stay
/// <c>internal</c>. A Host that had to name <c>AccountRegistrationStore</c> would need it public,
/// and a public adapter is one a later story can reach past its port.
/// </remarks>
public static class InfrastructureServices
{
    /// <summary>
    /// Adds the database, the password hasher, the identifier generator and the localised
    /// registration copy.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="connectionString">
    /// The SQL Server connection, injected by Aspire under the resource name
    /// <c>Directory.Build.props</c> owns. Nullable, and passed through as-is: a Host started
    /// outside Aspire has none, and AR-33's shape is that the process starts anyway and reports
    /// the problem rather than refusing to boot. EF Core accepts a null connection string at
    /// configuration time and fails when the context is first used, which puts the error at the
    /// request that needed a database rather than at startup.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddYelloInfrastructure(
        this IServiceCollection services,
        string? connectionString)
    {
        // No migration is applied here or anywhere at startup. AR-36 makes applying them an
        // explicit deploy step, which is story 1.10's.
        services.AddDbContext<YelloDbContext>(options => options.UseSqlServer(connectionString));

        // Singleton: the generator holds a counter that has to advance across calls, which is
        // the whole basis of the sequence. Registered per-request it would restart from the
        // clock on every request and lose the ordering the clustered index is built on.
        services.AddSingleton<IIdentifierGenerator, SequentialGuidIdentifierGenerator>();

        // AD-1: Identity, for password hashing, and nothing else. No AddIdentity, no
        // AddIdentityCore, no IdentityDbContext, no UserManager<>, no SignInManager<> - and by
        // Gate C's construction, no role surface of any kind. Story 1.4 adds authentication.
        services.Configure<PasswordHasherOptions>(options =>
        {
            // Explicit, so NFR-6's work factor is a decision rather than an inherited default.
            options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            options.IterationCount = PasswordWorkFactor.IterationCount;
        });

        services.AddSingleton<IPasswordHasher<HashedCredential>, PasswordHasher<HashedCredential>>();
        services.AddSingleton<Domain.Accounts.IPasswordHasher, IdentityPasswordHasher>();

        services.AddLocalization();
        services.AddScoped<IPersonalSpaceNaming, ResourcePersonalSpaceNaming>();

        services.AddScoped<IAccountRegistrationStore, AccountRegistrationStore>();

        return services;
    }
}
