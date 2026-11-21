using Yello.Domain.Statuses;

namespace Yello.Application.Statuses;

/// <summary>
/// One Project's effective Status set with how many of its Tasks hold each Status: what the read and every delta write
/// answer, so the editor re-renders from the server's own answer.
/// </summary>
/// <param name="Statuses">The effective set, in Board order.</param>
/// <param name="TaskCounts">The Project's Tasks per Status; a Status no Task holds is absent.</param>
public sealed record ProjectStatusSet(IReadOnlyList<EffectiveStatus> Statuses, IReadOnlyDictionary<Guid, int> TaskCounts);

/// <summary>
/// The outcome of a Project Status delta write.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Set">The Project's set after the write, when <see cref="Outcome"/> is <see cref="ProjectStatusWriteOutcome.Written"/>.</param>
public sealed record ProjectStatusWriteResult(ProjectStatusWriteOutcome Outcome, ProjectStatusSet? Set)
{
    /// <summary>
    /// A refusal; nothing was written.
    /// </summary>
    /// <param name="outcome">Why.</param>
    /// <returns>The result.</returns>
    public static ProjectStatusWriteResult Refused(ProjectStatusWriteOutcome outcome) => new(outcome, null);
}

/// <summary>
/// What a Project Status delta write came to.
/// </summary>
public enum ProjectStatusWriteOutcome
{
    /// <summary>
    /// The write was recorded.
    /// </summary>
    Written,

    /// <summary>
    /// The Project is not the active Space's, or the Status in the path is not in the Project's set.
    /// </summary>
    NotFound,

    /// <summary>
    /// The name is another Status's in the Project's set, compared case-insensitively.
    /// </summary>
    NameTaken,

    /// <summary>
    /// The write would leave the Project with no Status.
    /// </summary>
    SetEmpty,

    /// <summary>
    /// The Status to place after is not another Status of the Project's set.
    /// </summary>
    PositionInvalid,
}
