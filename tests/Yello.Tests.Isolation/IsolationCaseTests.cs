using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// SM-1's epic-1 isolation cases, every one on both surfaces: two Accounts, each with its own Personal
/// Space, each reaching for the other's by direct identifier, deep link, listing, aggregate, write and
/// API. AC1, AC2, AC6, AC10; I-1, I-3, I-4, I-5, I-9, I-11, I-12, I-13, I-17.
/// </summary>
/// <remarks>
/// <para>
/// <b>One generated theory over <see cref="IsolationCases"/></b>, <c>cases x surfaces</c> (B6). The
/// story-1.9 ATDD scaffolds ISO-001 to ISO-011 were one fact per row, each looping both surfaces; on
/// activation they became the rows, and their assertions became the rows' expectations.
/// </para>
/// <para>
/// <b>One world for the class</b>, so each row reads deltas (the refusal rows it added, the log lines
/// since its cursor) and none depends on another having run first.
/// </para>
/// </remarks>
/// <param name="fixture">The class's world.</param>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "FR-15")]
[Trait("Requirement", "FR-36")]
[Trait("Requirement", "NFR-7")]
[Trait("Requirement", "AD-3")]
[Trait("Requirement", "AD-4")]
[Trait("Requirement", "AD-20")]
public sealed class IsolationCaseTests(IsolationWorldFixture fixture) : IClassFixture<IsolationWorldFixture>
{
    /// <summary>
    /// The boundary runner's rows: every active row it owns, on every surface.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> Rows => IsolationCases.BoundaryRows;

    /// <summary>
    /// Each row answers on each surface exactly as the row says, discloses nothing in the body, and
    /// leaves no secret in the Host's log.
    /// </summary>
    /// <param name="caseId">The row.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(Rows))]
    public Task Each_case_answers_on_each_surface_as_its_row_says(string caseId, IsolationSurface surface) =>
        CaseRun.RunAsync(fixture.World, IsolationCases.Named(caseId), surface);
}
