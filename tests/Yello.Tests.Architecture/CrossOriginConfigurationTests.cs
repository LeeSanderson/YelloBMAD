using Mono.Cecil;
using Xunit;
using Yello.Host.Sessions;

namespace Yello.Tests.Architecture;

/// <summary>
/// The two sides of the allowed-origin setting agree on its name. AD-7 / AR-14, AR-37.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>Yello.AppHost</c> injects the value and <c>Yello.Host</c> reads it, and neither can
/// compile against the other.</b> The Aspire SDK marks the AppHost's project references
/// <c>ReferenceOutputAssembly=false</c> - which is why <c>BuildConstants</c> exists and reads
/// assembly metadata rather than sharing a type - so the configuration key is necessarily written
/// twice: once as a literal in the AppHost, once as
/// <see cref="CrossOriginPolicy.OriginConfigurationKey"/> here.
/// </para>
/// <para>
/// <b>The consequence of a drift is a morning lost.</b> The Host would see no origin, fail closed,
/// log its warning and refuse every browser request - and the symptom in a browser console is a
/// CORS error that points at neither file. This gate is cheaper than that, and cheaper than the
/// assembly-metadata plumbing the SQL image and the database resource name use: those two failed
/// <i>silently</i>, which is what earned them a shared value.
/// </para>
/// <para>
/// <b>Read from compiled IL rather than from source text</b>, following the pattern
/// <c>LocalisationGateTests.The_client_calls_the_document_language_interop</c> established: a source
/// scan would be satisfied by the key appearing in the AppHost's comments, which it does.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-14")]
[Trait("Requirement", "AD-7")]
[Trait("Requirement", "AR-37")]
public sealed class CrossOriginConfigurationTests
{
    /// <summary>
    /// The AppHost states the same configuration key the Host reads.
    /// </summary>
    [Fact]
    public void The_app_host_injects_the_key_the_host_reads()
    {
        var appHost = ProductionAssemblies.All
            .Single(assembly => assembly.GetName().Name == "Yello.AppHost");

        using var module = ModuleDefinition.ReadModule(appHost.Location);

        var states = module.GetTypes()
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Any(instruction =>
                instruction.Operand is string text
                && text.Equals(CrossOriginPolicy.OriginConfigurationKey, StringComparison.Ordinal));

        Assert.True(
            states,
            $"Yello.AppHost states no '{CrossOriginPolicy.OriginConfigurationKey}' literal, so "
            + "nothing injects the client's origin into Yello.Host under `aspire run`. The Host "
            + "fails closed when the setting is absent - every cross-origin browser request is "
            + "refused - and it says so at startup, but the symptom a developer meets first is a "
            + "CORS error in a browser console that names neither file. The AppHost cannot compile "
            + "against Yello.Host (the Aspire SDK marks its project references "
            + "ReferenceOutputAssembly=false), so the key is written twice by necessity and this "
            + "is what keeps the two spellings equal.");
    }

    /// <summary>
    /// The planted signal for the assertion above.
    /// </summary>
    /// <remarks>
    /// The assertion is a search for one literal in one assembly, and it would pass just as happily
    /// over a comparison that matched anything - a case-insensitive contains, say, or a scan of the
    /// wrong member. This runs the same comparison against the key with one character changed and
    /// requires it to fail, which is the planted-signal rule applied to a presence assertion rather
    /// than to an absence one.
    /// </remarks>
    [Fact]
    public void The_scan_does_not_match_a_key_that_is_merely_similar()
    {
        var appHost = ProductionAssemblies.All
            .Single(assembly => assembly.GetName().Name == "Yello.AppHost");

        using var module = ModuleDefinition.ReadModule(appHost.Location);

        var nearMiss = CrossOriginPolicy.OriginConfigurationKey + "s";

        var states = module.GetTypes()
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Any(instruction =>
                instruction.Operand is string text
                && text.Equals(nearMiss, StringComparison.Ordinal));

        Assert.False(
            states,
            $"The scan matched '{nearMiss}', which is not the key the Host reads - so the "
            + "assertion beside it does not establish that the two sides agree.");
    }
}
