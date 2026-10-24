using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yello.Application.Spaces;
using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Spaces;
using Yello.Domain.Statuses;
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
        // Gate C's construction, no role surface of any kind.
        //
        // Story 1.4 added authentication and did not change that list. The Session is a plain
        // cookie-authentication ticket configured once in Yello.Host's composition root
        // (SessionCookiePolicy), not Identity's cookie stack: AddIdentityCookies would have
        // brought a security-stamp validator with a per-request revalidation interval that AD-10
        // forbids, and MapIdentityApi would have forked Account creation away from AD-22's single
        // slice. What this story did add here is the Verify half of the hasher below.
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
        services.AddScoped<IAccountCredentialStore, AccountCredentialStore>();

        // SPACE RESOLUTION, FROM STORY 1.5. Every registration below is SCOPED, and that is AC7 and
        // NFR-2 rather than a default: the Role on an ActiveSpaceContext is the value the Membership
        // row held when this request read it, and a singleton or a cache here would be a Role
        // outliving the request that read it. The DbContext takes the same SpaceScope, which is how
        // its query filters see this request's Space and no other.
        services.AddScoped<SpaceScope>();
        services.AddScoped<IActiveSpaceResolver, MembershipActiveSpaceResolver>();

        // For a slice that needs the Role. It throws rather than defaulting when no Space has been
        // resolved: a slice asking for a context on a route the pipeline never resolved is a
        // wiring defect, and an empty context would be a Role nobody read from anywhere.
        services.AddScoped(provider => provider.GetRequiredService<SpaceScope>().Resolved
            ?? throw new InvalidOperationException(
                "No active Space has been resolved on this request. ActiveSpaceContext exists only " +
                "on a route whose pattern carries {spaceId}, after the pipeline has verified the " +
                "Membership."));

        services.AddScoped<IStatusDefinitionReader, StatusDefinitionReader>();

        // THE SPACE BOUNDARY'S REFUSAL RECORD, FROM STORY 1.6. The request pipeline is its only
        // caller (AR-3). Scoped, and it builds a context of its own for each record - see
        // AccessRefusalRecorder for why it never reuses the request's.
        services.AddScoped<IAccessRefusalRecorder, AccessRefusalRecorder>();

        // Its bound and its retention, registered beside it - split out only to keep this class's
        // dependencies within S1200's limit.
        services.AddSpaceBoundaryLimits();

        return services;
    }

    /// <summary>
    /// Materialises the password hasher's dummy hash, so that the first unknown-address sign-in in
    /// this process is not the request that pays to create it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>AC4's duration clause, on the one axis laziness left open.</b> The dummy hash a
    /// non-existent address is verified against is built on first use. Without this call that first
    /// use is a real request, which then performs a full <c>HashPassword</c> in addition to its
    /// verification - roughly twice what a known address costs on the same cold process, one sample
    /// per process, on exactly the axis AC4 requires to be indistinguishable. Found at code review
    /// on 2026-09-19; no test could see it because <c>DurationIndistinguishability</c> warms each
    /// arm before measuring, which hides the single occurrence.
    /// </para>
    /// <para>
    /// Exposed here rather than on <c>Domain.Accounts.IPasswordHasher</c> deliberately: warming is a
    /// property of <i>this</i> implementation, and putting it on the port would make every future
    /// hasher answer a question only this one has. Costs one hash (~273 ms) at startup and is
    /// idempotent, so calling it twice is harmless.
    /// </para>
    /// </remarks>
    /// <param name="services">The built provider.</param>
    public static void WarmPasswordHashing(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.GetRequiredService<Domain.Accounts.IPasswordHasher>() is IdentityPasswordHasher hasher)
        {
            hasher.Warm();
        }
    }
}
