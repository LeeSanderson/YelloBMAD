namespace Yello.Contracts.Tasks;

/// <summary>
/// A Task creation: its title and nothing else. FR-19; story 2.2, AC2.
/// </summary>
/// <remarks>
/// <b>No Status, Project, Space or position</b>, deliberately: the Project and Space are the route's, the
/// Status is the first of the Project's effective set, and the position key is the server's - so "takes
/// the first Status" and "belongs to one Project and one Space" are true by construction.
/// </remarks>
/// <param name="Title">The title; trimmed, then 1-200 characters.</param>
public sealed record CreateTaskRequest(string? Title);
