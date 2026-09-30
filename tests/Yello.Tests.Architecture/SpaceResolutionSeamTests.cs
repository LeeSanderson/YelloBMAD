using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yello.Application.Spaces;
using Yello.Infrastructure;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 1.5's structural gates: one session-context seam, no route around the EF Core query
/// filters, and no Space resolution state that outlives the request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every gate here is an absence assertion, and each was failed on purpose before it was
/// trusted</b> - <c>TESTING-CONVENTIONS.md:111</c> requires it, and the plants are recorded in story
/// 1.5's Completion Notes. They were planted against the <i>scanner</i>, by editing production
/// source and running the gate, not against a helper the scanner calls; story 1.4's review found a
/// plant that validated the predicate instead of the scan and therefore proved nothing.
/// </para>
/// <para>
/// <b>Globbed, never a file list.</b> A gate naming files that do not exist yet passes vacuously,
/// which is the defect this repository has rediscovered in three separate stories. Every scan below
/// goes through <see cref="RepositoryLayout.EnumerateSourceFiles"/> so the exclusion list applies
/// and a vendored file arriving under <c>.claude</c> cannot fail a build nobody changed.
/// </para>
/// <para>
/// <b>This file scans production projects only.</b> Test code legitimately opens raw connections -
/// <c>RegistrationDatabase</c> exists to do exactly that - so a scan that included <c>tests/</c>
/// would have to carry an exception list, and an exception list is where this kind of rule rots.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-5")]
[Trait("Requirement", "AR-6")]
[Trait("Requirement", "AD-1")]
[Trait("Requirement", "AD-2")]
[Trait("Requirement", "NFR-2")]
public sealed class SpaceResolutionSeamTests
{
    /// <summary>
    /// The one statement the whole isolation model rests on.
    /// </summary>
    private const string SessionContextProcedure = "sp_set_session_context";

    /// <summary>
    /// After story 1.5 there is exactly one shared implementation that sets the Space session
    /// context, and every caller goes through it.
    /// </summary>
    private const int ExpectedSessionContextCallSites = 1;

    /// <summary>
    /// AC2 - one seam sets the session context, not two.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one call site is <c>SpaceSessionContext</c>, which both registration and the per-request
    /// resolver go through. The hazard is a <i>second</i> copy of the same statement: two places
    /// writing the key, two places to get the parameter types wrong, and two places a later story
    /// has to remember when the key changes.
    /// </para>
    /// <para>
    /// <b>A second call site is also a live defect rather than a style problem.</b>
    /// <c>@read_only = 1</c> means the key cannot be written twice on one logical connection, so
    /// two independent setters on one unit of work do not merely duplicate - the second is refused
    /// and the request fails. Extracting one implementation is what makes that unrepresentable.
    /// </para>
    /// </remarks>
    [Fact]
    public void Exactly_one_production_call_site_sets_the_Space_session_context()
    {
        // Comment lines are removed before matching. The statement is discussed in prose across the
        // codebase - AccountCredentialStore explains why it does NOT set one - and a scan that
        // counted those mentions would report files that execute nothing.
        var offenders = ProductionSourceFiles()
            .Where(file => CodeLinesOf(file).Any(line => line.Contains(SessionContextProcedure, StringComparison.Ordinal)))
            .Select(RepositoryLayout.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == ExpectedSessionContextCallSites,
            $"Expected exactly {ExpectedSessionContextCallSites} production file to execute "
            + $"{SessionContextProcedure}, found {offenders.Count}: {string.Join(", ", offenders)}. "
            + "AD-2 puts this statement in Infrastructure and AR-3 forbids a slice re-implementing "
            + "Space resolution; a second copy is the shape both exist to prevent.");
    }

    /// <summary>
    /// AC4 / test id <b>A-13</b> - no raw SQL bypasses the global query filters outside
    /// <c>Yello.Infrastructure</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AD-2 (<c>ARCHITECTURE-SPINE.md:84</c>): "Raw SQL that bypasses global query filters is
    /// forbidden outside <c>Infrastructure</c>." The filter is one of NFR-1's two layers, and a
    /// slice reaching around it removes that layer for its own queries while leaving the
    /// architecture document describing a system that no longer exists.
    /// </para>
    /// <para>
    /// <c>IgnoreQueryFilters</c> is included deliberately: it is the sanctioned way to disarm the
    /// layer, which makes it exactly the call that must not appear outside the ring that owns the
    /// decision. Inside <c>Infrastructure</c> it is permitted and is how AC4's own layer-isolation
    /// test will disarm the filter to prove row-level security holds alone.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_production_code_outside_Infrastructure_bypasses_the_query_filters()
    {
        string[] bypasses =
        [
            "FromSqlRaw",
            "FromSqlInterpolated",
            "ExecuteSqlRaw",
            "ExecuteSqlInterpolated",
            "IgnoreQueryFilters",
        ];

        var offenders = ProductionSourceFiles()
            .Where(file => !RepositoryLayout.RelativePath(file)
                .StartsWith("Yello.Infrastructure/", StringComparison.Ordinal))
            .Select(file => (Path: RepositoryLayout.RelativePath(file), Text: string.Join('\n', CodeLinesOf(file))))
            .Where(candidate => Array.Exists(bypasses, call => candidate.Text.Contains(call, StringComparison.Ordinal)))
            .Select(candidate => candidate.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Raw SQL or a query-filter bypass appears outside Yello.Infrastructure, which removes "
            + "one of NFR-1's two independent layers for those queries: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// AC7 / NFR-2 - everything Space resolution holds lives exactly as long as one request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AD-1: a Role is never read from "any cache outliving the request". The resolved context
    /// carries the Role, the scope carries the Space the query filters read, and the resolver and the
    /// context it reads through are what populate both. Any one of them registered as a singleton is
    /// a Role or a Space from an earlier request served to a later one - and, for the scope, another
    /// request's Space applied to this request's query filters.
    /// </para>
    /// <para>
    /// Read from the real composition method rather than restated, so the registration a Host
    /// actually receives is the one asserted.
    /// </para>
    /// </remarks>
    [Fact]
    public void Space_resolution_state_is_registered_per_request()
    {
        var services = new ServiceCollection().AddYelloInfrastructure(connectionString: null);

        Type[] requestScoped =
        [
            typeof(SpaceScope),
            typeof(ActiveSpaceContext),
            typeof(IActiveSpaceResolver),
            typeof(YelloDbContext),
        ];

        var problems = requestScoped
            .Select(type => (Type: type, Lifetimes: services
                .Where(descriptor => descriptor.ServiceType == type)
                .Select(descriptor => descriptor.Lifetime)
                .ToList()))
            .Where(entry => entry.Lifetimes is not [ServiceLifetime.Scoped])
            .Select(entry => $"{entry.Type.Name}: [{string.Join(", ", entry.Lifetimes)}]")
            .ToList();

        Assert.True(
            problems.Count == 0,
            "Space resolution state must be registered exactly once, as Scoped - anything longer "
            + "lives past the request that read the Membership row, which is the cache AD-1 forbids: "
            + string.Join("; ", problems));
    }

    /// <summary>
    /// AC7 / NFR-2 - no production code reaches for a cache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The DI gate above catches a lifetime; this catches the other way a Role or a Space-scoped
    /// answer outlives its request - an explicit cache in front of the read. NFR-2
    /// (<c>quality-budgets.md:20</c>): "no cache may outlive a request, so a delay of even one request
    /// means a cache was introduced, which is the failure this requirement exists to catch." A cached
    /// response to a Space-scoped route is the same failure at the edge: the next caller is served a
    /// body resolved against someone else's Membership.
    /// </para>
    /// <para>
    /// <b>A ban rather than a rule about what may be cached.</b> Nothing in the product caches today,
    /// so any of these arriving is a design change, and the place to argue it is the architecture
    /// rather than a code review that happens to notice.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_production_code_introduces_a_cache()
    {
        string[] caches =
        [
            "IMemoryCache",
            "IDistributedCache",
            "HybridCache",
            "AddMemoryCache",
            "AddDistributedMemoryCache",
            "AddOutputCache",
            "CacheOutput",
            "AddResponseCaching",
            "UseResponseCaching",
        ];

        var offenders = ProductionSourceFiles()
            .Select(file => (Path: RepositoryLayout.RelativePath(file), Text: string.Join('\n', CodeLinesOf(file))))
            .SelectMany(candidate => caches
                .Where(cache => candidate.Text.Contains(cache, StringComparison.Ordinal))
                .Select(cache => $"{candidate.Path} ({cache})"))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A cache appears in production code. AD-1 forbids a Role from any cache outliving the "
            + "request and NFR-2 forbids any cache that does at all: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Every <c>.cs</c> file belonging to a production project.
    /// </summary>
    /// <remarks>
    /// Derived from the project files on disk rather than from a list, so a ninth production
    /// project is covered without anyone extending this class - and a project that disappears
    /// cannot leave a gate silently scanning nothing.
    /// </remarks>
    private static IEnumerable<string> CodeLinesOf(FileInfo file) =>
        File.ReadLines(file.FullName)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

    private static IEnumerable<FileInfo> ProductionSourceFiles() =>
        RepositoryLayout.AllProjectFiles
            .Where(project => !RepositoryLayout.IsUnderTestsDirectory(project))
            .SelectMany(RepositoryLayout.SourceFilesOf);
}
