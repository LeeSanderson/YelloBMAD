using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The test side of assigning a Task: the assignment route (<c>PUT /api/v1/spaces/{spaceId}/tasks/{taskId}/assignee</c>,
/// body <c>{ membershipId }</c>, <c>null</c> unassigns, answered <c>200</c> with the Task), the candidates read
/// (<c>GET /api/v1/spaces/{spaceId}/assignees</c>, answered <c>{ assignees: [{ membershipId, displayName }] }</c>), the List
/// View's Assignee sort and filter parameters, and the fixed values the suites assert against.
/// </summary>
/// <remarks>
/// <para>
/// <b>Temporary stand-ins</b>, the 4.4 and 4.5 precedent: <see cref="AssigneeRoute"/>, <see cref="AssigneeOf"/>,
/// <see cref="AssigneesRoute"/>, <see cref="AssigneesOf"/>, <see cref="InvalidAssigneeProblemType"/>,
/// <see cref="SortAssignee"/>, <see cref="AssigneeParameter"/> and <see cref="UnassignedParameter"/> name members the
/// contract does not declare yet (<c>TaskRoutes.Assignee</c> / <c>AssigneeOf</c>, <c>AssigneeRoutes.List</c> /
/// <c>ListOf</c>, <c>ProblemTypes.InvalidAssignee</c>, <c>ListSorts.Assignee</c>, the <c>ListRoutes</c> parameters). When the
/// contract declares them, every use is swapped for the contract member and the stand-in deleted. Everything else here is
/// a lasting reading of the wire.
/// </para>
/// <para>
/// <b>Bodies are JSON text or anonymous objects</b>, never a contract type, so a case can send what the route
/// distinguishes: a <c>membershipId</c> set to a value, set to <c>null</c> (unassign), or absent (refused).
/// </para>
/// </remarks>
public static class AssignmentWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The assignment route's pattern. Stand-in for <c>TaskRoutes.Assignee</c>.
    /// </summary>
    public static string AssigneeRoute => "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}/assignee";

    /// <summary>
    /// The candidates read's pattern. Stand-in for <c>AssigneeRoutes.List</c>.
    /// </summary>
    public static string AssigneesRoute => "/api/v1/spaces/{spaceId:guid}/assignees";

    /// <summary>
    /// The refusal of a missing, malformed, random, foreign or just-removed Membership id: byte-identical for every one.
    /// Stand-in for <c>ProblemTypes.InvalidAssignee</c>.
    /// </summary>
    public static string InvalidAssigneeProblemType => "urn:yello:problem:invalid-assignee";

    /// <summary>
    /// The capability the assignment route declares, as its <c>403</c> body names it: the matrix's own "Assign a Task" row.
    /// </summary>
    public static string AssignCapabilityCode => "assign-task";

    /// <summary>
    /// The capability the candidates read declares: every Role reads it.
    /// </summary>
    public static string AssigneesCapabilityCode => "read-projects-and-tasks";

    /// <summary>
    /// The outbox kind of the assignment email, as the send record stores it.
    /// </summary>
    public static string TaskAssignedKind => "task-assigned";

    /// <summary>
    /// The outbox kind of the Invitation email, as the send record stores it.
    /// </summary>
    public static string InvitationIssuedKind => "invitation-issued";

    /// <summary>
    /// The Assignee sort's code. Stand-in for <c>ListSorts.Assignee</c>.
    /// </summary>
    public static string SortAssignee => "assignee";

    /// <summary>
    /// The List filter's repeatable Membership-id parameter. Stand-in for the <c>ListRoutes</c> member.
    /// </summary>
    public static string AssigneeParameter => "assignee";

    /// <summary>
    /// The List filter's "no Assignee" flag, the <c>dueNone</c> precedent. Stand-in for the <c>ListRoutes</c> member.
    /// </summary>
    public static string UnassignedParameter => "unassigned";

    /// <summary>
    /// The candidates read's JSON members, in ordinal order: <c>AssigneeListResponse(Assignees)</c>.
    /// </summary>
    public static IReadOnlyList<string> ListMembers { get; } = ["assignees"];

    /// <summary>
    /// One candidate's JSON members, in ordinal order: <c>AssigneeResponse(MembershipId, DisplayName)</c>. Never an
    /// address, a Role, an Account id or a Space id.
    /// </summary>
    public static IReadOnlyList<string> CandidateMembers { get; } = ["displayName", "membershipId"];

    /// <summary>
    /// The assignment path of one Task. Stand-in for <c>TaskRoutes.AssigneeOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="taskId">The Task.</param>
    /// <returns>The path.</returns>
    public static string AssigneeOf(Guid spaceId, Guid taskId) => $"/api/v1/spaces/{spaceId:D}/tasks/{taskId:D}/assignee";

    /// <summary>
    /// The candidates path of one Space. Stand-in for <c>AssigneeRoutes.ListOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string AssigneesOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/assignees";

    /// <summary>
    /// The assignment body: <c>{ membershipId }</c>, written as JSON <c>null</c> when <paramref name="membershipId"/> is
    /// <c>null</c> (unassign).
    /// </summary>
    /// <param name="membershipId">The Membership, or <c>null</c>.</param>
    /// <returns>The body.</returns>
    public static HttpContent Body(Guid? membershipId) => JsonContent.Create(new { membershipId }, options: JsonOptions);

    /// <summary>
    /// A body of the case's own text, for the refusals of a body that is not an object or names no Membership.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The body.</returns>
    public static HttpContent RawBody(string json) => new StringContent(json, Encoding.UTF8, "application/json");

    /// <summary>
    /// The <c>assigneeMembershipId</c> a Task answer carries, or <c>null</c> when it is <c>null</c> or absent.
    /// </summary>
    /// <param name="taskBody">A <c>TaskResponse</c> body.</param>
    /// <returns>The Membership id, or <c>null</c>.</returns>
    public static Guid? AssigneeIn(string taskBody)
    {
        using var document = JsonDocument.Parse(taskBody);

        return document.RootElement.TryGetProperty("assigneeMembershipId", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetGuid()
            : null;
    }

    /// <summary>
    /// Reads the candidates read's body.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>Each candidate, in served order.</returns>
    public static IReadOnlyList<Candidate> ParseCandidates(string body) =>
        (JsonSerializer.Deserialize<CandidateList>(body, JsonOptions) ?? throw new InvalidOperationException("The read answered no list.")).Assignees;

    /// <summary>
    /// The member names of the candidates body and of each candidate, in ordinal order, so a shape assertion sees a member
    /// a record would silently drop.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The list's names, and one ordered list per candidate.</returns>
    public static (IReadOnlyList<string> List, IReadOnlyList<IReadOnlyList<string>> Candidates) MemberNames(string body)
    {
        using var document = JsonDocument.Parse(body);

        var list = NamesOf(document.RootElement);
        var candidates = new List<IReadOnlyList<string>>();

        if (document.RootElement.TryGetProperty("assignees", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            candidates.AddRange(rows.EnumerateArray().Select(NamesOf));
        }

        return (list, candidates);
    }

    private static IReadOnlyList<string> NamesOf(JsonElement element) =>
        [.. element.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Whether <paramref name="email"/> is an assignment email: the subject the story's copy gives
    /// ("{Assigner} assigned you a Task in {Space}"). An Invitation email to the same address is not one.
    /// </summary>
    /// <param name="email">A dropped email.</param>
    /// <returns>Whether it is an assignment email.</returns>
    public static bool IsAssignmentEmail(DroppedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return email.Subject.Contains("assigned you a Task", StringComparison.Ordinal);
    }

    /// <summary>
    /// The assignment emails to <paramref name="address"/> among <paramref name="emails"/>.
    /// </summary>
    /// <param name="emails">Every dropped email.</param>
    /// <param name="address">The recipient.</param>
    /// <returns>The assignment emails to it.</returns>
    public static IReadOnlyList<DroppedEmail> AssignmentEmailsTo(IEnumerable<DroppedEmail> emails, string address) =>
        [.. emails.Where(email => string.Equals(email.To, address, StringComparison.OrdinalIgnoreCase) && IsAssignmentEmail(email))];
}

/// <summary>
/// The candidates read as the test reads it: <c>AssigneeListResponse</c>'s shape, restated so a contract change is seen.
/// </summary>
/// <param name="Assignees">Every candidate, unpaged, in join order.</param>
public sealed record CandidateList(IReadOnlyList<Candidate> Assignees);

/// <summary>
/// One candidate as the test reads it: <c>AssigneeResponse</c>'s shape.
/// </summary>
/// <param name="MembershipId">The Membership the assignment body names.</param>
/// <param name="DisplayName">The Account's display name, as written.</param>
public sealed record Candidate(Guid MembershipId, string DisplayName);
