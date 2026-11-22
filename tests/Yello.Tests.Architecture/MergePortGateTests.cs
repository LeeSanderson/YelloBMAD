using ArchUnitNET.xUnitV3;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectedAssembly = System.Reflection.Assembly;

namespace Yello.Tests.Architecture;

/// <summary>
/// A-8 (AD-12): one merge port, one implementation that nothing outside <c>Yello.Merge</c> can name, and one registration
/// per composition root, always from the factory. So two stories cannot choose different merge semantics, and a second
/// merge cannot be wired in beside the first.
/// </summary>
/// <remarks>
/// <para>
/// <b>By name, not by type.</b> The port is found by its name across the eight production assemblies, so a second
/// interface of the same name elsewhere is counted rather than invisible, and this file compiles before the port exists.
/// </para>
/// <para>
/// <b>Every gate is an absence assertion and is proved against its plant</b> (a second interface named
/// <c>ITextMergeStrategy</c> in another assembly, a second implementation, a Host type naming the implementation, a second
/// registration), each restored from saved content in a <c>finally</c>. The plant results are in the story's Dev Agent
/// Record.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
public sealed class MergePortGateTests
{
    private const string Red = "red: AC6 - the port is a stand-in in Yello.Tests.Merge; Yello.Merge declares no ITextMergeStrategy yet";

    private const string PortName = "ITextMergeStrategy";

    private const string ReplicaName = "ITextReplica";

    private const string PortFullName = "Yello.Merge." + PortName;

    private const string FactoryFullName = "Yello.Merge.TextMerge";

    private const string FactoryMethod = "CreateStrategy";

    [Fact(Skip = Red)]
    public void Exactly_one_production_type_is_named_ITextMergeStrategy_and_it_is_an_interface_in_Yello_Merge()
    {
        var named = ProductionTypes().Where(type => type.Name == PortName).ToList();

        var port = Assert.Single(named);

        Assert.True(port.IsInterface, $"{port.FullName} is not an interface.");
        Assert.Same(ProductionAssemblies.Merge, port.Assembly);
        Assert.Equal(PortFullName, port.FullName);
    }

    [Fact(Skip = Red)]
    public void Exactly_one_production_type_implements_the_port_and_it_is_not_public_and_lives_in_Yello_Merge()
    {
        var implementation = Assert.Single(Implementations(Port()));

        Assert.Same(ProductionAssemblies.Merge, implementation.Assembly);
        Assert.False(implementation.IsVisible, $"{implementation.FullName} is visible outside Yello.Merge; only the port and its factory may be.");
    }

    [Fact(Skip = Red)]
    public void No_type_outside_Yello_Merge_depends_on_the_implementation()
    {
        var implementation = Assert.Single(Implementations(Port()));

        Types().That().ResideInAssembly(ProductionAssemblies.Domain, OutsideMerge())
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(Exactly(implementation))
            .Because("the merge is reached through ITextMergeStrategy and TextMerge.CreateStrategy only, so it can be replaced behind the port (AD-12)")
            .Check(ProductionAssemblies.Architecture);
    }

    [Fact(Skip = Red)]
    public void The_port_is_registered_once_in_each_composition_root_and_always_from_the_factory()
    {
        var host = Registrations(ProductionAssemblies.Host);
        var client = Registrations(ProductionAssemblies.Client);

        Assert.Equal(["AddSingleton"], host.Registrations);
        Assert.Equal(["AddScoped"], client.Registrations);
        Assert.Equal(1, host.FactoryCalls);
        Assert.Equal(1, client.FactoryCalls);

        foreach (var assembly in new[] { ProductionAssemblies.Domain, ProductionAssemblies.Application, ProductionAssemblies.Infrastructure, ProductionAssemblies.AppHost })
        {
            Assert.Empty(Registrations(assembly).Registrations);
        }
    }

    [Fact(Skip = Red)]
    public void No_production_type_outside_Yello_Merge_makes_a_replica_except_through_the_port()
    {
        var replicaPort = Assert.Single(ProductionTypes(), type => type.Name == ReplicaName);
        var replicas = Implementations(replicaPort);

        Assert.NotEmpty(replicas);
        Assert.All(replicas, replica => Assert.Same(ProductionAssemblies.Merge, replica.Assembly));
        Assert.All(replicas, replica => Assert.False(replica.IsVisible, $"{replica.FullName} is visible outside Yello.Merge."));

        Types().That().ResideInAssembly(ProductionAssemblies.Domain, OutsideMerge())
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(string.Join('|', replicas.Select(Exactly)))
            .Because("a replica is created and loaded through ITextMergeStrategy only (AD-12)")
            .Check(ProductionAssemblies.Architecture);
    }

    private static ReflectedAssembly[] OutsideMerge() =>
        [.. ProductionAssemblies.All.Where(assembly => assembly != ProductionAssemblies.Merge && assembly != ProductionAssemblies.Domain)];

    private static IEnumerable<Type> ProductionTypes() => ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes());

    private static Type Port() => Assert.Single(ProductionTypes(), type => type.FullName == PortFullName);

    private static List<Type> Implementations(Type port) =>
        [.. ProductionTypes().Where(type => type is { IsInterface: false, IsAbstract: false } && port.IsAssignableFrom(type))];

    private static string Exactly(Type type) => "^" + System.Text.RegularExpressions.Regex.Escape(type.FullName!) + "$";

    /// <summary>
    /// The service-registration calls in an assembly whose service type is the port, and its calls to the factory.
    /// </summary>
    private static (List<string> Registrations, int FactoryCalls) Registrations(ReflectedAssembly assembly)
    {
        using var module = ModuleDefinition.ReadModule(assembly.Location);

        var calls = (
            from type in module.GetTypes()
            from method in type.Methods
            where method.HasBody
            from instruction in method.Body.Instructions
            where instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt
            let called = instruction.Operand as MethodReference
            where called is not null
            select called).ToList();

        var registrations = calls
            .OfType<GenericInstanceMethod>()
            .Where(called => called.DeclaringType.FullName.Contains("DependencyInjection", StringComparison.Ordinal)
                && called.GenericArguments.Any(argument => argument.FullName == PortFullName))
            .Select(called => called.Name)
            .ToList();

        var factoryCalls = calls.Count(called => called.Name == FactoryMethod && called.DeclaringType.FullName == FactoryFullName);

        return (registrations, factoryCalls);
    }
}
