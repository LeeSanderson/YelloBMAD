using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;
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
/// <b>Statuses are added only in fresh actors' Spaces, never in A's or B's</b>: the boundary world is shared by every row,
/// and A's and B's Spaces must keep exactly their three defaults for <c>ISO-AGGREGATE</c> and the other Status rows.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private const string ConfigureCapabilityCode = "configure-project-status-delta";

    // The foreign Space these rows reach from A is a fresh Account's, seeded once per world and shared by every row: B's
    // Space already sits at its Projects-per-Space bound once the earlier rows have run, so one more Project there would
    // refuse another row's seed.
    private static readonly ConditionalWeakTable<IsolationWorld, Task<(ProductActor Owner, Guid ProjectId)>> ForeignProjects = [];

    private static IEnumerable<IsolationCase> ProjectStatusCases() =>
    [
        new IsolationCase(
            "ISO-DIRECT-PROJECT-STATUSES", "I-1, I-3", IsolationVector.DirectIdentifier, HttpMethod.Get, ProjectStatusRoutes.Collection,
            async world =>
            {
                var (foreignOwner, foreign) = await ForeignProjectAsync(world);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(ProjectStatusRoutes.CollectionOf(foreignOwner.SpaceId, foreign), ProjectStatusRoutes.CollectionOf(Guid.NewGuid(), foreign))],
                    foreignOwner.SpaceId);
            },
            Both(new BoundaryNotFound())),

        new IsolationCase(
            "ISO-WRITE-PROJECT-STATUS", "I-2", IsolationVector.Write, HttpMethod.Post, ProjectStatusRoutes.Collection,
            async world =>
            {
                var (foreignOwner, foreign) = await ForeignProjectAsync(world);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(ProjectStatusRoutes.CollectionOf(foreignOwner.SpaceId, foreign), ProjectStatusRoutes.CollectionOf(Guid.NewGuid(), foreign))],
                    foreignOwner.SpaceId);
            },
            Both(new BoundaryNotFound { Then = NoStatusNamedForTheCaseInTheTargetSpaceAsync })),

        new IsolationCase(
            "ISO-RENAME-PROJECT-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Patch, ProjectStatusRoutes.Item,
            async world =>
            {
                var (foreignOwner, foreign) = await ForeignProjectAsync(world);
                var theirStatus = (await StatusIdsAsync(foreignOwner))[0];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(ProjectStatusRoutes.ItemOf(foreignOwner.SpaceId, foreign, theirStatus), ProjectStatusRoutes.ItemOf(Guid.NewGuid(), foreign, theirStatus))],
                    foreignOwner.SpaceId);
            },
            Both(new BoundaryNotFound { Then = NoStatusNamedForTheCaseInTheTargetSpaceAsync })),

        new IsolationCase(
            "ISO-PLACE-PROJECT-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, ProjectStatusRoutes.Place,
            async world =>
            {
                var (foreignOwner, foreign) = await ForeignProjectAsync(world);
                var theirStatus = (await StatusIdsAsync(foreignOwner))[2];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(ProjectStatusRoutes.PlaceOf(foreignOwner.SpaceId, foreign, theirStatus), ProjectStatusRoutes.PlaceOf(Guid.NewGuid(), foreign, theirStatus))],
                    foreignOwner.SpaceId,
                    Body: () => ProjectStatusWire.PlaceBody(afterStatusId: null));
            },
            Both(new BoundaryNotFound())),

        new IsolationCase(
            "ISO-PROJECT-STATUSES-FOREIGN-PROJECT-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Get, ProjectStatusRoutes.Collection,
            async world =>
            {
                var (_, foreign) = await ForeignProjectAsync(world);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(ProjectStatusRoutes.CollectionOf(world.A.SpaceId, foreign), ProjectStatusRoutes.CollectionOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        // B's real Status and a sibling Project's added Status, each in the caller's own Project path: both are rows the
        // StatusDefinition query filter or the effective set must keep out, and each must answer what a random id answers.
        new IsolationCase(
            "ISO-RENAME-FOREIGN-STATUS-IN-OWN-PROJECT", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Patch, ProjectStatusRoutes.Item,
            world => ForeignStatusesInOwnProjectAsync(world, "Pr", HttpMethod.Patch, ProjectStatusRoutes.ItemOf, body: null),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = NoStatusNamedForTheCaseInTheActorsSpaceAsync })),

        new IsolationCase(
            "ISO-PLACE-FOREIGN-STATUS-IN-OWN-PROJECT", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Post, ProjectStatusRoutes.Place,
            world => ForeignStatusesInOwnProjectAsync(world, "Pp", HttpMethod.Post, ProjectStatusRoutes.PlaceOf, () => ProjectStatusWire.PlaceBody(afterStatusId: null)),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        new IsolationCase(
            "ISO-RECORD-KIND-PROJECT-STATUS-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, ProjectStatusRoutes.Collection,
            ViewerAddsAProjectStatusAsync,
            Both(new RoleRefused(ConfigureCapabilityCode) { Then = NoStatusNamedForTheCaseInTheActorsSpaceAsync })),

        new IsolationCase(
            "ISO-PROJECT-STATUSES-AGGREGATE", "I-3, FR-25", IsolationVector.Aggregate, HttpMethod.Get, ProjectStatusRoutes.Collection,
            async world =>
            {
                var own = await world.NewActorAsync("Pa");
                var other = await world.NewActorAsync("Pb");
                var ownProject = await SeedProjectAsync(own);
                var otherProject = await SeedProjectAsync(other);

                await AddProjectStatusThroughTheProductAsync(own, ownProject, "Parked here");
                await AddProjectStatusThroughTheProductAsync(other, otherProject, "Parked elsewhere");

                var path = ProjectStatusRoutes.CollectionOf(own.SpaceId, ownProject);

                return new CaseRequest(own, HttpMethod.Get, [new(path, path)], TargetSpaceId: null, Other: other);
            },
            Both(new Served(HttpStatusCode.OK, ListsExactlyTheCallersProjectSetAsync, "200, exactly the caller's three defaults and its own added Status, none of another Space's ids or names"))),

        TokenOnAProjectsStatusesElsewhere("PROJECT-STATUSES", HttpMethod.Get, ProjectStatusRoutes.Collection, HttpStatusCode.OK, (second, _) => ProjectStatusRoutes.CollectionOf(second.SpaceId, second.ProjectId)),
        TokenOnAProjectsStatusesElsewhere("PROJECT-STATUS-ADD", HttpMethod.Post, ProjectStatusRoutes.Collection, HttpStatusCode.Created, (second, _) => ProjectStatusRoutes.CollectionOf(second.SpaceId, second.ProjectId)),
        TokenOnAProjectsStatusesElsewhere("PROJECT-STATUS-RENAME", HttpMethod.Patch, ProjectStatusRoutes.Item, HttpStatusCode.OK, (second, status) => ProjectStatusRoutes.ItemOf(second.SpaceId, second.ProjectId, status)),
        TokenOnAProjectsStatusesElsewhere("PROJECT-STATUS-PLACE", HttpMethod.Post, ProjectStatusRoutes.Place, HttpStatusCode.OK, (second, status) => ProjectStatusRoutes.PlaceOf(second.SpaceId, second.ProjectId, status), () => ProjectStatusWire.PlaceBody(afterStatusId: null)),
    ];

    /// <summary>
    /// The foreign Space's owner and its Project the rows reaching from A name, seeded through the product once per world.
    /// </summary>
    private static Task<(ProductActor Owner, Guid ProjectId)> ForeignProjectAsync(IsolationWorld world) =>
        ForeignProjects.GetValue(world, SeedForeignProjectAsync);

    private static async Task<(ProductActor Owner, Guid ProjectId)> SeedForeignProjectAsync(IsolationWorld world)
    {
        var owner = await world.NewActorAsync("Pf");

        return (owner, await SeedProjectAsync(owner));
    }

    /// <summary>
    /// A Token bound to an Account's first Space, used on a Project of its second, which the same Account owns: refused
    /// exactly as a non-member would be, while the Session, which carries no Space, is served.
    /// </summary>
    private static IsolationCase TokenOnAProjectsStatusesElsewhere(string suffix, HttpMethod method, string route, HttpStatusCode sessionStatus, Func<SeededSpace, Guid, string> pathOf, Func<HttpContent>? body = null) =>
        new(
            $"ISO-TOKEN-OWN-OTHER-SPACE-{suffix}", "I-12", IsolationVector.TokenOwnOtherSpace, method, route,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Ts");
                var path = pathOf(second, (await StatusIdsAsync(second.Actor))[0]);

                return new CaseRequest(first.Actor, method, [new(path, path)], second.SpaceId, Body: body, Other: second.Actor);
            },
            TokenOwnOtherSpaceExpectations(sessionStatus));

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

        var path = ProjectStatusRoutes.CollectionOf(viewer.SpaceId, projectId);

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
            ProjectStatusRoutes.CollectionOf(owner.SpaceId, projectId),
            JsonContent.Create(new { name }, options: JsonOptions),
            headers: null);

        Assert.True(added.StatusCode == HttpStatusCode.Created, $"Adding a Project Status through the product: {added.StatusCode} {added.Body}");

        return ProjectStatusWire.EntriesOf(added.Body).Single(entry => entry.Name == name).Id;
    }

    /// <summary>
    /// The refused request's target Space holds Statuses (the anchor) and none is named for its body.
    /// </summary>
    private static async Task NoStatusNamedForTheCaseInTheTargetSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        var names = await world.Database.StatusNamesInAsync(request.TargetSpaceId ?? throw new InvalidOperationException("The case names no target Space."));

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
