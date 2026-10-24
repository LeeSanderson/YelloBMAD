using Yello.Domain.Memberships;

namespace Yello.Domain.AccessRefusals;

/// <summary>
/// One refusal at the Space boundary: every <c>403</c> and every Space-boundary <c>404</c>, written
/// by the request pipeline. AD-20 / AR-11; NFR-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Write-only from the product's point of view.</b> There is no read surface over this record -
/// AD-24 allows exactly two Account-scoped surfaces and this is neither - and a gate fails the build
/// on any type but the recorder and the purge referring to it.
/// </para>
/// <para>
/// <b>Not Space-scoped, by design.</b> The target is <see cref="TargetSpaceId"/>, not
/// <c>SpaceId</c>, with no foreign key to <c>Space</c>: a refusal for a Space that does not exist is
/// recorded like any other, and a cross-Space purge has to see every row. So the table carries no
/// row-level security predicate and no query filter (story 1.6, decision 2).
/// </para>
/// <para>
/// <b>Retained 90 days</b> (PRD section 12, assumption 11) and purged by request traffic, at most
/// daily, never on a timer (AD-10).
/// </para>
/// </remarks>
public sealed class AccessRefusal
{
    /// <summary>
    /// The record's identity, produced application-side.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Account that was refused.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The Space the request named - which may not exist.
    /// </summary>
    public required Guid TargetSpaceId { get; init; }

    /// <summary>
    /// The capability the refused endpoint declares.
    /// </summary>
    public required Capability Capability { get; init; }

    /// <summary>
    /// The HTTP status the caller received: <c>404</c> or <c>403</c>.
    /// </summary>
    public required int Outcome { get; init; }

    /// <summary>
    /// Which of the two refusals this was.
    /// </summary>
    public required AccessRefusalKind Kind { get; init; }

    /// <summary>
    /// When it happened, in UTC.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
