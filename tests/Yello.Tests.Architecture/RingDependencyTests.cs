using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Yello.Tests.Architecture;

/// <summary>
/// Gate B - the type-dependency gate. A-1 (the ring rule, 4 assertions) and A-2 (no EF Core
/// in Domain, no ASP.NET Core in Application or Domain, 2 assertions): 6 of the 24 the
/// architecture suite reaches across the project's life, and 6 of the 10 story 1.1 delivers.
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
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(@"^Microsoft\.EntityFrameworkCore\.")
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
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(@"^Microsoft\.AspNetCore\.")
            .Because("ASP.NET Core is the Host's concern; a use case or an invariant must not depend on the transport that invoked it (AD-21 / AR-2)")
            .Check(ProductionAssemblies.Architecture);
    }
}
