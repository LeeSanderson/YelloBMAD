using System.Net.Http.Json;
using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The test side of story 4.4's Membership routes: the list (<c>GET /api/v1/spaces/{spaceId}/memberships</c>, an optional
/// <c>after</c> cursor) and the Role change (<c>PATCH .../memberships/{membershipId}</c>, body <c>{ role }</c>), the list's
/// page and row shapes, and the fixed values the suites assert against. Written by the story 4.4 ATDD run (2026-11-02).
/// </summary>
/// <remarks>
/// <para>
/// <b>The routes and problem types are the contract's</b> (<c>MembershipRoutes</c>, <c>ProblemTypes.OwnerRoleFixed</c>,
/// <c>ProblemTypes.RoleNotAssignable</c>): the ATDD run's six stand-ins were swapped for them and deleted when story 4.4
/// declared them. Everything here is the test's reading of the wire.
/// </para>
/// <para>
/// <b>Removal and leaving</b> use the same item route with <c>DELETE</c>, and the contract's leave route
/// (<c>MembershipRoutes.Own</c>) for the caller's own Membership.
/// </para>
/// </remarks>
public static class MembershipWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The capability both routes declare, as the <c>403</c> body names it (Lee's Q4 (a): the list sits under the write
    /// capability, never under <c>ReadProjectsAndTasks</c>).
    /// </summary>
    public static string CapabilityCode => "change-membership-role";

    /// <summary>
    /// The list's page size, Q8 (a) - stated here independently of the server's named constant, so a change to either is
    /// seen.
    /// </summary>
    public static int PageSize => 50;

    /// <summary>
    /// The structured log event every publish writes (AC6, Q7 (a)): one line per affected Account, carrying the Account and
    /// the Space and nothing else. Observable from the Host's console without test-only production code (1.9 Q4).
    /// </summary>
    public static string PublishedEvent => "MembershipChangedPublished";

    /// <summary>
    /// The page's JSON members, in ordinal order: <c>MembershipPageResponse(Total, Memberships, NextAfter)</c>.
    /// </summary>
    public static IReadOnlyList<string> PageMembers { get; } = ["memberships", "nextAfter", "total"];

    /// <summary>
    /// A row's JSON members, in ordinal order: <c>MembershipResponse(Id, DisplayName, EmailAddress, Role, IsYou,
    /// InSpaceNow)</c>, on the list and the single-row read alike. Never the Account id, the normalised address, the Space or
    /// the join date (AC1, AD-23).
    /// </summary>
    public static IReadOnlyList<string> RowMembers { get; } = ["displayName", "emailAddress", "id", "inSpaceNow", "isYou", "role"];

    /// <summary>
    /// The Role change's body: exactly <c>role</c>, the stable code.
    /// </summary>
    /// <param name="role">The code, or <c>null</c> to send JSON null.</param>
    /// <returns>The body.</returns>
    public static HttpContent ChangeBody(string? role) => JsonContent.Create(new { role }, options: JsonOptions);

    /// <summary>
    /// Reads a <c>200</c> page.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The page.</returns>
    public static MembershipPage ParsePage(string body) =>
        JsonSerializer.Deserialize<MembershipPage>(body, JsonOptions) ?? throw new InvalidOperationException("The list answered no page.");

    /// <summary>
    /// Reads a <c>200</c> Role change's answer: the updated row.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The row.</returns>
    public static ChangedRow ParseRow(string body) =>
        JsonSerializer.Deserialize<ChangedRow>(body, JsonOptions) ?? throw new InvalidOperationException("The change answered no row.");

    /// <summary>
    /// The Role change's row as JSON member names, in ordinal order: <c>MembershipRoleChangeResponse(Id, DisplayName, Role,
    /// IsYou)</c> - never an address (code review 2026-11-03, decided by Lee).
    /// </summary>
    public static IReadOnlyList<string> ChangedRowMembers { get; } = ["displayName", "id", "isYou", "role"];

    /// <summary>
    /// A JSON object's member names, in ordinal order.
    /// </summary>
    /// <param name="body">The JSON object.</param>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> ObjectMemberNames(string body)
    {
        using var document = JsonDocument.Parse(body);

        return NamesOf(document.RootElement);
    }

    /// <summary>
    /// The member names of a page body and of each of its rows, in ordinal order, so a shape assertion sees a member the
    /// record would silently drop.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The page's names, and one ordered list per row.</returns>
    public static (IReadOnlyList<string> Page, IReadOnlyList<IReadOnlyList<string>> Rows) MemberNames(string body)
    {
        using var document = JsonDocument.Parse(body);

        var page = document.RootElement.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal).ToList();
        var rows = document.RootElement.TryGetProperty("memberships", out var memberships) && memberships.ValueKind == JsonValueKind.Array
            ? memberships.EnumerateArray().Select(NamesOf).ToList()
            : [];

        return (page, rows);
    }

    /// <summary>
    /// The capability the removal route declares, as its <c>403</c> body names it.
    /// </summary>
    public static string RemoveCapabilityCode => "remove-membership";

    /// <summary>
    /// The capability the leave route declares: the one every Role holds, because leaving is decided by the caller's
    /// identity and not by its Role.
    /// </summary>
    public static string LeaveCapabilityCode => "read-projects-and-tasks";

    /// <summary>
    /// Reads the single-row read's body.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The row.</returns>
    public static ListedMembership ParseReadRow(string body) =>
        JsonSerializer.Deserialize<ListedMembership>(body, JsonOptions) ?? throw new InvalidOperationException("The read answered no row.");

    private static IReadOnlyList<string> NamesOf(JsonElement row) =>
        [.. row.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal)];
}

/// <summary>
/// Story 4.4's list page as the test reads it: <c>MembershipPageResponse</c>'s shape (Q8 (a), Q9 (a)), restated independently
/// so a contract change is seen by the suites rather than silently followed.
/// </summary>
/// <param name="Total">The true count of Memberships in the Space - a separate count, never the page length.</param>
/// <param name="Memberships">This page's rows, in join order.</param>
/// <param name="NextAfter">The opaque cursor for the next page, or <c>null</c> on the last.</param>
public sealed record MembershipPage(int Total, IReadOnlyList<ListedMembership> Memberships, string? NextAfter);

/// <summary>
/// One row of story 4.4's Membership list as the test reads it: <c>MembershipResponse</c>'s shape. Kept in the harness
/// because <c>Yello.Tests.Client</c>'s fake serialises the same shape.
/// </summary>
/// <param name="Id">The Membership's id, which the Role change names.</param>
/// <param name="DisplayName">The Account's display name, read live (Lee's Q5 (a)).</param>
/// <param name="EmailAddress">The Account's address as typed at registration, never the normalised form (Lee's Q4 (a)).</param>
/// <param name="Role">The Role's stable code: <c>owner</c>, <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IsYou">Whether the row is the caller's own Membership.</param>
/// <param name="InSpaceNow">Whether an unexpired presence row exists for this Account in this Space.</param>
public sealed record ListedMembership(Guid Id, string DisplayName, string EmailAddress, string Role, bool IsYou, bool InSpaceNow = false);

/// <summary>
/// The row a Role change answers, as the test reads it: <c>MembershipRoleChangeResponse</c>'s shape, no address (code review
/// 2026-11-03, decided by Lee).
/// </summary>
/// <param name="Id">The Membership's id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Role">The Role's stable code.</param>
/// <param name="IsYou">Whether it is the caller's own row.</param>
public sealed record ChangedRow(Guid Id, string DisplayName, string Role, bool IsYou);
