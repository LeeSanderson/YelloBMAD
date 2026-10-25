using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The operational record, <c>docs/operations.md</c>, carries what the PRD wrote as release
/// conditions: the §6.4 data-protection gate and its five prerequisites, the obligation parked against
/// it, AR-40c's status, the operator-side tripwire, and the state of each of Lee's prerequisites.
/// Story 1.10 AC7, AC8, AC9 (R5, R11); Task 12, Q18 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Reads <c>docs/operations.md</c></b>, the operational record story 1.10 wrote.
/// </para>
/// <para>
/// <b>Why a gate over prose.</b> R11's verification is that "the five prerequisites are enumerated
/// ... so a missing one is a story failure rather than a later discovery". A story record stops being
/// read when the story closes; this file is the record that outlives it, and these cases keep a later
/// edit from dropping a prerequisite. The phrases asserted are the ACs' own words, so a reworded
/// record that still says the same thing needs the gate corrected, not the record weakened - record
/// any such correction in the Debug Log.
/// </para>
/// <para>
/// <b>What it cannot prove</b>: that the operator agrees (P7). AC8's assertion is Lee's to confirm.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
public sealed partial class OperationsRecordTests
{
    /// <summary>
    /// 1.10-ARCH-017 - the §6.4 gate is recorded as an operational condition: a single-operator
    /// deployment claiming no data-protection posture, the trigger that makes the PRD non-compliant,
    /// and the five prerequisites that become live at that moment.
    /// </summary>
    [Fact]
    [Trait("Requirement", "PRD-6.4")]
    public void The_data_protection_gate_is_recorded_with_its_five_prerequisites()
    {
        var record = OperationsRecord();

        string[] required =
        [
            "single-operator deployment",
            "no data-protection posture",
            "anyone other than the operator",
            "non-compliant until amended",
            "operational condition",
            "lawful basis",
            "data region",
            "encryption at rest",
            "breach-notification",
            "subject-access",
        ];

        var missing = required.Where(phrase => !record.Contains(phrase, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.True(missing.Count == 0, "docs/operations.md does not carry the §6.4 gate in full (AC8). Missing: " + string.Join("; ", missing));
        Assert.Contains("§6.3", record, StringComparison.Ordinal);
    }

    /// <summary>
    /// 1.10-ARCH-018 - the browser spellcheck and cloud IME egress, accepted for v1, is carried here
    /// with the reason it was parked: "against §6.4's data-protection gate".
    /// </summary>
    [Fact]
    [Trait("Requirement", "PRD-6.4")]
    public void The_spellcheck_and_ime_egress_travels_with_the_gate()
    {
        var record = OperationsRecord();

        Assert.Contains("spellcheck", record, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IME", record, StringComparison.Ordinal);
        Assert.Matches(ParkedAgainstTheGate, record);
        Assert.Contains("largest free-text surface", record, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 1.10-ARCH-019 - AR-40c's status and procedure: not confirmed, <c>MAXDOP = 1</c> in force, relaxed
    /// only by a new migration and only with the pooled-connection case green.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-40c")]
    [Trait("Requirement", "AD-2")]
    public void The_session_context_defect_finding_and_the_maxdop_rule_are_recorded()
    {
        var record = OperationsRecord();

        Assert.Contains("AR-40c", record, StringComparison.Ordinal);
        Assert.Matches(MaxdopOne, record);
        Assert.Contains("new migration", record, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pooled-connection", record, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 1.10-ARCH-020 - R11's tripwire is an operator-side datastore query counting Accounts, stated as
    /// never an endpoint (§6.1 forbids an in-product aggregate spanning Spaces).
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "PRD-6.4")]
    public void The_tripwire_is_an_operator_query_and_never_an_endpoint()
    {
        var record = OperationsRecord();

        Assert.Matches(AccountCountQuery, record);
        Assert.Matches(NeverAnEndpoint, record);
    }

    /// <summary>
    /// 1.10-ARCH-021 - each of Lee's prerequisites P1-P7 carries a recorded state, and none is claimed
    /// done on Lee's behalf.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-38")]
    public void Each_operator_prerequisite_carries_a_state()
    {
        var record = OperationsRecord();

        var stateless = Enumerable.Range(1, 7)
            .Select(n => $"P{n}")
            .Where(prerequisite => !Regex.IsMatch(
                record,
                $@"^.*\b{prerequisite}\b.*\b(?:provided|not yet|declined)\b.*$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1)))
            .ToList();

        Assert.True(stateless.Count == 0, "A prerequisite has no recorded state (provided / not yet / declined): " + string.Join(", ", stateless));
    }

    private static string OperationsRecord()
    {
        var file = new FileInfo(Path.Combine(RepositoryLayout.Root.FullName, "docs", "operations.md"));

        Assert.True(file.Exists, "docs/operations.md does not exist (story 1.10 Task 12).");

        return File.ReadAllText(file.FullName);
    }

    [GeneratedRegex(@"against §6\.4['’]s data-protection gate", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex ParkedAgainstTheGate { get; }

    [GeneratedRegex(@"MAXDOP\s*=\s*1\b", RegexOptions.None, 1000)]
    private static partial Regex MaxdopOne { get; }

    [GeneratedRegex(@"SELECT\s+COUNT\(\*\)\s+FROM\s+dbo\.Account\b", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex AccountCountQuery { get; }

    [GeneratedRegex(@"\b(?:never|not)\s+an\s+endpoint\b", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex NeverAnEndpoint { get; }
}
