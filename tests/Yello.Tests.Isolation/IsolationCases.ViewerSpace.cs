using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Labels;
using Yello.Contracts.Memberships;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// A Viewer in its own Space, joined through the product: every write route that had no Viewer row in the Viewer's own
/// Space is refused on both surfaces with the route's capability and changes nothing, and the two writes a Viewer may make -
/// leaving, and issuing itself a Token - are served.
/// </summary>
/// <remarks>
/// <para>
/// <b>Live, and green when written.</b> The pipeline already refuses each of these by the matrix; the rows close a coverage
/// gap (the earlier Viewer rows demote an Owner by raw SQL, or name a foreign Space), so they stand as regression gates.
/// </para>
/// <para>
/// <b>Seeding.</b> A fresh host owns its Personal Space; the Viewer registers, is invited at <c>viewer</c> through the
/// product, accepts, and issues itself a Token bound to the host's Space (<c>JoinedThroughTheProductAsync</c>). Each row's
/// Project or Label is created by the host through the product. No Membership, Space or Account is written by hand, so
/// <c>FK_Membership_Account_AccountId</c>, <c>AK_Membership_SpaceId_AccountId</c> and <c>UX_Membership_SpaceId_Owner</c> are
/// the product's to keep.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private static IEnumerable<IsolationCase> ViewerSpaceCases() =>
    [
        new IsolationCase(
            "ISO-PROJECT-RENAME-VIEWER", "V-1, C-6, NFR-7, FR-16", IsolationVector.RecordKind, HttpMethod.Patch, ProjectRoutes.Rename,
            async world =>
            {
                var (host, viewer) = await ViewerOfAFreshHostAsync(world, "Pr");
                var projectId = await SeedProjectAsync(host);

                return RefusedInTheHostsSpace(viewer, host, HttpMethod.Patch, ProjectRoutes.ProjectOf(host.SpaceId, projectId));
            },
            Both(new RoleRefused("manage-projects") { Then = NothingNamedForTheCaseInTheHostsProjectsAsync })),

        new IsolationCase(
            "ISO-LABEL-CREATE-VIEWER", "V-1, C-6, NFR-7, FR-16", IsolationVector.RecordKind, HttpMethod.Post, LabelRoutes.Create,
            async world =>
            {
                var (host, viewer) = await ViewerOfAFreshHostAsync(world, "Lc");

                _ = await SeedLabelAsync(host);

                return RefusedInTheHostsSpace(viewer, host, HttpMethod.Post, LabelRoutes.LabelsOf(host.SpaceId));
            },
            Both(new RoleRefused("manage-labels") { Then = TheHostsLabelsAreUnchangedAsync })),

        new IsolationCase(
            "ISO-LABEL-DELETE-VIEWER", "V-1, C-6, NFR-7, FR-16", IsolationVector.RecordKind, HttpMethod.Delete, LabelRoutes.Label,
            async world =>
            {
                var (host, viewer) = await ViewerOfAFreshHostAsync(world, "Ld");
                var labelId = await SeedLabelAsync(host);

                return RefusedInTheHostsSpace(viewer, host, HttpMethod.Delete, LabelRoutes.LabelOf(host.SpaceId, labelId));
            },
            Both(new RoleRefused("manage-labels") { Then = TheHostsLabelsAreUnchangedAsync })),

        new IsolationCase(
            "ISO-SPACE-RENAME-VIEWER", "V-1, C-6, NFR-7, FR-16", IsolationVector.RecordKind, HttpMethod.Patch, SpaceRoutes.Active,
            async world =>
            {
                var (host, viewer) = await ViewerOfAFreshHostAsync(world, "Sr");

                return RefusedInTheHostsSpace(viewer, host, HttpMethod.Patch, SpaceRoutes.ActiveOf(host.SpaceId));
            },
            Both(new RoleRefused("rename-space") { Then = TheHostsSpaceKeepsItsNameAsync })),

        new IsolationCase(
            "ISO-SPACE-DELETE-VIEWER", "V-1, C-6, NFR-7, FR-16", IsolationVector.RecordKind, HttpMethod.Delete, SpaceRoutes.Active,
            async world =>
            {
                var (host, viewer) = await ViewerOfAFreshHostAsync(world, "Sd");

                return RefusedInTheHostsSpace(viewer, host, HttpMethod.Delete, SpaceRoutes.ActiveOf(host.SpaceId));
            },
            Both(new RoleRefused("delete-space") { Then = TheHostsSpaceKeepsItsNameAsync })),

        // Leaving is decided by identity, not Role, so a Viewer leaves on either surface. By its Token, the Token dies with
        // the Membership in the same request.
        new IsolationCase(
            "ISO-LEAVE-VIEWER", "FR-14, FR-16", IsolationVector.Write, HttpMethod.Delete, MembershipRoutes.Own,
            async world =>
            {
                var (host, viewer) = await ViewerOfAFreshHostAsync(world, "Lv");
                var path = MembershipRoutes.OwnOf(host.SpaceId);

                return new CaseRequest(viewer, HttpMethod.Delete, [new(path, path)], host.SpaceId, Other: viewer);
            },
            Both(new Served(HttpStatusCode.NoContent, (_, _, _) => Task.CompletedTask, "204 - a Viewer may leave") { Then = TheOtherHoldsNoMembershipAsync })),

        // The matrix's last row: every Role issues itself a Token. Issuing is Session-only, so a Token is no credential there.
        new IsolationCase(
            "ISO-TOKEN-ISSUE-VIEWER", "FR-16, AR-14", IsolationVector.Write, HttpMethod.Post, ApiTokenRoutes.Issue,
            async world =>
            {
                var (host, viewer) = await ViewerOfAFreshHostAsync(world, "Ti");
                var path = ApiTokenRoutes.IssueIn(host.SpaceId);

                return new CaseRequest(viewer, HttpMethod.Post, [new(path, path)], host.SpaceId);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.Created, (_, _, _) => Task.CompletedTask, "201 - every Role issues itself a Token"),
                [IsolationSurface.ApiToken] = new NoCredential(),
            }),
    ];

    /// <summary>
    /// A fresh host and a fresh Account that joined the host's Space at Viewer through the product.
    /// </summary>
    private static async Task<(ProductActor Host, ProductActor Viewer)> ViewerOfAFreshHostAsync(IsolationWorld world, string label)
    {
        var host = await world.NewActorAsync(label + "h");

        return (host, await JoinedThroughTheProductAsync(world, host, label + "v", "viewer"));
    }

    private static CaseRequest RefusedInTheHostsSpace(ProductActor viewer, ProductActor host, HttpMethod method, string path) =>
        new(viewer, method, [new(path, path)], host.SpaceId, Other: host);

    /// <summary>
    /// The host's Space still holds its Project (the anchor: the seed landed), and none carries the refused request's name.
    /// </summary>
    private static async Task NothingNamedForTheCaseInTheHostsProjectsAsync(IsolationWorld world, CaseRequest request)
    {
        var host = request.Other ?? throw new InvalidOperationException("The case names no host.");
        var projects = await world.Database.ProjectsInAsync(host.SpaceId);

        Assert.NotEmpty(projects);
        Assert.DoesNotContain(projects, project => string.Equals(project.Name, CaseBodyName, StringComparison.Ordinal));
    }

    /// <summary>
    /// The host's Space still holds the one Label it seeded, and none carries the refused request's name.
    /// </summary>
    private static async Task TheHostsLabelsAreUnchangedAsync(IsolationWorld world, CaseRequest request)
    {
        var host = request.Other ?? throw new InvalidOperationException("The case names no host.");
        var labels = await world.Database.LabelsInAsync(host.SpaceId);

        Assert.Single(labels);
        Assert.DoesNotContain(labels, label => string.Equals(label.Name, CaseBodyName, StringComparison.Ordinal));
    }

    /// <summary>
    /// The host still reads its Space under the name the product gave it: neither renamed nor deleted.
    /// </summary>
    private static async Task TheHostsSpaceKeepsItsNameAsync(IsolationWorld world, CaseRequest request)
    {
        var host = request.Other ?? throw new InvalidOperationException("The case names no host.");
        var read = await host.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.ActiveOf(host.SpaceId));

        Assert.True(read.StatusCode == HttpStatusCode.OK, $"The host's Space after the refusal: {read.StatusCode} {read.Body}");

        using var document = JsonDocument.Parse(read.Body);

        Assert.Equal(host.SpaceName, document.RootElement.GetProperty("name").GetString());
    }
}
