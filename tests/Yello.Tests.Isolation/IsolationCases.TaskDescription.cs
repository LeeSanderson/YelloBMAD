using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Merge;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// A Task's description, on both surfaces and both routes. The change route and the read route are reached from another
/// Space (the boundary <c>404</c>, recorded) and with another Space's Task under the caller's own Space path (the in-Space
/// <c>404</c>, byte-identical to a random id). A Viewer is the Role refusal on the change route and is served on the read
/// route, and a Token bound to an Account's other Space is refused as a non-member's would be. No refused row leaves a
/// description behind.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every Task a row names is seeded in a fresh actor's Space</b>, never in A's or B's, whose Tasks other rows assert
/// exactly. Each change body is one insert made by a fresh replica of the real strategy, so every send carries a valid
/// change and never text. Whether a refused row left anything is read through the product, as the Task's own Owner, so the
/// check needs no access to the log the policy hides.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private const string IsolationCaseDescription = "written by an isolation case";

    private static IEnumerable<IsolationCase> TaskDescriptionCases() =>
    [
        new IsolationCase(
            "ISO-DESCRIPTION-CHANGES-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, TaskDescriptionWire.ChangesRoute,
            async world =>
            {
                var foreign = await world.NewActorAsync("Df");
                var (_, taskId) = await SeedProjectWithTaskAsync(foreign);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(TaskDescriptionWire.ChangesOf(foreign.SpaceId, taskId), TaskDescriptionWire.ChangesOf(Guid.NewGuid(), taskId))],
                    foreign.SpaceId,
                    Body: OneInsert,
                    Other: foreign);
            },
            Both(new BoundaryNotFound { Then = TheNamedTaskHasNoDescriptionAsync })),

        new IsolationCase(
            "ISO-DESCRIPTION-FOREIGN-SPACE", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, TaskDescriptionWire.Route,
            async world =>
            {
                var foreign = await world.NewActorAsync("Dr");
                var (_, taskId) = await SeedProjectWithTaskAsync(foreign);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(TaskDescriptionWire.Of(foreign.SpaceId, taskId), TaskDescriptionWire.Of(Guid.NewGuid(), taskId))],
                    foreign.SpaceId,
                    Other: foreign);
            },
            Both(new BoundaryNotFound())),

        // Another Space's Task under the caller's own Space path is a Task that never existed there: the unrecorded in-Space
        // not-found, byte-identical to a random Task id. A leak would write into the foreign Task's log.
        new IsolationCase(
            "ISO-DESCRIPTION-CHANGES-FOREIGN-TASK-IN-OWN-SPACE", "I-1, I-4, I-13", IsolationVector.DeepLink, HttpMethod.Post, TaskDescriptionWire.ChangesRoute,
            async world =>
            {
                var own = await world.NewActorAsync("Do");
                var foreign = await world.NewActorAsync("Dt");
                var (_, foreignTask) = await SeedProjectWithTaskAsync(foreign);

                _ = await SeedProjectWithTaskAsync(own);

                return new CaseRequest(
                    own,
                    HttpMethod.Post,
                    [new(TaskDescriptionWire.ChangesOf(own.SpaceId, foreignTask), TaskDescriptionWire.ChangesOf(own.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Body: OneInsert,
                    Other: foreign);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = TheNamedTaskHasNoDescriptionAsync })),

        new IsolationCase(
            "ISO-DESCRIPTION-FOREIGN-TASK-IN-OWN-SPACE", "I-1, I-4, I-13", IsolationVector.DeepLink, HttpMethod.Get, TaskDescriptionWire.Route,
            async world =>
            {
                var own = await world.NewActorAsync("Dp");
                var foreign = await world.NewActorAsync("Dq");
                var (_, foreignTask) = await SeedProjectWithTaskAsync(foreign);

                _ = await SeedProjectWithTaskAsync(own);

                return new CaseRequest(
                    own,
                    HttpMethod.Get,
                    [new(TaskDescriptionWire.Of(own.SpaceId, foreignTask), TaskDescriptionWire.Of(own.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Other: foreign);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        new IsolationCase(
            "ISO-RECORD-KIND-DESCRIPTION-CHANGES-VIEWER", "V-1, C-6, FR-16, X-9", IsolationVector.RecordKind, HttpMethod.Post, TaskDescriptionWire.ChangesRoute,
            async world =>
            {
                var (viewer, taskId) = await ViewerWithATaskAsync(world, "Dv");
                var path = TaskDescriptionWire.ChangesOf(viewer.SpaceId, taskId);

                return new CaseRequest(viewer, HttpMethod.Post, [new(path, path)], viewer.SpaceId, Body: OneInsert);
            },
            Both(new RoleRefused(TaskDescriptionWire.EditCapabilityCode) { Then = TheNamedTaskHasNoDescriptionAsync })),

        // The read declares reading, which every Role holds: a Viewer is served the projection of its own Space's Task.
        new IsolationCase(
            "ISO-DESCRIPTION-VIEWER-READS", "V-1, FR-16", IsolationVector.Api, HttpMethod.Get, TaskDescriptionWire.Route,
            async world =>
            {
                var (viewer, taskId) = await ViewerWithATaskAsync(world, "Dw");
                var path = TaskDescriptionWire.Of(viewer.SpaceId, taskId);

                return new CaseRequest(viewer, HttpMethod.Get, [new(path, path)], TargetSpaceId: null);
            },
            Both(new Served(HttpStatusCode.OK, ServedAnEmptyDescriptionAsync, "200, the empty projection and a state; a Viewer reads the description"))),

        // One row per Space-scoped route a Token reaches. By Session the Account writes to and reads a Task of its second
        // Space; by the first Space's Token each is a non-member's.
        TokenOwnOtherSpace("DESCRIPTION-CHANGES", HttpMethod.Post, TaskDescriptionWire.ChangesRoute, HttpStatusCode.OK, sa2 => TaskDescriptionWire.ChangesOf(sa2.SpaceId, sa2.TaskIds[0]), OneInsert),

        TokenOwnOtherSpace("DESCRIPTION", HttpMethod.Get, TaskDescriptionWire.Route, HttpStatusCode.OK, sa2 => TaskDescriptionWire.Of(sa2.SpaceId, sa2.TaskIds[0])),
    ];

    /// <summary>
    /// A change batch of one insert, made by a fresh replica: a valid change and the replica's version, never text.
    /// </summary>
    private static HttpContent OneInsert()
    {
        var editor = TextMerge.CreateStrategy().CreateReplica(new ReplicaId(Guid.NewGuid()));
        var change = editor.Insert(0, IsolationCaseDescription);

        return TaskDescriptionWire.Body([change.Encoded.ToArray()], editor.Version.Encoded.ToArray());
    }

    /// <summary>
    /// A fresh Account with a Task in its own Personal Space, then demoted to Viewer there, so its Session and its Token both
    /// hold exactly the Viewer Role.
    /// </summary>
    private static async Task<(ProductActor Viewer, Guid TaskId)> ViewerWithATaskAsync(IsolationWorld world, string name)
    {
        var viewer = await world.NewActorAsync(name);
        var (_, taskId) = await SeedProjectWithTaskAsync(viewer);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        return (viewer, taskId);
    }

    /// <summary>
    /// The Task the refused request named still has no description, read through the product by an actor of the Task's own
    /// Space: the case's other actor when there is one, otherwise the actor itself.
    /// </summary>
    private static async Task TheNamedTaskHasNoDescriptionAsync(IsolationWorld world, CaseRequest request)
    {
        var holder = request.Other ?? request.Actor;
        var taskId = Guid.Parse(request.Paths[0].Path.Split('/')[^3]);
        var read = await holder.SendAsync(IsolationSurface.Session, HttpMethod.Get, TaskDescriptionWire.Of(holder.SpaceId, taskId));

        Assert.True(read.StatusCode == HttpStatusCode.OK, $"Reading the named Task's description: {read.StatusCode} {read.Body}");
        Assert.Equal(string.Empty, TaskDescriptionWire.TextIn(read.Body));
    }

    private static Task ServedAnEmptyDescriptionAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        Assert.Equal(string.Empty, TaskDescriptionWire.TextIn(answer.Body));
        Assert.NotNull(TaskDescriptionWire.StateIn(answer.Body));

        return Task.CompletedTask;
    }
}
