using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Boards;
using Yello.Contracts.Labels;
using Yello.Contracts.Lists;
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

        // Story 2.9 - the column-page route (Q1 (a), AC9, Task 3; 1.9-GATE-002 on both surfaces). The route is a read every
        // Role holds (ReadProjectsAndTasks), so both surfaces get the same expectation. Seeded through the product.
        new(
            "ISO-DIRECT-BOARD-COLUMN", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, BoardRoutes.Column,
            async world =>
            {
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);
                var theirStatus = (await StatusIdsAsync(world.B))[0];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(BoardRoutes.ColumnOf(world.B.SpaceId, foreign, theirStatus), BoardRoutes.ColumnOf(Guid.NewGuid(), foreign, theirStatus))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound())),

        new(
            "ISO-BOARD-COLUMN-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Get, BoardRoutes.Column,
            async world =>
            {
                var (own, _) = await SeedProjectWithTaskAsync(world.A);
                var (foreign, _) = await SeedProjectWithTaskAsync(world.B);
                var ownStatus = (await StatusIdsAsync(world.A))[0];
                var theirStatus = (await StatusIdsAsync(world.B))[0];

                // B's Project (with B's Status) and B's Status (in A's own Project) each in A's own Space: the Project's and the
                // Status's query filters and FILTER predicates stand between A and them, ahead of the Task's own two layers -
                // the handler answers no page for a Project it cannot find or a Status outside the effective set. Planted in
                // story 2.9: the handler's two checks skipped - the row fails on both surfaces.
                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [
                        new(BoardRoutes.ColumnOf(world.A.SpaceId, foreign, theirStatus), BoardRoutes.ColumnOf(world.A.SpaceId, Guid.NewGuid(), theirStatus)),
                        new(BoardRoutes.ColumnOf(world.A.SpaceId, own, theirStatus), BoardRoutes.ColumnOf(world.A.SpaceId, own, Guid.NewGuid())),
                        new(BoardRoutes.ColumnOf(world.A.SpaceId, foreign, ownStatus), BoardRoutes.ColumnOf(world.A.SpaceId, Guid.NewGuid(), ownStatus)),
                    ],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        new(
            "ISO-BOARD-COLUMN-AGGREGATE", "I-3 (includes Board column counts), FR-28", IsolationVector.Aggregate, HttpMethod.Get, BoardRoutes.Column,
            async world =>
            {
                var (own, _) = await SeedProjectWithTaskAsync(world.A);
                _ = await SeedProjectWithTaskAsync(world.B);

                return await OwnAsync(world, BoardRoutes.ColumnOf(world.A.SpaceId, own, (await StatusIdsAsync(world.A))[0]));
            },
            Both(new Served(HttpStatusCode.OK, ColumnPageHoldsExactlyTheCallersTasksAsync, "200, the page's total and Tasks are A's only, none of B's ids or titles"))),

        // Story 2.9 - the Board read itself, once it pages (AC9): ISO-BOARD-AGGREGATE's `tasks == total` rule stopped being
        // true above 50 and is folded into the paged rule both rows now share. This row is about more than one page - 51
        // Tasks in one column, so the first page is full and a cursor follows - and walking the pages must yield exactly A's.
        new(
            "ISO-BOARD-AGGREGATE-PAGED", "I-3 (includes Board column counts), FR-28, AC9 (2.9)", IsolationVector.Aggregate, HttpMethod.Get, BoardRoutes.Board,
            async world =>
            {
                var (own, _) = await SeedProjectWithTaskAsync(world.A);

                await SeedTasksThroughTheProductAsync(world.A, own, PageSize);
                _ = await SeedProjectWithTaskAsync(world.B);

                return await OwnAsync(world, BoardRoutes.BoardOf(world.A.SpaceId, own));
            },
            Both(new Served(HttpStatusCode.OK, BoardPagesHoldExactlyTheCallersTasksAsync, "200, each column at most 50 with a cursor iff more follow; walking the pages yields exactly A's Tasks"))),

        // Story 2.10 - the List View route (Q1 (a), AC21, Task 7; 1.9-GATE-002 on both surfaces). A read every Role holds
        // (ReadProjectsAndTasks), so both surfaces get the same expectation. Seeded through the product. Live since the route
        // was mapped (GATE-003). The route is the ListRoutes contract member.
        new(
            "ISO-DIRECT-LIST", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, ListRoute,
            async world =>
            {
                var (foreign, _) = await AnyProjectWithTaskAsync(world, world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(ListOf(world.B.SpaceId, foreign), ListOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound())),

        new(
            "ISO-LIST-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Get, ListRoute,
            async world =>
            {
                var (foreign, _) = await AnyProjectWithTaskAsync(world, world.B);

                // B's Project in A's own Space: the Project's query filter and FILTER predicate stand between A and it, ahead
                // of the Task's two layers - the handler answers no page for a Project it cannot find. Plant owed by 2.10
                // Task 7: the handler's Project check skipped - the row must fail on both surfaces.
                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(ListOf(world.A.SpaceId, foreign), ListOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        // More than one page of A's Tasks, every one carrying A's Label, and B's Project with a Task carrying B's Label: walking
        // every page under every sort with A's own Label as the filter yields exactly A's Tasks, the total is exactly A's, and
        // nothing of B's - id, title or Label name - appears. Plant owed by 2.10 Task 7: the Task's two layers dropped (query
        // filter and FILTER predicate) - the row must fail on both surfaces.
        new(
            "ISO-LIST-AGGREGATE", "I-3 (includes the List View page and its total), FR-30", IsolationVector.Aggregate, HttpMethod.Get, ListRoute,
            async world =>
            {
                // Another of A's Projects holding a Task, so a page that lost its Project predicate shows it.
                _ = await AnyProjectWithTaskAsync(world, world.A);

                var (own, first) = await SeedProjectWithTaskAsync(world.A);
                var ownLabel = await SeedLabelAsync(world.A);

                await ApplyLabelThroughTheProductAsync(world.A, first, ownLabel);
                await SeedLabelledTasksThroughTheProductAsync(world.A, own, ownLabel, PageSize);

                var (_, theirs) = await AnyProjectWithTaskAsync(world, world.B);

                await ApplyLabelThroughTheProductAsync(world.B, theirs, await AnyLabelAsync(world, world.B));

                return await OwnAsync(world, ListOf(world.A.SpaceId, own, ListSortStatus, labelId: ownLabel));
            },
            Both(new Served(HttpStatusCode.OK, ListPagesHoldExactlyTheCallersTasksAsync, "200, under every sort each page at most 50, the total A's own, the walk exactly A's Tasks, none of B's ids, titles or Label names"))),

        // A filter naming B's real Label id, or B's real Status id, in A's own Project matches nothing and answers bytes
        // identical to a random id's (AC15, the I-13 shape of 2.3 and 2.4) - it reveals neither that the id exists nor whose
        // it is. Plant owed by 2.10 Task 7: the handler's narrowing of filter ids skipped - caught 0/2 here, masked (TaskLabel's
        // own two layers and the Project predicate already hide a foreign id's rows), so ViewListHandlerTests holds the
        // narrowing itself (2/2). This row holds the bytes; the narrowing's proof is the handler test's.
        new(
            "ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE", "I-13, I-4", IsolationVector.DeepLink, HttpMethod.Get, ListRoute,
            async world =>
            {
                var (own, _) = await AnyProjectWithTaskAsync(world, world.A);
                var (_, theirs) = await AnyProjectWithTaskAsync(world, world.B);
                var theirLabel = await AnyLabelAsync(world, world.B);
                var theirStatus = (await StatusIdsAsync(world.B))[0];

                await ApplyLabelThroughTheProductAsync(world.B, theirs, theirLabel);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [
                        new(ListOf(world.A.SpaceId, own, ListSortStatus, labelId: theirLabel), ListOf(world.A.SpaceId, own, ListSortStatus, labelId: Guid.NewGuid())),
                        new(ListOf(world.A.SpaceId, own, ListSortStatus, statusId: theirStatus), ListOf(world.A.SpaceId, own, ListSortStatus, statusId: Guid.NewGuid())),
                    ],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.OK))),

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

        // Story 3.1 (written by its ATDD run, 2026-10-29; live since its dev phase): the two routes 3.1 maps, each on
        // both surfaces (1.9-GATE-002). Each write
        // row acts as a FRESH actor, because A's switcher is ISO-LIST-SPACES' subject ("exactly [SA]") and A's Space
        // name is every row's presence anchor.
        new(
            "ISO-CREATE-SPACE", "I-3, I-11 (3.1 AC2, AC10)", IsolationVector.Write, HttpMethod.Post, SpaceRoutes.Directory,
            FreshActorCreatesASpaceAsync,
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.Created, TheCreatedSpaceIsTheCallersAloneAsync, "201, {id, name}; listed by the caller alone; A's and B's Spaces unchanged"),
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheTokenCreatedNoSpaceAsync },
            }),

        new(
            "ISO-RENAME-SPACE-CROSS", "I-2, I-4 (3.1 AC10)", IsolationVector.Write, HttpMethod.Patch, SpaceRoutes.Active,
            world => ForeignAsync(world, SpaceRoutes.ActiveOf, HttpMethod.Patch),
            Both(new BoundaryNotFound { Then = TheForeignSpaceKeepsItsNameAsync })),

        new(
            "ISO-RENAME-SPACE-OWN", "I-12 (3.1 AC10 in-Space anchor)", IsolationVector.Api, HttpMethod.Patch, SpaceRoutes.Active,
            FreshActorRenamesItsOwnSpaceAsync,
            Both(new Served(HttpStatusCode.OK, RenamedOnlyTheCallersOwnSpaceAsync, "200, {id, name, role: owner}; B's Space keeps its name"))),
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
        return await SeedLabelNamedAsync(owner).ConfigureAwait(false);
    }

    /// <summary>
    /// A Project of <paramref name="owner"/>'s that already holds a Task, or a new one with one. Story 2.10: the List
    /// rows read the world A and B share and need no Project of their own on the foreign side, so they reuse one rather
    /// than spend the projects-per-Space bound (50) every other row seeds against.
    /// </summary>
    private static async Task<(Guid ProjectId, Guid TaskId)> AnyProjectWithTaskAsync(IsolationWorld world, ProductActor owner)
    {
        var tasks = await world.Database.TasksInAsync(owner.SpaceId);

        return tasks.Count > 0 ? (tasks[0].ProjectId, tasks[0].Id) : await SeedProjectWithTaskAsync(owner);
    }

    /// <summary>
    /// A Label of <paramref name="owner"/>'s Space, or a new one. Story 2.10, for the reason
    /// <see cref="AnyProjectWithTaskAsync"/> gives.
    /// </summary>
    private static async Task<Guid> AnyLabelAsync(IsolationWorld world, ProductActor owner)
    {
        var labels = await world.Database.LabelsInAsync(owner.SpaceId);

        return labels.Count > 0 ? labels[0].Id : await SeedLabelAsync(owner);
    }

    private static async Task<Guid> SeedLabelNamedAsync(ProductActor owner)
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
        var ownTask = Guid.Parse(request.Paths[0].Path.Split('/')[^1]);

        var content = Assert.IsType<Func<HttpContent>>(request.Body)();

        using var body = JsonDocument.Parse(await content.ReadAsStringAsync());

        var foreignLabel = body.RootElement.GetProperty("labelIds")[0].GetGuid();

        // Scoped to the case's own Task and the foreign Label (story 2.10): the List rows apply Labels elsewhere in the
        // Spaces A and B share, so "no application anywhere" would depend on which rows ran first.
        Assert.Contains(await world.Database.LabelsInAsync(world.B.SpaceId), label => label.Id == foreignLabel);
        Assert.DoesNotContain(await world.Database.ApplicationsInAsync(world.A.SpaceId), application => application.TaskId == ownTask);
        Assert.DoesNotContain(await world.Database.ApplicationsInAsync(world.B.SpaceId), application => application.LabelId == foreignLabel);
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
    /// I-3 with Board column counts: A's Board holds exactly A's Tasks of that Project, each column's total is A's own,
    /// and none of B's Task ids or titles appear - anchored both ways, since both Spaces hold Tasks. Story 2.9 folded 2.2's
    /// "every Task is returned (Q6)" (a column's Tasks == its total) into the paged rule
    /// (<see cref="BoardPagesHoldExactlyTheCallersTasksAsync"/>): a column holds at most a page, with a cursor exactly when
    /// more follow, and walking its pages yields exactly its total.
    /// </summary>
    private static Task BoardHoldsExactlyTheCallersTasksAsync(IsolationWorld world, CaseRequest request, WireResponse answer) =>
        WalkTheBoardsPagesAsync(world, request, answer);

    /// <summary>
    /// AR-22's page size (an assumption, spine <c>:259</c>), stated once in Application as <c>BoardPage.Size</c>; this
    /// suite sees only the Host and Contracts, so the number is restated here.
    /// </summary>
    private const int PageSize = 50;

    /// <summary>
    /// <paramref name="count"/> more Tasks in <paramref name="owner"/>'s Project, created through the product one request
    /// each, every title unique and added to the owner's secrets (I-17). Story 2.9.
    /// </summary>
    private static async Task SeedTasksThroughTheProductAsync(ProductActor owner, Guid projectId, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var title = $"Task {Guid.NewGuid():N}";

            owner.HoldsTaskTitled(title);

            var created = await owner.SendAsync(
                IsolationSurface.Session,
                HttpMethod.Post,
                TaskRoutes.CreateIn(owner.SpaceId, projectId),
                JsonContent.Create(new { title }, options: JsonOptions),
                headers: null);

            Assert.True(created.StatusCode == HttpStatusCode.Created, $"Seeding a Task through the product: {created.StatusCode} {created.Body}");
        }
    }

    /// <summary>
    /// I-3 for one column page (story 2.9): the page's total is the number of A's Tasks of that Project in that Status, its
    /// Tasks are exactly those (fewer than a page here, so all of them, in key order), and none of B's ids or titles appear.
    /// Anchored both ways, since both Spaces hold Tasks.
    /// </summary>
    private static async Task ColumnPageHoldsExactlyTheCallersTasksAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var segments = request.Paths[0].Path.Split('?')[0].Split('/');
        var projectId = Guid.Parse(segments[^4]);
        var statusId = Guid.Parse(segments[^1]);
        var inProject = (await world.Database.TasksInAsync(world.A.SpaceId)).Where(task => task.ProjectId == projectId).Select(task => task.Id).ToHashSet();
        var own = (await world.Database.TaskPlacementsInAsync(world.A.SpaceId))
            .Where(task => inProject.Contains(task.Id) && task.StatusId == statusId)
            .OrderBy(task => task.PositionKey, StringComparer.Ordinal)
            .ToList();
        var theirs = await world.Database.TasksInAsync(world.B.SpaceId);

        Assert.NotEmpty(own);
        Assert.NotEmpty(theirs);

        using var document = JsonDocument.Parse(answer.Body);
        var listed = document.RootElement.GetProperty("tasks").EnumerateArray().Select(task => task.GetProperty("id").GetGuid()).ToList();

        Assert.Equal(own.Count, document.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(own.Select(task => task.Id), listed);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("nextAfter").ValueKind);
        Assert.Empty(listed.Intersect(theirs.Select(task => task.Id)));
        Assert.All(theirs, task => Assert.DoesNotContain(task.Title, answer.Body, StringComparison.Ordinal));
    }

    /// <summary>
    /// I-3 with Board column counts, once the Board pages (story 2.9 AC9): each column holds at most a page, carries a
    /// cursor exactly when its total exceeds what it holds, and walking every column's pages (on A's own Session) yields
    /// exactly A's Tasks of that Project - each column's walk exactly its total - and none of B's ids or titles.
    /// </summary>
    private static async Task BoardPagesHoldExactlyTheCallersTasksAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var projectId = Guid.Parse(request.Paths[0].Path.Split('/')[^2]);
        var own = (await world.Database.TasksInAsync(world.A.SpaceId)).Count(task => task.ProjectId == projectId);

        Assert.True(own > PageSize, $"The row's Project holds {own} Tasks; the case is about more than one page.");

        await WalkTheBoardsPagesAsync(world, request, answer);
    }

    /// <summary>
    /// The paged I-3 rule both Board rows share (story 2.9): each column at most a page with a cursor iff more follow, each
    /// column's walk exactly its total, all of them exactly A's Tasks of the Project, none of B's ids or titles.
    /// </summary>
    private static async Task WalkTheBoardsPagesAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var projectId = Guid.Parse(request.Paths[0].Path.Split('/')[^2]);
        var own = (await world.Database.TasksInAsync(world.A.SpaceId)).Where(task => task.ProjectId == projectId).ToList();
        var theirs = await world.Database.TasksInAsync(world.B.SpaceId);
        var walked = new List<Guid>();

        Assert.NotEmpty(own);
        Assert.NotEmpty(theirs);

        using var document = JsonDocument.Parse(answer.Body);

        foreach (var column in document.RootElement.GetProperty("columns").EnumerateArray())
        {
            var statusId = column.GetProperty("statusId").GetGuid();
            var total = column.GetProperty("total").GetInt32();
            var tasks = column.GetProperty("tasks").EnumerateArray().Select(task => task.GetProperty("id").GetGuid()).ToList();
            var cursor = column.GetProperty("nextAfter");
            var inColumn = new List<Guid>(tasks);

            Assert.True(tasks.Count <= PageSize, $"A Board column served {tasks.Count} Tasks; a page is at most {PageSize}.");
            Assert.Equal(tasks.Count < total, cursor.ValueKind != JsonValueKind.Null);

            var after = cursor.ValueKind == JsonValueKind.Null ? null : cursor.GetString();

            while (after is not null)
            {
                var page = await world.A.SendAsync(IsolationSurface.Session, HttpMethod.Get, BoardRoutes.ColumnOf(world.A.SpaceId, projectId, statusId, after));

                Assert.True(page.StatusCode == HttpStatusCode.OK, $"Walking A's own column: {page.StatusCode} {page.Body}");
                Assert.All(theirs, task => Assert.DoesNotContain(task.Title, page.Body, StringComparison.Ordinal));

                using var next = JsonDocument.Parse(page.Body);

                inColumn.AddRange(next.RootElement.GetProperty("tasks").EnumerateArray().Select(task => task.GetProperty("id").GetGuid()));
                after = next.RootElement.GetProperty("nextAfter").ValueKind == JsonValueKind.Null ? null : next.RootElement.GetProperty("nextAfter").GetString();
                Assert.True(inColumn.Count <= own.Count, "A column's walk returned more Tasks than A's Project holds.");
            }

            Assert.Equal(total, inColumn.Count);
            walked.AddRange(inColumn);
        }

        Assert.Equal(own.Select(task => task.Id).Order(), walked.Order());
        Assert.Empty(walked.Intersect(theirs.Select(task => task.Id)));
        Assert.All(theirs, task => Assert.DoesNotContain(task.Title, answer.Body, StringComparison.Ordinal));
    }

    /// <summary>
    /// Story 2.10 Q1 (a)'s List route, from the contract; the gap gate reads the Host's own route table, so a pattern that
    /// differs from the one mapped shows there.
    /// </summary>
    private static string ListRoute => ListRoutes.List;

    /// <summary>
    /// Story 2.10's Status sort code (Q7 (a)) - a literal, because it is an optional parameter's default; a code that
    /// drifted from <c>ListSorts.Status</c> would be refused <c>400</c> and fail every List row.
    /// </summary>
    private const string ListSortStatus = "status";

    /// <summary>
    /// Every sort story 2.10 offers (Q3, Q5, Q7), from the contract.
    /// </summary>
    private static readonly string[] OfferedListSorts = [ListSorts.Status, ListSorts.DueDate, ListSorts.Label];

    /// <summary>
    /// One Project's List path as a client calls it (story 2.10 Task 4 (a)): a sort, an optional Label or Status filter, and
    /// an optional cursor, every value escaped.
    /// </summary>
    private static string ListOf(Guid spaceId, Guid projectId, string sort = ListSortStatus, Guid? labelId = null, Guid? statusId = null, string? after = null)
    {
        var path = $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/list?sort={Uri.EscapeDataString(sort)}";

        if (labelId is { } label)
        {
            path += $"&label={label:D}";
        }

        if (statusId is { } status)
        {
            path += $"&status={status:D}";
        }

        return after is null ? path : path + "&after=" + Uri.EscapeDataString(after);
    }

    /// <summary>
    /// Sets exactly <paramref name="labelId"/> on a Task through the product's Labels edit (story 2.4 Q1 (a)), so the
    /// product maintains the Task's Label sort key itself. Story 2.10.
    /// </summary>
    private static async Task ApplyLabelThroughTheProductAsync(ProductActor owner, Guid taskId, Guid labelId)
    {
        var applied = await owner.SendAsync(IsolationSurface.Session, HttpMethod.Patch, TaskRoutes.TaskOf(owner.SpaceId, taskId), LabelIdsBody(labelId), headers: null);

        Assert.True(applied.StatusCode == HttpStatusCode.OK, $"Applying a Label through the product: {applied.StatusCode} {applied.Body}");
    }

    /// <summary>
    /// <paramref name="count"/> more Tasks in <paramref name="owner"/>'s Project, each created and then labelled
    /// <paramref name="labelId"/> through the product, every title unique and held as a secret (I-17). Story 2.10.
    /// </summary>
    private static async Task SeedLabelledTasksThroughTheProductAsync(ProductActor owner, Guid projectId, Guid labelId, int count)
    {
        for (var index = 0; index < count; index++)
        {
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

            await ApplyLabelThroughTheProductAsync(owner, document.RootElement.GetProperty("id").GetGuid(), labelId);
        }
    }

    /// <summary>
    /// I-3 for the List View (story 2.10 AC21): the row's Project holds more than a page of A's Tasks; under every offered
    /// sort, walking A's own pages with the row's Label filter (on A's own Session) gives pages of at most 50, a total on
    /// every page equal to A's Tasks of that Project, and a walk of exactly those Tasks, each once; nothing of B's - Task id,
    /// title or Label name - appears in any page. Anchored both ways: both Spaces hold labelled Tasks.
    /// </summary>
    private static async Task ListPagesHoldExactlyTheCallersTasksAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var path = request.Paths[0].Path;
        var projectId = Guid.Parse(path.Split('?')[0].Split('/')[^2]);
        var labelId = Guid.Parse(path.Split("label=")[1].Split('&')[0]);
        var own = (await world.Database.TasksInAsync(world.A.SpaceId)).Where(task => task.ProjectId == projectId).Select(task => task.Id).ToList();
        var theirs = await world.Database.TasksInAsync(world.B.SpaceId);
        var theirLabels = await world.Database.LabelsInAsync(world.B.SpaceId);

        Assert.True(own.Count > PageSize, $"The row's Project holds {own.Count} Tasks; the case is about more than one page.");
        Assert.NotEmpty(theirs);
        Assert.NotEmpty(theirLabels);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);

        // The page served on the surface under test (the Token surface included) holds only A's Tasks of this Project, A's
        // total, and nothing of B's - the walk below reads on A's Session, so this is the surface's own evidence (AC21).
        Assert.All(theirs, task => Assert.DoesNotContain(task.Title, answer.Body, StringComparison.Ordinal));
        Assert.All(theirLabels, label => Assert.DoesNotContain(label.Name, answer.Body, StringComparison.Ordinal));

        using (var served = JsonDocument.Parse(answer.Body))
        {
            var servedIds = served.RootElement.GetProperty("tasks").EnumerateArray().Select(task => task.GetProperty("id").GetGuid()).ToList();

            Assert.Equal(own.Count, served.RootElement.GetProperty("total").GetInt32());
            Assert.NotEmpty(servedIds);
            Assert.True(servedIds.Count <= PageSize, $"The surface served {servedIds.Count} Tasks; a page is at most {PageSize}.");
            Assert.Empty(servedIds.Except(own));
        }

        foreach (var sort in OfferedListSorts)
        {
            // Under the row's own Label filter, and unfiltered: the filter alone would hide a Task of another Project or
            // Space that carries none of A's Labels, so the unfiltered walk is the one a dropped Project predicate or a
            // dropped isolation layer shows in (story 2.10 Task 7's plants).
            foreach (var filter in new Guid?[] { labelId, null })
            {
                var walked = await WalkOwnListAsync(world, projectId, sort, filter, own.Count, theirs, theirLabels);

                Assert.Equal(walked.Count, walked.Distinct().Count());
                Assert.Equal(own.Order(), walked.Order());
                Assert.Empty(walked.Intersect(theirs.Select(task => task.Id)));
            }
        }
    }

    /// <summary>
    /// Walks A's own List of <paramref name="projectId"/> on A's Session to its end, holding every page to at most 50
    /// Tasks, a total of exactly <paramref name="total"/>, and nothing of B's - title or Label name - in its body.
    /// </summary>
    private static async Task<List<Guid>> WalkOwnListAsync(
        IsolationWorld world,
        Guid projectId,
        string sort,
        Guid? labelId,
        int total,
        IReadOnlyList<(Guid Id, Guid ProjectId, string Title)> theirs,
        IReadOnlyList<(Guid Id, string Name)> theirLabels)
    {
        var walked = new List<Guid>();
        string? after = null;
        var pages = 0;

        do
        {
            var page = await world.A.SendAsync(IsolationSurface.Session, HttpMethod.Get, ListOf(world.A.SpaceId, projectId, sort, labelId: labelId, after: after));

            Assert.True(page.StatusCode == HttpStatusCode.OK, $"Walking A's own List under {sort}: {page.StatusCode} {page.Body}");
            Assert.All(theirs, task => Assert.DoesNotContain(task.Title, page.Body, StringComparison.Ordinal));
            Assert.All(theirLabels, label => Assert.DoesNotContain(label.Name, page.Body, StringComparison.Ordinal));

            using var document = JsonDocument.Parse(page.Body);
            var tasks = document.RootElement.GetProperty("tasks").EnumerateArray().Select(task => task.GetProperty("id").GetGuid()).ToList();
            var cursor = document.RootElement.GetProperty("nextAfter");

            Assert.Equal(total, document.RootElement.GetProperty("total").GetInt32());
            Assert.True(tasks.Count <= PageSize, $"A List page served {tasks.Count} Tasks; a page is at most {PageSize}.");

            walked.AddRange(tasks);
            after = cursor.ValueKind == JsonValueKind.Null ? null : cursor.GetString();
            Assert.True(++pages <= total + 1, "The List's cursor never reached null.");
        }
        while (after is not null);

        return walked;
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

    /// <summary>
    /// Story 3.1 - a fresh Account sends the create on its own; the case body's name becomes one of its Space names, so
    /// I-17 looks for it in the log (a Space name never belongs there, AR-34).
    /// </summary>
    private static async Task<CaseRequest> FreshActorCreatesASpaceAsync(IsolationWorld world)
    {
        var creator = await world.NewActorAsync("Cs");

        creator.HoldsSpaceNamed(CaseBodyName);

        return new CaseRequest(creator, HttpMethod.Post, [new(SpaceRoutes.Directory, SpaceRoutes.Directory)], TargetSpaceId: null);
    }

    /// <summary>
    /// Story 3.1 AC2, AC9 - the answer is exactly <c>{id, name}</c> with the case's name; the creator's switcher lists
    /// its Personal Space and the new one; A's switcher is still exactly [SA] and B's still exactly [SB], neither naming
    /// the new id; the creator holds the new Space as Owner.
    /// </summary>
    private static async Task TheCreatedSpaceIsTheCallersAloneAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        Guid created;

        using (var document = JsonDocument.Parse(answer.Body))
        {
            Assert.Equal(["id", "name"], document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            Assert.Equal(CaseBodyName, document.RootElement.GetProperty("name").GetString());
            created = document.RootElement.GetProperty("id").GetGuid();
        }

        Assert.Equal(SpaceRoutes.ActiveOf(created), answer.Header("Location"));

        var own = Ids((await request.Actor.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.Directory)).Body);

        Assert.Equal(new[] { created, request.Actor.SpaceId }.Order(), own.Order());

        foreach (var other in new[] { world.A, world.B })
        {
            var listed = await other.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.Directory);

            Assert.Equal([other.SpaceId], Ids(listed.Body));
            Assert.DoesNotContain(created.ToString("D"), listed.Body, StringComparison.OrdinalIgnoreCase);
        }

        using var arrival = JsonDocument.Parse((await request.Actor.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.ActiveOf(created))).Body);

        Assert.Equal("owner", arrival.RootElement.GetProperty("role").GetString());
    }

    /// <summary>
    /// Story 3.1 AC10 - a Token is no credential on creation, so the creator still holds exactly its Personal Space.
    /// </summary>
    private static async Task TheTokenCreatedNoSpaceAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Equal([request.Actor.SpaceId], Ids((await request.Actor.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.Directory)).Body));

    /// <summary>
    /// Story 3.1 AC10 - B's Space keeps its name after A's refused rename, read by B through the product.
    /// </summary>
    private static async Task TheForeignSpaceKeepsItsNameAsync(IsolationWorld world, CaseRequest request)
    {
        using var document = JsonDocument.Parse((await world.B.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId))).Body);

        Assert.Equal(world.B.SpaceName, document.RootElement.GetProperty("name").GetString());
    }

    /// <summary>
    /// Story 3.1 - a fresh Account renames its own Personal Space, on either surface (its Token is bound there, as
    /// Owner). The new name joins its Space names, so I-17 looks for it.
    /// </summary>
    private static async Task<CaseRequest> FreshActorRenamesItsOwnSpaceAsync(IsolationWorld world)
    {
        var owner = await world.NewActorAsync("Rn");

        owner.HoldsSpaceNamed(CaseBodyName);

        return new CaseRequest(owner, HttpMethod.Patch, [new(SpaceRoutes.ActiveOf(owner.SpaceId), SpaceRoutes.ActiveOf(owner.SpaceId))], owner.SpaceId);
    }

    /// <summary>
    /// Story 3.1 AC3, AC9 - the answer is exactly <c>{id, name, role}</c> for the caller's own Space, renamed, at
    /// Owner; B's Space keeps its name.
    /// </summary>
    private static async Task RenamedOnlyTheCallersOwnSpaceAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        using (var document = JsonDocument.Parse(answer.Body))
        {
            var root = document.RootElement;

            Assert.Equal(["id", "name", "role"], root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            Assert.Equal(request.Actor.SpaceId, root.GetProperty("id").GetGuid());
            Assert.Equal(CaseBodyName, root.GetProperty("name").GetString());
            Assert.Equal("owner", root.GetProperty("role").GetString());
        }

        await TheForeignSpaceKeepsItsNameAsync(world, request);
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
