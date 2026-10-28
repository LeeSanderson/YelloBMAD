using System.Net;
using System.Net.Http.Json;
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
/// SM-1's epic-1 isolation cases, held as data: one row per case, each naming the route it is about
/// and what <b>every</b> surface gets. The theory data is generated from this list as
/// <c>cases x surfaces</c>, so a row with no expectation for a surface fails as a gap (AC1, AD-4) and a
/// mapped route with no row fails as a gap (AC8, B6). Story 1.9 Tasks 4 and 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no "not applicable".</b> Where a surface cannot reach a route, its expectation says what
/// it gets - a Token on a Session-only route is the no-credential <c>401</c>, byte for byte.
/// </para>
/// <para>
/// <b>A case never branches on the surface.</b> It reads its expectation from the row; the only
/// surface-shaped code is <see cref="ProductActor.SendAsync(IsolationSurface, HttpMethod, string)"/>, which sends what each surface sends.
/// </para>
/// <para>
/// <b>Rows owned by a later story are rows too</b>, with <see cref="IsolationCase.PendingUntil"/> naming
/// it. They execute as nothing, and <c>IsolationCaseListTests</c> fails the suite once that story reaches
/// <c>review</c> (or any status past <c>in-progress</c>) while the row is still pending - story 2.1 Q1 (c).
/// </para>
/// <para>
/// <b>Two cross-cutting invariants are not rows</b>, because they are not requests: I-13 (no Space name
/// and no unrequested Space id in a refusal body) and I-17 (no secret in the case's log slice). Every
/// row's run applies both - see <see cref="CaseRun"/>.
/// </para>
/// </remarks>
internal static class IsolationCases
{
    /// <summary>
    /// The kind <c>AccessRefusal</c> stores for a cross-Space attempt.
    /// </summary>
    public const string CrossSpace = "CrossSpace";

    /// <summary>
    /// The kind <c>AccessRefusal</c> stores for a within-Space Role failure.
    /// </summary>
    public const string InsufficientRole = "InsufficientRole";

    /// <summary>
    /// Every case, active and pending.
    /// </summary>
    public static IReadOnlyList<IsolationCase> All { get; } =
    [
        new(
            "ISO-DIRECT-SPACE", "I-1, I-9", IsolationVector.DirectIdentifier, HttpMethod.Get, SpaceRoutes.Active,
            world => ForeignAsync(world, SpaceRoutes.ActiveOf),
            Both(new BoundaryNotFound())),

        new(
            "ISO-DIRECT-STATUSES", "I-1, I-3", IsolationVector.DirectIdentifier, HttpMethod.Get, SpaceRoutes.Statuses,
            world => ForeignAsync(world, SpaceRoutes.StatusesOf),
            Both(new BoundaryNotFound())),

        new(
            "ISO-WRITE-TOKEN", "I-2 (epic-1 analogue)", IsolationVector.Write, HttpMethod.Post, ApiTokenRoutes.Issue,
            world => ForeignAsync(world, ApiTokenRoutes.IssueIn, HttpMethod.Post),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new BoundaryNotFound { Then = NoTokenWrittenInTheForeignSpaceAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = NoTokenWrittenInTheForeignSpaceAsync },
            }),

        new(
            "ISO-LIST-SPACES", "I-3, I-11", IsolationVector.Listing, HttpMethod.Get, SpaceRoutes.Directory,
            world => OwnAsync(world, SpaceRoutes.Directory),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, ListsExactlyTheCallersSpaceAsync, "200, exactly [SA], rows exactly {id, name}"),
                [IsolationSurface.ApiToken] = new NoCredential(),
            }),

        new(
            "ISO-LIST-TOKENS", "I-11", IsolationVector.Listing, HttpMethod.Get, ApiTokenRoutes.List,
            world => OwnAsync(world, ApiTokenRoutes.List),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, ListsOnlyTheCallersTokensAsync, "200, only A's Tokens, rows exactly {createdAt, id, name, space}"),
                [IsolationSurface.ApiToken] = new NoCredential(),
            }),

        new(
            "ISO-REVOKE-FOREIGN-TOKEN", "I-4", IsolationVector.DirectIdentifier, HttpMethod.Delete, ApiTokenRoutes.Revoke,
            world => Task.FromResult(new CaseRequest(world.A, HttpMethod.Delete, [new(ApiTokenRoutes.RevokeOf(world.B.TokenId), ApiTokenRoutes.RevokeOf(Guid.NewGuid()))], TargetSpaceId: null)),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new SameAsAnchor(HttpStatusCode.NoContent) { Then = TheForeignTokenStillWorksAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheForeignTokenStillWorksAsync },
            }),

        new(
            "ISO-AGGREGATE", "I-3", IsolationVector.Aggregate, HttpMethod.Get, SpaceRoutes.Statuses,
            async world =>
            {
                // B is active in B's Space first, on both surfaces, so anything B touched is in play.
                foreach (var surface in Enum.GetValues<IsolationSurface>())
                {
                    Assert.Equal(HttpStatusCode.OK, (await world.B.SendAsync(surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId))).StatusCode);
                }

                return await OwnAsync(world, SpaceRoutes.StatusesOf(world.A.SpaceId));
            },
            Both(new Served(HttpStatusCode.OK, HoldsExactlyTheCallersThreeStatusesAsync, "exactly SA's three status ids, none of SB's"))),

        new(
            "ISO-IDENTIFIER-CONFERS-NOTHING", "I-4", IsolationVector.DeepLink, HttpMethod.Get, SpaceRoutes.Active,
            world => Task.FromResult(new CaseRequest(
                world.A,
                HttpMethod.Get,
                [
                    new(SpaceRoutes.ActiveOf(world.B.SpaceId), SpaceRoutes.ActiveOf(Guid.NewGuid())),
                    new(SpaceRoutes.StatusesOf(world.B.SpaceId), SpaceRoutes.StatusesOf(Guid.NewGuid())),
                ],
                world.B.SpaceId)),
            Both(new BoundaryNotFound())),

        new(
            "ISO-TOKEN-BOUND-ELSEWHERE", "I-12", IsolationVector.Api, HttpMethod.Get, SpaceRoutes.Active,
            TokenBoundElsewhereAsync,
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, (_, _, _) => Task.CompletedTask, "200 - a Session carries no Space (FR-2)"),
                [IsolationSurface.ApiToken] = new SameAsNonMember(),
            }),

        new(
            "ISO-UNMATCHED-UNDER-SPACE", "I-5", IsolationVector.DeepLink, HttpMethod.Get, UnmatchedApi,
            world => Task.FromResult(new CaseRequest(
                world.A,
                HttpMethod.Get,
                [new(SpaceRoutes.ActiveOf(world.B.SpaceId) + "/nothing", SpaceRoutes.ActiveOf(world.A.SpaceId) + "/nothing")],
                world.B.SpaceId)),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        new(
            "ISO-RECORD-KIND", "AC6, NFR-7", IsolationVector.RecordKind, HttpMethod.Get, SpaceRoutes.Statuses,
            world => Task.FromResult(new CaseRequest(
                world.A,
                HttpMethod.Get,
                [
                    new(SpaceRoutes.ActiveOf(world.B.SpaceId), SpaceRoutes.ActiveOf(Guid.NewGuid())),
                    new(SpaceRoutes.StatusesOf(world.B.SpaceId), SpaceRoutes.StatusesOf(Guid.NewGuid())),
                ],
                world.B.SpaceId)),
            Both(new BoundaryNotFound())),

        // Story 2.1 - the Project routes (AC8, Task 9), each on both surfaces. ISO-RECORD-KIND-INSUFFICIENT-ROLE
        // is the first wire 403: a Viewer's Session and a Viewer's Token creating a Project.
        new(
            "ISO-RECORD-KIND-INSUFFICIENT-ROLE", "AC6 (1.9), NFR-7, V-1", IsolationVector.RecordKind, HttpMethod.Post, ProjectRoutes.Create,
            ViewerCreatesAProjectAsync,
            Both(new RoleRefused("manage-projects") { Then = NoProjectWrittenAsync })),

        new(
            "ISO-DIRECT-PROJECTS", "I-1, I-3", IsolationVector.DirectIdentifier, HttpMethod.Get, ProjectRoutes.List,
            async world =>
            {
                await SeedProjectAsync(world.B);

                return await ForeignAsync(world, ProjectRoutes.ListIn);
            },
            Both(new BoundaryNotFound())),

        new(
            "ISO-LIST-PROJECTS-AGGREGATE", "I-3, FR-18", IsolationVector.Aggregate, HttpMethod.Get, ProjectRoutes.List,
            async world =>
            {
                await SeedProjectAsync(world.A);
                await SeedProjectAsync(world.B);

                return await OwnAsync(world, ProjectRoutes.ListIn(world.A.SpaceId));
            },
            Both(new Served(HttpStatusCode.OK, ListsExactlyTheCallersProjectsAsync, "200, exactly SA's Project ids, none of SB's"))),

        new(
            "ISO-WRITE-PROJECT", "I-2", IsolationVector.Write, HttpMethod.Post, ProjectRoutes.Create,
            async world =>
            {
                await SeedProjectAsync(world.B);

                return await ForeignAsync(world, ProjectRoutes.ListIn, HttpMethod.Post);
            },
            Both(new BoundaryNotFound { Then = NothingWrittenInTheForeignSpaceAsync })),

        new(
            "ISO-RENAME-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Patch, ProjectRoutes.Rename,
            async world =>
            {
                var foreign = await SeedProjectAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(ProjectRoutes.ProjectOf(world.B.SpaceId, foreign), ProjectRoutes.ProjectOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = NothingWrittenInTheForeignSpaceAsync })),

        new(
            "ISO-RENAME-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Patch, ProjectRoutes.Rename,
            async world =>
            {
                var foreign = await SeedProjectAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(ProjectRoutes.ProjectOf(world.A.SpaceId, foreign), ProjectRoutes.ProjectOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = NothingWrittenInTheForeignSpaceAsync })),

        // Story 2.2 - the Board and Task routes (AC9, AC10, Task 9), each on both surfaces. Seeded through the
        // product: A and B each create a Project and a Task in their own Personal Space.
        new(
            "ISO-DIRECT-BOARD", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, BoardRoutes.Board,
            async world =>
            {
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(BoardRoutes.BoardOf(world.B.SpaceId, foreign), BoardRoutes.BoardOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound())),

        new(
            "ISO-BOARD-AGGREGATE", "I-3 (includes Board column counts), FR-28", IsolationVector.Aggregate, HttpMethod.Get, BoardRoutes.Board,
            async world =>
            {
                var (own, _) = await SeedProjectWithTaskAsync(world.A);
                _ = await SeedProjectWithTaskAsync(world.B);

                return await OwnAsync(world, BoardRoutes.BoardOf(world.A.SpaceId, own));
            },
            Both(new Served(HttpStatusCode.OK, BoardHoldsExactlyTheCallersTasksAsync, "200, every column's total and Tasks are A's only, none of B's ids or titles"))),

        new(
            "ISO-WRITE-TASK", "I-2", IsolationVector.Write, HttpMethod.Post, TaskRoutes.Create,
            async world =>
            {
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(TaskRoutes.CreateIn(world.B.SpaceId, foreign), TaskRoutes.CreateIn(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = NoTaskWrittenInEitherSpaceAsync })),

        new(
            "ISO-BOARD-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Get, BoardRoutes.Board,
            async world =>
            {
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(BoardRoutes.BoardOf(world.A.SpaceId, foreign), BoardRoutes.BoardOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        new(
            "ISO-WRITE-TASK-FOREIGN-PROJECT-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Post, TaskRoutes.Create,
            async world =>
            {
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(TaskRoutes.CreateIn(world.A.SpaceId, foreign), TaskRoutes.CreateIn(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = NoTaskWrittenInEitherSpaceAsync })),

        new(
            "ISO-RECORD-KIND-TASK-VIEWER", "V-1, NFR-7", IsolationVector.RecordKind, HttpMethod.Post, TaskRoutes.Create,
            ViewerCreatesATaskAsync,
            Both(new RoleRefused("manage-tasks") { Then = NoTaskWrittenByTheViewerAsync })),

        // Story 2.3 - the by-id Task routes (AC8, Task 8), each on both surfaces. A PATCH
        // sends ProductActor's valid body, whose title is ProductActor.CaseBodyText, so a write that landed
        // anywhere would carry it.
        new(
            "ISO-DIRECT-TASK", "I-1, I-13", IsolationVector.DirectIdentifier, HttpMethod.Get, TaskRoutes.Task,
            async world =>
            {
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(TaskRoutes.TaskOf(world.B.SpaceId, foreign), TaskRoutes.TaskOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound())),

        new(
            "ISO-EDIT-TASK-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Patch, TaskRoutes.Task,
            async world =>
            {
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(TaskRoutes.TaskOf(world.B.SpaceId, foreign), TaskRoutes.TaskOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = NoTaskWrittenInEitherSpaceAsync })),

        new(
            "ISO-TASK-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-5; deferred-work.md:485", IsolationVector.DeepLink, HttpMethod.Get, TaskRoutes.Task,
            async world =>
            {
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);

                // The first read that is not narrowed by a Project (2.2 P3): only the Task's own query filter and
                // FILTER predicate stand between A and B's Task. The dev phase plants the drop of both (Task 8).
                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(TaskRoutes.TaskOf(world.A.SpaceId, foreign), TaskRoutes.TaskOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        new(
            "ISO-EDIT-TASK-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Patch, TaskRoutes.Task,
            async world =>
            {
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(TaskRoutes.TaskOf(world.A.SpaceId, foreign), TaskRoutes.TaskOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = NoTaskWrittenInEitherSpaceAsync })),

        new(
            "ISO-RECORD-KIND-TASK-EDIT-VIEWER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Patch, TaskRoutes.Task,
            ViewerEditsATaskAsync,
            Both(new RoleRefused("manage-tasks") { Then = TheViewersTaskIsUnchangedAsync })),

        // Story 2.4 - the Label routes and the Task PATCH carrying labelIds (AC8, AC9, Task 8), each on both surfaces,
        // live since the dev phase mapped them. Seeded through the product; every Label name a case writes is added to
        // its owner's secrets.
        new(
            "ISO-LIST-LABELS-FOREIGN-SPACE", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, LabelRoutes.List,
            async world =>
            {
                _ = await SeedLabelAsync(world.B);

                return await ForeignAsync(world, LabelRoutes.LabelsOf);
            },
            Both(new BoundaryNotFound())),

        new(
            "ISO-LIST-LABELS-OWN-SPACE", "I-3", IsolationVector.Listing, HttpMethod.Get, LabelRoutes.List,
            async world =>
            {
                _ = await SeedLabelAsync(world.A);
                _ = await SeedLabelAsync(world.B);

                return await OwnAsync(world, LabelRoutes.LabelsOf(world.A.SpaceId));
            },
            Both(new Served(HttpStatusCode.OK, ListsExactlyTheCallersLabelsAsync, "200, exactly SA's Label ids, none of SB's ids or names"))),

        new(
            "ISO-CREATE-LABEL-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, LabelRoutes.Create,
            async world =>
            {
                _ = await SeedLabelAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(LabelRoutes.LabelsOf(world.B.SpaceId), LabelRoutes.LabelsOf(Guid.NewGuid()))],
                    world.B.SpaceId,
                    Body: CaseLabelDefinition);
            },
            Both(new BoundaryNotFound { Then = NoLabelWrittenInEitherSpaceAsync })),

        new(
            "ISO-DELETE-LABEL-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Delete, LabelRoutes.Label,
            async world =>
            {
                var foreign = await SeedLabelAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(LabelRoutes.LabelOf(world.B.SpaceId, foreign), LabelRoutes.LabelOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = TheForeignLabelIsStillPresentAsync })),

        new(
            "ISO-LABEL-FOREIGN-ID-IN-OWN-SPACE", "I-1, I-13", IsolationVector.DeepLink, HttpMethod.Delete, LabelRoutes.Label,
            async world =>
            {
                var foreign = await SeedLabelAsync(world.B);

                // Only the Label's own query filter and FILTER predicate stand between A and B's Label: dropping both
                // fails the row on both surfaces, and either alone keeps it green (story 2.4 Task 8, planted).
                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(LabelRoutes.LabelOf(world.A.SpaceId, foreign), LabelRoutes.LabelOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheForeignLabelIsStillPresentAsync })),

        new(
            "ISO-APPLY-FOREIGN-LABEL", "I-2, I-13", IsolationVector.Write, HttpMethod.Patch, TaskRoutes.Task,
            async world =>
            {
                var (_, own) = await SeedProjectWithTaskAsync(world.A);
                var foreign = await SeedLabelAsync(world.B);
                var path = TaskRoutes.TaskOf(world.A.SpaceId, own);

                // The foreign id is in the body, not the path: the anchor is the same Task with a random Label id.
                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => LabelIdsBody(foreign),
                    AnchorBody: () => LabelIdsBody(Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidTaskLabels) { Then = NoLabelAppliedInEitherSpaceAsync })),

        new(
            "ISO-RECORD-KIND-LABEL-DEFINE-MEMBER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Post, LabelRoutes.Create,
            MemberDefinesALabelAsync,
            Both(new RoleRefused("manage-labels") { Then = NoLabelInTheActorsSpaceAsync })),

        new(
            "ISO-RECORD-KIND-LABEL-APPLY-VIEWER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Patch, TaskRoutes.Task,
            ViewerClearsALabelAsync,
            Both(new RoleRefused("manage-tasks") { Then = TheViewersLabelIsStillAppliedAsync })),

        // Story 2.5 - DELETE on the Task's item route and on the Project's item route (AC7, AC8, Task 8), each on both
        // surfaces. Seeded through the product; every Project name and Task title a case writes is
        // already in its owner's secrets (SeedProjectWithTaskAsync), so a name or title in the Host's log fails I-17.
        new(
            "ISO-DELETE-TASK-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Delete, TaskRoutes.Task,
            async world =>
            {
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(TaskRoutes.TaskOf(world.B.SpaceId, foreign), TaskRoutes.TaskOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = TheForeignTaskIsStillPresentAsync })),

        new(
            "ISO-DELETE-TASK-FOREIGN-ID-IN-OWN-SPACE", "I-1, I-13", IsolationVector.DeepLink, HttpMethod.Delete, TaskRoutes.Task,
            async world =>
            {
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);

                // Only the Task's own query filter and FILTER predicate stand between A and B's Task (no BEFORE DELETE
                // predicate, Q6): dropping both fails the row on both surfaces, either alone keeps it green - planted by
                // story 2.5 (Debug Log).
                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(TaskRoutes.TaskOf(world.A.SpaceId, foreign), TaskRoutes.TaskOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheForeignTaskIsStillPresentAsync })),

        new(
            "ISO-DELETE-PROJECT-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Delete, ProjectRoutes.Project,
            async world =>
            {
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(ProjectRoutes.ProjectOf(world.B.SpaceId, foreign), ProjectRoutes.ProjectOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = TheForeignProjectAndItsTasksAreStillPresentAsync })),

        new(
            "ISO-DELETE-PROJECT-FOREIGN-ID-IN-OWN-SPACE", "I-1, I-13", IsolationVector.DeepLink, HttpMethod.Delete, ProjectRoutes.Project,
            async world =>
            {
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);

                // Only the Project's own query filter and FILTER predicate stand between A and B's Project and, through
                // the cascade, its Tasks: dropping both fails the row, either alone keeps it green - planted by story 2.5.
                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(ProjectRoutes.ProjectOf(world.A.SpaceId, foreign), ProjectRoutes.ProjectOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheForeignProjectAndItsTasksAreStillPresentAsync })),

        new(
            "ISO-RECORD-KIND-TASK-DELETE-VIEWER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Delete, TaskRoutes.Task,
            ViewerDeletesATaskAsync,
            Both(new RoleRefused("manage-tasks") { Then = TheViewersRowIsStillPresentAsync })),

        new(
            "ISO-RECORD-KIND-PROJECT-DELETE-VIEWER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Delete, ProjectRoutes.Project,
            ViewerDeletesAProjectAsync,
            Both(new RoleRefused("manage-projects") { Then = TheViewersRowIsStillPresentAsync })),

        // Story 2.6 - the move route (AC9, AC12, Task 8). The route is Session-only (Q1 (a), FR-35: position is not
        // writable over the API), so every ApiToken expectation is the no-credential 401 - a refusal, never a success - on
        // the ISO-WRITE-TOKEN model. Bodies follow Q2 (a): { statusId, afterTaskId } and never a key. Seeded through the
        // product.
        new(
            "ISO-MOVE-TASK-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, TaskRoutes.Move,
            async world =>
            {
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);
                var theirStatus = (await StatusIdsAsync(world.B))[1];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(MoveOf(world.B.SpaceId, foreign), MoveOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId,
                    Body: () => MoveBody(theirStatus, afterTaskId: null));
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new BoundaryNotFound { Then = TheNamedTaskWasNotMovedAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheNamedTaskWasNotMovedAsync },
            }),

        new(
            "ISO-MOVE-TASK-FOREIGN-ID-IN-OWN-SPACE", "I-1, I-13", IsolationVector.Write, HttpMethod.Post, TaskRoutes.Move,
            async world =>
            {
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);
                var ownStatus = (await StatusIdsAsync(world.A))[1];

                // Only the Task's own query filter and FILTER predicate stand between A and B's Task: the dev phase plants
                // the drop of both (the Session row fails), then each alone (green) - story Task 8.
                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(MoveOf(world.A.SpaceId, foreign), MoveOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Body: () => MoveBody(ownStatus, afterTaskId: null));
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheNamedTaskWasNotMovedAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheNamedTaskWasNotMovedAsync },
            }),

        new(
            "ISO-MOVE-FOREIGN-NEIGHBOUR-IN-OWN-SPACE", "I-1, I-13", IsolationVector.DirectIdentifier, HttpMethod.Post, TaskRoutes.Move,
            async world =>
            {
                var (_, own) = await SeedProjectWithTaskAsync(world.A);
                var (_, foreign) = await SeedProjectWithTaskAsync(world.B);
                var ownStatus = (await StatusIdsAsync(world.A))[1];
                var path = MoveOf(world.A.SpaceId, own);

                // The foreign id is the neighbour, in the body: the anchor is the same move after a random id (Q3 (a)).
                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => MoveBody(ownStatus, foreign),
                    AnchorBody: () => MoveBody(ownStatus, Guid.NewGuid()));
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new SameAsAnchor(HttpStatusCode.Conflict, ExpectedType: ProblemTypes.TaskPositionUnavailable) { Then = TheNamedTaskWasNotMovedAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheNamedTaskWasNotMovedAsync },
            }),

        new(
            "ISO-MOVE-FOREIGN-STATUS-IN-OWN-SPACE", "I-1, I-13", IsolationVector.DirectIdentifier, HttpMethod.Post, TaskRoutes.Move,
            async world =>
            {
                var (_, own) = await SeedProjectWithTaskAsync(world.A);
                var theirStatus = (await StatusIdsAsync(world.B))[1];
                var path = MoveOf(world.A.SpaceId, own);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => MoveBody(theirStatus, afterTaskId: null),
                    AnchorBody: () => MoveBody(Guid.NewGuid(), afterTaskId: null));
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidTaskStatus) { Then = TheNamedTaskWasNotMovedAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheNamedTaskWasNotMovedAsync },
            }),

        new(
            "ISO-RECORD-KIND-TASK-MOVE-VIEWER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Post, TaskRoutes.Move,
            ViewerMovesATaskAsync,
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new RoleRefused("manage-tasks") { Then = TheNamedTaskWasNotMovedAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheNamedTaskWasNotMovedAsync },
            }),

        new(
            "ISO-TIMING-BOUNDARY", "I-6", IsolationVector.Timing, HttpMethod.Get, SpaceRoutes.Active,
            world => ForeignAsync(world, SpaceRoutes.ActiveOf),
            Both(new IndistinguishableFromAbsent()),
            Runner: CaseRunner.Timing),

        new(
            "ISO-TIMING-TOKEN-ELSEWHERE", "I-6, I-12", IsolationVector.Timing, HttpMethod.Get, SpaceRoutes.Active,
            TokenBoundElsewhereAsync,
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, (_, _, _) => Task.CompletedTask, "200 - a Session carries no Space (FR-2), so there is no refusal to time"),
                [IsolationSurface.ApiToken] = new IndistinguishableFromNonMember(),
            },
            Runner: CaseRunner.Timing),

        new(
            "ISO-TIMING-IN-SPACE", "I-6, AC10 (2.2)", IsolationVector.Timing, HttpMethod.Get, BoardRoutes.Board,
            async world =>
            {
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);

                // Arm 1, the path: the boundary 404 for B's Board. Arm 2, the anchor: A's own Space and a
                // Project id that matches nothing there - the in-Space not-found, which records nothing (AD-20).
                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(BoardRoutes.BoardOf(world.B.SpaceId, foreign), BoardRoutes.BoardOf(world.A.SpaceId, Guid.NewGuid()))],
                    world.B.SpaceId);
            },
            Both(new IndistinguishableFromInSpaceNotFound()),
            Runner: CaseRunner.Timing),

        new(
            "ISO-POOLED", "I-8", IsolationVector.PooledConnection, HttpMethod.Get, SpaceRoutes.Statuses,
            world => OwnAsync(world, SpaceRoutes.StatusesOf(world.A.SpaceId)),
            Both(new EachSeesOnlyItsOwnOnOneConnection()),
            Runner: CaseRunner.Pooled),
    ];

    /// <summary>
    /// The name every case's write body carries (<c>ProductActor</c>'s valid body): a Project created or
    /// renamed by a refused request would carry it.
    /// </summary>
    private const string CaseBodyName = "from an isolation case";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The fallback the Host maps under <c>/api</c> for any unmatched path.
    /// </summary>
    public static string UnmatchedApi => "/api/{**path}";

    /// <summary>
    /// The generated theory data: every active case on every surface, named "case id / surface".
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> CrossProduct =>
        All.Where(@case => @case.PendingUntil is null).SelectMany(Rows);

    /// <summary>
    /// The generated theory data for one runner's cases.
    /// </summary>
    /// <param name="runner">The runner.</param>
    /// <returns>Its rows.</returns>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> CrossProductFor(CaseRunner runner) =>
        All.Where(@case => @case.PendingUntil is null && @case.Runner == runner).SelectMany(Rows);

    /// <summary>
    /// The boundary runner's rows, for <c>MemberData</c>.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> BoundaryRows => CrossProductFor(CaseRunner.Boundary);

    /// <summary>
    /// The timing runner's rows, for <c>MemberData</c>.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> TimingRows => CrossProductFor(CaseRunner.Timing);

    /// <summary>
    /// The pooled runner's rows, for <c>MemberData</c>.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> PooledRows => CrossProductFor(CaseRunner.Pooled);

    /// <summary>
    /// A case by its id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The case.</returns>
    public static IsolationCase Named(string id) => All.Single(@case => string.Equals(@case.Id, id, StringComparison.Ordinal));

    private static IEnumerable<TheoryDataRow<string, IsolationSurface>> Rows(IsolationCase @case) =>
        Enum.GetValues<IsolationSurface>().Select(surface =>
            new TheoryDataRow<string, IsolationSurface>(@case.Id, surface) { TestDisplayName = $"{@case.Id} / {surface}" });

    private static Dictionary<IsolationSurface, Expectation> Both(Expectation expectation) =>
        Enum.GetValues<IsolationSurface>().ToDictionary(surface => surface, _ => expectation);

    private static Task<CaseRequest> ForeignAsync(IsolationWorld world, Func<Guid, string> route, HttpMethod? method = null) =>
        Task.FromResult(new CaseRequest(
            world.A,
            method ?? HttpMethod.Get,
            [new(route(world.B.SpaceId), route(Guid.NewGuid()))],
            world.B.SpaceId));

    private static Task<CaseRequest> OwnAsync(IsolationWorld world, string path) =>
        Task.FromResult(new CaseRequest(world.A, HttpMethod.Get, [new(path, path)], TargetSpaceId: null));

    /// <summary>
    /// A fresh Account that owns its Personal Space and a second Space; its Token is bound to the first,
    /// and the request is for the second. B, a non-member, is the comparison.
    /// </summary>
    private static async Task<CaseRequest> TokenBoundElsewhereAsync(IsolationWorld world)
    {
        var owner = await world.NewActorAsync("Cy");
        var secondName = $"{owner.DisplayName} second";
        var second = await world.Database.AddOwnedSpaceAsync(owner.AccountId, secondName);

        owner.HoldsSpaceNamed(secondName);

        return new CaseRequest(owner, HttpMethod.Get, [new(SpaceRoutes.ActiveOf(second), SpaceRoutes.ActiveOf(Guid.NewGuid()))], second);
    }

    /// <summary>
    /// Creates a Project in <paramref name="owner"/>'s Personal Space through the product, with a name
    /// unique to this call, and adds the name to the owner's secrets (I-17: a Project name never belongs
    /// in a log). The request is sent after the case's log cursor, so its slice is checked too.
    /// </summary>
    /// <param name="owner">The actor.</param>
    /// <returns>The Project's id.</returns>
    private static async Task<Guid> SeedProjectAsync(ProductActor owner)
    {
        var name = $"Project {Guid.NewGuid():N}";

        owner.HoldsProjectNamed(name);

        var created = await owner.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Post,
            ProjectRoutes.ListIn(owner.SpaceId),
            JsonContent.Create(new { name }, options: JsonOptions),
            headers: null);

        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Seeding a Project through the product: {created.StatusCode} {created.Body}");

        using var document = JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// A fresh Account demoted to Viewer in its own Personal Space, so its Session and its Token (bound to
    /// that Space) both hold exactly the Viewer Role there; the request is a Project creation in it.
    /// <c>UX_Membership_SpaceId_Owner</c> is a filtered unique index and permits a Space with no Owner.
    /// </summary>
    private static async Task<CaseRequest> ViewerCreatesAProjectAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vi");

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        return new CaseRequest(viewer, HttpMethod.Post, [new(ProjectRoutes.ListIn(viewer.SpaceId), ProjectRoutes.ListIn(viewer.SpaceId))], viewer.SpaceId);
    }

    private static async Task NoProjectWrittenAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Empty(await world.Database.ProjectsInAsync(request.Actor.SpaceId));

    /// <summary>
    /// B's Space holds Projects (the anchor: the seed landed), and none of them carries the refused
    /// request's name - nothing was created there, and nothing was renamed to it.
    /// </summary>
    private static async Task NothingWrittenInTheForeignSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        var theirs = await world.Database.ProjectsInAsync(world.B.SpaceId);

        Assert.NotEmpty(theirs);
        Assert.DoesNotContain(theirs, project => string.Equals(project.Name, CaseBodyName, StringComparison.Ordinal));
    }

    private static async Task ListsExactlyTheCallersProjectsAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var listed = Ids(answer.Body);
        var own = await world.Database.ProjectsInAsync(world.A.SpaceId);
        var theirs = await world.Database.ProjectsInAsync(world.B.SpaceId);

        // Anchored both ways: A's Space holds Projects and so does B's, so "none of B's" is about something.
        Assert.NotEmpty(own);
        Assert.NotEmpty(theirs);
        Assert.Equal(own.Select(project => project.Id).Order(), listed.Order());
        Assert.Empty(listed.Intersect(theirs.Select(project => project.Id)));
        Assert.All(theirs, project => Assert.DoesNotContain(project.Name, answer.Body, StringComparison.Ordinal));
    }


    /// <summary>
    /// Creates a Project and one Task in it in <paramref name="owner"/>'s Personal Space through the product,
    /// each named uniquely for this call, both added to the owner's secrets (I-17). Story 2.2.
    /// </summary>
    /// <param name="owner">The actor.</param>
    /// <returns>The Project's and the Task's ids.</returns>
    private static async Task<(Guid ProjectId, Guid TaskId)> SeedProjectWithTaskAsync(ProductActor owner)
    {
        var projectId = await SeedProjectAsync(owner);
        var title = $"Task {Guid.NewGuid():N}";

        owner.HoldsTaskTitled(title);

        var created = await owner.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Post,
            TaskRoutes.CreateIn(owner.SpaceId, projectId),
            JsonContent.Create(new { title }, options: JsonOptions),
            headers: null);

        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Seeding a Task through the product: {created.StatusCode} {created.Body}");

        using var document = JsonDocument.Parse(created.Body);

        return (projectId, document.RootElement.GetProperty("id").GetGuid());
    }

    /// <summary>
    /// A fresh Account with a Project in its own Personal Space, then demoted to Viewer there, so its Session
    /// and its Token both hold exactly the Viewer Role; the request is a Task creation in that Project.
    /// </summary>
    private static async Task<CaseRequest> ViewerCreatesATaskAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vt");
        var projectId = await SeedProjectAsync(viewer);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = TaskRoutes.CreateIn(viewer.SpaceId, projectId);

        return new CaseRequest(viewer, HttpMethod.Post, [new(path, path)], viewer.SpaceId);
    }

    private static async Task NoTaskWrittenByTheViewerAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Empty(await world.Database.TasksInAsync(request.Actor.SpaceId));

    /// <summary>
    /// A fresh Account with a Project and a Task in its own Personal Space, then demoted to Viewer there, so
    /// its Session and its Token both hold exactly the Viewer Role; the request edits that Task. Story 2.3.
    /// </summary>
    private static async Task<CaseRequest> ViewerEditsATaskAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Ve");
        var (_, taskId) = await SeedProjectWithTaskAsync(viewer);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = TaskRoutes.TaskOf(viewer.SpaceId, taskId);

        return new CaseRequest(viewer, HttpMethod.Patch, [new(path, path)], viewer.SpaceId);
    }

    /// <summary>
    /// The Viewer's Space still holds its Task (the anchor: the seed landed), and no Task there carries the
    /// refused edit's title. Story 2.3.
    /// </summary>
    private static async Task TheViewersTaskIsUnchangedAsync(IsolationWorld world, CaseRequest request)
    {
        var tasks = await world.Database.TasksInAsync(request.Actor.SpaceId);

        Assert.NotEmpty(tasks);
        Assert.DoesNotContain(tasks, task => string.Equals(task.Title, ProductActor.CaseBodyText, StringComparison.Ordinal));
    }

    /// <summary>
    /// B's Space holds Tasks (the anchor: the seed landed), and no Task in either Space carries the refused
    /// request's title.
    /// </summary>
    private static async Task NoTaskWrittenInEitherSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        var theirs = await world.Database.TasksInAsync(world.B.SpaceId);
        var own = await world.Database.TasksInAsync(world.A.SpaceId);

        Assert.NotEmpty(theirs);
        Assert.DoesNotContain(theirs.Concat(own), task => string.Equals(task.Title, ProductActor.CaseBodyText, StringComparison.Ordinal));
    }

    /// <summary>
    /// Defines a Label in <paramref name="owner"/>'s Personal Space through the product (story 2.4's route), named
    /// uniquely for this call and added to the owner's secrets (I-17: a Label name is Space content).
    /// </summary>
    /// <param name="owner">The actor.</param>
    /// <returns>The Label's id.</returns>
    private static async Task<Guid> SeedLabelAsync(ProductActor owner)
    {
        var name = $"Label {Guid.NewGuid():N}"[..30];

        owner.HoldsLabelNamed(name);

        var created = await owner.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Post,
            LabelRoutes.LabelsOf(owner.SpaceId),
            JsonContent.Create(new { name, colour = LabelColours.Keys[0] }, options: JsonOptions),
            headers: null);

        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Seeding a Label through the product: {created.StatusCode} {created.Body}");

        using var document = JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// A valid Label definition whose name is <see cref="ProductActor.CaseBodyText"/>, so a Label written by a refused
    /// request would carry it.
    /// </summary>
    private static HttpContent CaseLabelDefinition() =>
        JsonContent.Create(new { name = ProductActor.CaseBodyText, colour = LabelColours.Keys[0] }, options: JsonOptions);

    /// <summary>
    /// A Task edit naming exactly <paramref name="labelIds"/> (story 2.4 Q1 (a)).
    /// </summary>
    private static HttpContent LabelIdsBody(params Guid[] labelIds) =>
        JsonContent.Create(new { labelIds }, options: JsonOptions);

    /// <summary>
    /// A fresh Account demoted to Member in its own Personal Space, so its Session and its Token both hold exactly the
    /// Member Role there; the request is a valid Label definition in it. Story 2.4.
    /// </summary>
    private static async Task<CaseRequest> MemberDefinesALabelAsync(IsolationWorld world)
    {
        var member = await world.NewActorAsync("Ml");

        await world.Database.ChangeRoleAsync(member.SpaceId, member.AccountId, "member");

        var path = LabelRoutes.LabelsOf(member.SpaceId);

        return new CaseRequest(member, HttpMethod.Post, [new(path, path)], member.SpaceId, Body: CaseLabelDefinition);
    }

    /// <summary>
    /// A fresh Account with a Project, a Task and a Label applied to it in its own Personal Space, then demoted to Viewer
    /// there; the request clears the Task's Labels (<c>labelIds: []</c>), so an edit that landed would remove the
    /// application. Story 2.4.
    /// </summary>
    private static async Task<CaseRequest> ViewerClearsALabelAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vl");
        var (_, taskId) = await SeedProjectWithTaskAsync(viewer);
        var labelId = await SeedLabelAsync(viewer);
        var path = TaskRoutes.TaskOf(viewer.SpaceId, taskId);

        var applied = await viewer.SendAsync(IsolationSurface.Session, HttpMethod.Patch, path, LabelIdsBody(labelId), headers: null);

        Assert.True(applied.StatusCode == HttpStatusCode.OK, $"Applying a Label through the product: {applied.StatusCode} {applied.Body}");

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        return new CaseRequest(viewer, HttpMethod.Patch, [new(path, path)], viewer.SpaceId, Body: () => LabelIdsBody());
    }

    /// <summary>
    /// B's Space holds Labels (the anchor: the seed landed), and no Label in either Space carries the refused request's
    /// name. Story 2.4.
    /// </summary>
    private static async Task NoLabelWrittenInEitherSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        var theirs = await world.Database.LabelsInAsync(world.B.SpaceId);
        var own = await world.Database.LabelsInAsync(world.A.SpaceId);

        Assert.NotEmpty(theirs);
        Assert.DoesNotContain(theirs.Concat(own), label => string.Equals(label.Name, ProductActor.CaseBodyText, StringComparison.Ordinal));
    }

    /// <summary>
    /// The Label the refused request named - the last segment of its path - is still in B's Space. Story 2.4.
    /// </summary>
    private static async Task TheForeignLabelIsStillPresentAsync(IsolationWorld world, CaseRequest request)
    {
        var named = Guid.Parse(request.Paths[0].Path.Split('/')[^1]);

        Assert.Contains(await world.Database.LabelsInAsync(world.B.SpaceId), label => label.Id == named);
    }

    /// <summary>
    /// Neither Space holds an application: A's Task took neither B's Label nor the random one, and B's Label is applied
    /// nowhere. Anchored: B's Label exists. Story 2.4.
    /// </summary>
    private static async Task NoLabelAppliedInEitherSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        Assert.NotEmpty(await world.Database.LabelsInAsync(world.B.SpaceId));
        Assert.Empty(await world.Database.ApplicationsInAsync(world.A.SpaceId));
        Assert.Empty(await world.Database.ApplicationsInAsync(world.B.SpaceId));
    }

    private static async Task NoLabelInTheActorsSpaceAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Empty(await world.Database.LabelsInAsync(request.Actor.SpaceId));

    /// <summary>
    /// The Viewer's Task still carries the Label seeded before the demotion. Story 2.4.
    /// </summary>
    private static async Task TheViewersLabelIsStillAppliedAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Single(await world.Database.ApplicationsInAsync(request.Actor.SpaceId));

    /// <summary>
    /// The Task the refused deletion named - the last segment of its path - is still in B's Space. Story 2.5.
    /// </summary>
    private static async Task TheForeignTaskIsStillPresentAsync(IsolationWorld world, CaseRequest request)
    {
        var named = Guid.Parse(request.Paths[0].Path.Split('/')[^1]);

        Assert.Contains(await world.Database.TasksInAsync(world.B.SpaceId), task => task.Id == named);
    }

    /// <summary>
    /// The Project the refused deletion named - the last segment of its path - is still in B's Space, and so is the Task
    /// seeded in it: a deletion that had landed would have cascaded to it. Story 2.5.
    /// </summary>
    private static async Task TheForeignProjectAndItsTasksAreStillPresentAsync(IsolationWorld world, CaseRequest request)
    {
        var named = Guid.Parse(request.Paths[0].Path.Split('/')[^1]);

        Assert.Contains(await world.Database.ProjectsInAsync(world.B.SpaceId), project => project.Id == named);
        Assert.Contains(await world.Database.TasksInAsync(world.B.SpaceId), task => task.ProjectId == named);
    }

    /// <summary>
    /// A fresh Account with a Project and a Task in its own Personal Space, then demoted to Viewer there; the request
    /// deletes that Task. Story 2.5.
    /// </summary>
    private static async Task<CaseRequest> ViewerDeletesATaskAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vd");
        var (_, taskId) = await SeedProjectWithTaskAsync(viewer);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = TaskRoutes.TaskOf(viewer.SpaceId, taskId);

        return new CaseRequest(viewer, HttpMethod.Delete, [new(path, path)], viewer.SpaceId);
    }

    /// <summary>
    /// A fresh Account with a Project holding a Task in its own Personal Space, then demoted to Viewer there; the request
    /// deletes that Project. Story 2.5.
    /// </summary>
    private static async Task<CaseRequest> ViewerDeletesAProjectAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vp");
        var (projectId, _) = await SeedProjectWithTaskAsync(viewer);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = ProjectRoutes.ProjectOf(viewer.SpaceId, projectId);

        return new CaseRequest(viewer, HttpMethod.Delete, [new(path, path)], viewer.SpaceId);
    }

    /// <summary>
    /// The row the Viewer's refused deletion named - a Task or a Project, the last segment of its path - is still in the
    /// Viewer's Space, with the Task the seed created. Story 2.5.
    /// </summary>
    private static async Task TheViewersRowIsStillPresentAsync(IsolationWorld world, CaseRequest request)
    {
        var named = Guid.Parse(request.Paths[0].Path.Split('/')[^1]);
        var tasks = await world.Database.TasksInAsync(request.Actor.SpaceId);

        Assert.Single(tasks);
        Assert.True(
            tasks[0].Id == named || tasks[0].ProjectId == named,
            $"Neither the Viewer's Task nor its Project is {named} any more.");
    }

    /// <summary>
    /// One Task's move path (story 2.6, Q1 (a)).
    /// </summary>
    private static string MoveOf(Guid spaceId, Guid taskId) => TaskRoutes.MoveOf(spaceId, taskId);

    /// <summary>
    /// A move request as Q2 (a) shapes it: the destination Status and the Task to land after (<c>null</c> = the top of the
    /// column). Never a position key.
    /// </summary>
    private static HttpContent MoveBody(Guid statusId, Guid? afterTaskId) =>
        JsonContent.Create(new { statusId, afterTaskId }, options: JsonOptions);

    /// <summary>
    /// The actor's Space's Status ids, as its own Session reads them (the effective order). Story 2.6.
    /// </summary>
    private static async Task<List<Guid>> StatusIdsAsync(ProductActor actor)
    {
        var answer = await actor.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.StatusesOf(actor.SpaceId));

        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"Reading the Statuses through the product: {answer.StatusCode} {answer.Body}");

        return Ids(answer.Body);
    }

    /// <summary>
    /// A fresh Account with a Project and a Task in its own Personal Space, then demoted to Viewer there; the request moves
    /// that Task to its second Status with a valid body, so only the Role can refuse it. Story 2.6.
    /// </summary>
    private static async Task<CaseRequest> ViewerMovesATaskAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vm");
        var (_, taskId) = await SeedProjectWithTaskAsync(viewer);
        var destination = (await StatusIdsAsync(viewer))[1];

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = MoveOf(viewer.SpaceId, taskId);

        return new CaseRequest(viewer, HttpMethod.Post, [new(path, path)], viewer.SpaceId, Body: () => MoveBody(destination, afterTaskId: null));
    }

    /// <summary>
    /// The Task the refused move named - the segment before <c>/move</c> in its path - is still where its seed put it: the
    /// first Task of a fresh Project, so in its Space's first Status at <c>PositionKey.First</c> (<c>V0000</c>). It is
    /// looked for in B's Space and in the actor's own, whichever holds it. Story 2.6.
    /// </summary>
    private static async Task TheNamedTaskWasNotMovedAsync(IsolationWorld world, CaseRequest request)
    {
        var named = Guid.Parse(request.Paths[0].Path.Split('/')[^2]);

        foreach (var owner in new[] { world.B, request.Actor })
        {
            var placement = (await world.Database.TaskPlacementsInAsync(owner.SpaceId)).SingleOrDefault(task => task.Id == named);

            if (placement.Id == named)
            {
                Assert.Equal((await StatusIdsAsync(owner))[0], placement.StatusId);
                Assert.Equal("V0000", placement.PositionKey);

                return;
            }
        }

        Assert.Fail($"The Task {named} the move named is in neither Space any more.");
    }

    /// <summary>
    /// I-3 for Labels: A's list holds exactly A's Label ids, and none of B's ids or names - anchored both ways, since both
    /// Spaces hold Labels. Story 2.4.
    /// </summary>
    private static async Task ListsExactlyTheCallersLabelsAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var listed = Ids(answer.Body);
        var own = await world.Database.LabelsInAsync(world.A.SpaceId);
        var theirs = await world.Database.LabelsInAsync(world.B.SpaceId);

        Assert.NotEmpty(own);
        Assert.NotEmpty(theirs);
        Assert.Equal(own.Select(label => label.Id).Order(), listed.Order());
        Assert.Empty(listed.Intersect(theirs.Select(label => label.Id)));
        Assert.All(theirs, label => Assert.DoesNotContain(label.Name, answer.Body, StringComparison.Ordinal));
    }

    /// <summary>
    /// I-3 with Board column counts: A's Board holds exactly A's Tasks of that Project, its column totals sum to
    /// their number, and none of B's Task ids or titles appear - anchored both ways, since both Spaces hold
    /// Tasks.
    /// </summary>
    private static async Task BoardHoldsExactlyTheCallersTasksAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var projectId = Guid.Parse(request.Paths[0].Path.Split('/')[^2]);
        var own = (await world.Database.TasksInAsync(world.A.SpaceId)).Where(task => task.ProjectId == projectId).ToList();
        var theirs = await world.Database.TasksInAsync(world.B.SpaceId);

        Assert.NotEmpty(own);
        Assert.NotEmpty(theirs);

        using var document = JsonDocument.Parse(answer.Body);
        var columns = document.RootElement.GetProperty("columns").EnumerateArray().ToList();
        var listed = columns.SelectMany(column => column.GetProperty("tasks").EnumerateArray()).Select(task => task.GetProperty("id").GetGuid()).ToList();

        Assert.Equal(own.Count, columns.Sum(column => column.GetProperty("total").GetInt32()));

        // Each column's own total, not just their sum: every Task is returned (Q6), so a total that also
        // counted B's Tasks would exceed the column's listed Tasks.
        Assert.All(columns, column => Assert.Equal(column.GetProperty("tasks").GetArrayLength(), column.GetProperty("total").GetInt32()));
        Assert.Equal(own.Select(task => task.Id).Order(), listed.Order());
        Assert.Empty(listed.Intersect(theirs.Select(task => task.Id)));
        Assert.All(theirs, task => Assert.DoesNotContain(task.Title, answer.Body, StringComparison.Ordinal));
    }

    private static async Task NoTokenWrittenInTheForeignSpaceAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Equal(0, await world.Database.TokenCountAsync(request.Actor.AccountId, world.B.SpaceId));

    private static async Task TheForeignTokenStillWorksAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Equal(HttpStatusCode.OK, (await world.B.SendAsync(IsolationSurface.ApiToken, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId))).StatusCode);

    private static Task ListsExactlyTheCallersSpaceAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        using var document = JsonDocument.Parse(answer.Body);
        var rows = document.RootElement.EnumerateArray().ToList();

        Assert.Equal([world.A.SpaceId], rows.Select(row => row.GetProperty("id").GetGuid()));
        Assert.All(rows, row => Assert.Equal(["id", "name"], row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
        Assert.DoesNotContain(world.B.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(world.B.DisplayName, answer.Body, StringComparison.Ordinal);

        return Task.CompletedTask;
    }

    private static Task ListsOnlyTheCallersTokensAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        using var document = JsonDocument.Parse(answer.Body);
        var rows = document.RootElement.EnumerateArray().ToList();

        Assert.Equal([world.A.TokenId], rows.Select(row => row.GetProperty("id").GetGuid()));
        Assert.All(rows, row => Assert.Equal(["createdAt", "id", "name", "space"], row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
        Assert.All(rows, row => Assert.Equal(world.A.SpaceId, row.GetProperty("space").GetProperty("id").GetGuid()));
        Assert.DoesNotContain(world.B.TokenId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(world.B.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);

        return Task.CompletedTask;
    }

    private static async Task HoldsExactlyTheCallersThreeStatusesAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var own = Ids(answer.Body);
        var theirs = Ids((await world.B.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId))).Body);

        Assert.Equal(3, own.Count);
        Assert.Equal(3, theirs.Count);
        Assert.Empty(own.Intersect(theirs));
    }

    /// <summary>
    /// The status ids in a statuses answer.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The ids.</returns>
    public static List<Guid> Ids(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
    }
}

/// <summary>
/// One isolation case.
/// </summary>
/// <param name="Id">The case id, for example <c>ISO-DIRECT-SPACE</c>.</param>
/// <param name="TestDesignId">The test-design ids it lands (<c>test-design-qa.md:265-277</c>).</param>
/// <param name="Vector">How the attempt reaches across.</param>
/// <param name="Method">The route's method.</param>
/// <param name="Route">The route pattern, from a <c>Yello.Contracts</c> constant - the gap gate's key.</param>
/// <param name="Prepare">Builds the request against a world.</param>
/// <param name="Expectations">What each surface gets. Every surface, always.</param>
/// <param name="Runner">Which test class executes it.</param>
/// <param name="PendingUntil">The story that owns the row, while it is pending.</param>
/// <param name="PendingReason">Why it is pending.</param>
internal sealed record IsolationCase(
    string Id,
    string TestDesignId,
    IsolationVector Vector,
    HttpMethod Method,
    string Route,
    Func<IsolationWorld, Task<CaseRequest>> Prepare,
    IReadOnlyDictionary<IsolationSurface, Expectation> Expectations,
    CaseRunner Runner = CaseRunner.Boundary,
    string? PendingUntil = null,
    string? PendingReason = null);

/// <summary>
/// How an attempt reaches for another Space (the story's vector column).
/// </summary>
internal enum IsolationVector
{
    DirectIdentifier,
    DeepLink,
    Listing,
    Aggregate,
    Api,
    Write,
    Timing,
    PooledConnection,
    RecordKind,
}

/// <summary>
/// Which test class runs a case: the boundary cases share one world; the timing and pooled cases run
/// in collections with parallelisation disabled, on worlds of their own.
/// </summary>
internal enum CaseRunner
{
    Boundary,
    Timing,
    Pooled,
}

/// <summary>
/// What a case sends: who, how, and each path with its anchor - the same route on a Space that does not
/// exist (or, for a served case, the path itself).
/// </summary>
/// <param name="Actor">Who sends it.</param>
/// <param name="Method">The method.</param>
/// <param name="Paths">Each path and its anchor path.</param>
/// <param name="TargetSpaceId">The Space in the route, for the refusal record; <c>null</c> when there is none.</param>
/// <param name="Body">
/// A body of the case's own for every path it sends, instead of <c>ProductActor</c>'s valid body; <c>null</c> keeps that
/// one. A factory, because a sent <see cref="HttpContent"/> is spent. Added by the story 2.4 ATDD run: a Label definition
/// needs a colour the default body lacks, and a <c>labelIds</c> edit is a body, not a path.
/// </param>
/// <param name="AnchorBody">
/// The anchor's own body when it differs from <paramref name="Body"/> - the in-Space comparison for a foreign id carried
/// in the body (<c>labelIds: [random]</c> against <c>labelIds: [LB]</c>) rather than in the path. Story 2.4.
/// </param>
internal sealed record CaseRequest(
    ProductActor Actor,
    HttpMethod Method,
    IReadOnlyList<CasePath> Paths,
    Guid? TargetSpaceId,
    Func<HttpContent>? Body = null,
    Func<HttpContent>? AnchorBody = null)
{
    /// <summary>
    /// Sends <paramref name="path"/> as the case's actor on <paramref name="surface"/>, with the case's body if it has one.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="path">The path.</param>
    /// <returns>The answer.</returns>
    public Task<WireResponse> SendAsync(IsolationSurface surface, string path) =>
        Body is null
            ? Actor.SendAsync(surface, Method, path)
            : Actor.SendAsync(surface, Method, path, Body(), headers: null);

    /// <summary>
    /// Sends the anchor path, with the anchor's own body when the case gives one, otherwise as <see cref="SendAsync"/>.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="anchorPath">The anchor path.</param>
    /// <returns>The answer.</returns>
    public Task<WireResponse> SendAnchorAsync(IsolationSurface surface, string anchorPath) =>
        AnchorBody is null
            ? SendAsync(surface, anchorPath)
            : Actor.SendAsync(surface, Method, anchorPath, AnchorBody(), headers: null);
}

/// <summary>
/// One path a case sends and the path its answer is compared with.
/// </summary>
/// <param name="Path">The path sent.</param>
/// <param name="Anchor">The comparison path.</param>
internal sealed record CasePath(string Path, string Anchor);
