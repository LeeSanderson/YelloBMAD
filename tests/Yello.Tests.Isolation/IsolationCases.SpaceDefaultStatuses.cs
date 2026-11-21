using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// The Space's default Statuses changed at Space level - add, rename, place - on both surfaces. Each write is reached from
/// another Space (the boundary <c>404</c>, recorded, nothing written there), with another Space's default or a Project's added
/// Status in the caller's own Space path (the in-Space <c>404</c>, byte-identical to a random id), and with another Space's
/// default as the body's anchor (the write's own <c>400 invalid-status-position</c>, byte-identical to a random anchor). A
/// Viewer is the Role refusal, a Token bound to an Account's other Space is refused as a non-member's would be, and a rename's
/// conflict report names the caller's Projects and nothing of another Space's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pending until the routes are mapped</b>; the develop phase makes these rows live before review (GATE-003) and swaps
/// <see cref="SpaceStatusWire"/>'s stand-ins (<see cref="SpaceStatusWire.Item"/>, <see cref="SpaceStatusWire.Place"/>,
/// <see cref="SpaceStatusWire.StatusRenameConflict"/>) for the contract members.
/// </para>
/// <para>
/// <b>Defaults are changed only in fresh actors' Spaces, never in A's or B's</b>: <c>ISO-DIRECT-STATUSES</c>,
/// <c>ISO-AGGREGATE</c> and <c>ISO-RECORD-KIND</c> assert A's and B's three defaults exactly, and the foreign Space these rows
/// reach is the fresh one <see cref="ForeignProjectAsync"/> seeds, never B's, which sits at its Projects bound.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private const string SpaceDefaultStatusPending = "The Space-level add, rename and place routes are not mapped until the dev phase of the story that defines the Space default Statuses.";

    private const string SpaceDefaultStatusStory = "6.3";

    private const string ConfigureDefaultsCapabilityCode = "configure-space-default-statuses";

    private const string OwnConflictName = "Renamed in the caller's Project";

    private const string ForeignConflictName = "Renamed in another Space's Project";

    private static IEnumerable<IsolationCase> SpaceDefaultStatusCases() =>
    [
        new IsolationCase(
            "ISO-ADD-SPACE-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, SpaceRoutes.Statuses,
            async world =>
            {
                var (foreignOwner, _) = await ForeignProjectAsync(world);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(SpaceRoutes.StatusesOf(foreignOwner.SpaceId), SpaceRoutes.StatusesOf(Guid.NewGuid()))],
                    foreignOwner.SpaceId,
                    Body: () => SpaceStatusWire.AddBody(CaseBodyName));
            },
            Both(new BoundaryNotFound { Then = NoStatusNamedForTheCaseInTheTargetSpaceAsync }),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        new IsolationCase(
            "ISO-RENAME-SPACE-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Patch, SpaceStatusWire.Item,
            async world =>
            {
                var (foreignOwner, _) = await ForeignProjectAsync(world);
                var theirStatus = (await StatusIdsAsync(foreignOwner))[0];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(SpaceStatusWire.ItemOf(foreignOwner.SpaceId, theirStatus), SpaceStatusWire.ItemOf(Guid.NewGuid(), theirStatus))],
                    foreignOwner.SpaceId,
                    Body: () => SpaceStatusWire.RenameBody(CaseBodyName));
            },
            Both(new BoundaryNotFound { Then = NoStatusNamedForTheCaseInTheTargetSpaceAsync }),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        new IsolationCase(
            "ISO-PLACE-SPACE-STATUS-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Post, SpaceStatusWire.Place,
            async world =>
            {
                var (foreignOwner, _) = await ForeignProjectAsync(world);
                var theirStatus = (await StatusIdsAsync(foreignOwner))[2];

                return new CaseRequest(
                    world.A,
                    HttpMethod.Post,
                    [new(SpaceStatusWire.PlaceOf(foreignOwner.SpaceId, theirStatus), SpaceStatusWire.PlaceOf(Guid.NewGuid(), theirStatus))],
                    foreignOwner.SpaceId,
                    Body: () => SpaceStatusWire.PlaceBody(afterStatusId: null));
            },
            Both(new BoundaryNotFound()),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        // B's real default and a Project's added Status of the caller's own Space, each in the caller's own Space path: the
        // first is a row the StatusDefinition query filter must keep out, the second one the write must refuse as not a
        // default, and each must answer what a random id answers.
        new IsolationCase(
            "ISO-RENAME-FOREIGN-STATUS-IN-OWN-SPACE", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Patch, SpaceStatusWire.Item,
            world => NotADefaultInOwnSpaceAsync(world, "Dr", HttpMethod.Patch, SpaceStatusWire.ItemOf, () => SpaceStatusWire.RenameBody(CaseBodyName)),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = NoStatusNamedForTheCaseInTheActorsSpaceAsync }),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        new IsolationCase(
            "ISO-PLACE-FOREIGN-STATUS-IN-OWN-SPACE", "I-4, I-5, FR-20", IsolationVector.DeepLink, HttpMethod.Post, SpaceStatusWire.Place,
            world => NotADefaultInOwnSpaceAsync(world, "Dp", HttpMethod.Post, SpaceStatusWire.PlaceOf, () => SpaceStatusWire.PlaceBody(afterStatusId: null)),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable)),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        // An anchor is body-borne, so a foreign one is the write's own 400, identical to a random anchor's, never the
        // boundary 404 a path id gets.
        new IsolationCase(
            "ISO-PLACE-SPACE-STATUS-FOREIGN-ANCHOR", "I-1, I-13, FR-27", IsolationVector.DirectIdentifier, HttpMethod.Post, SpaceStatusWire.Place,
            async world =>
            {
                var actor = await world.NewActorAsync("Da");
                var foreign = (await StatusIdsAsync(world.B))[0];
                var path = SpaceStatusWire.PlaceOf(actor.SpaceId, (await StatusIdsAsync(actor))[1]);

                return new CaseRequest(
                    actor,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => SpaceStatusWire.PlaceBody(foreign),
                    AnchorBody: () => SpaceStatusWire.PlaceBody(Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidStatusPosition)),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        new IsolationCase(
            "ISO-ADD-SPACE-STATUS-FOREIGN-ANCHOR", "I-1, I-13, FR-24", IsolationVector.DirectIdentifier, HttpMethod.Post, SpaceRoutes.Statuses,
            async world =>
            {
                var actor = await world.NewActorAsync("Dn");
                var foreign = (await StatusIdsAsync(world.B))[0];
                var path = SpaceRoutes.StatusesOf(actor.SpaceId);

                return new CaseRequest(
                    actor,
                    HttpMethod.Post,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => SpaceStatusWire.AddBody(CaseBodyName, foreign),
                    AnchorBody: () => SpaceStatusWire.AddBody(CaseBodyName, Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidStatusPosition) { Then = NoStatusNamedForTheCaseInTheActorsSpaceAsync }),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        new IsolationCase(
            "ISO-RECORD-KIND-SPACE-STATUS-ADD-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, SpaceRoutes.Statuses,
            world => ViewerOnTheDefaultsAsync(world, "Va", HttpMethod.Post, (spaceId, _) => SpaceRoutes.StatusesOf(spaceId), () => SpaceStatusWire.AddBody(CaseBodyName)),
            Both(new RoleRefused(ConfigureDefaultsCapabilityCode) { Then = NoStatusNamedForTheCaseInTheActorsSpaceAsync }),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        new IsolationCase(
            "ISO-RECORD-KIND-SPACE-STATUS-RENAME-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Patch, SpaceStatusWire.Item,
            world => ViewerOnTheDefaultsAsync(world, "Vn", HttpMethod.Patch, SpaceStatusWire.ItemOf, () => SpaceStatusWire.RenameBody(CaseBodyName)),
            Both(new RoleRefused(ConfigureDefaultsCapabilityCode) { Then = NoStatusNamedForTheCaseInTheActorsSpaceAsync }),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        new IsolationCase(
            "ISO-RECORD-KIND-SPACE-STATUS-PLACE-VIEWER", "V-1, C-6, FR-16", IsolationVector.RecordKind, HttpMethod.Post, SpaceStatusWire.Place,
            world => ViewerOnTheDefaultsAsync(world, "Vp", HttpMethod.Post, SpaceStatusWire.PlaceOf, () => SpaceStatusWire.PlaceBody(afterStatusId: null)),
            Both(new RoleRefused(ConfigureDefaultsCapabilityCode)),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        new IsolationCase(
            "ISO-SPACE-STATUS-RENAME-CONFLICT-AGGREGATE", "I-3, FR-27, UX-DR16", IsolationVector.Aggregate, HttpMethod.Patch, SpaceStatusWire.Item,
            ConflictReportBesideAForeignRenameAsync,
            Both(new Served(HttpStatusCode.Conflict, ReportsOnlyTheCallersProjectAsync, "409 status-rename-conflict naming the caller's one Project and nothing of the other Space's same-named rename")),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending),

        TokenOnTheDefaultsElsewhere("SPACE-STATUS-ADD", HttpMethod.Post, SpaceRoutes.Statuses, HttpStatusCode.Created, (second, _) => SpaceRoutes.StatusesOf(second.SpaceId), () => SpaceStatusWire.AddBody(CaseBodyName)),
        TokenOnTheDefaultsElsewhere("SPACE-STATUS-RENAME", HttpMethod.Patch, SpaceStatusWire.Item, HttpStatusCode.OK, (second, status) => SpaceStatusWire.ItemOf(second.SpaceId, status), () => SpaceStatusWire.RenameBody(CaseBodyName)),
        TokenOnTheDefaultsElsewhere("SPACE-STATUS-PLACE", HttpMethod.Post, SpaceStatusWire.Place, HttpStatusCode.OK, (second, status) => SpaceStatusWire.PlaceOf(second.SpaceId, status), () => SpaceStatusWire.PlaceBody(afterStatusId: null)),
    ];

    /// <summary>
    /// A fresh Account whose Project added a Status; the request names, in its own Space path, B's real default and then
    /// that added Status, each anchored on a random Status id.
    /// </summary>
    private static async Task<CaseRequest> NotADefaultInOwnSpaceAsync(IsolationWorld world, string label, HttpMethod method, Func<Guid, Guid, string> route, Func<HttpContent> body)
    {
        var actor = await world.NewActorAsync(label);
        var own = await SeedProjectAsync(actor);
        var added = await AddProjectStatusThroughTheProductAsync(actor, own, "Added here only");
        var theirs = (await StatusIdsAsync(world.B))[1];

        return new CaseRequest(
            actor,
            method,
            [
                new(route(actor.SpaceId, theirs), route(actor.SpaceId, Guid.NewGuid())),
                new(route(actor.SpaceId, added), route(actor.SpaceId, Guid.NewGuid())),
            ],
            TargetSpaceId: null,
            Body: body);
    }

    /// <summary>
    /// A fresh Account demoted to Viewer in its own Personal Space; the request writes one of its defaults (the second, for a
    /// path that names one).
    /// </summary>
    private static async Task<CaseRequest> ViewerOnTheDefaultsAsync(IsolationWorld world, string label, HttpMethod method, Func<Guid, Guid, string> route, Func<HttpContent> body)
    {
        var viewer = await world.NewActorAsync(label);
        var second = (await StatusIdsAsync(viewer))[1];

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = route(viewer.SpaceId, second);

        return new CaseRequest(viewer, method, [new(path, path)], viewer.SpaceId, Body: body);
    }

    /// <summary>
    /// Two fresh Accounts whose first defaults share a Space name: the caller's Project renamed its own, and the other
    /// Account's Project renamed its own. The caller then renames its first default with no decision.
    /// </summary>
    private static async Task<CaseRequest> ConflictReportBesideAForeignRenameAsync(IsolationWorld world)
    {
        var own = await world.NewActorAsync("Dc");
        var other = await world.NewActorAsync("Do");
        var ownProject = await SeedProjectAsync(own);
        var otherProject = await SeedProjectAsync(other);
        var ownFirst = (await StatusIdsAsync(own))[0];

        await RenameProjectStatusThroughTheProductAsync(own, ownProject, ownFirst, OwnConflictName);
        await RenameProjectStatusThroughTheProductAsync(other, otherProject, (await StatusIdsAsync(other))[0], ForeignConflictName);

        var path = SpaceStatusWire.ItemOf(own.SpaceId, ownFirst);

        return new CaseRequest(own, HttpMethod.Patch, [new(path, path)], TargetSpaceId: null, Body: () => SpaceStatusWire.RenameBody(CaseBodyName), Other: other);
    }

    /// <summary>
    /// The refusal is the conflict, its report names exactly one Project under the caller's own name for the Status, and the
    /// other Space's rename and its Space id appear nowhere in the body.
    /// </summary>
    private static Task ReportsOnlyTheCallersProjectAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no other Space.");

        Assert.Equal(SpaceStatusWire.StatusRenameConflict, ProjectStatusWire.ProblemTypeOf(answer.Body));
        Assert.Equal([OwnConflictName], SpaceStatusWire.ConflictsOf(answer.Body).Select(conflict => conflict.Name));
        Assert.DoesNotContain(ForeignConflictName, answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(other.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);

        return Task.CompletedTask;
    }

    /// <summary>
    /// A Token bound to an Account's first Space, used on the defaults of its second, which the same Account owns: refused
    /// exactly as a non-member would be, while the Session, which carries no Space, is served.
    /// </summary>
    private static IsolationCase TokenOnTheDefaultsElsewhere(string suffix, HttpMethod method, string route, HttpStatusCode sessionStatus, Func<SeededSpace, Guid, string> pathOf, Func<HttpContent> body) =>
        new(
            $"ISO-TOKEN-OWN-OTHER-SPACE-{suffix}", "I-12", IsolationVector.TokenOwnOtherSpace, method, route,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Td");
                var path = pathOf(second, (await StatusIdsAsync(second.Actor))[2]);

                return new CaseRequest(first.Actor, method, [new(path, path)], second.SpaceId, Body: body, Other: second.Actor);
            },
            TokenOwnOtherSpaceExpectations(sessionStatus),
            PendingUntil: SpaceDefaultStatusStory,
            PendingReason: SpaceDefaultStatusPending);

    /// <summary>
    /// Renames one Status of <paramref name="owner"/>'s Project through the product.
    /// </summary>
    private static async Task RenameProjectStatusThroughTheProductAsync(ProductActor owner, Guid projectId, Guid statusId, string name)
    {
        var renamed = await owner.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Patch,
            ProjectStatusRoutes.ItemOf(owner.SpaceId, projectId, statusId),
            ProjectStatusWire.NameBody(name),
            headers: null);

        Assert.True(renamed.StatusCode == HttpStatusCode.OK, $"Renaming a Project Status through the product: {renamed.StatusCode} {renamed.Body}");
    }
}
