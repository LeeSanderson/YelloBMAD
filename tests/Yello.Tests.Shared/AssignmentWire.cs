using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The test side of assigning a Task: the assignment body (<c>{ membershipId }</c>, <c>null</c> unassigns), readings of the
/// Task answer and of the candidates read (<c>{ assignees: [{ membershipId, displayName }] }</c>), and the fixed values the
/// suites assert against. The routes, the problem type and the List parameters are the contract's own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bodies are JSON text or anonymous objects</b>, never a contract type, so a case can send what the route
/// distinguishes: a <c>membershipId</c> set to a value, set to <c>null</c> (unassign), or absent (refused).
/// </para>
/// </remarks>
public static class AssignmentWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
    /// The candidates read's JSON members, in ordinal order: <c>AssigneeListResponse(Assignees)</c>.
    /// </summary>
    public static IReadOnlyList<string> ListMembers { get; } = ["assignees"];

    /// <summary>
    /// One candidate's JSON members, in ordinal order: <c>AssigneeResponse(MembershipId, DisplayName)</c>. Never an
    /// address, a Role, an Account id or a Space id.
    /// </summary>
    public static IReadOnlyList<string> CandidateMembers { get; } = ["displayName", "membershipId"];

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
