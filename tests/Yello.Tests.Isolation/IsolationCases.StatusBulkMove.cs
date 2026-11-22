using System.Collections.Concurrent;
using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Every Task of one Status moved into another Project, on both surfaces. The move is reached from another Space (the boundary
/// <c>404</c>, recorded); with another Space's Project or Status as the source in the caller's own Space path (the in-Space
/// <c>404</c>, byte-identical to a random id); with another Space's Project or Status in the body (the move's own <c>400</c>,
/// byte-identical to a random id). A Viewer is the Role refusal, and a Token bound to an Account's other Space is refused as
/// a non-member's would be. No Task changes Project in any row.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every Task and Project a row names is seeded in a fresh actor's Space</b>, never in A's or B's, whose Projects and
/// Tasks <c>ISO-DIRECT-BOARD</c>, <c>ISO-DIRECT-STATUSES</c> and <c>ISO-AGGREGATE</c> assert exactly; B is read only for a
/// Status id, so its Project bound is untouched.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    /// <summary>
    /// The Tasks each seeded source Project held, so a row's check can say they are all still there.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, Guid[]> StatusBulkMoveSeeds = new();

    private static IEnumerable<IsolationCase> StatusBulkMoveCases() =>
    [
        new IsolationCase(
            "ISO-STATUS-BULK-MOVE-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, ProjectStatusRoutes.TasksToProject,
            async world =>
            {
                var foreign = await world.NewActorAsync("Bf");
                var (sourceId, statusId, destinationId) = await StatusAndAnotherProjectAsync(foreign);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(ProjectStatusRoutes.TasksToProjectOf(foreign.SpaceId, sourceId, statusId), ProjectStatusRoutes.TasksToProjectOf(Guid.NewGuid(), sourceId, statusId))],
                    foreign.SpaceId,
                    Body: () => ProjectMoveWire.Body(destinationId),
                    Other: foreign);
            },
            Both(new BoundaryNotFound { Then = TheNamedSourceKeptItsTasksAsync })),

        // Another Space's Project as the source under the caller's own Space path is a Project that never existed there: the
        // unrecorded in-Space not-found, byte-identical to a random Project id. The body names the caller's own other Project,
        // so a leak would move the foreign Tasks into the caller's Space.
        new IsolationCase(
            "ISO-STATUS-BULK-MOVE-FOREIGN-PROJECT-IN-OWN-SPACE", "I-1, I-4, I-13", IsolationVector.DeepLink, HttpMethod.Post, ProjectStatusRoutes.TasksToProject,
            async world =>
            {
                var own = await world.NewActorAsync("Bo");
                var foreign = await world.NewActorAsync("Bt");
                var (foreignSource, _, _) = await StatusAndAnotherProjectAsync(foreign);
                var (_, ownStatus, ownDestination) = await StatusAndAnotherProjectAsync(own);

                return new CaseRequest(
                    own,
                    HttpMethod.Post,
                    [new(ProjectStatusRoutes.TasksToProjectOf(own.SpaceId, foreignSource, ownStatus), ProjectStatusRoutes.TasksToProjectOf(own.SpaceId, Guid.NewGuid(), ownStatus))],
                    TargetSpaceId: null,
                    Body: () => ProjectMoveWire.Body(ownDestination),
                    Other: foreign);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheNamedSourceKeptItsTasksAsync })),

        // A Status is path-borne here, so another Space's real Status as the source is the in-Space 404, identical to a random
        // Status id's, never the body's 400.
        new IsolationCase(
            "ISO-STATUS-BULK-MOVE-FOREIGN-STATUS-IN-OWN-PATH", "I-1, I-4, I-13", IsolationVector.DeepLink, HttpMethod.Post, ProjectStatusRoutes.TasksToProject,
            async world =>
            {
                var actor = await world.NewActorAsync("Bs");
                var (sourceId, _, destinationId) = await StatusAndAnotherProjectAsync(actor);
                var foreignStatus = (await StatusIdsAsync(world.B))[0];

                return new CaseRequest(
                    actor,
                    HttpMethod.Post,
                    [new(ProjectStatusRoutes.TasksToProjectOf(actor.SpaceId, sourceId, foreignStatus), ProjectStatusRoutes.TasksToProjectOf(actor.SpaceId, sourceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Body: () => ProjectMoveWire.Body(destinationId));
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheNamedSourceKeptItsTasksAsync })),

        // The destination is body-borne, so another Space's real Project is the move's own 400, identical to a random id's.
        new IsolationCase(
            "ISO-STATUS-BULK-MOVE-FOREIGN-PROJECT-IN-BODY", "I-1, I-13, FR-41", IsolationVector.DirectIdentifier, HttpMethod.Post, ProjectStatusRoutes.TasksToProject,
            async world =>
            {
                var actor = await world.NewActorAsync("Bp");
                var (sourceId, statusId, _) = await StatusAndAnotherProjectAsync(actor);
                var (_, foreignProject) = await ForeignProjectAsync(world);
                var path = ProjectStatusRoutes.TasksToProjectOf(actor.SpaceId, sourceId, statusId);

                return new CaseRequest(
                    actor,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => ProjectMoveWire.Body(foreignProject),
                    AnchorBody: () => ProjectMoveWire.Body(Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidTaskProject) { Then = TheNamedSourceKeptItsTasksAsync })),

        new IsolationCase(
            "ISO-STATUS-BULK-MOVE-FOREIGN-STATUS-IN-BODY", "I-1, I-13, FR-20", IsolationVector.DirectIdentifier, HttpMethod.Post, ProjectStatusRoutes.TasksToProject,
            async world =>
            {
                var actor = await world.NewActorAsync("Bb");
                var (sourceId, statusId, destinationId) = await StatusAndAnotherProjectAsync(actor);
                var foreignStatus = (await StatusIdsAsync(world.B))[1];
                var path = ProjectStatusRoutes.TasksToProjectOf(actor.SpaceId, sourceId, statusId);

                return new CaseRequest(
                    actor,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => ProjectMoveWire.Body(destinationId, foreignStatus),
                    AnchorBody: () => ProjectMoveWire.Body(destinationId, Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidStatusDestination) { Then = TheNamedSourceKeptItsTasksAsync })),

        new IsolationCase(
            "ISO-RECORD-KIND-STATUS-BULK-MOVE-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, ProjectStatusRoutes.TasksToProject,
            ViewerMovesAStatusToAnotherProjectAsync,
            Both(new RoleRefused("manage-tasks") { Then = TheNamedSourceKeptItsTasksAsync })),

        // One row per Space-scoped route a Token reaches. By Session the Account names a random destination for
        // a Status of its second Space's Project (the move's own 400, so nothing moves); by the first Space's Token it is a
        // non-member's.
        TokenOnAProjectsStatusesElsewhere(
            "STATUS-BULK-MOVE",
            HttpMethod.Post,
            ProjectStatusRoutes.TasksToProject,
            HttpStatusCode.BadRequest,
            (second, status) => ProjectStatusRoutes.TasksToProjectOf(second.SpaceId, second.ProjectId, status),
            () => ProjectMoveWire.Body(Guid.NewGuid())),
    ];

    /// <summary>
    /// A Project holding one Task in the Space's first Status, and a second, empty Project, in <paramref name="owner"/>'s
    /// Space, seeded through the product; the source's Tasks are remembered for <see cref="TheNamedSourceKeptItsTasksAsync"/>.
    /// </summary>
    private static async Task<(Guid SourceId, Guid StatusId, Guid DestinationId)> StatusAndAnotherProjectAsync(ProductActor owner)
    {
        var (sourceId, taskId) = await SeedProjectWithTaskAsync(owner);
        var destinationId = await SeedProjectAsync(owner);

        StatusBulkMoveSeeds[sourceId] = [taskId];

        return (sourceId, (await StatusIdsAsync(owner))[0], destinationId);
    }

    /// <summary>
    /// A fresh Account with a Task and a second Project in its own Space, then demoted to Viewer there; the request moves the
    /// Task's Status into the second Project with a valid body, so only the Role can refuse it.
    /// </summary>
    private static async Task<CaseRequest> ViewerMovesAStatusToAnotherProjectAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vb");
        var (sourceId, statusId, destinationId) = await StatusAndAnotherProjectAsync(viewer);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = ProjectStatusRoutes.TasksToProjectOf(viewer.SpaceId, sourceId, statusId);

        return new CaseRequest(viewer, HttpMethod.Post, [new(path, path)], viewer.SpaceId, Body: () => ProjectMoveWire.Body(destinationId));
    }

    /// <summary>
    /// Every Task of the source Project the refused move named - the Project segment of its path - is still in that Project,
    /// in whichever of the actor's or the other Space's it lives.
    /// </summary>
    private static async Task TheNamedSourceKeptItsTasksAsync(IsolationWorld world, CaseRequest request)
    {
        var named = Guid.Parse(request.Paths[0].Path.Split('/')[^5]);
        var seeded = StatusBulkMoveSeeds[named];
        var found = new List<(Guid Id, Guid ProjectId, string Title)>();

        foreach (var spaceId in new[] { request.Actor.SpaceId, request.Other?.SpaceId }.OfType<Guid>())
        {
            found.AddRange((await world.Database.TasksInAsync(spaceId)).Where(row => seeded.Contains(row.Id)));
        }

        Assert.Equal(seeded.Order(), found.Select(row => row.Id).Order());
        Assert.All(found, row => Assert.Equal(named, row.ProjectId));
    }
}
