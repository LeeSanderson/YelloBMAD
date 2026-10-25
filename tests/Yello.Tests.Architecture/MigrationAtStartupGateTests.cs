using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// AR-36 at build: nothing in production migrates. Migrations are applied by one artefact (the
/// migration bundle), run as its own step before the Host serves, and never on application start.
/// Story 1.10 AC15 (and AC2, whose policies only the migration creates); Task 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as a red-phase scaffold by the ATDD run on 2026-10-25, and green today.</b> No
/// production assembly calls a migrator now - <c>YelloDbContext.cs:15-20</c> says so in prose, and
/// this makes the prose a gate. It is left active as a standing regression gate.
/// </para>
/// <para>
/// <b>Read from the compiled IL</b>, the way <c>SpaceSwitcherGateTests.CallersOf</c> reads it: every
/// method body in the four production rings, nested and compiler-generated types included, so a
/// call inside a lambda or a local function is attributed to its outermost type.
/// </para>
/// <para>
/// <b>Vacuity guard:</b> the same scan must find <c>UseSqlServer</c> in <c>InfrastructureServices</c>
/// before it asserts, so a scanner that reads no EF Core call at all fails rather than passing.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-36")]
[Trait("Requirement", "AD-2")]
public sealed class MigrationAtStartupGateTests
{
    /// <summary>
    /// The calls that apply a schema, by declaring type and method name.
    /// </summary>
    private static readonly (string DeclaringType, string Method)[] Migrators =
    [
        ("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "Migrate"),
        ("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "MigrateAsync"),
        ("Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "EnsureCreated"),
        ("Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "EnsureCreatedAsync"),
        ("Microsoft.EntityFrameworkCore.Migrations.IMigrator", "Migrate"),
        ("Microsoft.EntityFrameworkCore.Migrations.IMigrator", "MigrateAsync"),
    ];

    /// <summary>
    /// 1.10-ARCH-001 - no type in Host, Application, Infrastructure or Domain calls
    /// <c>Migrate</c>, <c>MigrateAsync</c>, <c>EnsureCreated</c>, <c>EnsureCreatedAsync</c> or an
    /// <c>IMigrator</c>.
    /// </summary>
    /// <remarks>
    /// <b>Active - green today, a standing gate.</b> Plant (story Task 2, plant 1): add
    /// <c>context.Database.Migrate()</c> to a production type; the gate names that type.
    /// </remarks>
    [Fact]
    public void No_production_assembly_applies_migrations()
    {
        var calls = CallsInProduction();

        // Vacuity guard: the scan reads EF Core calls at all.
        Assert.Contains(calls, call =>
            call.Method.Equals("UseSqlServer", StringComparison.Ordinal)
            && call.Caller.Equals("Yello.Infrastructure.InfrastructureServices", StringComparison.Ordinal));

        var offenders = calls
            .Where(call => Array.Exists(Migrators, migrator =>
                migrator.DeclaringType.Equals(call.DeclaringType, StringComparison.Ordinal)
                && migrator.Method.Equals(call.Method, StringComparison.Ordinal)))
            .Select(call => $"{call.Caller} calls {call.DeclaringType}.{call.Method}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Migrations are an explicit deploy step, never applied on application start (AR-36, AC15). "
            + "Apply them with the migration bundle:\n" + string.Join('\n', offenders));
    }

    /// <summary>
    /// Every call instruction in the four production rings: its outermost calling type, and the
    /// called method's declaring type and name.
    /// </summary>
    private static List<(string Caller, string DeclaringType, string Method)> CallsInProduction()
    {
        var calls = new List<(string Caller, string DeclaringType, string Method)>();

        System.Reflection.Assembly[] rings =
        [
            ProductionAssemblies.Domain,
            ProductionAssemblies.Application,
            ProductionAssemblies.Infrastructure,
            ProductionAssemblies.Host,
        ];

        foreach (var assembly in rings)
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);

            foreach (var type in module.GetTypes())
            {
                var caller = OutermostOf(type).FullName;

                calls.AddRange(type.Methods
                    .Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions)
                    .Where(instruction => instruction.OpCode.FlowControl == FlowControl.Call || instruction.OpCode == OpCodes.Ldftn)
                    .Select(instruction => instruction.Operand)
                    .OfType<MethodReference>()
                    .Select(called => (caller, called.DeclaringType.FullName, called.Name)));
            }
        }

        return calls;
    }

    private static TypeDefinition OutermostOf(TypeDefinition type) =>
        type.DeclaringType is null ? type : OutermostOf(type.DeclaringType);
}
