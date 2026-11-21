using System.Net;
using System.Net.Http.Json;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// A Project's Status delta on both surfaces: the read of its effective set and the add, rename and place writes. Each
/// route is reached from another Space (the boundary <c>404</c>, recorded), with another Space's Project id in the caller's
/// own Space path, and with another Space's or a sibling Project's Status id in the caller's own Project path (the in-Space
/// <c>404</c>, byte-identical to a random id, recorded nowhere). A Viewer's add is the Role refusal, and the read lists the
/// caller's own Project's set and nothing of another Space's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pending until the routes are mapped</b>; the develop phase makes these rows live before review (GATE-003) and swaps
/// <see cref="ProjectStatusWire"/>'s stand-ins for the contract members.
/// </para>
/// <para>
/// <b>Statuses are added only in fresh actors' Spaces, never in A's or B's</b>: the boundary world is shared by every row,
/// and A's and B's Spaces must keep exactly their three defaults for <c>ISO-AGGREGATE</c> and the other Status rows.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private const string ProjectStatusesPending = "The Project Status routes are not mapped until the dev phase of the story that shapes a Project's Statuses.";

    private static IEnumerable<IsolationCase> ProjectStatusCases() =>
    [
        new IsolationCase(
            "ISO-DIRECT-PROJECT-STATUSES", "I-1, I-3", IsolationVector.DirectIdentifier, HttpMethod.Get, ProjectStatusWire.Collection,
            async world =>
            {
                var foreign = await SeedProjectAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(ProjectStatusWire.CollectionOf(world.B.SpaceId, foreign), ProjectStatusWire.CollectionOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound()),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),

        new IsolationCase(
            "ISO-WRITE-PROJECT-STATUS", "I-2", IsolationVector.Write, HttpMethod.Post, ProjectStatusWire.Collection,
            async world =>
            {
                var foreign = await SeedProjectAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(ProjectStatusWire.CollectionOf(world.B.SpaceId, foreign), ProjectStatusWire.CollectionOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = NoStatusNamedForTheCaseInBAsync }),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),

        new IsolationCase(
            "ISO-RENAME-PROJECT-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Patch, ProjectStatusWire.Item,
            async world =>
            {
                var foreign = await SeedProjectAsync(world.B);
                var theirStatus = (await StatusIdsAsync(world.B))[0];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(ProjectStatusWire.ItemOf(world.B.SpaceId, foreign, theirStatus), ProjectStatusWire.ItemOf(Guid.NewGuid(), foreign, theirStatus))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = NoStatusNamedForTheCaseInBAsync }),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),

        new IsolationCase(
            "ISO-PLACE-PROJECT-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, ProjectStatusWire.Place,
            async world =>
            {
                var foreign = await SeedProjectAsync(world.B);
                var theirStatus = (await StatusIdsAsync(world.B))[2];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(ProjectStatusWire.PlaceOf(world.B.SpaceId, foreign, theirStatus), ProjectStatusWire.PlaceOf(Guid.NewGuid(), foreign, theirStatus))],
                    world.B.SpaceId,
                    Body: () => ProjectStatusWire.PlaceBody(afterStatusId: null));
            },
            Both(new BoundaryNotFound()),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),

        new IsolationCase(
            "ISO-PROJECT-STATUSES-FOREIGN-PROJECT-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Get, ProjectStatusWire.Collection,
            async world =>
            {
                var foreign = await SeedProjectAsync(world.B);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(ProjectStatusWire.CollectionOf(world.A.SpaceId, foreign), ProjectStatusWire.CollectionOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable)),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),

        // B's real Status and a sibling Project's added Status, each in the caller's own Project path: both are rows the
        // StatusDefinition query filter or the effective set must keep out, and each must answer what a random id answers.
        new IsolationCase(
            "ISO-RENAME-FOREIGN-STATUS-IN-OWN-PROJECT", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Patch, ProjectStatusWire.Item,
            world => ForeignStatusesInOwnProjectAsync(world, "Pr", HttpMethod.Patch, ProjectStatusWire.ItemOf, body: null),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = NoStatusNamedForTheCaseInTheActorsSpaceAsync }),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),

        new IsolationCase(
            "ISO-PLACE-FOREIGN-STATUS-IN-OWN-PROJECT", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Post, ProjectStatusWire.Place,
            world => ForeignStatusesInOwnProjectAsync(world, "Pp", HttpMethod.Post, ProjectStatusWire.PlaceOf, () => ProjectStatusWire.PlaceBody(afterStatusId: null)),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable)),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),

        new IsolationCase(
            "ISO-RECORD-KIND-PROJECT-STATUS-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, ProjectStatusWire.Collection,
            ViewerAddsAProjectStatusAsync,
            Both(new RoleRefused(ProjectStatusWire.ConfigureCapability) { Then = NoStatusNamedForTheCaseInTheActorsSpaceAsync }),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),

        new IsolationCase(
            "ISO-PROJECT-STATUSES-AGGREGATE", "I-3, FR-25", IsolationVector.Aggregate, HttpMethod.Get, ProjectStatusWire.Collection,
            async world =>
            {
                var own = await world.NewActorAsync("Pa");
                var other = await world.NewActorAsync("Pb");
                var ownProject = await SeedProjectAsync(own);
                var otherProject = await SeedProjectAsync(other);

                await AddProjectStatusThroughTheProductAsync(own, ownProject, "Parked here");
                await AddProjectStatusThroughTheProductAsync(other, otherProject, "Parked elsewhere");

                var path = ProjectStatusWire.CollectionOf(own.SpaceId, ownProject);

                return new CaseRequest(own, HttpMethod.Get, [new(path, path)], TargetSpaceId: null, Other: other);
            },
            Both(new Served(HttpStatusCode.OK, ListsExactlyTheCallersProjectSetAsync, "200, exactly the caller's three defaults and its own added Status, none of another Space's ids or names")),
            PendingUntil: "6.1",
            PendingReason: ProjectStatusesPending),
    ];

    /// <summary>
    /// A fresh Account with two Projects in its own Space, the second holding an added Status; the request names, in the first
    /// Project's path, B's real Status and then the sibling's added Status, each anchored on a random Status id.
    /// </summary>
    private static async Task<CaseRequest> ForeignStatusesInOwnProjectAsync(
        IsolationWorld world,
        string label,
        HttpMethod method,
        Func<Guid, Guid, Guid, string> route,
        Func<HttpContent>? body)
    {
        var actor = await world.NewActorAsync(label);
        var own = await SeedProjectAsync(actor);
        var sibling = await SeedProjectAsync(actor);
        var siblings = await AddProjectStatusThroughTheProductAsync(actor, sibling, "Sibling only");
        var theirs = (await StatusIdsAsync(world.B))[1];

        return new CaseRequest(
            actor,
            method,
            [
                new(route(actor.SpaceId, own, theirs), route(actor.SpaceId, own, Guid.NewGuid())),
                new(route(actor.SpaceId, own, siblings), route(actor.SpaceId, own, Guid.NewGuid())),
            ],
            TargetSpaceId: null,
            Body: body);
    }

    /// <summary>
    /// A fresh Account with a Project in its own Personal Space, then demoted to Viewer there; the request adds a Status to
    /// that Project.
    /// </summary>
    private static async Task<CaseRequest> ViewerAddsAProjectStatusAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Vs");
        var projectId = await SeedProjectAsync(viewer);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = ProjectStatusWire.CollectionOf(viewer.SpaceId, projectId);

        return new CaseRequest(viewer, HttpMethod.Post, [new(path, path)], viewer.SpaceId);
    }

    /// <summary>
    /// Adds a Status to one of <paramref name="owner"/>'s Projects through the product and answers its id.
    /// </summary>
    private static async Task<Guid> AddProjectStatusThroughTheProductAsync(ProductActor owner, Guid projectId, string name)
    {
        var added = await owner.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Post,
            ProjectStatusWire.CollectionOf(owner.SpaceId, projectId),
            JsonContent.Create(new { name }, options: JsonOptions),
            headers: null);

        Assert.True(added.StatusCode == HttpStatusCode.Created, $"Adding a Project Status through the product: {added.StatusCode} {added.Body}");

        return ProjectStatusWire.EntriesOf(added.Body).Single(entry => entry.Name == name).Id;
    }

    /// <summary>
    /// B's Space holds Statuses (the anchor) and none is named for the refused request's body.
    /// </summary>
    private static async Task NoStatusNamedForTheCaseInBAsync(IsolationWorld world, CaseRequest request)
    {
        var names = await world.Database.StatusNamesInAsync(world.B.SpaceId);

        Assert.NotEmpty(names);
        Assert.DoesNotContain(CaseBodyName, names);
    }

    /// <summary>
    /// The actor's own Space holds Statuses (the anchor) and none is named for the refused request's body.
    /// </summary>
    private static async Task NoStatusNamedForTheCaseInTheActorsSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        var names = await world.Database.StatusNamesInAsync(request.Actor.SpaceId);

        Assert.NotEmpty(names);
        Assert.DoesNotContain(CaseBodyName, names);
    }

    /// <summary>
    /// The read lists exactly the caller's Space's Statuses (its three defaults and the one its only Project added), and none
    /// of the other Account's ids or names - anchored both ways, since the other Space holds an added Status too.
    /// </summary>
    private static async Task ListsExactlyTheCallersProjectSetAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no other Space.");
        var listed = ProjectStatusWire.EntriesOf(answer.Body);
        var own = await world.Database.StatusIdsInAsync(request.Actor.SpaceId);
        var theirs = await world.Database.StatusIdsInAsync(other.SpaceId);
        var theirNames = await world.Database.StatusNamesInAsync(other.SpaceId);

        Assert.Equal(4, own.Count);
        Assert.Equal(4, theirs.Count);
        Assert.Equal(own.Order(), listed.Select(entry => entry.Id).Order());
        Assert.Empty(listed.Select(entry => entry.Id).Intersect(theirs));
        Assert.DoesNotContain("Parked elsewhere", answer.Body, StringComparison.Ordinal);
        Assert.Contains("Parked elsewhere", theirNames);
    }
}
