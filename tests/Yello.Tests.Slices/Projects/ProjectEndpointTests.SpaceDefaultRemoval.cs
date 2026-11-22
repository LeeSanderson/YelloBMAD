using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Contracts.Lists;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;
using ListWire = Yello.Tests.Slices.Lists.ListWire;
using MoveWire = Yello.Tests.Slices.Tasks.MoveWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using SliceListQuery = Yello.Tests.Slices.Lists.ListQuery;
using Space = Yello.Tests.Slices.Projects.SpaceDefaultStatusWire;
using StatusRow = Yello.Tests.Slices.Tasks.StatusRow;
using TaskEditWire = Yello.Tests.Slices.Tasks.TaskEditWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// A Space default removed at Space level, across every Project of the Space: one Space-wide destination where a Project can
/// take it, a destination of its own for each Project that cannot, no fallback and no silent placement, a set never left
/// empty at either level, and one transaction that migrates every Project's Tasks, re-anchors every dependent place, deletes
/// every operation naming the Status and then the default's own row.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds.</b> Every case is skipped with the AC it waits on. The route, the problem type and the report's
/// member are <see cref="SpaceStatusWire"/>'s stand-ins (<c>Remove</c>, <c>RemoveOf</c>,
/// <c>StatusProjectDestinationRequired</c>, <c>RemovalOf</c>) until the contract declares them; the develop phase swaps each
/// for the contract member.
/// </para>
/// <para>
/// <b>The shared seed</b> (<see cref="SeedRemovalAsync"/>) is written by the product alone, so every composite key the
/// removal touches holds by construction: <c>FK_Task_StatusDefinition</c> on <c>(SpaceId, StatusId)</c>,
/// <c>FK_StatusDeltaOperation_StatusDefinition_SpaceId_StatusId</c> and its <c>AfterStatusId</c> twin (both <c>NO ACTION</c>,
/// which is why the definition goes last), <c>FK_StatusDeltaOperation_Project_SpaceId_ProjectId</c>, the unique
/// <c>(SpaceId, ProjectId, Sequence)</c> index and <c>IX_StatusDeltaOperation_SpaceId_ProjectId_StatusId_Kind</c>.
/// </para>
/// <para>
/// <b>Every case ends with the Status invariant</b>: no Task of the Space holds a Status outside its own Project's set.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private const string RedRoute = "the Space-level removal route is not mapped yet";

    /// <summary>
    /// Removing the unoccupied third default with no destination answers <c>200</c> with the two defaults left; a plain
    /// Project and one that renamed and placed the third both lose it from their set and their Board; no operation names it as
    /// its Status or its anchor; and its definition is gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC7 - " + RedRoute + " (Q4: an unoccupied removal needs no destination)")]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-16")]
    public async Task An_unoccupied_default_is_removed_with_no_destination_from_the_defaults_and_every_Projects_set()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var plain = Guid.Empty;
        var shaped = Guid.Empty;
        WireResponse? removed = null;
        var sets = new Dictionary<Guid, IReadOnlyList<Guid>>();
        var columns = new Dictionary<Guid, IReadOnlyList<Guid>>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            plain = IdOf(await CreateAsync(owner, "Plain"));
            shaped = IdOf(await CreateAsync(owner, "Shaped"));
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, shaped, defaults[2].Id, "Shipped")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, shaped, defaults[2].Id, afterStatusId: null)).StatusCode);

            removed = await Space.RemoveAsync(actor, owner.SpaceId, defaults[2].Id, destinationStatusId: null);

            foreach (var projectId in new[] { plain, shaped })
            {
                sets[projectId] = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, projectId)).Select(entry => entry.Id)];
                columns[projectId] = [.. (await Wire.BoardColumnsAsync(actor, owner.SpaceId, projectId)).Select(column => column.StatusId)];
            }
        });

        Assert.NotNull(owner);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");
        Assert.Equal([defaults[0].Id, defaults[1].Id], SpaceStatusWire.DefaultsOf(removed!.Body).Select(row => row.Id));
        Assert.All(sets.Values, set => Assert.Equal([defaults[0].Id, defaults[1].Id], set));
        Assert.All(columns.Values, set => Assert.Equal([defaults[0].Id, defaults[1].Id], set));
        Assert.DoesNotContain(await Space.SpaceDeltaRowsAsync(database, owner.SpaceId), row => row.StatusId == defaults[2].Id || row.AfterStatusId == defaults[2].Id);
        Assert.DoesNotContain(await Wire.DefinitionsAsync(database, owner.SpaceId), row => row.Id == defaults[2].Id);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Removing the occupied first default with no destination is refused <c>409 status-destination-required</c> carrying
    /// the occupancy report: every Project holding it with Tasks in it - the plain one, the one that cannot take the second
    /// default, the one that renamed it - each with its count and its own set after the removal, an added Status included;
    /// never the Project that already removed it. Nothing is written anywhere.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC2 - " + RedRoute + " (Q2: the removal itself reports occupancy in its 409)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "UX-DR16")]
    public async Task An_occupied_default_removed_without_a_destination_is_refused_409_with_the_occupancy_report()
    {
        var database = Available();
        Actor? owner = null;
        SpaceRemovalSeed? seed = null;
        WireResponse? refused = null;
        SpaceSnapshot? before = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            seed = await SeedRemovalAsync(owner, database);
            before = await SnapshotAsync(database, owner.SpaceId);
            refused = await Space.RemoveAsync(AsTaskActor(owner), owner.SpaceId, seed.Defaults[0].Id, destinationStatusId: null);
        });

        Assert.NotNull(owner);
        Assert.NotNull(seed);
        RemovalWire.AssertRefused(refused, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);

        var report = SpaceStatusWire.RemovalOf(refused!.Body).Select(Shown).Order(StringComparer.Ordinal);
        var d = seed.Defaults;

        Assert.Equal(
            new[]
            {
                Shown(new SpaceStatusOccupancy(seed.Plain, "Plain", 2, [new(d[1].Id, d[1].Name), new(d[2].Id, d[2].Name)])),
                Shown(new SpaceStatusOccupancy(seed.Blocked, "Blocked", 1, [new(d[2].Id, d[2].Name), new(seed.Parked, "Parked")])),
                Shown(new SpaceStatusOccupancy(seed.Renamed, "Renamed", 1, [new(d[1].Id, d[1].Name), new(d[2].Id, d[2].Name)])),
            }.Order(StringComparer.Ordinal),
            report);
        Assert.DoesNotContain(seed.RemovedIt.ToString("D"), refused.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await SnapshotAsync(database, owner.SpaceId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A destination every affected Project holds: each Project's Tasks move to the end of the second default's column in
    /// their source order, after the Task already there, whose key is unchanged; each moved Task keeps everything but its Status
    /// and key; the Project that renamed the first default is migrated like any other; the defaults lose it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC7 - " + RedRoute)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AD-17")]
    public async Task A_destination_every_Project_accepts_moves_each_Projects_Tasks_to_the_end_of_it_in_source_order()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var plain = Guid.Empty;
        var renamed = Guid.Empty;
        var resident = Guid.Empty;
        var residentKey = string.Empty;
        IReadOnlyList<Guid> sources = [];
        var viewsBefore = new Dictionary<Guid, IReadOnlyList<string>>();
        var viewsAfter = new Dictionary<Guid, IReadOnlyList<string>>();
        WireResponse? removed = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            plain = IdOf(await CreateAsync(owner, "Plain"));
            renamed = IdOf(await CreateAsync(owner, "Renamed"));
            resident = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, plain, "Already there"));
            Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(actor, owner.SpaceId, resident, defaults[1].Id, afterTaskId: null)).StatusCode);
            residentKey = await MoveWire.KeyOfAsync(database, owner.SpaceId, resident);

            foreach (var title in SourceTitles)
            {
                _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, plain, title));
            }

            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, renamed, "Renamed's own"));
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, renamed, defaults[0].Id, "Intake")).StatusCode);

            sources = await MoveWire.ColumnAsync(database, owner.SpaceId, plain, defaults[0].Id);

            foreach (var id in sources)
            {
                viewsBefore[id] = WithoutPlacement((await TaskEditWire.ViewTaskAsync(actor, owner.SpaceId, id)).Body);
            }

            removed = await Space.RemoveAsync(actor, owner.SpaceId, defaults[0].Id, defaults[1].Id);

            foreach (var id in sources)
            {
                viewsAfter[id] = WithoutPlacement((await TaskEditWire.ViewTaskAsync(actor, owner.SpaceId, id)).Body);
            }
        });

        Assert.NotNull(owner);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");
        Assert.Equal([defaults[1].Id, defaults[2].Id], SpaceStatusWire.DefaultsOf(removed!.Body).Select(row => row.Id));
        Assert.Equal(3, sources.Count);
        Assert.Equal([resident, .. sources], await MoveWire.ColumnAsync(database, owner.SpaceId, plain, defaults[1].Id));
        Assert.Equal(residentKey, await MoveWire.KeyOfAsync(database, owner.SpaceId, resident));
        Assert.All(sources, id => Assert.Equal(viewsBefore[id], viewsAfter[id]));
        Assert.All(await TaskWire.TaskRowsAsync(database, owner.SpaceId), row => Assert.Equal(defaults[1].Id, row.StatusId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The Space-wide destination is the second default, which the "Blocked" Project removed: with no exception for it the
    /// removal is refused <c>409</c> naming exactly that Project, with its count and its own set after the removal, and nothing
    /// is written anywhere - no Project falls back to its first Status.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AC4 - " + RedRoute + " (Q2: status-project-destination-required is a stand-in type)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "UX-DR16")]
    public async Task A_blocked_Project_without_an_exception_is_refused_409_naming_it_and_nothing_is_written()
    {
        var database = Available();
        Actor? owner = null;
        SpaceRemovalSeed? seed = null;
        WireResponse? refused = null;
        SpaceSnapshot? before = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            seed = await SeedRemovalAsync(owner, database);
            before = await SnapshotAsync(database, owner.SpaceId);
            refused = await Space.RemoveAsync(AsTaskActor(owner), owner.SpaceId, seed.Defaults[0].Id, seed.Defaults[1].Id);
        });

        Assert.NotNull(owner);
        Assert.NotNull(seed);
        RemovalWire.AssertRefused(refused, HttpStatusCode.Conflict, SpaceStatusWire.StatusProjectDestinationRequired);
        Assert.Equal(
            [Shown(new SpaceStatusOccupancy(seed.Blocked, "Blocked", 1, [new(seed.Defaults[2].Id, seed.Defaults[2].Name), new(seed.Parked, "Parked")]))],
            SpaceStatusWire.RemovalOf(refused!.Body).Select(Shown));
        Assert.Equal(before, await SnapshotAsync(database, owner.SpaceId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With an exception for the blocked Project - a default it still holds, or a Status it added - the removal answers
    /// <c>200</c>: the blocked Project's Task holds its own destination, every other affected Project's Tasks hold the
    /// Space-wide one, and the Project that already removed the default keeps its set as it was.
    /// </summary>
    /// <param name="toAddedStatus">Whether the exception is the Status the blocked Project added rather than a default.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC3, AC7, AC8 - " + RedRoute)]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-17")]
    public async Task A_blocked_Project_with_an_exception_takes_its_own_destination_and_the_rest_take_the_Space_wide_one(bool toAddedStatus)
    {
        var database = Available();
        Actor? owner = null;
        SpaceRemovalSeed? seed = null;
        var exception = Guid.Empty;
        WireResponse? removed = null;
        IReadOnlyList<string> removedItBefore = [];
        IReadOnlyList<string> removedItAfter = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            seed = await SeedRemovalAsync(owner, database);
            exception = toAddedStatus ? seed.Parked : seed.Defaults[2].Id;
            removedItBefore = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, seed.RemovedIt)).Select(Shown)];
            removed = await Space.RemoveAsync(actor, owner.SpaceId, seed.Defaults[0].Id, seed.Defaults[1].Id, [(seed.Blocked, exception)]);
            removedItAfter = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, seed.RemovedIt)).Select(Shown)];
        });

        Assert.NotNull(owner);
        Assert.NotNull(seed);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");

        var rows = await TaskWire.TaskRowsAsync(database, owner.SpaceId);

        Assert.All(rows.Where(row => row.ProjectId == seed.Blocked), row => Assert.Equal(exception, row.StatusId));
        Assert.All(rows.Where(row => row.ProjectId == seed.Plain || row.ProjectId == seed.Renamed), row => Assert.Equal(seed.Defaults[1].Id, row.StatusId));
        Assert.Equal(4, rows.Count(row => row.ProjectId != seed.RemovedIt));
        Assert.Equal(removedItBefore, removedItAfter);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// An exception destination outside the blocked Project's set after the removal - the Space-wide destination it removed,
    /// the Status being removed, another Space's default, a random id - is refused <c>400 invalid-status-destination</c>
    /// naming that Project, the foreign one byte-identical to the random one; a list naming one Project twice and an
    /// unreadable body are the same type; nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AC12 - " + RedRoute)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "FR-20")]
    public async Task An_exception_outside_the_blocked_Projects_set_is_refused_400_naming_it_and_a_foreign_one_is_identical_to_a_random_one()
    {
        var database = Available();
        Actor? owner = null;
        SpaceRemovalSeed? seed = null;
        var refusals = new List<WireResponse>();
        WireResponse? foreign = null;
        WireResponse? random = null;
        WireResponse? twice = null;
        WireResponse? unreadable = null;
        SpaceSnapshot? before = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            seed = await SeedRemovalAsync(owner, database);
            before = await SnapshotAsync(database, owner.SpaceId);

            var d = seed.Defaults;

            foreach (var destination in new[] { d[1].Id, d[0].Id })
            {
                refusals.Add(await Space.RemoveAsync(actor, owner.SpaceId, d[0].Id, d[1].Id, [(seed.Blocked, destination)]));
            }

            foreign = await Space.RemoveAsync(actor, owner.SpaceId, d[0].Id, d[1].Id, [(seed.Blocked, (await Wire.DefaultsAsync(database, other.SpaceId))[2].Id)]);
            random = await Space.RemoveAsync(actor, owner.SpaceId, d[0].Id, d[1].Id, [(seed.Blocked, Guid.NewGuid())]);
            twice = await Space.RemoveAsync(actor, owner.SpaceId, d[0].Id, d[1].Id, [(seed.Blocked, d[2].Id), (seed.Blocked, seed.Parked)]);
            unreadable = await Space.RawAsync(actor, HttpMethod.Post, SpaceStatusWire.RemoveOf(owner.SpaceId, d[0].Id), "{ not json");
        });

        Assert.NotNull(owner);
        Assert.NotNull(seed);
        RemovalWire.AssertRefused(random, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
        Assert.Equal([seed.Blocked], SpaceStatusWire.ProjectsOf(random!.Body).Select(project => project.Id));
        RemovalWire.AssertRefused(foreign, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination, random);
        Assert.All(refusals, refusal =>
        {
            RemovalWire.AssertRefused(refusal, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
            Assert.Equal([seed.Blocked], SpaceStatusWire.ProjectsOf(refusal.Body).Select(project => project.Id));
        });
        RemovalWire.AssertRefused(twice, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
        RemovalWire.AssertRefused(unreadable, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
        Assert.Equal(before, await SnapshotAsync(database, owner.SpaceId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// An exception for a Project that is not blocked - the plain one, which can take the Space-wide destination - is ignored,
    /// and so are exceptions naming another Space's Project and a random Project id: the removal answers <c>200</c>, the plain
    /// Project's Tasks take the Space-wide destination, and the foreign Space is untouched.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AC12 - " + RedRoute + " (Q6: exceptions for unblocked or unknown Projects are ignored)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "NFR-1")]
    public async Task Exceptions_for_an_unblocked_a_foreign_or_a_random_Project_are_ignored()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        SpaceRemovalSeed? seed = null;
        WireResponse? removed = null;
        IReadOnlyList<StatusRow> theirsBefore = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var foreignProject = IdOf(await CreateAsync(other, "Theirs"));

            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(AsTaskActor(other), other.SpaceId, foreignProject, "Theirs held"));
            seed = await SeedRemovalAsync(owner, database);
            theirsBefore = await Wire.DefaultsAsync(database, other.SpaceId);

            var d = seed.Defaults;

            removed = await Space.RemoveAsync(
                actor,
                owner.SpaceId,
                d[0].Id,
                d[1].Id,
                [(seed.Blocked, seed.Parked), (seed.Plain, d[2].Id), (foreignProject, d[2].Id), (Guid.NewGuid(), d[2].Id)]);
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);
        Assert.NotNull(seed);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");
        Assert.All((await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == seed.Plain), row => Assert.Equal(seed.Defaults[1].Id, row.StatusId));
        Assert.Equal(theirsBefore, await Wire.DefaultsAsync(database, other.SpaceId));
        Assert.All(await TaskWire.TaskRowsAsync(database, other.SpaceId), row => Assert.Equal(theirsBefore[0].Id, row.StatusId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, other.SpaceId);
    }

    /// <summary>
    /// The Space-wide destination must be another default of this Space: the Status being removed, a Status a Project added,
    /// another Space's default and a random id are each refused with the same <c>400 invalid-status-destination</c> body, byte
    /// for byte, naming no Project; nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC12 - " + RedRoute)]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "NFR-1")]
    public async Task Every_invalid_Space_wide_destination_is_refused_with_one_identical_400()
    {
        var database = Available();
        Actor? owner = null;
        var refusals = new List<WireResponse>();
        WireResponse? random = null;
        SpaceSnapshot? before = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var seed = await SeedRemovalAsync(owner, database);
            var d = seed.Defaults;

            before = await SnapshotAsync(database, owner.SpaceId);

            foreach (var destination in new[] { d[0].Id, seed.Parked, (await Wire.DefaultsAsync(database, other.SpaceId))[1].Id })
            {
                refusals.Add(await Space.RemoveAsync(actor, owner.SpaceId, d[0].Id, destination));
            }

            random = await Space.RemoveAsync(actor, owner.SpaceId, d[0].Id, Guid.NewGuid());
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(random, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
        Assert.Empty(SpaceStatusWire.ProjectsOf(random!.Body));
        Assert.Equal(3, refusals.Count);
        Assert.All(refusals, refusal => RemovalWire.AssertRefused(refusal, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination, random));
        Assert.Equal(before, await SnapshotAsync(database, owner.SpaceId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Never empty at either level. A Project that removed the other two defaults and added none holds only the first; the
    /// Space-level removal of the first is refused <c>409 status-set-empty</c> naming that Project, before any destination
    /// check (a random destination does not turn it into a <c>400</c>). In a second Space whose defaults were removed down to
    /// one, removing the last is <c>409 status-set-empty</c> naming no Project. Nothing is written by either.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - " + RedRoute + " (Q7: a Project left empty is refused, naming it; contradiction C1)")]
    [Trait("Requirement", "FR-24")]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "FR-27")]
    public async Task The_last_default_or_a_Project_left_with_only_it_is_refused_409_status_set_empty()
    {
        var database = Available();
        Actor? owner = null;
        Actor? bare = null;
        var onlyIt = Guid.Empty;
        WireResponse? leftEmpty = null;
        WireResponse? leftEmptyRandom = null;
        WireResponse? lastDefault = null;
        SpaceSnapshot? ownBefore = null;
        IReadOnlyList<StatusRow> bareBefore = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            bare = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var bareActor = AsTaskActor(bare);
            var d = await Wire.DefaultsAsync(database, owner.SpaceId);
            var b = await Wire.DefaultsAsync(database, bare.SpaceId);

            onlyIt = IdOf(await CreateAsync(owner, "Only it"));
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, onlyIt, d[1].Id);
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, onlyIt, d[2].Id);
            ownBefore = await SnapshotAsync(database, owner.SpaceId);
            leftEmpty = await Space.RemoveAsync(actor, owner.SpaceId, d[0].Id, d[1].Id);
            leftEmptyRandom = await Space.RemoveAsync(actor, owner.SpaceId, d[0].Id, Guid.NewGuid());

            _ = await Space.RemovedAsync(bareActor, bare.SpaceId, b[1].Id);
            _ = await Space.RemovedAsync(bareActor, bare.SpaceId, b[2].Id);
            bareBefore = await Wire.DefaultsAsync(database, bare.SpaceId);
            lastDefault = await Space.RemoveAsync(bareActor, bare.SpaceId, b[0].Id, destinationStatusId: null);
        });

        Assert.NotNull(owner);
        Assert.NotNull(bare);
        RemovalWire.AssertRefused(leftEmpty, HttpStatusCode.Conflict, ProblemTypes.StatusSetEmpty);
        Assert.Equal([(onlyIt, "Only it")], SpaceStatusWire.ProjectsOf(leftEmpty!.Body));
        RemovalWire.AssertRefused(leftEmptyRandom, HttpStatusCode.Conflict, ProblemTypes.StatusSetEmpty, leftEmpty);
        RemovalWire.AssertRefused(lastDefault, HttpStatusCode.Conflict, ProblemTypes.StatusSetEmpty);
        Assert.Empty(SpaceStatusWire.ProjectsOf(lastDefault!.Body));
        Assert.Equal(ownBefore, await SnapshotAsync(database, owner.SpaceId));
        Assert.Single(bareBefore);
        Assert.Equal(bareBefore, await Wire.DefaultsAsync(database, bare.SpaceId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Project that already removed the default is unaffected: its set, order and names are the same before and after, and
    /// its <c>Remove</c> of the default goes with the definition. A Project that renamed it is affected by identity, not name:
    /// its Task is migrated and its <c>Rename</c> op is gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - " + RedRoute)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-16")]
    public async Task A_Project_that_removed_the_default_is_unaffected_and_one_that_renamed_it_is_migrated()
    {
        var database = Available();
        Actor? owner = null;
        SpaceRemovalSeed? seed = null;
        IReadOnlyList<string> setBefore = [];
        IReadOnlyList<string> setAfter = [];
        IReadOnlyList<(Guid StatusId, string Name)> columnsBefore = [];
        IReadOnlyList<(Guid StatusId, string Name)> columnsAfter = [];
        WireResponse? removed = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            seed = await SeedRemovalAsync(owner, database);
            setBefore = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, seed.RemovedIt)).Select(Shown)];
            columnsBefore = await Wire.BoardColumnsAsync(actor, owner.SpaceId, seed.RemovedIt);
            removed = await Space.RemoveAsync(actor, owner.SpaceId, seed.Defaults[0].Id, seed.Defaults[1].Id, [(seed.Blocked, seed.Defaults[2].Id)]);
            setAfter = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, seed.RemovedIt)).Select(Shown)];
            columnsAfter = await Wire.BoardColumnsAsync(actor, owner.SpaceId, seed.RemovedIt);
        });

        Assert.NotNull(owner);
        Assert.NotNull(seed);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");
        Assert.Equal(setBefore, setAfter);
        Assert.Equal(columnsBefore, columnsAfter);

        var delta = await Space.SpaceDeltaRowsAsync(database, owner.SpaceId);

        Assert.DoesNotContain(delta, row => row.StatusId == seed.Defaults[0].Id || row.AfterStatusId == seed.Defaults[0].Id);
        Assert.All((await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == seed.Renamed), row => Assert.Equal(seed.Defaults[1].Id, row.StatusId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The rest of a Project's order stays as it was when the removed default anchors places on both sides: with the second
    /// default placed first and an added Status placed after it (order: second, added, first, third), removing the second at
    /// Space level leaves added, first, third - the added Status does not fall back to the end. No operation names the
    /// removed default, the re-anchored places have distinct sequences, the Board follows, and a plain Project's set is the
    /// other two defaults in Space order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - " + RedRoute + " (re-anchoring reuses EffectiveStatusSet.Reanchored)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-16")]
    public async Task A_Project_whose_places_anchor_on_the_default_keeps_the_rest_of_its_order()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var anchored = Guid.Empty;
        var plain = Guid.Empty;
        var parked = Guid.Empty;
        IReadOnlyList<Guid> orderBefore = [];
        IReadOnlyList<Guid> orderAfter = [];
        IReadOnlyList<Guid> plainAfter = [];
        IReadOnlyList<Guid> columns = [];
        WireResponse? removed = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            anchored = IdOf(await CreateAsync(owner, "Anchored"));
            plain = IdOf(await CreateAsync(owner, "Plain"));
            parked = await Wire.AddedAsync(actor, owner.SpaceId, anchored, "Parked");
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, anchored, defaults[1].Id, afterStatusId: null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, anchored, parked, defaults[1].Id)).StatusCode);
            orderBefore = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, anchored)).Select(entry => entry.Id)];

            removed = await Space.RemoveAsync(actor, owner.SpaceId, defaults[1].Id, destinationStatusId: null);
            orderAfter = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, anchored)).Select(entry => entry.Id)];
            plainAfter = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, plain)).Select(entry => entry.Id)];
            columns = [.. (await Wire.BoardColumnsAsync(actor, owner.SpaceId, anchored)).Select(column => column.StatusId)];
        });

        Assert.NotNull(owner);
        Assert.Equal([defaults[1].Id, parked, defaults[0].Id, defaults[2].Id], orderBefore);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");
        Assert.Equal([parked, defaults[0].Id, defaults[2].Id], orderAfter);
        Assert.Equal(orderAfter, columns);
        Assert.Equal([defaults[0].Id, defaults[2].Id], plainAfter);

        var delta = await RemovalWire.DeltaRowsAsync(database, owner.SpaceId, anchored);

        Assert.DoesNotContain(delta, row => row.StatusId == defaults[1].Id || row.AfterStatusId == defaults[1].Id);
        Assert.Equal(delta.Count, delta.Select(row => row.Sequence).Distinct().Count());
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Adding back, in a Project that had removed it, a default the Space has since removed answers the in-Space <c>404</c>
    /// byte-identical to a random id, and writes nothing: the Space removal deleted that Project's <c>Remove</c> with the
    /// definition, and the add-back also requires a current default.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - " + RedRoute + " (Q9: the definition and every op of it are deleted)")]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "AD-3")]
    public async Task Adding_back_a_default_removed_at_Space_level_answers_404_and_writes_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? restored = null;
        WireResponse? random = null;
        var deltaBefore = -1;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            projectId = IdOf(await CreateAsync(owner, "Removed it first"));
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[2].Id);
            _ = await Space.RemovedAsync(actor, owner.SpaceId, defaults[2].Id);
            deltaBefore = await Wire.DeltaCountAsync(database, owner.SpaceId, projectId);
            restored = await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, defaults[2].Id);
            random = await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, Guid.NewGuid());
        });

        Assert.NotNull(owner);
        Assert.NotNull(restored);
        Assert.NotNull(random);
        AssertSameNotAvailable(restored, random);
        Assert.Equal(0, deltaBefore);
        Assert.Equal(deltaBefore, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The readers follow on their next read: a List walk by Status whose cursor names the removed default answers <c>400
    /// invalid-list-cursor</c>; the removed default's column page is the <c>404</c>; the Board has no column for it; and a
    /// new Task lands in the Project's first Status after the removal.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC14 - " + RedRoute)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "FR-30")]
    public async Task After_a_Space_removal_the_List_cursor_the_column_page_the_Board_and_Task_creation_follow()
    {
        const int seeded = 60;
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? stale = null;
        WireResponse? columnPage = null;
        IReadOnlyList<Guid> columns = [];
        var created = Guid.Empty;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Read after"));
            var query = SliceListQuery.By(ListSorts.Status);

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            await TaskWire.SeedTasksAsync(database, owner.SpaceId, projectId, defaults[0].Id, seeded);

            var first = ListWire.PageOf(await ListWire.PageAsync(actor, owner.SpaceId, projectId, query));

            Assert.NotNull(first.NextAfter);
            _ = await Space.RemovedAsync(actor, owner.SpaceId, defaults[0].Id, defaults[1].Id);
            stale = await ListWire.PageAsync(actor, owner.SpaceId, projectId, query, first.NextAfter);
            columnPage = await actor.Session.GetAsync(BoardRoutes.ColumnOf(owner.SpaceId, projectId, defaults[0].Id));
            columns = [.. (await Wire.BoardColumnsAsync(actor, owner.SpaceId, projectId)).Select(column => column.StatusId)];
            created = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Lands in the new first"));
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(stale, HttpStatusCode.BadRequest, ProblemTypes.InvalidListCursor);
        RemovalWire.AssertRefused(columnPage, HttpStatusCode.NotFound, ProblemTypes.NotAvailable);
        Assert.Equal([defaults[1].Id, defaults[2].Id], columns);
        Assert.Equal(defaults[1].Id, (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == created).StatusId);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Member and a Viewer are refused the removal on their Session and their Token: <c>403 insufficient-role</c> naming
    /// <c>configure-space-default-statuses</c>, two refusals recorded as <c>InsufficientRole</c>, nothing written. Anchor: the
    /// same caller reads the Space's Statuses.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC11 - " + RedRoute)]
    [InlineData("Member")]
    [InlineData("Viewer")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Member_or_a_Viewer_is_refused_403_on_the_Space_removal_on_both_surfaces(string role)
    {
        var database = Available();
        Actor? owner = null;
        Actor? caller = null;
        IReadOnlyList<StatusRow> defaults = [];
        var refused = new List<WireResponse>();
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            caller = await RegisterAsync(client, database);
            await AddMemberAsync(database, owner.SpaceId, caller.AccountId, role);

            var actor = AsTaskActor(caller);
            var token = await IssueTokenAsync(caller, owner.SpaceId);

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            refused.Add(await Space.RemoveAsync(actor, owner.SpaceId, defaults[2].Id, destinationStatusId: null));
            refused.Add(await Space.RemoveWithTokenAsync(actor, token, owner.SpaceId, defaults[2].Id, destinationStatusId: null));
            anchor = await actor.Session.GetWithTokenAsync(SpaceRoutes.StatusesOf(owner.SpaceId), token);
        });

        Assert.NotNull(owner);
        Assert.NotNull(caller);
        Assert.Equal(2, refused.Count);
        Assert.All(refused, answer =>
        {
            Assert.True(answer.StatusCode == HttpStatusCode.Forbidden, $"{answer.StatusCode} {answer.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(answer)?.Type);
            Assert.Equal(ConfigureDefaultsCapability, Problem(answer)?.Capability);
        });
        Assert.Equal(Enumerable.Repeat(InsufficientRoleKind, 2), await RefusalKindsAsync(database, caller.AccountId, owner.SpaceId));
        Assert.Equal(defaults, await Wire.DefaultsAsync(database, owner.SpaceId));
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// An Admin holds the capability as the Owner does: it removes one unoccupied default on its Session and another on its
    /// Token, each answering <c>200</c> with the defaults left.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - " + RedRoute)]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-27")]
    public async Task An_Admin_removes_a_default_on_both_surfaces()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var answers = new List<WireResponse>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var admin = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, admin.AccountId, "Admin");

            var actor = AsTaskActor(admin);
            var token = await IssueTokenAsync(admin, owner.SpaceId);

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            answers.Add(await Space.RemoveAsync(actor, owner.SpaceId, defaults[2].Id, destinationStatusId: null));
            answers.Add(await Space.RemoveWithTokenAsync(actor, token, owner.SpaceId, defaults[1].Id, destinationStatusId: null));
        });

        Assert.NotNull(owner);
        Assert.All(answers, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}"));
        Assert.Equal([defaults[0].Id], (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => row.Id));
    }

    /// <summary>
    /// Another Space's path answers the boundary <c>404</c>, byte-identical to a Space that does not exist, and is recorded as
    /// <c>CrossSpace</c>; in the caller's own Space path another Space's default and a Status a Project added each answer the
    /// in-Space <c>404</c>, byte-identical to a random id's, and record nothing. Neither Space's defaults change.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - " + RedRoute)]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task Another_Spaces_path_or_default_or_a_Project_added_Status_answers_the_404_byte_identical()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        IReadOnlyList<StatusRow> theirs = [];
        IReadOnlyList<StatusRow> ours = [];
        WireResponse? foreignPath = null;
        WireResponse? absentPath = null;
        var inSpace = new List<(WireResponse Answer, WireResponse Random)>();
        IReadOnlyList<string> inSpaceRefusals = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Adds its own"));
            var parked = await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked");

            theirs = await Wire.DefaultsAsync(database, other.SpaceId);
            ours = await Wire.DefaultsAsync(database, owner.SpaceId);

            foreach (var statusId in new[] { theirs[0].Id, parked })
            {
                inSpace.Add((
                    await Space.RemoveAsync(actor, owner.SpaceId, statusId, destinationStatusId: null),
                    await Space.RemoveAsync(actor, owner.SpaceId, Guid.NewGuid(), destinationStatusId: null)));
            }

            inSpaceRefusals = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            foreignPath = await Space.RemoveAsync(actor, other.SpaceId, theirs[2].Id, destinationStatusId: null);
            absentPath = await Space.RemoveAsync(actor, Guid.NewGuid(), theirs[2].Id, destinationStatusId: null);
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);
        Assert.NotNull(foreignPath);
        Assert.NotNull(absentPath);
        Assert.Equal(2, inSpace.Count);
        Assert.All(inSpace, pair => AssertSameNotAvailable(pair.Answer, pair.Random));
        Assert.Empty(inSpaceRefusals);
        AssertSameNotAvailable(foreignPath, absentPath);
        Assert.Equal([CrossSpaceKind], await RefusalKindsAsync(database, owner.AccountId, other.SpaceId));
        Assert.Equal(theirs, await Wire.DefaultsAsync(database, other.SpaceId));
        Assert.Equal(ours, await Wire.DefaultsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// Another Space's Project holds a Status of the same name with Tasks in it. The caller's removal of its first default with
    /// no destination reports only its own Project, and nothing of the other's id or name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2, AC12 - " + RedRoute)]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "FR-27")]
    public async Task A_foreign_Project_holding_a_same_named_Status_is_never_named_in_the_report()
    {
        var database = Available();
        Actor? owner = null;
        var ownProject = Guid.Empty;
        var foreignProject = Guid.Empty;
        WireResponse? report = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var ours = await Wire.DefaultsAsync(database, owner.SpaceId);
            var theirs = await Wire.DefaultsAsync(database, other.SpaceId);

            Assert.Equal(ours[0].Name, theirs[0].Name);

            ownProject = IdOf(await CreateAsync(owner, "Ours"));
            foreignProject = IdOf(await CreateAsync(other, "Theirs and secret"));
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, ownProject, "Ours held"));
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(AsTaskActor(other), other.SpaceId, foreignProject, "Theirs held"));

            report = await Space.RemoveAsync(actor, owner.SpaceId, ours[0].Id, destinationStatusId: null);
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(report, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);
        Assert.Equal([ownProject], SpaceStatusWire.RemovalOf(report!.Body).Select(entry => entry.ProjectId));
        Assert.DoesNotContain(foreignProject.ToString("D"), report.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Theirs and secret", report.Body, StringComparison.Ordinal);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A removal sent twice with one <c>Idempotency-Key</c> answers the original <c>200</c> and body the second time and
    /// migrates once: every Task keeps the key the first answer gave it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - " + RedRoute)]
    [Trait("Requirement", "AD-18")]
    public async Task A_replayed_Space_removal_returns_the_original_answer_and_migrates_once()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? original = null;
        WireResponse? replay = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> afterOriginal = [];
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> afterReplay = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            var key = Guid.NewGuid().ToString("D");
            var projectId = IdOf(await CreateAsync(owner, "Replayed"));

            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Moved once"));
            original = await Space.RemoveAsync(actor, owner.SpaceId, defaults[0].Id, defaults[1].Id, key: key);
            afterOriginal = await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId);
            replay = await Space.RemoveAsync(actor, owner.SpaceId, defaults[0].Id, defaults[1].Id, key: key);
            afterReplay = await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.OK, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(original!.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Single(afterOriginal);
        Assert.Equal(afterOriginal, afterReplay);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Four Projects through the product, over the Space's three defaults: "Plain" holds two Tasks in the first default;
    /// "Blocked" removed the second default, added "Parked" and holds one Task in the first; "Renamed" renamed the first
    /// "Intake" and holds one Task in it; "Removed it" removed the first default and holds one Task in the second.
    /// </summary>
    private static async Task<SpaceRemovalSeed> SeedRemovalAsync(Actor owner, Yello.Tests.Slices.Accounts.RegisterAccount.RegistrationDatabase database)
    {
        var actor = AsTaskActor(owner);
        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
        var plain = IdOf(await CreateAsync(owner, "Plain"));
        var blocked = IdOf(await CreateAsync(owner, "Blocked"));
        var renamed = IdOf(await CreateAsync(owner, "Renamed"));
        var removedIt = IdOf(await CreateAsync(owner, "Removed it"));

        _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, plain, "Plain one"));
        _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, plain, "Plain two"));

        _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, blocked, defaults[1].Id);
        var parked = await Wire.AddedAsync(actor, owner.SpaceId, blocked, "Parked");
        _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, blocked, "Blocked one"));

        _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, renamed, "Renamed one"));
        Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, renamed, defaults[0].Id, "Intake")).StatusCode);

        _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, removedIt, defaults[0].Id);
        _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, removedIt, "Removed it one"));

        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);

        return new SpaceRemovalSeed(defaults, plain, blocked, renamed, removedIt, parked);
    }

    /// <summary>
    /// Everything a refused removal must leave as it was, read under the Space's key: its defaults, its definitions, its whole
    /// delta and every Task's Status and key.
    /// </summary>
    private static async Task<SpaceSnapshot> SnapshotAsync(Yello.Tests.Slices.Accounts.RegisterAccount.RegistrationDatabase database, Guid spaceId) =>
        new(
            string.Join(';', (await Wire.DefaultsAsync(database, spaceId)).Select(row => $"{row.Id}|{row.Name}|{row.Position}")),
            string.Join(';', (await Wire.DefinitionsAsync(database, spaceId)).Select(row => $"{row.Id}|{row.Name}|{row.OwnerProjectId}")),
            string.Join(';', (await Space.SpaceDeltaRowsAsync(database, spaceId)).Select(row => $"{row.ProjectId}|{row.StatusId}|{row.Kind}|{row.AfterStatusId}")),
            string.Join(';', (await TaskWire.TaskRowsAsync(database, spaceId)).OrderBy(row => row.Id).Select(row => $"{row.Id}|{row.StatusId}|{row.PositionKey}")));

    // Destinations is a list, which a record compares by reference.
    private static string Shown(SpaceStatusOccupancy entry) =>
        $"{entry.ProjectId}|{entry.ProjectName}|{entry.TaskCount}|{string.Join(',', entry.Destinations.Select(choice => $"{choice.Id}:{choice.Name}"))}";

    /// <summary>
    /// The Space and Projects <see cref="SeedRemovalAsync"/> leaves.
    /// </summary>
    /// <param name="Defaults">The Space's three defaults before any removal.</param>
    /// <param name="Plain">Holds two Tasks in the first default and every default.</param>
    /// <param name="Blocked">Removed the second default, added "Parked", holds one Task in the first.</param>
    /// <param name="Renamed">Renamed the first default "Intake", holds one Task in it.</param>
    /// <param name="RemovedIt">Removed the first default, holds one Task in the second.</param>
    /// <param name="Parked">The Status "Blocked" added.</param>
    private sealed record SpaceRemovalSeed(IReadOnlyList<StatusRow> Defaults, Guid Plain, Guid Blocked, Guid Renamed, Guid RemovedIt, Guid Parked);

    /// <summary>
    /// A Space's rows as flat strings, so two reads compare by value.
    /// </summary>
    /// <param name="Defaults">The defaults as id, name and position.</param>
    /// <param name="Definitions">Every definition as id, name and owner.</param>
    /// <param name="Delta">Every delta operation as Project, Status, kind and anchor.</param>
    /// <param name="Tasks">Every Task as id, Status and key.</param>
    private sealed record SpaceSnapshot(string Defaults, string Definitions, string Delta, string Tasks);
}
