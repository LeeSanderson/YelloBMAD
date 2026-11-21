namespace Yello.Contracts.Spaces;

/// <summary>
/// One Project in a <see cref="ProblemTypes.StatusRenameConflict"/> refusal: a Project of the request's Space that gave the
/// default a name of its own.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="ProjectName">Its name.</param>
/// <param name="Name">Its own name for the Status.</param>
/// <param name="TaskCount">How many of its Tasks hold the Status.</param>
public sealed record StatusRenameConflictResponse(Guid ProjectId, string ProjectName, string Name, int TaskCount);
