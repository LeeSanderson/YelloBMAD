namespace Yello.Contracts.Spaces;

/// <summary>
/// The body of an add to the Space's default Statuses: its name, and optionally the default it goes right after. Never a
/// Space - that is the route's.
/// </summary>
/// <param name="Name">The name, 1-100 characters once trimmed, unique among the defaults ignoring case.</param>
/// <param name="AfterStatusId">The default it goes after, or <c>null</c> to put it last.</param>
public sealed record AddSpaceStatusRequest(string? Name, Guid? AfterStatusId);
