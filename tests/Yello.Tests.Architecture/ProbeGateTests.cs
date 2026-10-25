using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The liveness and readiness probes answer from process state, and nothing about them can wake an
/// idle database. Story 1.10 AC6 (AD-10 / AR-33); Task 4, Q15 (a), Finding F6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two gates.</b> <see cref="No_production_code_registers_the_health_check_service"/> bans the
/// call; <see cref="The_probe_handlers_depend_on_nothing_that_reaches_the_database"/> reads the type that
/// maps the probes (<c>HealthProbes</c>). Both are planted.
/// </para>
/// <para>
/// <b>Why a gate of its own beside A-9.</b>
/// <c>RefusalBoundaryGateTests.No_production_code_runs_on_a_timer_or_as_a_hosted_service</c> reads the
/// types Yello's own IL depends on. <c>AddHealthChecks()</c> registers
/// <c>HealthCheckPublisherHostedService</c> inside the framework, so Yello's IL never names it and A-9
/// stays green through it. The call is the only thing this repository can see, so the call is banned.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-33")]
[Trait("Requirement", "AD-10")]
public sealed class ProbeGateTests
{
    private const string LivenessPath = "/health/live";

    private const string ReadinessPath = "/health/ready";

    /// <summary>
    /// Assemblies a probe handler must not reach: anything that opens a connection.
    /// </summary>
    private static readonly string[] DatabaseAssemblies =
    [
        "Yello.Infrastructure",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Relational",
        "Microsoft.EntityFrameworkCore.SqlServer",
        "Microsoft.Data.SqlClient",
    ];

    /// <summary>
    /// 1.10-ARCH-002 - no production code calls <c>AddHealthChecks</c> or <c>MapHealthChecks</c>.
    /// </summary>
    /// <remarks>
    /// <b>Active - green today, a standing gate.</b> Vacuity guard: the same scan finds the Host's
    /// <c>AddRateLimiter</c> call. Plant: <c>builder.Services.AddHealthChecks();</c> in
    /// <c>Program.cs</c>.
    /// </remarks>
    [Fact]
    public void No_production_code_registers_the_health_check_service()
    {
        var calls = HostCalls();

        Assert.Contains(calls, call => call.Method.Equals("AddRateLimiter", StringComparison.Ordinal));

        var offenders = calls
            .Where(call => call.Method is "AddHealthChecks" or "MapHealthChecks")
            .Select(call => $"{call.Caller} calls {call.DeclaringType}.{call.Method}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "AddHealthChecks registers HealthCheckPublisherHostedService, an IHostedService - the timer-shaped "
            + "service AR-33 and A-9 forbid. Map the probes as plain endpoints (Q15 (a)):\n" + string.Join('\n', offenders));
    }

    /// <summary>
    /// 1.10-ARCH-003 - the type that maps <c>/health/live</c> and <c>/health/ready</c> is not the
    /// composition root, and its IL references nothing from Infrastructure, EF Core or SqlClient.
    /// </summary>
    /// <remarks>
    /// <b>Why "not Program".</b> <c>Program</c> references everything, so a probe written inline there
    /// cannot be told apart from the rest of the composition root by its IL. The probes need a type of
    /// their own (for example a static <c>HealthProbes.Map(app)</c>), and that type is what this gate
    /// reads. Plant (story Task 13): a probe that opens a <c>SqlConnection</c>.
    /// </remarks>
    [Fact]
    public void The_probe_handlers_depend_on_nothing_that_reaches_the_database()
    {
        using var module = ModuleDefinition.ReadModule(ProductionAssemblies.Host.Location);

        var declaring = module.GetTypes()
            .Where(type => type.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                instruction.OpCode == OpCodes.Ldstr
                && instruction.Operand is string text
                && (text.Equals(LivenessPath, StringComparison.Ordinal) || text.Equals(ReadinessPath, StringComparison.Ordinal)))))
            .Select(OutermostOf)
            .DistinctBy(type => type.FullName)
            .ToList();

        var probeType = Assert.Single(declaring);

        Assert.NotEqual("Program", probeType.FullName);

        var reached = ReferencedAssemblies(probeType)
            .Where(name => DatabaseAssemblies.Contains(name, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            reached.Count == 0,
            $"{probeType.FullName} maps the probes and reaches {string.Join(", ", reached)}. A probe answers from "
            + "process state with no database round trip, so an idle database is never woken (AC6).");
    }

    private static List<(string Caller, string DeclaringType, string Method)> HostCalls()
    {
        using var module = ModuleDefinition.ReadModule(ProductionAssemblies.Host.Location);

        return [.. module.GetTypes()
            .SelectMany(type => type.Methods
                .Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions)
                .Select(instruction => instruction.Operand)
                .OfType<MethodReference>()
                .Select(called => (OutermostOf(type).FullName, called.DeclaringType.FullName, called.Name)))];
    }

    /// <summary>
    /// Every assembly the IL of a type and its nested types names, through a called method, a field
    /// or a type token.
    /// </summary>
    private static SortedSet<string> ReferencedAssemblies(TypeDefinition type)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var instruction in AllTypes(type)
            .SelectMany(nested => nested.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions))
        {
            var reference = instruction.Operand switch
            {
                MethodReference method => method.DeclaringType,
                FieldReference field => field.DeclaringType,
                TypeReference token => token,
                _ => null,
            };

            if (reference is not null)
            {
                names.Add(AssemblyOf(reference));
            }
        }

        return names;
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type) =>
        new[] { type }.Concat(type.NestedTypes.SelectMany(AllTypes));

    private static string AssemblyOf(TypeReference reference) =>
        reference.Scope switch
        {
            AssemblyNameReference assembly => assembly.Name,
            ModuleDefinition module => module.Assembly.Name.Name,
            _ => reference.Scope?.Name ?? string.Empty,
        };

    private static TypeDefinition OutermostOf(TypeDefinition type) =>
        type.DeclaringType is null ? type : OutermostOf(type.DeclaringType);
}
