using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// A Space default removed at Space level, on both surfaces. The removal is reached from another Space (the boundary
/// <c>404</c>, recorded, nothing removed there); with another Space's default or a Project's added Status in the caller's
/// own Space path (the in-Space <c>404</c>, byte-identical to a random id); with another Space's default as the Space-wide
/// destination or as a blocked Project's own destination (the removal's own <c>400 invalid-status-destination</c>,
/// byte-identical to a random id). A Viewer is the Role refusal, a Token bound to an Account's other Space is refused as a
/// non-member's would be, and the occupancy report names the caller's Projects and nothing of another Space's same-named
/// Status.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defaults are removed only in fresh actors' Spaces, never in A's or B's</b>: <c>ISO-DIRECT-STATUSES</c>,
/// <c>ISO-AGGREGATE</c> and <c>ISO-RECORD-KIND</c> assert A's and B's three defaults exactly. The foreign Space the first row
/// reaches is the fresh one <see cref="ForeignProjectAsync"/> seeds, and every removal that succeeds runs in a Space of its
/// own.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private static IEnumerable<IsolationCase> SpaceDefaultStatusRemovalCases() =>
    [
        new IsolationCase(
            "ISO-REMOVE-SPACE-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, SpaceRoutes.StatusRemove,
            async world =>
            {
                var (foreignOwner, _) = await ForeignProjectAsync(world);
                var theirStatus = (await StatusIdsAsync(foreignOwner))[2];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(SpaceRoutes.StatusRemoveOf(foreignOwner.SpaceId, theirStatus), SpaceRoutes.StatusRemoveOf(Guid.NewGuid(), theirStatus))],
                    foreignOwner.SpaceId,
                    Body: () => SpaceStatusWire.RemoveBody(destinationStatusId: null),
                    Other: foreignOwner);
            },
            Both(new BoundaryNotFound { Then = TheOtherSpaceKeepsItsThreeDefaultsAsync })),

        // B's real default and a Project's added Status of the caller's own Space, each in the caller's own Space path: the
        // first is a row the StatusDefinition query filter must keep out, the second one the removal must refuse as not a
        // default, and each must answer what a random id answers.
        new IsolationCase(
            "ISO-REMOVE-FOREIGN-STATUS-IN-OWN-SPACE", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Post, SpaceRoutes.StatusRemove,
            world => NotADefaultInOwnSpaceAsync(world, "Dx", HttpMethod.Post, SpaceRoutes.StatusRemoveOf, () => SpaceStatusWire.RemoveBody(destinationStatusId: null)),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        // A destination is body-borne, so a foreign one is the removal's own 400, identical to a random destination's, never
        // the boundary 404 a path id gets. Unoccupied: a destination sent anyway is still validated.
        new IsolationCase(
            "ISO-REMOVE-SPACE-STATUS-FOREIGN-DESTINATION", "I-1, I-13, FR-27", IsolationVector.DirectIdentifier, HttpMethod.Post, SpaceRoutes.StatusRemove,
            async world =>
            {
                var actor = await world.NewActorAsync("Dd");
                var foreign = (await StatusIdsAsync(world.B))[1];
                var path = SpaceRoutes.StatusRemoveOf(actor.SpaceId, (await StatusIdsAsync(actor))[2]);

                return new CaseRequest(
                    actor,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => SpaceStatusWire.RemoveBody(foreign),
                    AnchorBody: () => SpaceStatusWire.RemoveBody(Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidStatusDestination))),

        new IsolationCase(
            "ISO-REMOVE-SPACE-STATUS-FOREIGN-EXCEPTION-DESTINATION", "I-1, I-13, FR-27", IsolationVector.DirectIdentifier, HttpMethod.Post, SpaceRoutes.StatusRemove,
            BlockedProjectGivenAForeignDestinationAsync,
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidStatusDestination))),

        new IsolationCase(
            "ISO-RECORD-KIND-SPACE-STATUS-REMOVE-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, SpaceRoutes.StatusRemove,
            world => ViewerOnTheDefaultsAsync(world, "Vx", HttpMethod.Post, SpaceRoutes.StatusRemoveOf, () => SpaceStatusWire.RemoveBody(destinationStatusId: null)),
            Both(new RoleRefused(ConfigureDefaultsCapabilityCode))),

        new IsolationCase(
            "ISO-SPACE-STATUS-REMOVAL-REPORT-AGGREGATE", "I-3, FR-27, UX-DR16", IsolationVector.Aggregate, HttpMethod.Post, SpaceRoutes.StatusRemove,
            OccupancyReportBesideAForeignSameNamedStatusAsync,
            Both(new Served(HttpStatusCode.Conflict, ReportsOnlyTheCallersOccupiedProjectAsync, "409 status-destination-required reporting the caller's one occupied Project and nothing of the other Space's"))),

        new(
            "ISO-TOKEN-OWN-OTHER-SPACE-SPACE-STATUS-REMOVE", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Post, SpaceRoutes.StatusRemove,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tx");
                var path = SpaceRoutes.StatusRemoveOf(second.SpaceId, (await StatusIdsAsync(second.Actor))[2]);

                return new CaseRequest(first.Actor, HttpMethod.Post, [new(path, path)], second.SpaceId, Body: () => SpaceStatusWire.RemoveBody(destinationStatusId: null), Other: second.Actor);
            },
            TokenOwnOtherSpaceExpectations(HttpStatusCode.OK)),
    ];

    /// <summary>
    /// A fresh Account whose Project removed the second default and holds a Task in the first; the request removes the first
    /// default with the second as the Space-wide destination, so that Project is blocked, and gives it B's default as its own
    /// destination - anchored on a random id as that destination.
    /// </summary>
    private static async Task<CaseRequest> BlockedProjectGivenAForeignDestinationAsync(IsolationWorld world)
    {
        var actor = await world.NewActorAsync("De");
        var (projectId, _) = await SeedProjectWithTaskAsync(actor);
        var statuses = await StatusIdsAsync(actor);
        var foreign = (await StatusIdsAsync(world.B))[2];

        await RemoveProjectStatusThroughTheProductAsync(actor, projectId, statuses[1]);

        var path = SpaceRoutes.StatusRemoveOf(actor.SpaceId, statuses[0]);

        return new CaseRequest(
            actor,
            HttpMethod.Post,
            [new(path, path)],
            TargetSpaceId: null,
            Body: () => SpaceStatusWire.RemoveBody(statuses[1], [(projectId, foreign)]),
            AnchorBody: () => SpaceStatusWire.RemoveBody(statuses[1], [(projectId, Guid.NewGuid())]));
    }

    /// <summary>
    /// Two fresh Accounts, each with a Project holding a Task in its first default - the same Space name in both. The caller
    /// removes its first default with no destination.
    /// </summary>
    private static async Task<CaseRequest> OccupancyReportBesideAForeignSameNamedStatusAsync(IsolationWorld world)
    {
        var own = await world.NewActorAsync("Dr");
        var other = await world.NewActorAsync("Ds");

        _ = await SeedProjectWithTaskAsync(own);
        _ = await SeedProjectWithTaskAsync(other);

        var path = SpaceRoutes.StatusRemoveOf(own.SpaceId, (await StatusIdsAsync(own))[0]);

        return new CaseRequest(own, HttpMethod.Post, [new(path, path)], TargetSpaceId: null, Body: () => SpaceStatusWire.RemoveBody(destinationStatusId: null), Other: other);
    }

    /// <summary>
    /// The refusal is the occupancy one; its report names exactly the caller's one Project; the other Space's Project, its
    /// name and its Space id appear nowhere in the body.
    /// </summary>
    private static async Task ReportsOnlyTheCallersOccupiedProjectAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no other Space.");
        var own = await world.Database.ProjectsInAsync(request.Actor.SpaceId);
        var theirs = await world.Database.ProjectsInAsync(other.SpaceId);

        Assert.Equal(ProblemTypes.StatusDestinationRequired, ProjectStatusWire.ProblemTypeOf(answer.Body));
        Assert.NotEmpty(theirs);
        Assert.Equal(own.Select(project => project.Id), SpaceStatusWire.RemovalOf(answer.Body).Select(entry => entry.ProjectId));
        Assert.All(theirs, project =>
        {
            Assert.DoesNotContain(project.Id.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(project.Name, answer.Body, StringComparison.Ordinal);
        });
        Assert.DoesNotContain(other.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The refused request's target Space still serves its three defaults through its own owner's read.
    /// </summary>
    private static async Task TheOtherSpaceKeepsItsThreeDefaultsAsync(IsolationWorld world, CaseRequest request)
    {
        var owner = request.Other ?? throw new InvalidOperationException("The case names no other Space.");

        Assert.Equal(3, (await StatusIdsAsync(owner)).Count);
    }
}
