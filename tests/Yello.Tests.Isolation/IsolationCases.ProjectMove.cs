using System.Collections.Concurrent;
using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// A Task moved into another Project, on both surfaces. The move is reached from another Space (the boundary <c>404</c>,
/// recorded); with another Space's Task in the caller's own Space path (the in-Space <c>404</c>, byte-identical to a random
/// id); with another Space's Project or Status in the body (the move's own <c>400</c>, byte-identical to a random id). A
/// Viewer is the Role refusal, and a Token bound to an Account's other Space is refused as a non-member's would be. No Task
/// changes Project in any row.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every Task and Project a row names is seeded in a fresh actor's Space</b>, never in A's or B's, whose Projects and
/// Tasks <c>ISO-DIRECT-BOARD</c> and <c>ISO-AGGREGATE</c> assert exactly; B is read only for a Status id.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    /// <summary>
    /// The Project each seeded Task was created in, so a row's check can say the Task is still there.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, Guid> ProjectMoveSeeds = new();

    private static IEnumerable<IsolationCase> ProjectMoveCases() =>
    [
        new IsolationCase(
            "ISO-PROJECT-MOVE-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, TaskRoutes.ProjectMove,
            async world =>
            {
                var foreign = await world.NewActorAsync("Mf");
                var (taskId, destinationId) = await TaskAndAnotherProjectAsync(foreign);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(TaskRoutes.ProjectMoveOf(foreign.SpaceId, taskId), TaskRoutes.ProjectMoveOf(Guid.NewGuid(), taskId))],
                    foreign.SpaceId,
                    Body: () => ProjectMoveWire.Body(destinationId),
                    Other: foreign);
            },
            Both(new BoundaryNotFound { Then = TheNamedTaskKeptItsProjectAsync })),

        // Another Space's Task under the caller's own Space path is a Task that never existed there: the unrecorded in-Space
        // not-found, byte-identical to a random Task id. The body names the caller's own other Project, so a leak would move
        // the foreign Task into the caller's Space.
        new IsolationCase(
            "ISO-PROJECT-MOVE-FOREIGN-TASK-IN-OWN-SPACE", "I-1, I-4, I-13", IsolationVector.DeepLink, HttpMethod.Post, TaskRoutes.ProjectMove,
            async world =>
            {
                var own = await world.NewActorAsync("Mo");
                var foreign = await world.NewActorAsync("Mt");
                var (foreignTask, _) = await TaskAndAnotherProjectAsync(foreign);
                var (_, ownDestination) = await TaskAndAnotherProjectAsync(own);

                return new CaseRequest(
                    own,
                    HttpMethod.Post,
                    [new(TaskRoutes.ProjectMoveOf(own.SpaceId, foreignTask), TaskRoutes.ProjectMoveOf(own.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Body: () => ProjectMoveWire.Body(ownDestination),
                    Other: foreign);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheNamedTaskKeptItsProjectAsync })),

        // A destination is body-borne, so another Space's real Project is the move's own 400, identical to a random id's, never
        // the boundary 404 a path id gets.
        new IsolationCase(
            "ISO-PROJECT-MOVE-FOREIGN-PROJECT-IN-BODY", "I-1, I-13, FR-41", IsolationVector.DirectIdentifier, HttpMethod.Post, TaskRoutes.ProjectMove,
            async world =>
            {
                var actor = await world.NewActorAsync("Mp");
                var (taskId, _) = await TaskAndAnotherProjectAsync(actor);
                var (_, foreignProject) = await ForeignProjectAsync(world);
                var path = TaskRoutes.ProjectMoveOf(actor.SpaceId, taskId);

                return new CaseRequest(
                    actor,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => ProjectMoveWire.Body(foreignProject),
                    AnchorBody: () => ProjectMoveWire.Body(Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidTaskProject) { Then = TheNamedTaskKeptItsProjectAsync })),

        new IsolationCase(
            "ISO-PROJECT-MOVE-FOREIGN-STATUS-IN-BODY", "I-1, I-13, FR-20", IsolationVector.DirectIdentifier, HttpMethod.Post, TaskRoutes.ProjectMove,
            async world =>
            {
                var actor = await world.NewActorAsync("Ms");
                var (taskId, destinationId) = await TaskAndAnotherProjectAsync(actor);
                var foreignStatus = (await StatusIdsAsync(world.B))[1];
                var path = TaskRoutes.ProjectMoveOf(actor.SpaceId, taskId);

                return new CaseRequest(
                    actor,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => ProjectMoveWire.Body(destinationId, foreignStatus),
                    AnchorBody: () => ProjectMoveWire.Body(destinationId, Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidStatusDestination) { Then = TheNamedTaskKeptItsProjectAsync })),

        new IsolationCase(
            "ISO-RECORD-KIND-TASK-PROJECT-MOVE-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, TaskRoutes.ProjectMove,
            ViewerMovesATaskToAnotherProjectAsync,
            Both(new RoleRefused("manage-tasks") { Then = TheNamedTaskKeptItsProjectAsync })),

        // The 3.4 gate: one row per Space-scoped route a Token reaches. By Session the Account names a random destination for a
        // Task of its second Space (the move's own 400, so nothing moves); by the first Space's Token it is a non-member's.
        TokenOwnOtherSpace(
            "TASK-PROJECT-MOVE",
            HttpMethod.Post,
            TaskRoutes.ProjectMove,
            HttpStatusCode.BadRequest,
            sa2 => TaskRoutes.ProjectMoveOf(sa2.SpaceId, sa2.TaskIds[0]),
            () => ProjectMoveWire.Body(Guid.NewGuid())),
    ];

    /// <summary>
    /// A Project holding one Task, and a second, empty Project, in <paramref name="owner"/>'s Space, seeded through the
    /// product; the Task's Project is remembered for <see cref="TheNamedTaskKeptItsProjectAsync"/>.
    /// </summary>
    private static async Task<(Guid TaskId, Guid DestinationId)> TaskAndAnotherProjectAsync(ProductActor owner)
    {
        var (sourceId, taskId) = await SeedProjectWithTaskAsync(owner);
        var destinationId = await SeedProjectAsync(owner);

        ProjectMoveSeeds[taskId] = sourceId;

        return (taskId, destinationId);
    }

    /// <summary>
    /// A fresh Account with a Task and a second Project in its own Space, then demoted to Viewer there; the request moves that
    /// Task into the second Project with a valid body, so only the Role can refuse it.
    /// </summary>
    private static async Task<CaseRequest> ViewerMovesATaskToAnotherProjectAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vj");
        var (taskId, destinationId) = await TaskAndAnotherProjectAsync(viewer);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = TaskRoutes.ProjectMoveOf(viewer.SpaceId, taskId);

        return new CaseRequest(viewer, HttpMethod.Post, [new(path, path)], viewer.SpaceId, Body: () => ProjectMoveWire.Body(destinationId));
    }

    /// <summary>
    /// The Task the refused move named - the segment before <c>/project</c> in its path - is still in the Project it was
    /// seeded in, in whichever of the actor's or the other Space's it lives.
    /// </summary>
    private static async Task TheNamedTaskKeptItsProjectAsync(IsolationWorld world, CaseRequest request)
    {
        var named = Guid.Parse(request.Paths[0].Path.Split('/')[^2]);
        var seededIn = ProjectMoveSeeds[named];

        foreach (var spaceId in new[] { request.Actor.SpaceId, request.Other?.SpaceId }.OfType<Guid>())
        {
            var task = (await world.Database.TasksInAsync(spaceId)).SingleOrDefault(row => row.Id == named);

            if (task.Id == named)
            {
                Assert.Equal(seededIn, task.ProjectId);

                return;
            }
        }

        Assert.Fail($"The Task {named} the move named is in neither Space any more.");
    }
}
