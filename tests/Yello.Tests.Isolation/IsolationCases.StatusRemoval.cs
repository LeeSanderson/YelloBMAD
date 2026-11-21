using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Removing a Status from a Project, and adding a removed default back, on both surfaces. Each route is reached from another
/// Space (the boundary <c>404</c>, recorded, nothing written there), with another Space's Project in the caller's own Space
/// path, and with another Space's or a sibling Project's Status in the caller's own Project path (the in-Space <c>404</c>,
/// byte-identical to a random id). A destination in the body that is another Space's or a sibling Project's Status is the
/// removal's own <c>400 invalid-status-destination</c>, byte-identical to a random destination. A Viewer is the Role
/// refusal, and a Token bound to an Account's other Space is refused as a non-member's would be.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pending until the routes are mapped</b>; the develop phase makes these rows live before review (GATE-003) and swaps
/// <see cref="ProjectStatusWire"/>'s stand-ins (<see cref="ProjectStatusWire.Remove"/>, <see cref="ProjectStatusWire.Restore"/>,
/// <see cref="ProjectStatusWire.InvalidStatusDestination"/>) for the contract members.
/// </para>
/// <para>
/// <b>Statuses are removed only in fresh actors' Spaces, never in A's or B's</b>, for the reason the Project Status rows give:
/// A's and B's Spaces must keep exactly their three defaults for every other row.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private const string StatusRemovalPending = "The remove and restore routes are not mapped until the dev phase of the story that removes a Status from a Project.";

    private const string StatusRemovalStory = "6.2";

    private static IEnumerable<IsolationCase> StatusRemovalCases() =>
    [
        new IsolationCase(
            "ISO-REMOVE-PROJECT-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, ProjectStatusWire.Remove,
            async world =>
            {
                var (foreignOwner, foreign) = await ForeignProjectAsync(world);
                var theirStatus = (await StatusIdsAsync(foreignOwner))[2];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(ProjectStatusWire.RemoveOf(foreignOwner.SpaceId, foreign, theirStatus), ProjectStatusWire.RemoveOf(Guid.NewGuid(), foreign, theirStatus))],
                    foreignOwner.SpaceId,
                    Body: () => ProjectStatusWire.RemoveBody(destinationStatusId: null));
            },
            Both(new BoundaryNotFound { Then = NoDeltaOperationInTheTargetSpaceAsync }),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        new IsolationCase(
            "ISO-RESTORE-PROJECT-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, ProjectStatusWire.Restore,
            async world =>
            {
                var (foreignOwner, foreign) = await ForeignProjectAsync(world);
                var theirStatus = (await StatusIdsAsync(foreignOwner))[1];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(ProjectStatusWire.RestoreOf(foreignOwner.SpaceId, foreign, theirStatus), ProjectStatusWire.RestoreOf(Guid.NewGuid(), foreign, theirStatus))],
                    foreignOwner.SpaceId);
            },
            Both(new BoundaryNotFound { Then = NoDeltaOperationInTheTargetSpaceAsync }),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        new IsolationCase(
            "ISO-REMOVE-PROJECT-STATUS-FOREIGN-PROJECT-IN-OWN-SPACE", "I-4, I-5", IsolationVector.DeepLink, HttpMethod.Post, ProjectStatusWire.Remove,
            async world =>
            {
                var (_, foreign) = await ForeignProjectAsync(world);
                var ownStatus = (await StatusIdsAsync(world.A))[2];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(ProjectStatusWire.RemoveOf(world.A.SpaceId, foreign, ownStatus), ProjectStatusWire.RemoveOf(world.A.SpaceId, Guid.NewGuid(), ownStatus))],
                    TargetSpaceId: null,
                    Body: () => ProjectStatusWire.RemoveBody(destinationStatusId: null));
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable)),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        new IsolationCase(
            "ISO-REMOVE-FOREIGN-STATUS-IN-OWN-PROJECT", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Post, ProjectStatusWire.Remove,
            world => ForeignStatusesInOwnProjectAsync(world, "Rm", HttpMethod.Post, ProjectStatusWire.RemoveOf, () => ProjectStatusWire.RemoveBody(destinationStatusId: null)),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable)),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        new IsolationCase(
            "ISO-RESTORE-FOREIGN-STATUS-IN-OWN-PROJECT", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Post, ProjectStatusWire.Restore,
            world => ForeignStatusesInOwnProjectAsync(world, "Rs", HttpMethod.Post, ProjectStatusWire.RestoreOf, body: null),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable)),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        // A destination is body-borne, so a foreign one is the removal's own 400, identical to a random destination's, never
        // the boundary 404 a path id gets.
        new IsolationCase(
            "ISO-REMOVE-FOREIGN-DESTINATION-IN-OWN-PROJECT", "I-1, I-13, FR-26", IsolationVector.DirectIdentifier, HttpMethod.Post, ProjectStatusWire.Remove,
            async world => await ForeignDestinationAsync(await world.NewActorAsync("Rd"), (await StatusIdsAsync(world.B))[1]),
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProjectStatusWire.InvalidStatusDestination)),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        new IsolationCase(
            "ISO-REMOVE-SIBLING-DESTINATION-IN-OWN-PROJECT", "I-13, FR-20, FR-26", IsolationVector.DirectIdentifier, HttpMethod.Post, ProjectStatusWire.Remove,
            async world =>
            {
                var actor = await world.NewActorAsync("Rn");
                var sibling = await SeedProjectAsync(actor);

                return await ForeignDestinationAsync(actor, await AddProjectStatusThroughTheProductAsync(actor, sibling, "Sibling destination"));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProjectStatusWire.InvalidStatusDestination)),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        new IsolationCase(
            "ISO-RECORD-KIND-PROJECT-STATUS-REMOVE-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, ProjectStatusWire.Remove,
            world => ViewerOnAProjectStatusAsync(world, "Vr", restore: false),
            Both(new RoleRefused(ConfigureCapabilityCode)),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        new IsolationCase(
            "ISO-RECORD-KIND-PROJECT-STATUS-RESTORE-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, ProjectStatusWire.Restore,
            world => ViewerOnAProjectStatusAsync(world, "Vb", restore: true),
            Both(new RoleRefused(ConfigureCapabilityCode)),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending),

        TokenOnARemovalElsewhere("PROJECT-STATUS-REMOVE", ProjectStatusWire.Remove, restore: false),
        TokenOnARemovalElsewhere("PROJECT-STATUS-RESTORE", ProjectStatusWire.Restore, restore: true),
    ];

    /// <summary>
    /// <paramref name="actor"/>'s own Project, whose third default it asks to remove to <paramref name="destination"/>,
    /// anchored on the same removal to a random destination.
    /// </summary>
    private static async Task<CaseRequest> ForeignDestinationAsync(ProductActor actor, Guid destination)
    {
        var own = await SeedProjectAsync(actor);
        var path = ProjectStatusWire.RemoveOf(actor.SpaceId, own, (await StatusIdsAsync(actor))[2]);

        return new CaseRequest(
            actor,
            HttpMethod.Post,
            [new(path, path)],
            TargetSpaceId: null,
            Body: () => ProjectStatusWire.RemoveBody(destination),
            AnchorBody: () => ProjectStatusWire.RemoveBody(Guid.NewGuid()));
    }

    /// <summary>
    /// A fresh Account with a Project in its own Personal Space - its third default removed first when the request is the
    /// add-back - then demoted to Viewer there; the request removes the second default, or adds the third back.
    /// </summary>
    private static async Task<CaseRequest> ViewerOnAProjectStatusAsync(IsolationWorld world, string label, bool restore)
    {
        var viewer = await world.NewActorAsync(label);
        var projectId = await SeedProjectAsync(viewer);
        var statuses = await StatusIdsAsync(viewer);

        if (restore)
        {
            await RemoveProjectStatusThroughTheProductAsync(viewer, projectId, statuses[2]);
        }

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = restore
            ? ProjectStatusWire.RestoreOf(viewer.SpaceId, projectId, statuses[2])
            : ProjectStatusWire.RemoveOf(viewer.SpaceId, projectId, statuses[1]);

        return new CaseRequest(viewer, HttpMethod.Post, [new(path, path)], viewer.SpaceId, Body: restore ? null : () => ProjectStatusWire.RemoveBody(destinationStatusId: null));
    }

    /// <summary>
    /// A Token bound to an Account's first Space, used to remove (or add back) a Status of a Project in its second, which the
    /// same Account owns: refused exactly as a non-member would be, while the Session, which carries no Space, is served.
    /// </summary>
    private static IsolationCase TokenOnARemovalElsewhere(string suffix, string route, bool restore) =>
        new(
            $"ISO-TOKEN-OWN-OTHER-SPACE-{suffix}", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Post, route,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync(restore ? "Tb" : "Tr");
                var statuses = await StatusIdsAsync(second.Actor);

                if (restore)
                {
                    await RemoveProjectStatusThroughTheProductAsync(second.Actor, second.ProjectId, statuses[2]);
                }

                var path = restore
                    ? ProjectStatusWire.RestoreOf(second.SpaceId, second.ProjectId, statuses[2])
                    : ProjectStatusWire.RemoveOf(second.SpaceId, second.ProjectId, statuses[2]);

                return new CaseRequest(
                    first.Actor,
                    HttpMethod.Post,
                    [new(path, path)],
                    second.SpaceId,
                    Body: restore ? null : () => ProjectStatusWire.RemoveBody(statuses[0]),
                    Other: second.Actor);
            },
            TokenOwnOtherSpaceExpectations(HttpStatusCode.OK),
            PendingUntil: StatusRemovalStory,
            PendingReason: StatusRemovalPending);

    /// <summary>
    /// Removes one Status of <paramref name="owner"/>'s Project through the product, with no destination.
    /// </summary>
    private static async Task RemoveProjectStatusThroughTheProductAsync(ProductActor owner, Guid projectId, Guid statusId)
    {
        var removed = await owner.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Post,
            ProjectStatusWire.RemoveOf(owner.SpaceId, projectId, statusId),
            ProjectStatusWire.RemoveBody(destinationStatusId: null),
            headers: null);

        Assert.True(removed.StatusCode == HttpStatusCode.OK, $"Removing a Project Status through the product: {removed.StatusCode} {removed.Body}");
    }

    /// <summary>
    /// The refused request's target Space holds Statuses (the anchor) and no Status delta operation at all.
    /// </summary>
    private static async Task NoDeltaOperationInTheTargetSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        var target = request.TargetSpaceId ?? throw new InvalidOperationException("The case names no target Space.");

        Assert.NotEmpty(await world.Database.StatusNamesInAsync(target));
        Assert.Equal(0, await world.Database.DeltaOperationCountInAsync(target));
    }
}
