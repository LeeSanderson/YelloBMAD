namespace Yello.Contracts.Tasks;

/// <summary>
/// Where a bulk move put the Tasks and how many it moved. The Tasks themselves are read from the destination's Board or List.
/// </summary>
/// <param name="ProjectId">The destination Project.</param>
/// <param name="StatusId">The Status the moved Tasks hold there.</param>
/// <param name="MovedCount">How many Tasks were moved; zero when the Status held none.</param>
public sealed record MoveStatusTasksToProjectResponse(Guid ProjectId, Guid StatusId, int MovedCount);
