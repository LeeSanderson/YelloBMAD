using Microsoft.Extensions.DependencyInjection;
using Mono.Cecil;
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
/// trusted</b> - <c>TESTING-CONVENTIONS.md:111</c> requires it. They are planted against the
/// <i>scanner</i>, by editing production source and running the gate, not against a helper the
/// scanner calls: a plant that validates the predicate instead of the scan proves nothing.
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
    /// After story 1.5 there is exactly one shared implementation that sets a session-context key,
    /// and every caller goes through it - for <c>SpaceId</c> and, since story 1.7, <c>AccountId</c>.
    /// </summary>
    private const int ExpectedSessionContextCallSites = 1;

    /// <summary>
    /// AC2 - one seam sets the session context, not two - and that one line sets both of the keys
    /// the spine names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both keys, one line.</b> The line sets the <c>AccountId</c> key as well as <c>SpaceId</c>
    /// (AD-24), so the name says "a session context key"; the count is one line.
    /// </para>
    /// <para>
    /// The one call site is <c>SpaceSessionContext</c>, which registration, the per-request resolver
    /// and the Space switcher's directory all go through. The hazard is a <i>second</i> copy of the same statement: two places
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
    public void Exactly_one_production_line_sets_a_session_context_key()
    {
        // Comment lines are removed before matching. The statement is discussed in prose across the
        // codebase - AccountCredentialStore explains why it does NOT set one - and a scan that
        // counted those mentions would report files that execute nothing.
        // Lines, not files: a second copy inside the file that already holds the first is still a
        // second setter on the unit of work.
        var callSites = ProductionSourceFiles()
            .SelectMany(file => CodeLinesOf(file)
                .Where(line => line.Contains(SessionContextProcedure, StringComparison.Ordinal))
                .Select(_ => RepositoryLayout.RelativePath(file)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            callSites.Count == ExpectedSessionContextCallSites,
            $"Expected exactly {ExpectedSessionContextCallSites} production line to execute "
            + $"{SessionContextProcedure}, found {callSites.Count}: {string.Join(", ", callSites)}. "
            + "AD-2 puts this statement in Infrastructure and AR-3 forbids a slice re-implementing "
            + "Space resolution; a second copy is the shape both exist to prevent.");

        Assert.All(callSites, path => Assert.True(
            path.StartsWith("Yello.Infrastructure/", StringComparison.Ordinal),
            $"{path} executes {SessionContextProcedure}. AD-2 puts the statement in Infrastructure, "
            + "and a copy anywhere else is one the query-filter bypass gate cannot see."));
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
    /// decision. Inside <c>Infrastructure</c> it is permitted; the tests that prove row-level
    /// security holds alone use it too, from <c>tests/</c>, which this scan does not read.
    /// </para>
    /// <para>
    /// <b>Every way in, not only EF's named raw-SQL methods.</b> The entries are prefixes, so
    /// <c>FromSql</c> also covers <c>FromSqlRaw</c> and <c>FromSqlInterpolated</c>, and
    /// <c>ExecuteSql</c> covers its variants and their async forms. The ADO.NET entries catch the
    /// route past EF altogether: <c>Yello.Host</c> references the SQL driver for its startup
    /// connectivity check, which opens a connection and runs no command.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_production_code_outside_Infrastructure_bypasses_the_query_filters()
    {
        string[] bypasses =
        [
            "FromSql",
            "SqlQuery",
            "ExecuteSql",
            "IgnoreQueryFilters",
            "GetDbConnection",
            "SqlCommand",
            "CreateCommand",
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
            typeof(AccountScope),
            typeof(IAccountSpaceDirectory),
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
            "OutputCache",
            "CacheOutput",
            "ResponseCache",
            "ResponseCaching",
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
    /// AR-3 / test id <b>A-12</b>, for Space resolution: nothing but the pipeline asks for the
    /// resolver.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IActiveSpaceResolver"/> is public, because it is a port declared in
    /// <c>Yello.Application</c> and implemented in <c>Yello.Infrastructure</c>. A slice that takes it
    /// and resolves a Space for itself is re-implementing Space resolution, which AR-3 names as the
    /// defect: its unit of work would be a second one, beside the pipeline's, on the same connection.
    /// </para>
    /// <para>
    /// <b>Read from the compiled IL, not the source</b>, so a dependency taken through a
    /// constructor, a field, a local or a <c>GetRequiredService&lt;T&gt;</c> call is seen alike, and
    /// compiler-generated state machines are attributed to the type that wrote the code. A-12's
    /// authorisation, refusal-recording and bound clauses are in <c>RefusalBoundaryGateTests</c>;
    /// its idempotency clause waits for story 2.1, the first endpoint that has one.
    /// </para>
    /// </remarks>
    [Fact]
    public void Only_the_pipeline_depends_on_the_Space_resolver()
    {
        var port = typeof(IActiveSpaceResolver).FullName!;

        string[] permitted =
        [
            port,
            "Yello.Host.Spaces.ActiveSpaceResolution",
            "Yello.Infrastructure.Persistence.MembershipActiveSpaceResolver",
            "Yello.Infrastructure.InfrastructureServices",
        ];

        var dependents = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in ProductionAssemblies.All)
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);

            foreach (var type in module.GetTypes().Where(type => ReferencesType(type, port)))
            {
                dependents.Add(OutermostOf(type).FullName);
            }
        }

        // Vacuity guard: the pipeline is a known dependent, so a scan that cannot see it is not
        // reading what it claims to.
        Assert.Contains("Yello.Host.Spaces.ActiveSpaceResolution", dependents);

        var offenders = dependents.Where(name => !permitted.Contains(name, StringComparer.Ordinal)).ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} depend on {nameof(IActiveSpaceResolver)}. Space resolution "
            + "is the pipeline's (AR-3): a slice reads ActiveSpaceContext, and never opens a unit of "
            + "work of its own.");
    }

    private static bool ReferencesType(TypeDefinition type, string fullName)
    {
        bool Names(TypeReference? candidate) =>
            candidate is not null
            && (candidate.FullName == fullName
                || (candidate is GenericInstanceType generic && generic.GenericArguments.Any(Names)));

        bool InOperand(object? operand) => operand switch
        {
            GenericInstanceMethod method => Names(method.DeclaringType) || method.GenericArguments.Any(Names),
            MethodReference method => Names(method.DeclaringType) || Names(method.ReturnType),
            FieldReference field => Names(field.FieldType) || Names(field.DeclaringType),
            TypeReference reference => Names(reference),
            _ => false,
        };

        bool InSignature(MethodDefinition method) =>
            Names(method.ReturnType) || method.Parameters.Any(parameter => Names(parameter.ParameterType));

        bool InBody(MethodDefinition method) =>
            method.HasBody
            && (method.Body.Variables.Any(variable => Names(variable.VariableType))
                || method.Body.Instructions.Any(instruction => InOperand(instruction.Operand)));

        var inDeclaration = type.Interfaces.Any(implemented => Names(implemented.InterfaceType))
            || type.Fields.Any(field => Names(field.FieldType));

        return inDeclaration || type.Methods.Any(method => InSignature(method) || InBody(method));
    }

    private static TypeDefinition OutermostOf(TypeDefinition type) =>
        type.DeclaringType is null ? type : OutermostOf(type.DeclaringType);

    /// <summary>
    /// A file's lines, less those that are wholly a comment.
    /// </summary>
    private static IEnumerable<string> CodeLinesOf(FileInfo file) =>
        File.ReadLines(file.FullName)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

    /// <summary>
    /// Every <c>.cs</c> file belonging to a production project.
    /// </summary>
    /// <remarks>
    /// Derived from the project files on disk rather than from a list, so a ninth production
    /// project is covered without anyone extending this class - and a project that disappears
    /// cannot leave a gate silently scanning nothing.
    /// </remarks>
    private static IEnumerable<FileInfo> ProductionSourceFiles() =>
        RepositoryLayout.AllProjectFiles
            .Where(project => !RepositoryLayout.IsUnderTestsDirectory(project))
            .SelectMany(RepositoryLayout.SourceFilesOf);
}
