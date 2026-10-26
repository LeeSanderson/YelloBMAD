namespace Yello.Contracts.Tasks;

/// <summary>
/// A Task edit: any of its title, Status, due date and Labels. FR-20; story 2.3, open question 1 (a); story 2.4, open
/// question 1 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Read as a JSON merge patch</b> (RFC 7396's semantics): a member the body does not name is unchanged;
/// <c>"dueDate": null</c> clears the due date; <c>"title": null</c> and <c>"statusId": null</c> are refused,
/// because neither may be cleared; <c>"labelIds": []</c> clears the Labels and <c>"labelIds": null</c> is refused; a
/// body naming none of the four is refused. This record states the members
/// a client may send; the Host reads the body as a document so that "absent" and <c>null</c> stay distinct.
/// </para>
/// <para>
/// <b>No description, position key, Project or Space</b>, deliberately: the description is FR-31's (Epic 7),
/// the position key is the server's (AD-15), the Project is FR-41's own move and the Space never changes.
/// </para>
/// </remarks>
/// <param name="Title">The new title; trimmed, then 1-200 characters.</param>
/// <param name="StatusId">The new Status; one of the Task's Project's effective set.</param>
/// <param name="DueDate">The new due date, an instant with an offset; stored in UTC.</param>
/// <param name="LabelIds">
/// The Task's whole new set of Labels, each one of its Space's, without repeats (story 2.4). Applying a Label is an
/// edit of the Task, so it takes the Task's capability, not the Label-definition one.
/// </param>
public sealed record EditTaskRequest(string? Title, Guid? StatusId, DateTimeOffset? DueDate, IReadOnlyList<Guid>? LabelIds = null);
