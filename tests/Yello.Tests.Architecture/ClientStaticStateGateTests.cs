using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 3.4 AC12 (<c>deferred-work.md:675</c>; Q7 (a)) - no state in the browser client outlives a Space by living
/// outside the component tree: no writable static, no singleton service, and no <c>static readonly</c> field holding a
/// mutable collection. A shared static is what the two-tab browser case cannot see (one tab per runtime), so it is ruled
/// out here, over the compiled client. Written by the story 3.4 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Compiler-generated members are not the client's state.</b> Lambda caches (<c>&lt;&gt;c</c>), state machines and
/// the regex source generator's types carry <see cref="CompilerGeneratedAttribute"/> or live in a generated namespace;
/// they are skipped, and only what a person wrote is judged.
/// </para>
/// <para>
/// <b>Every rule is proved by a plant</b> (<see cref="The_detector_fires_on_each_planted_kind_of_shared_state"/>): the
/// same detector over a type planted in this assembly, which must fire for each kind. The plant the story names - a
/// <c>static List&lt;string&gt;</c> in a client service - is the dev phase's, against the real client.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR31")]
[Trait("Requirement", "NFR-1")]
public sealed class ClientStaticStateGateTests
{
    /// <summary>
    /// Q7 (a): <c>static readonly</c> fields of these types are immutable once built, or (for
    /// <c>JsonSerializerOptions</c>) frozen on first use, and may be shared.
    /// </summary>
    private static readonly HashSet<string> ImmutableTypeNames = new(StringComparer.Ordinal)
    {
        "System.Text.Json.JsonSerializerOptions",
        "System.Text.CompositeFormat",
        "System.Text.RegularExpressions.Regex",
    };

    /// <summary>
    /// AC12 - no type a person wrote in <c>Yello.Client</c> declares a static field that is not <c>readonly</c> (or a
    /// constant), or a static property with a setter.
    /// </summary>
    [Fact]
    public void No_client_type_holds_a_writable_static()
    {
        var types = ClientTypes();

        Assert.True(types.Count > 20, $"Only {types.Count} client types were read; the gate would pass having looked at nothing.");
        Assert.Equal([], WritableStatics(types));
    }

    /// <summary>
    /// AC12 - <c>Yello.Client</c> registers no singleton: every service it registers lives for the one user's scope, and a
    /// singleton would be a second place for state to survive a Space switch.
    /// </summary>
    [Fact]
    public void No_client_service_is_registered_as_a_singleton()
    {
        var callers = SingletonRegistrations(ClientModulePath());

        Assert.Equal([], callers);
    }

    /// <summary>
    /// AC12, Q7 (a) - no <c>static readonly</c> field in <c>Yello.Client</c> holds a mutable collection (an array, a list,
    /// a dictionary, a set). Immutable and frozen collections, <c>SearchValues</c> and strings are allowed. Red until story
    /// 3.4 converted <c>RefusalCopy.OwnKeys</c> to a <c>FrozenDictionary</c> and the three <c>char[] PathEnds</c> fields to
    /// one <c>SearchValues&lt;char&gt;</c> in <c>ClientRoutes</c> (Q7 (a)).
    /// </summary>
    [Fact]
    public void No_client_static_readonly_field_holds_a_mutable_collection()
    {
        Assert.Equal([], MutableCollectionStatics(ClientTypes()));
    }

    /// <summary>
    /// AC12's plant, run every time: each detector fires on a type planted here with exactly that kind of shared state,
    /// and stays quiet on an immutable one (the anchor).
    /// </summary>
    [Fact]
    public void The_detector_fires_on_each_planted_kind_of_shared_state()
    {
        Assert.Single(WritableStatics([typeof(PlantedWritableField)]));
        Assert.Single(WritableStatics([typeof(PlantedStaticSetter)]));
        Assert.Single(MutableCollectionStatics([typeof(PlantedMutableCollection)]));
        Assert.Single(MutableCollectionStatics([typeof(PlantedMutableAutoProperty)]));
        Assert.Empty(WritableStatics([typeof(PlantedImmutable)]));
        Assert.Empty(MutableCollectionStatics([typeof(PlantedImmutable)]));
        var planted = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        PlantedSingletonRegistration.Register(planted);
        Assert.Single(planted);
        Assert.Contains(SingletonRegistrations(typeof(ClientStaticStateGateTests).Assembly.Location), caller => caller.Contains(nameof(PlantedSingletonRegistration), StringComparison.Ordinal));
    }

    private static List<Type> ClientTypes() =>
        [.. ProductionAssemblies.All
            .Single(assembly => assembly.GetName().Name == "Yello.Client")
            .GetTypes()
            .Where(type => !IsGenerated(type))];

    private static string ClientModulePath() =>
        ProductionAssemblies.All.Single(assembly => assembly.GetName().Name == "Yello.Client").Location;

    private static bool IsGenerated(Type type)
    {
        var generatedNamespace = type.Namespace?.StartsWith("System.Text.RegularExpressions.Generated", StringComparison.Ordinal) ?? false;

        if (generatedNamespace || type.Name.StartsWith('<') || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        {
            return true;
        }

        return type.DeclaringType is { } outer && IsGenerated(outer);
    }

    private const BindingFlags DeclaredStatics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static List<string> WritableStatics(IEnumerable<Type> types) =>
        [.. types.SelectMany(type =>
            type.GetFields(DeclaredStatics)
                .Where(field => !field.IsInitOnly && !field.IsLiteral && !field.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                .Select(field => $"{type.FullName}.{field.Name}: a static field that is not readonly")
                .Concat(type.GetProperties(DeclaredStatics)
                    .Where(property => property.SetMethod is not null)
                    .Select(property => $"{type.FullName}.{property.Name}: a static property with a setter")))];

    private static List<string> MutableCollectionStatics(IEnumerable<Type> types) =>
        [.. types.SelectMany(type =>
            type.GetFields(DeclaredStatics)
                .Where(field => field.IsInitOnly
                    && (IsAutoPropertyBackingField(field) || !field.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                    && IsMutableCollection(field.FieldType))
                .Select(field => $"{type.FullName}.{field.Name}: static readonly {field.FieldType.Name}, a mutable collection"))];

    /// <summary>
    /// A getter-only static auto-property (<c>static List&lt;string&gt; Seen { get; } = [];</c>) is a person's state behind a
    /// compiler-generated readonly field, so it is judged as the field it is (story 3.4 review).
    /// </summary>
    private static bool IsAutoPropertyBackingField(FieldInfo field) =>
        field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal);

    private static bool IsMutableCollection(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (type == typeof(string) || ImmutableTypeNames.Contains(type.FullName ?? string.Empty) || IsImmutableCollection(type))
        {
            return false;
        }

        // A field typed as a read-only interface can still hold a Dictionary; the declared type is what this judges, and
        // the dev phase's plant (a static List) is caught either way.
        return typeof(ICollection).IsAssignableFrom(type)
            || Array.Exists(type.GetInterfaces(), contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICollection<>));
    }

    private static bool IsImmutableCollection(Type type)
    {
        var space = type.Namespace ?? string.Empty;
        string[] immutableSpaces = ["System.Collections.Immutable", "System.Collections.Frozen", "System.Buffers"];

        if (Array.Exists(immutableSpaces, prefix => space.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return true;
        }

        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : null;

        return definition == typeof(System.Collections.ObjectModel.ReadOnlyCollection<>)
            || definition == typeof(System.Collections.ObjectModel.ReadOnlyDictionary<,>);
    }

    private static List<string> SingletonRegistrations(string modulePath)
    {
        using var module = ModuleDefinition.ReadModule(modulePath);

        return [.. (
            from type in module.GetTypes()
            from method in type.Methods
            where method.HasBody
            from instruction in method.Body.Instructions
            let called = instruction.Operand as MethodReference
            where called is not null && IsCall(instruction.OpCode) && RegistersASingleton(called)
            select $"{type.FullName}.{method.Name} calls {called.DeclaringType.Name}.{called.Name}")
            .Distinct(StringComparer.Ordinal)];
    }

    private static bool IsCall(OpCode code) => code == OpCodes.Call || code == OpCodes.Callvirt;

    private static bool RegistersASingleton(MethodReference called) =>
        called.Name.Contains("Singleton", StringComparison.Ordinal)
        && called.DeclaringType.FullName.Contains("DependencyInjection", StringComparison.Ordinal);

    internal static class PlantedWritableField
    {
        internal static int Shared = 1;
    }

    internal static class PlantedStaticSetter
    {
        internal static string Current { get; set; } = string.Empty;
    }

    internal static class PlantedMutableCollection
    {
        internal static readonly List<string> Seen = [];
    }

    internal static class PlantedMutableAutoProperty
    {
        internal static List<string> Seen { get; } = [];
    }

    internal static class PlantedImmutable
    {
        internal static readonly string Name = nameof(PlantedImmutable).ToUpperInvariant();

        internal static readonly System.Collections.Frozen.FrozenSet<string> Names = System.Collections.Frozen.FrozenSet.ToFrozenSet(["a"], StringComparer.Ordinal);
    }

    internal static class PlantedSingletonRegistration
    {
        internal static void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<object>(services, new object());
    }
}
