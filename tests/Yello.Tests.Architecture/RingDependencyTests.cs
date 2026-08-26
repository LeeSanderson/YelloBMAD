using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Yello.Tests.Architecture;

/// <summary>
/// Gate B - the type-dependency gate. A-1 (the ring rule, 4 assertions) and A-2 (no EF Core
/// in Domain, no ASP.NET Core in Application or Domain, 2 assertions): 6 of the 24 the
/// architecture suite reaches across the project's life, and 6 of the 10 story 1.1 delivers.
/// The assertions below those six extend the same two rules to the four assemblies A-1 and
/// A-2 do not name, and are numbered outside the A-series for the same reason Gate A's are.
/// </summary>
/// <remarks>
/// <para>
/// <b>These assertions are vacuously true today.</b> The production projects hold only an
/// <c>AssemblyMarker</c>, so there is no dependency for them to catch. That is expected and
/// correct - AC5 has the suites running against a solution with no feature code - but it
/// also means they are unproven by their own passing. The test design is explicit that "a
/// test asserting the absence of a signal must be validated against a planted signal, or it
/// is not a test", which is what Task 9 does: each gate here was failed on purpose against a
/// real violation before being trusted. The results are in the story's Dev Agent Record.
/// </para>
/// <para>
/// A-4 onward need routes, tables, a bound registry and slices, and accrue in stories 1.5,
/// 1.6, 2.6, 5.2, 5.3 and 7.1.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
public sealed class RingDependencyTests
{
    /// <summary>
    /// Every type whose full name says "EF Core", however it is spelled.
    /// </summary>
    /// <remarks>
    /// Anchoring on <c>^Microsoft\.EntityFrameworkCore\.</c> matched the namespace rather than
    /// the dependency, and missed the way EF Core actually enters an inner ring:
    /// <c>services.AddDbContext&lt;...&gt;()</c> is
    /// <c>Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions</c>,
    /// which does not start with the banned prefix. An unanchored match on
    /// <c>EntityFramework</c> catches both, and there is no legitimate type in this solution
    /// whose name contains it.
    /// </remarks>
    private const string EfCorePattern = @".*EntityFramework.*";

    /// <summary>
    /// ASP.NET Core surface, including the parts of it that do not live under
    /// <c>Microsoft.AspNetCore</c>.
    /// </summary>
    /// <remarks>
    /// The second alternation is a curated list rather than a general rule, because
    /// <c>Microsoft.Extensions.*</c> is mostly framework-agnostic and banning it wholesale
    /// would forbid logging and options from the inner rings, which AD-21 permits. The
    /// backstop for what a curated list misses is the package-level ban in
    /// <see cref="AllowedReferenceEdges.ForbiddenPackagePrefixes"/>: none of these types is
    /// reachable without referencing an ASP.NET Core package, and that reference is asserted
    /// against a table by Gate A whether or not any type is touched.
    /// </remarks>
    private const string AspNetCorePattern =
        @"^(Microsoft\.AspNetCore\..*|Microsoft\.Extensions\.DependencyInjection\.(Mvc|Authorization|Authentication|Cors|Routing|SignalR|Http|Endpoint).*)$";

    /// <summary>
    /// A-1.1 - Domain is the innermost ring and depends on no other Yello assembly.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void Domain_types_depend_on_no_other_Yello_assembly()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Domain)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProductionAssemblies.Application,
                    ProductionAssemblies.Infrastructure,
                    ProductionAssemblies.Host,
                    ProductionAssemblies.Contracts,
                    ProductionAssemblies.Merge,
                    ProductionAssemblies.Client,
                    ProductionAssemblies.AppHost))
            .Because("Yello.Domain is the innermost ring: it holds entities, invariants and ports, and references nothing (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// A-1.2 - Application may reach inward to Domain, never outward to Infrastructure.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void Application_types_do_not_depend_on_Infrastructure()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Application)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(ProductionAssemblies.Infrastructure))
            .Because("Yello.Application holds use-case slices and the request pipeline; it depends inward on Domain only, and reaches Infrastructure through ports (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// A-1.3 - Application never reaches the outermost ring.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void Application_types_do_not_depend_on_Host()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Application)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(ProductionAssemblies.Host))
            .Because("Yello.Host is the composition root and the outermost ring; nothing inward may depend on it (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// A-1.4 - Infrastructure implements ports; it does not depend on the Host that composes it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void Infrastructure_types_do_not_depend_on_Host()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Infrastructure)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(ProductionAssemblies.Host))
            .Because("Yello.Infrastructure implements the ports Application declares; the Host composes it, not the other way round (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// A-2.1 - persistence is an Infrastructure concern. An EF Core type in Domain would put
    /// the storage model inside the invariants, which is the inversion AD-21 exists to stop.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void No_EF_Core_type_is_referenced_from_Domain()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Domain)
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(EfCorePattern)
            .Because("EF Core lives in Yello.Infrastructure; the Domain holds entities and invariants, not a persistence model (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// A-2.2 - the web framework belongs to the Host. An ASP.NET Core type in Application or
    /// Domain would make a use case or an invariant depend on how it happened to be invoked.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void No_ASP_NET_Core_type_is_referenced_from_Application_or_Domain()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Application, ProductionAssemblies.Domain)
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(AspNetCorePattern)
            .Because("ASP.NET Core is the Host's concern; a use case or an invariant must not depend on the transport that invoked it (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// Contracts is the shared wire vocabulary, and like Domain it sits at the bottom of its
    /// own chain: both the server and the WebAssembly client depend on it, so anything it
    /// depends on is dragged across the wire boundary into the browser payload.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void Contracts_types_depend_on_no_other_Yello_assembly()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Contracts)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProductionAssemblies.Domain,
                    ProductionAssemblies.Application,
                    ProductionAssemblies.Infrastructure,
                    ProductionAssemblies.Host,
                    ProductionAssemblies.Merge,
                    ProductionAssemblies.Client,
                    ProductionAssemblies.AppHost))
            .Because("Yello.Contracts holds wire DTOs shared by client and server, and references nothing, so that neither side drags a ring across the boundary")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// Merge reaches Contracts and nothing else. It is compiled into the WebAssembly client,
    /// so a dependency here is downloaded by every browser session.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void Merge_types_depend_on_nothing_but_Contracts()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Merge)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProductionAssemblies.Domain,
                    ProductionAssemblies.Application,
                    ProductionAssemblies.Infrastructure,
                    ProductionAssemblies.Host,
                    ProductionAssemblies.Client,
                    ProductionAssemblies.AppHost))
            .Because("Yello.Merge implements ITextMergeStrategy for both client and server; its only outbound edge is Contracts (spine dependency graph)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// The client is a browser process. It reaches the server over HTTP, never by linking
    /// against it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void Client_types_depend_on_nothing_but_Contracts_and_Merge()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Client)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProductionAssemblies.Domain,
                    ProductionAssemblies.Application,
                    ProductionAssemblies.Infrastructure,
                    ProductionAssemblies.Host,
                    ProductionAssemblies.AppHost))
            .Because("Yello.Client is Blazor WebAssembly: it shares Contracts and Merge with the server and reaches everything else over HTTP (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// A-2 extended to the two assemblies that ship to the browser. Neither EF Core nor
    /// ASP.NET Core has any business in a WebAssembly payload, and A-2 as written named only
    /// Application and Domain.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void No_EF_Core_or_ASP_NET_Core_type_is_referenced_from_Contracts_or_Merge()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Contracts, ProductionAssemblies.Merge)
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching($"({EfCorePattern}|{AspNetCorePattern})")
            .Because("Contracts and Merge are compiled into the WebAssembly client as well as the server; a persistence or transport type here crosses the wire boundary (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// The AppHost orchestrates processes; it does not link against them.
    /// </summary>
    /// <remarks>
    /// It sits in no ring - it appears in the spine's source tree and dependency graph but in
    /// no ring-table row - which is why Gate A asserts it against its own allowed edges. That
    /// left it with no type-level rule at all. It does have one, and a strict one: the Aspire
    /// SDK marks an AppHost's project references as project <i>resources</i>
    /// (<c>ReferenceOutputAssembly=false</c>), so the AppHost genuinely cannot compile against
    /// Host or Client even though its row permits the edge. A type dependency appearing here
    /// means someone has turned that off, and the orchestrator has started linking against the
    /// things it is supposed to be launching.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void AppHost_types_depend_on_no_Yello_assembly()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.AppHost)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProductionAssemblies.Domain,
                    ProductionAssemblies.Application,
                    ProductionAssemblies.Infrastructure,
                    ProductionAssemblies.Host,
                    ProductionAssemblies.Contracts,
                    ProductionAssemblies.Merge,
                    ProductionAssemblies.Client))
            .Because("Yello.AppHost launches Yello.Host and Yello.Client as Aspire project resources; it orchestrates processes rather than linking against assemblies")
            .Check(ProductionAssemblies.Architecture);
    }

    /// <summary>
    /// EF Core is Infrastructure's. A use case names a port, never the ORM behind it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void No_EF_Core_type_is_referenced_from_Application()
    {
        Types().That().ResideInAssembly(ProductionAssemblies.Application)
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(EfCorePattern)
            .Because("Yello.Application declares ports and holds use-case slices; the ORM is reached through Infrastructure, never named in a slice (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }
}
