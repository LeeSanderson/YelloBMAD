using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Boards;
using Yello.Contracts.Labels;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Story 3.4 - one Account, several Spaces (written by its ATDD run, 2026-10-31). SM-1 extended to one Account holding
/// two Spaces (<c>deferred-work.md:407</c>): AC1 (a foreign object named under the first Space), AC2 (the Owner of one
/// Space holding a lesser Role in another, and I-10), AC3 (a Token on another Space its own Account owns, one row per
/// Space-scoped route family, vector <see cref="IsolationVector.TokenOwnOtherSpace"/>), AC4 (the pooled connection) and
/// AC9 (aggregates and listings). Every row states both surfaces (AC6, GATE-001).
/// </summary>
/// <remarks>
/// <para>
/// <b>Every row acts as fresh Accounts</b> (<see cref="IsolationWorld.OwnerOfTwoAsync"/>,
/// <see cref="IsolationWorld.OwnerElsewhereAsync"/>), so A and B - every other row's subject - are never changed, and
/// no row depends on another having run first. B is the stranger: it holds no Membership in any Space these rows create.
/// </para>
/// <para>
/// <b>AC2's no-Membership arm has no rows here.</b> A owns SA and holds no Membership in SB, so the existing rows
/// <c>ISO-WRITE-PROJECT</c>, <c>ISO-WRITE-TASK</c>, <c>ISO-CREATE-LABEL-FOREIGN-SPACE</c>,
/// <c>ISO-EDIT-TASK-FOREIGN-SPACE</c>, <c>ISO-MOVE-TASK-FOREIGN-SPACE</c>, <c>ISO-RENAME-SPACE-CROSS</c>,
/// <c>ISO-DELETION-SUMMARY-CROSS</c> and <c>ISO-DELETE-SPACE-CROSS</c> are exactly that arm, each a boundary
/// <c>404</c> with one <c>CrossSpace</c> record (AC7).
/// </para>
/// <para>
/// <b>C1 (decided by Lee, 2026-10-31).</b> The lesser-Role rows assert <c>403</c> and <c>InsufficientRole</c> (AD-3,
/// AR-10); AC7's <c>CrossSpace</c> binds the cross-Space rows (AC3's ApiToken arm, and the no-Membership arm above).
/// AC1's rows record nothing (Q5 (a)).
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private static IEnumerable<IsolationCase> SeveralSpacesCases() =>
    [
        // AC1 - an SA2 object id named under SA1, answered exactly as the in-Space not-found and exactly as a stranger.
        OwnOtherSpace("PROJECT-BOARD", "I-1, I-4", HttpMethod.Get, BoardRoutes.Board, (space, id) => BoardRoutes.BoardOf(space, id), seeded => seeded.ProjectId),
        OwnOtherSpace("PROJECT-LIST", "I-1, I-4", HttpMethod.Get, ListRoute, (space, id) => ListOf(space, id), seeded => seeded.ProjectId),
        OwnOtherSpace("TASK-READ", "I-1, I-4", HttpMethod.Get, TaskRoutes.Task, TaskRoutes.TaskOf, seeded => seeded.TaskIds[0]),
        OwnOtherSpace("TASK-EDIT", "I-2, I-4", HttpMethod.Patch, TaskRoutes.Task, TaskRoutes.TaskOf, seeded => seeded.TaskIds[0]),
        OwnOtherSpace("TASK-DELETE", "I-2, I-4", HttpMethod.Delete, TaskRoutes.Task, TaskRoutes.TaskOf, seeded => seeded.TaskIds[0]),
        OwnOtherSpace("PROJECT-RENAME", "I-2, I-4", HttpMethod.Patch, ProjectRoutes.Rename, ProjectRoutes.ProjectOf, seeded => seeded.ProjectId),
        OwnOtherSpace("PROJECT-DELETE", "I-2, I-4", HttpMethod.Delete, ProjectRoutes.Project, ProjectRoutes.ProjectOf, seeded => seeded.ProjectId),
        OwnOtherSpace("LABEL-DELETE", "I-2, I-4", HttpMethod.Delete, LabelRoutes.Label, LabelRoutes.LabelOf, seeded => seeded.LabelId),
        OwnOtherSpace("TASK-CREATE", "I-2, I-4", HttpMethod.Post, TaskRoutes.Create, TaskRoutes.CreateIn, seeded => seeded.ProjectId),

        new(
            "ISO-OWN-OTHER-SPACE-TASK-MOVE", "I-2, I-4 (3.4 AC1)", IsolationVector.DeepLink, HttpMethod.Post, TaskRoutes.Move,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Om");
                var ownStatus = (await StatusIdsAsync(first.Actor))[1];

                return OwnOtherSpaceRequest(world, first, second, HttpMethod.Post, TaskRoutes.MoveOf, second.TaskIds[0]) with { Body = () => MoveBody(ownStatus, afterTaskId: null) };
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new SameAsAnchorAndStranger(HttpStatusCode.NotFound, ProblemTypes.NotAvailable) { Then = NeitherSpaceChangedAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = NeitherSpaceChangedAsync },
            }),

        new(
            "ISO-OWN-OTHER-SPACE-LABEL-APPLY", "I-2, I-13 (3.4 AC1)", IsolationVector.Write, HttpMethod.Patch, TaskRoutes.Task,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Oa");
                var (_, strangersTask) = await AnyProjectWithTaskAsync(world, world.B);
                var path = TaskRoutes.TaskOf(first.SpaceId, first.TaskIds[1]);

                // The foreign id is in the body: the anchor is the same Task with a random Label, and the stranger is B
                // naming the same SA2 Label on B's own Task.
                return new CaseRequest(
                    first.Actor,
                    HttpMethod.Patch,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => LabelIdsBody(second.LabelId),
                    AnchorBody: () => LabelIdsBody(Guid.NewGuid()),
                    Other: second.Actor,
                    StrangerPaths: [TaskRoutes.TaskOf(world.B.SpaceId, strangersTask)]);
            },
            Both(new SameAsAnchorAndStranger(HttpStatusCode.BadRequest, ProblemTypes.InvalidTaskLabels) { Then = NeitherSpaceChangedAsync })),

        // AC2 - the Owner of SA is a Viewer, then an Admin, in SB (another Account's Space): 403, InsufficientRole, nothing
        // written (C1). The actor is the Account's view of SB, so its Token is one it issued in SB.
        OwnerElsewhere("VIEWER-CREATE-PROJECT", "viewer", "manage-projects", HttpMethod.Post, ProjectRoutes.Create, sb => ProjectRoutes.ListIn(sb.SpaceId)),
        OwnerElsewhere("VIEWER-CREATE-TASK", "viewer", "manage-tasks", HttpMethod.Post, TaskRoutes.Create, sb => TaskRoutes.CreateIn(sb.SpaceId, sb.ProjectId)),
        OwnerElsewhere("VIEWER-CREATE-LABEL", "viewer", "manage-labels", HttpMethod.Post, LabelRoutes.Create, sb => LabelRoutes.LabelsOf(sb.SpaceId), CaseLabelDefinition),
        OwnerElsewhere("VIEWER-EDIT-TASK", "viewer", "manage-tasks", HttpMethod.Patch, TaskRoutes.Task, sb => TaskRoutes.TaskOf(sb.SpaceId, sb.TaskIds[0])),
        OwnerElsewhere("VIEWER-RENAME-SPACE", "viewer", "rename-space", HttpMethod.Patch, SpaceRoutes.Active, sb => SpaceRoutes.ActiveOf(sb.SpaceId)),
        OwnerElsewhere("VIEWER-DELETION-SUMMARY", "viewer", "delete-space", HttpMethod.Get, SpaceRoutes.DeletionSummary, sb => SpaceRoutes.DeletionSummaryOf(sb.SpaceId)),
        OwnerElsewhere("VIEWER-DELETE-SPACE", "viewer", "delete-space", HttpMethod.Delete, SpaceRoutes.Active, sb => SpaceRoutes.ActiveOf(sb.SpaceId)),
        OwnerElsewhere("ADMIN-DELETION-SUMMARY", "admin", "delete-space", HttpMethod.Get, SpaceRoutes.DeletionSummary, sb => SpaceRoutes.DeletionSummaryOf(sb.SpaceId)),
        OwnerElsewhere("ADMIN-DELETE-SPACE", "admin", "delete-space", HttpMethod.Delete, SpaceRoutes.Active, sb => SpaceRoutes.ActiveOf(sb.SpaceId)),

        new(
            "ISO-OWNER-ELSEWHERE-VIEWER-MOVE-TASK", "I-9 (3.4 AC2)", IsolationVector.RecordKind, HttpMethod.Post, TaskRoutes.Move,
            async world =>
            {
                var (own, elsewhere) = await world.OwnerElsewhereAsync("viewer");
                var destination = (await StatusIdsAsync(elsewhere.Actor))[1];
                var path = TaskRoutes.MoveOf(elsewhere.SpaceId, elsewhere.TaskIds[0]);

                return new CaseRequest(elsewhere.Actor, HttpMethod.Post, [new(path, path)], elsewhere.SpaceId, Body: () => MoveBody(destination, afterTaskId: null), Other: own.Actor);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new RoleRefused("manage-tasks") { Then = TheActorsSpaceIsUnchangedAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheActorsSpaceIsUnchangedAsync },
            }),

        new(
            "ISO-OWNER-ROLE-NOT-CARRIED", "I-9, I-10 (3.4 AC2)", IsolationVector.RecordKind, HttpMethod.Post, ProjectRoutes.Create,
            async world =>
            {
                var (own, elsewhere) = await world.OwnerElsewhereAsync("viewer");
                var path = ProjectRoutes.ListIn(elsewhere.SpaceId);

                return new CaseRequest(elsewhere.Actor, HttpMethod.Post, [new(path, path)], elsewhere.SpaceId, Other: own.Actor);
            },
            Both(new RoleNotCarriedAcrossSpaces("manage-projects") { Then = TheActorsSpaceIsUnchangedAsync })),

        // AC3 - a Token bound to SA1, used on SA2 which the same Account owns: refused exactly as a non-member, CrossSpace
        // recorded, SA2 unchanged. The Session carries no Space (FR-2), so the same request on it is served. The move and
        // Token-issue routes are Session-only and exempt by their own rows (a Token there is the no-credential 401).
        TokenOwnOtherSpace("ACTIVE", HttpMethod.Get, SpaceRoutes.Active, HttpStatusCode.OK, sa2 => SpaceRoutes.ActiveOf(sa2.SpaceId)),
        TokenOwnOtherSpace("STATUSES", HttpMethod.Get, SpaceRoutes.Statuses, HttpStatusCode.OK, sa2 => SpaceRoutes.StatusesOf(sa2.SpaceId)),
        TokenOwnOtherSpace("PROJECTS", HttpMethod.Get, ProjectRoutes.List, HttpStatusCode.OK, sa2 => ProjectRoutes.ListIn(sa2.SpaceId)),
        TokenOwnOtherSpace("PROJECT-CREATE", HttpMethod.Post, ProjectRoutes.Create, HttpStatusCode.Created, sa2 => ProjectRoutes.ListIn(sa2.SpaceId)),
        TokenOwnOtherSpace("PROJECT-RENAME", HttpMethod.Patch, ProjectRoutes.Rename, HttpStatusCode.OK, sa2 => ProjectRoutes.ProjectOf(sa2.SpaceId, sa2.ProjectId)),
        TokenOwnOtherSpace("PROJECT-DELETE", HttpMethod.Delete, ProjectRoutes.Project, HttpStatusCode.NoContent, sa2 => ProjectRoutes.ProjectOf(sa2.SpaceId, sa2.ProjectId)),
        TokenOwnOtherSpace("BOARD", HttpMethod.Get, BoardRoutes.Board, HttpStatusCode.OK, sa2 => BoardRoutes.BoardOf(sa2.SpaceId, sa2.ProjectId)),
        TokenOwnOtherSpace("LIST", HttpMethod.Get, ListRoute, HttpStatusCode.OK, sa2 => ListOf(sa2.SpaceId, sa2.ProjectId)),
        TokenOwnOtherSpace("TASK", HttpMethod.Get, TaskRoutes.Task, HttpStatusCode.OK, sa2 => TaskRoutes.TaskOf(sa2.SpaceId, sa2.TaskIds[0])),
        TokenOwnOtherSpace("TASK-CREATE", HttpMethod.Post, TaskRoutes.Create, HttpStatusCode.Created, sa2 => TaskRoutes.CreateIn(sa2.SpaceId, sa2.ProjectId)),
        TokenOwnOtherSpace("TASK-EDIT", HttpMethod.Patch, TaskRoutes.Task, HttpStatusCode.OK, sa2 => TaskRoutes.TaskOf(sa2.SpaceId, sa2.TaskIds[0])),
        TokenOwnOtherSpace("TASK-DELETE", HttpMethod.Delete, TaskRoutes.Task, HttpStatusCode.NoContent, sa2 => TaskRoutes.TaskOf(sa2.SpaceId, sa2.TaskIds[1])),
        TokenOwnOtherSpace("LABELS", HttpMethod.Get, LabelRoutes.List, HttpStatusCode.OK, sa2 => LabelRoutes.LabelsOf(sa2.SpaceId)),
        TokenOwnOtherSpace("LABEL-CREATE", HttpMethod.Post, LabelRoutes.Create, HttpStatusCode.Created, sa2 => LabelRoutes.LabelsOf(sa2.SpaceId), CaseLabelDefinition),

        // The seeded Label is applied, so the Session's own deletion is 409 label-in-use: served by the Space, not refused.
        TokenOwnOtherSpace("LABEL-DELETE", HttpMethod.Delete, LabelRoutes.Label, HttpStatusCode.Conflict, sa2 => LabelRoutes.LabelOf(sa2.SpaceId, sa2.LabelId)),
        TokenOwnOtherSpace("SPACE-RENAME", HttpMethod.Patch, SpaceRoutes.Active, HttpStatusCode.OK, sa2 => SpaceRoutes.ActiveOf(sa2.SpaceId)),
        TokenOwnOtherSpace("SPACE-DELETE", HttpMethod.Delete, SpaceRoutes.Active, HttpStatusCode.NoContent, sa2 => SpaceRoutes.ActiveOf(sa2.SpaceId)),
        TokenOwnOtherSpace("DELETION-SUMMARY", HttpMethod.Get, SpaceRoutes.DeletionSummary, HttpStatusCode.OK, sa2 => SpaceRoutes.DeletionSummaryOf(sa2.SpaceId)),

        new(
            "ISO-TOKEN-OWN-OTHER-SPACE-BOARD-COLUMN", "I-12 (3.4 AC3)", IsolationVector.TokenOwnOtherSpace, HttpMethod.Get, BoardRoutes.Column,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tc");
                var status = (await StatusIdsAsync(second.Actor))[0];
                var path = BoardRoutes.ColumnOf(second.SpaceId, second.ProjectId, status);

                return new CaseRequest(first.Actor, HttpMethod.Get, [new(path, path)], second.SpaceId, Other: second.Actor);
            },
            TokenOwnOtherSpaceExpectations(HttpStatusCode.OK)),

        // AC9 - read from SA1, every aggregate and listing holds exactly SA1's rows and totals and nothing of SA2's; the
        // same reads from SA2 hold SA2's (the anchor).
        TwoSpaces("STATUSES", SpaceRoutes.Statuses, first => SpaceRoutes.StatusesOf(first.SpaceId), StatusesAreEachSpacesOwnAsync),
        TwoSpaces("PROJECTS", ProjectRoutes.List, first => ProjectRoutes.ListIn(first.SpaceId), ProjectsAndTaskCountsAreEachSpacesOwnAsync),
        TwoSpaces("BOARD", BoardRoutes.Board, first => BoardRoutes.BoardOf(first.SpaceId, first.ProjectId), BoardTotalsAreEachSpacesOwnAsync),
        TwoSpaces("BOARD-COLUMN", BoardRoutes.Column, first => BoardRoutes.ColumnOf(first.SpaceId, first.ProjectId, Guid.Empty), ColumnTotalsAreEachSpacesOwnAsync, firstStatus: true),
        TwoSpaces("LIST", ListRoute, first => ListOf(first.SpaceId, first.ProjectId), ListTotalsAreEachSpacesOwnAsync),
        TwoSpaces("LABELS", LabelRoutes.List, first => LabelRoutes.LabelsOf(first.SpaceId), LabelsAreEachSpacesOwnAsync),

        new(
            "ISO-TWO-SPACES-SWITCHER", "I-3, I-11 (3.4 AC9)", IsolationVector.Listing, HttpMethod.Get, SpaceRoutes.Directory,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Sw");

                return new CaseRequest(first.Actor, HttpMethod.Get, [new(SpaceRoutes.Directory, SpaceRoutes.Directory)], TargetSpaceId: null, Other: second.Actor);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, ListsExactlyBothOfTheAccountsSpacesAsync, "200, exactly [SA1, SA2], rows exactly {id, name}"),
                [IsolationSurface.ApiToken] = new NoCredential(),
            }),

        new(
            "ISO-TWO-SPACES-TOKENS", "I-11 (3.4 AC9)", IsolationVector.Listing, HttpMethod.Get, ApiTokenRoutes.List,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tl");

                return new CaseRequest(first.Actor, HttpMethod.Get, [new(ApiTokenRoutes.List, ApiTokenRoutes.List)], TargetSpaceId: null, Other: second.Actor);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, EachTokenRowNamesItsOwnSpaceAsync, "200, exactly the Account's two Tokens, each naming its own Space"),
                [IsolationSurface.ApiToken] = new NoCredential(),
            }),

        // AC4 - one Account, two Spaces, one pooled physical connection.
        new(
            "ISO-POOLED-ONE-ACCOUNT-TWO-SPACES", "I-8 (3.4 AC4), R5", IsolationVector.PooledConnection, HttpMethod.Get, SpaceRoutes.Statuses,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Pl");
                var path = SpaceRoutes.StatusesOf(first.SpaceId);

                return new CaseRequest(first.Actor, HttpMethod.Get, [new(path, path)], TargetSpaceId: null, Other: second.Actor);
            },
            Both(new EachOwnSpaceOnOneConnection()),
            Runner: CaseRunner.Pooled),
    ];

    private static IsolationCase OwnOtherSpace(
        string suffix,
        string testDesignId,
        HttpMethod method,
        string route,
        Func<Guid, Guid, string> pathOf,
        Func<SeededSpace, Guid> foreignId) =>
        new(
            $"ISO-OWN-OTHER-SPACE-{suffix}", $"{testDesignId} (3.4 AC1)", IsolationVector.DeepLink, method, route,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Oo");

                return OwnOtherSpaceRequest(world, first, second, method, pathOf, foreignId(second));
            },
            Both(new SameAsAnchorAndStranger(HttpStatusCode.NotFound, ProblemTypes.NotAvailable) { Then = NeitherSpaceChangedAsync }));

    /// <summary>
    /// Q1 (a): SA2's id under SA1 (the path), a random id under SA1 (the anchor), and SA2's id under B's own Space (the
    /// stranger's path). B holds no Membership in SA2.
    /// </summary>
    private static CaseRequest OwnOtherSpaceRequest(IsolationWorld world, SeededSpace first, SeededSpace second, HttpMethod method, Func<Guid, Guid, string> pathOf, Guid foreign) =>
        new(
            first.Actor,
            method,
            [new(pathOf(first.SpaceId, foreign), pathOf(first.SpaceId, Guid.NewGuid()))],
            TargetSpaceId: null,
            Other: second.Actor,
            StrangerPaths: [pathOf(world.B.SpaceId, foreign)]);

    private static IsolationCase OwnerElsewhere(
        string suffix,
        string role,
        string capability,
        HttpMethod method,
        string route,
        Func<SeededSpace, string> pathOf,
        Func<HttpContent>? body = null) =>
        new(
            $"ISO-OWNER-ELSEWHERE-{suffix}", "I-9, V-1, NFR-7 (3.4 AC2)", IsolationVector.RecordKind, method, route,
            async world =>
            {
                var (own, elsewhere) = await world.OwnerElsewhereAsync(role);
                var path = pathOf(elsewhere);

                return new CaseRequest(elsewhere.Actor, method, [new(path, path)], elsewhere.SpaceId, Body: body, Other: own.Actor);
            },
            Both(new RoleRefused(capability) { Then = TheActorsSpaceIsUnchangedAsync }));

    private static IsolationCase TokenOwnOtherSpace(string suffix, HttpMethod method, string route, HttpStatusCode sessionStatus, Func<SeededSpace, string> pathOf, Func<HttpContent>? body = null) =>
        new(
            $"ISO-TOKEN-OWN-OTHER-SPACE-{suffix}", "I-12 (3.4 AC3)", IsolationVector.TokenOwnOtherSpace, method, route,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("To");
                var path = pathOf(second);

                return new CaseRequest(first.Actor, method, [new(path, path)], second.SpaceId, Body: body, Other: second.Actor);
            },
            TokenOwnOtherSpaceExpectations(sessionStatus));

    private static Dictionary<IsolationSurface, Expectation> TokenOwnOtherSpaceExpectations(HttpStatusCode sessionStatus) =>
        new()
        {
            [IsolationSurface.Session] = new Served(sessionStatus, (_, _, _) => Task.CompletedTask, $"{sessionStatus} - a Session carries no Space (FR-2); the Account owns SA2"),
            [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheOtherSpaceIsUnchangedAsync },
        };

    private static IsolationCase TwoSpaces(
        string suffix,
        string route,
        Func<SeededSpace, string> pathOf,
        Func<IsolationWorld, CaseRequest, WireResponse, Task> check,
        bool firstStatus = false) =>
        new(
            $"ISO-TWO-SPACES-{suffix}", "I-3, I-11 (3.4 AC9)", IsolationVector.Aggregate, HttpMethod.Get, route,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Ag");
                var path = firstStatus
                    ? BoardRoutes.ColumnOf(first.SpaceId, first.ProjectId, (await StatusIdsAsync(first.Actor))[0])
                    : pathOf(first);

                return new CaseRequest(first.Actor, HttpMethod.Get, [new(path, path)], TargetSpaceId: null, Other: second.Actor);
            },
            Both(new Served(HttpStatusCode.OK, check, "200, exactly SA1's rows and totals; SA2's own read holds SA2's")));

    private static Task NeitherSpaceChangedAsync(IsolationWorld world, CaseRequest request) =>
        Task.WhenAll(
            AssertSeededSpaceUnchangedAsync(world, request.Actor, SeededSpace.FirstSpaceTasks),
            AssertSeededSpaceUnchangedAsync(world, request.Other!, SeededSpace.OtherSpaceTasks));

    private static Task TheOtherSpaceIsUnchangedAsync(IsolationWorld world, CaseRequest request) =>
        AssertSeededSpaceUnchangedAsync(world, request.Other!, SeededSpace.OtherSpaceTasks);

    private static Task TheActorsSpaceIsUnchangedAsync(IsolationWorld world, CaseRequest request) =>
        AssertSeededSpaceUnchangedAsync(world, request.Actor, SeededSpace.OtherSpaceTasks);

    /// <summary>
    /// A Space seeded by story 3.4's world is exactly as seeded: its name (read by its holder through the product, so it
    /// still exists), one Project, its Tasks all in one Status, one Label applied once - and nothing carrying the case
    /// body's text. Anchored: every count is the seed's, so "unchanged" is about something.
    /// </summary>
    private static async Task AssertSeededSpaceUnchangedAsync(IsolationWorld world, ProductActor holder, int tasks)
    {
        var space = await holder.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.ActiveOf(holder.SpaceId));

        Assert.True(space.StatusCode == HttpStatusCode.OK, $"The seeded Space {holder.SpaceId} no longer answers its holder: {space.StatusCode}");

        using (var document = JsonDocument.Parse(space.Body))
        {
            Assert.Equal(holder.SpaceName, document.RootElement.GetProperty("name").GetString());
        }

        var project = Assert.Single(await world.Database.ProjectsInAsync(holder.SpaceId));
        var seededTasks = await world.Database.TasksInAsync(holder.SpaceId);
        var placements = await world.Database.TaskPlacementsInAsync(holder.SpaceId);
        var label = Assert.Single(await world.Database.LabelsInAsync(holder.SpaceId));

        Assert.NotEqual(ProductActor.CaseBodyText, project.Name);
        Assert.Equal(tasks, seededTasks.Count);
        Assert.DoesNotContain(seededTasks, task => string.Equals(task.Title, ProductActor.CaseBodyText, StringComparison.Ordinal));
        Assert.Single(placements.Select(task => task.StatusId).Distinct());
        Assert.NotEqual(ProductActor.CaseBodyText, label.Name);
        Assert.Single(await world.Database.ApplicationsInAsync(holder.SpaceId));
    }

    /// <summary>
    /// Every name and id the other Space holds, for "none of SA2's ids or names".
    /// </summary>
    private static async Task AssertHoldsNothingOfAsync(IsolationWorld world, ProductActor other, string body)
    {
        foreach (var (id, name) in await world.Database.ProjectsInAsync(other.SpaceId))
        {
            Assert.DoesNotContain(id.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(name, body, StringComparison.Ordinal);
        }

        foreach (var (id, _, title) in await world.Database.TasksInAsync(other.SpaceId))
        {
            Assert.DoesNotContain(id.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(title, body, StringComparison.Ordinal);
        }

        foreach (var (id, name) in await world.Database.LabelsInAsync(other.SpaceId))
        {
            Assert.DoesNotContain(id.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(name, body, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(other.SpaceId.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(other.SpaceName, body, StringComparison.Ordinal);
    }

    private static async Task<WireResponse> OwnReadAsync(ProductActor actor, string path)
    {
        var answer = await actor.SendAsync(IsolationSurface.Session, HttpMethod.Get, path);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{actor.SpaceId}'s own read of {path}: {answer.StatusCode} {answer.Body}");

        return answer;
    }

    private static async Task StatusesAreEachSpacesOwnAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other!;
        var own = Ids(answer.Body);
        var theirs = Ids((await OwnReadAsync(other, SpaceRoutes.StatusesOf(other.SpaceId))).Body);

        Assert.Equal(3, own.Count);
        Assert.Equal(3, theirs.Count);
        Assert.Empty(own.Intersect(theirs));
    }

    private static async Task ProjectsAndTaskCountsAreEachSpacesOwnAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other!;

        AssertOneProjectCounting(answer.Body, (await world.Database.ProjectsInAsync(request.Actor.SpaceId))[0].Id, SeededSpace.FirstSpaceTasks);
        AssertOneProjectCounting((await OwnReadAsync(other, ProjectRoutes.ListIn(other.SpaceId))).Body, (await world.Database.ProjectsInAsync(other.SpaceId))[0].Id, SeededSpace.OtherSpaceTasks);
        await AssertHoldsNothingOfAsync(world, other, answer.Body);
    }

    private static void AssertOneProjectCounting(string body, Guid projectId, int tasks)
    {
        using var document = JsonDocument.Parse(body);
        var row = Assert.Single(document.RootElement.EnumerateArray().ToList());

        Assert.Equal(projectId, row.GetProperty("id").GetGuid());
        Assert.Equal(tasks, row.GetProperty("taskCount").GetInt32());
    }

    private static async Task BoardTotalsAreEachSpacesOwnAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other!;
        var theirProject = (await world.Database.ProjectsInAsync(other.SpaceId))[0].Id;

        Assert.Equal(SeededSpace.FirstSpaceTasks, ColumnTotals(answer.Body).Sum());
        Assert.Equal(SeededSpace.OtherSpaceTasks, ColumnTotals((await OwnReadAsync(other, BoardRoutes.BoardOf(other.SpaceId, theirProject))).Body).Sum());
        await AssertHoldsNothingOfAsync(world, other, answer.Body);
    }

    private static List<int> ColumnTotals(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("total").GetInt32())];
    }

    private static async Task ColumnTotalsAreEachSpacesOwnAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other!;
        var theirProject = (await world.Database.ProjectsInAsync(other.SpaceId))[0].Id;
        var theirStatus = (await StatusIdsAsync(other))[0];

        Assert.Equal(SeededSpace.FirstSpaceTasks, TotalOf(answer.Body));
        Assert.Equal(SeededSpace.OtherSpaceTasks, TotalOf((await OwnReadAsync(other, BoardRoutes.ColumnOf(other.SpaceId, theirProject, theirStatus))).Body));
        await AssertHoldsNothingOfAsync(world, other, answer.Body);
    }

    private static int TotalOf(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("total").GetInt32();
    }

    private static async Task ListTotalsAreEachSpacesOwnAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other!;
        var theirProject = (await world.Database.ProjectsInAsync(other.SpaceId))[0].Id;

        Assert.Equal(SeededSpace.FirstSpaceTasks, TotalOf(answer.Body));
        Assert.Equal(SeededSpace.OtherSpaceTasks, TotalOf((await OwnReadAsync(other, ListOf(other.SpaceId, theirProject))).Body));
        await AssertHoldsNothingOfAsync(world, other, answer.Body);
    }

    private static async Task LabelsAreEachSpacesOwnAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other!;

        Assert.Equal([(await world.Database.LabelsInAsync(request.Actor.SpaceId))[0].Id], Ids(answer.Body));
        Assert.Equal([(await world.Database.LabelsInAsync(other.SpaceId))[0].Id], Ids((await OwnReadAsync(other, LabelRoutes.LabelsOf(other.SpaceId))).Body));
        await AssertHoldsNothingOfAsync(world, other, answer.Body);
    }

    private static Task ListsExactlyBothOfTheAccountsSpacesAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        using var document = JsonDocument.Parse(answer.Body);
        var rows = document.RootElement.EnumerateArray().ToList();

        Assert.Equal(new[] { request.Actor.SpaceId, request.Other!.SpaceId }.Order(), rows.Select(row => row.GetProperty("id").GetGuid()).Order());
        Assert.All(rows, row => Assert.Equal(["id", "name"], row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
        Assert.DoesNotContain(world.B.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);

        return Task.CompletedTask;
    }

    private static Task EachTokenRowNamesItsOwnSpaceAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        using var document = JsonDocument.Parse(answer.Body);
        var rows = document.RootElement.EnumerateArray().ToDictionary(row => row.GetProperty("id").GetGuid(), row => row.GetProperty("space").GetProperty("id").GetGuid());

        Assert.Equal(2, rows.Count);
        Assert.Equal(request.Actor.SpaceId, rows[request.Actor.TokenId]);
        Assert.Equal(request.Other!.SpaceId, rows[request.Other.TokenId]);
        Assert.DoesNotContain(world.B.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);

        return Task.CompletedTask;
    }
}
