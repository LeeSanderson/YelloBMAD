using Yello.Application.Statuses;
using Yello.Contracts;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// The refusals a Project Status write answers. None echoes the name or an id sent.
/// </summary>
internal static class ProjectStatusAnswers
{
    /// <summary>
    /// <c>400 invalid-status-name</c>.
    /// </summary>
    /// <returns>The answer.</returns>
    internal static IResult InvalidName() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidStatusName,
            title: "A Status's name is 1 to 100 characters.");

    /// <summary>
    /// The answer to a write the slice refused.
    /// </summary>
    /// <param name="outcome">Why it was refused.</param>
    /// <returns>The answer.</returns>
    internal static IResult Refusal(ProjectStatusWriteOutcome outcome) => outcome switch
    {
        ProjectStatusWriteOutcome.NameTaken => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.StatusNameTaken,
            title: "This Project already has a Status of that name."),

        ProjectStatusWriteOutcome.SetEmpty => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.StatusSetEmpty,
            title: "A Project keeps at least one Status."),

        ProjectStatusWriteOutcome.PositionInvalid => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidStatusPosition,
            title: "A Status is placed after another Status of the same Project."),

        // Not recorded: an in-Space not-found crossed no boundary. The body is the boundary's.
        _ => BoundaryProblems.NotAvailable(),
    };
}
