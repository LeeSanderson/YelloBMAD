using ArchUnitNET.xUnitV3;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectedAssembly = System.Reflection.Assembly;

namespace Yello.Tests.Architecture;

/// <summary>
/// One merge port, one implementation that nothing outside <c>Yello.Merge</c> can name, and one registration per
/// composition root, always from the factory. So two stories cannot choose different merge semantics, and a second merge
/// cannot be wired in beside the first.
/// </summary>
/// <remarks>
/// The port is found by name across the production assemblies, not by type, so a second interface of the same name
/// elsewhere is counted rather than invisible. Every gate is an absence assertion, proved against a planted violation.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-18")]
[Trait("Requirement", "AD-12")]
public sealed class MergePortGateTests
{
    private const string PortName = "ITextMergeStrategy";

    private const string ReplicaName = "ITextReplica";

    private const string PortFullName = "Yello.Merge." + PortName;

    private const string FactoryFullName = "Yello.Merge.TextMerge";

    private const string FactoryMethod = "CreateStrategy";

    [Fact]
    public void Exactly_one_production_type_is_named_ITextMergeStrategy_and_it_is_an_interface_in_Yello_Merge()
    {
        var named = ProductionTypes().Where(type => type.Name == PortName).ToList();

        var port = Assert.Single(named);

        Assert.True(port.IsInterface, $"{port.FullName} is not an interface.");
        Assert.Same(ProductionAssemblies.Merge, port.Assembly);
        Assert.Equal(PortFullName, port.FullName);
    }

    [Fact]
    public void Exactly_one_production_type_implements_the_port_and_it_is_not_public_and_lives_in_Yello_Merge()
    {
        var implementation = Assert.Single(Implementations(Port()));

        Assert.Same(ProductionAssemblies.Merge, implementation.Assembly);
        Assert.False(implementation.IsVisible, $"{implementation.FullName} is visible outside Yello.Merge; only the port and its factory may be.");
    }

    [Fact]
    public void No_type_outside_Yello_Merge_depends_on_the_implementation()
    {
        var implementation = Assert.Single(Implementations(Port()));

        Types().That().ResideInAssembly(ProductionAssemblies.Domain, OutsideMerge())
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(Exactly(implementation))
            .Because("the merge is reached through ITextMergeStrategy and TextMerge.CreateStrategy only, so it can be replaced behind the port (AD-12)")
            .Check(ProductionAssemblies.Architecture);
    }

    [Fact]
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

    [Fact]
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
