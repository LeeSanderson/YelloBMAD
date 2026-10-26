namespace Yello.Infrastructure.Persistence;

/// <summary>
/// One application of a Label to a Task: the many-to-many FR-22 names, and nothing more. Story 2.4, open question 9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Infrastructure only.</b> It has no behaviour of its own: the Domain sees the set of Label ids on a Task edit
/// and in a Task's read, and the stores turn that set into these rows.
/// </para>
/// <para>
/// <b>Space-scoped like every table it joins</b> (AD-2): a non-nullable <c>SpaceId</c>, three security predicates, a
/// query filter, and both references composite on <c>(SpaceId, ...)</c> (AR-8), so an application can never join a
/// Task of one Space to a Label of another.
/// </para>
/// </remarks>
internal sealed class TaskLabel
{
    /// <summary>
    /// The Space both the Task and the Label belong to.
    /// </summary>
    internal required Guid SpaceId { get; init; }

    /// <summary>
    /// The Task the Label is applied to.
    /// </summary>
    internal required Guid TaskId { get; init; }

    /// <summary>
    /// The Label applied.
    /// </summary>
    internal required Guid LabelId { get; init; }

    /// <summary>
    /// When it was applied, in UTC.
    /// </summary>
    internal required DateTimeOffset AppliedAt { get; init; }
}
