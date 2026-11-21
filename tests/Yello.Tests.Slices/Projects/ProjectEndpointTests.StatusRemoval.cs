using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Lists;
using Yello.Contracts.Memberships;
using Yello.Contracts.Projects;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using ListWire = Yello.Tests.Slices.Lists.ListWire;
using MoveWire = Yello.Tests.Slices.Tasks.MoveWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using SliceListQuery = Yello.Tests.Slices.Lists.ListQuery;
using TaskEditWire = Yello.Tests.Slices.Tasks.TaskEditWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Removing a Status from a Project over the wire, and adding a removed Space default back: an occupied Status needs a
/// destination valid in the set the removal leaves, every occupying Task moves to the end of it in one request, the rest of
/// the order stays as it was, and every refusal writes nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The routes and their answers</b>: <c>POST …/statuses/{id}/remove</c> with <c>{ destinationStatusId }</c> and
/// <c>POST …/statuses/{id}/restore</c> with no body, both answering <c>200</c> with the whole set; a <c>Remove</c> operation
/// for a default and a deleted definition for an added Status; dependent places re-anchored;
/// <c>409 status-destination-required</c> and <c>400 invalid-status-destination</c>; one Status per request; a Status-sort
/// cursor on a removed Status answers <c>400 invalid-list-cursor</c>.
/// </para>
/// <para>
/// <b>Every case ends with the Status invariant</b>, whose statement counts a Task in a Status its own Project removed.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private static readonly string[] PlacementMembers = ["statusId", "positionKey"];

    private static readonly string[] SourceTitles = ["Source one", "Source two", "Source three"];

    /// <summary>
    /// Removing the unoccupied third default answers <c>200</c> with the set less that Status; the read answers the same set;
    /// the Board has one column fewer; the Space's own read still lists its three defaults with their names; and the delta
    /// holds one <c>Remove</c> operation for it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AD-16")]
    public async Task Removing_an_unoccupied_default_drops_it_from_the_set_and_the_Board_and_leaves_the_Space_read_unchanged()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? removed = null;
        IReadOnlyList<ProjectStatusEntry> read = [];
        IReadOnlyList<(Guid StatusId, string Name)> columns = [];
        IReadOnlyList<(Guid Id, string Name)> spaceRead = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var third = (await Wire.DefaultsAsync(database, owner.SpaceId))[2].Id;

            projectId = IdOf(await CreateAsync(owner, "Two columns"));
            removed = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, third, destinationStatusId: null);
            read = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
            columns = await Wire.BoardColumnsAsync(actor, owner.SpaceId, projectId);
            spaceRead = await Wire.SpaceReadAsync(actor, owner.SpaceId);
        });

        Assert.NotNull(owner);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");

        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
        var entries = ProjectStatusWire.EntriesOf(removed!.Body);

        Assert.Equal([defaults[0].Id, defaults[1].Id], entries.Select(entry => entry.Id));
        Assert.All(entries, entry => Assert.Equal(ProjectStatusWire.EntryMembers, entry.Members));
        Assert.Equal(entries.Select(Shown), read.Select(Shown));
        Assert.Equal(entries.Select(entry => (entry.Id, entry.Name)), columns);
        Assert.Equal(defaults.Select(row => (row.Id, row.Name)), spaceRead);
        Assert.Equal([(defaults[2].Id, RemovalWire.RemoveKind)], (await RemovalWire.DeltaRowsAsync(database, owner.SpaceId, projectId)).Select(row => (row.StatusId, row.Kind)));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Removing an unoccupied Status the Project added deletes its definition and every operation naming it, writing no
    /// <c>Remove</c>; adding the same name again is admitted with a new id.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "FR-26")]
    public async Task Removing_an_unoccupied_added_Status_deletes_its_definition_and_its_name_is_added_again_with_a_new_id()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var parked = Guid.Empty;
        WireResponse? removed = null;
        WireResponse? readded = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Adds and removes"));
            parked = await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked");
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, projectId, parked, afterStatusId: null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, parked, "Parked here")).StatusCode);

            removed = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, parked, destinationStatusId: null);
            readded = await Wire.AddAsync(actor, owner.SpaceId, projectId, "Parked here");
        });

        Assert.NotNull(owner);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");
        Assert.DoesNotContain(ProjectStatusWire.EntriesOf(removed!.Body), entry => entry.Id == parked);
        Assert.True(readded?.StatusCode == HttpStatusCode.Created, $"{readded?.StatusCode} {readded?.Body}");

        var again = ProjectStatusWire.EntriesOf(readded!.Body).Single(entry => entry.Name == "Parked here");
        var definitions = await Wire.DefinitionsAsync(database, owner.SpaceId);

        Assert.NotEqual(parked, again.Id);
        Assert.Equal(ProjectStatusSources.AddedHere, again.Source);
        Assert.DoesNotContain(definitions, row => row.Id == parked);
        Assert.DoesNotContain(await RemovalWire.DeltaRowsAsync(database, owner.SpaceId, projectId), row => row.StatusId == parked || row.AfterStatusId == parked);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Removing the occupied first default to the second moves its three Tasks to the end of the second's column in their
    /// source order, after the Task already there, whose key is unchanged; each moved Task keeps its id, title, Assignee,
    /// Labels, due date and creation time; the answer's destination entry counts all four Tasks; and the first default leaves
    /// the set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AD-17")]
    public async Task Removing_an_occupied_Status_moves_every_Task_to_the_end_of_the_destination_in_source_order()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var resident = Guid.Empty;
        var residentKey = string.Empty;
        IReadOnlyList<Guid> sources = [];
        IReadOnlyList<StatusRowOf> defaults = [];
        var viewsBefore = new Dictionary<Guid, IReadOnlyList<string>>();
        var viewsAfter = new Dictionary<Guid, IReadOnlyList<string>>();
        var assignee = Guid.Empty;
        var assigned = Guid.Empty;
        Guid? assigneeAfter = null;
        WireResponse? removed = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var label = await LabelWire.SeedAsync(actor, owner.SpaceId, "Urgent");

            projectId = IdOf(await CreateAsync(owner, "Migrated"));
            defaults = [.. (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => new StatusRowOf(row.Id, row.Name))];
            resident = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Already there"));
            Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(actor, owner.SpaceId, resident, defaults[1].Id, afterTaskId: null)).StatusCode);
            residentKey = await MoveWire.KeyOfAsync(database, owner.SpaceId, resident);

            var created = new List<Guid>();

            foreach (var title in SourceTitles)
            {
                created.Add(TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, title)));
            }

            Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(actor, owner.SpaceId, created[0], TaskEditWire.Body(new { labelIds = new[] { label } }))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(actor, owner.SpaceId, created[1], TaskEditWire.Body(new { dueDate = "2026-12-01T00:00:00+00:00" }))).StatusCode);

            assignee = AssignmentWire.ParseCandidates((await actor.Session.GetAsync(AssigneeRoutes.ListOf(owner.SpaceId))).Body)[0].MembershipId;
            Assert.Equal(HttpStatusCode.OK, (await actor.Session.SendWithHeadersAsync(HttpMethod.Put, TaskRoutes.AssigneeOf(owner.SpaceId, created[2]), AssignmentWire.Body(assignee), TaskWire.KeyHeader(null))).StatusCode);
            assigned = created[2];

            sources = await MoveWire.ColumnAsync(database, owner.SpaceId, projectId, defaults[0].Id);

            foreach (var id in sources)
            {
                viewsBefore[id] = WithoutPlacement((await TaskEditWire.ViewTaskAsync(actor, owner.SpaceId, id)).Body);
            }

            removed = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, defaults[1].Id);

            foreach (var id in sources)
            {
                viewsAfter[id] = WithoutPlacement((await TaskEditWire.ViewTaskAsync(actor, owner.SpaceId, id)).Body);
            }

            assigneeAfter = AssignmentWire.AssigneeIn((await TaskEditWire.ViewTaskAsync(actor, owner.SpaceId, assigned)).Body);
        });

        Assert.NotNull(owner);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");
        Assert.Equal(3, sources.Count);

        var entries = ProjectStatusWire.EntriesOf(removed!.Body);

        Assert.DoesNotContain(entries, entry => entry.Id == defaults[0].Id);
        Assert.Equal(4, entries.Single(entry => entry.Id == defaults[1].Id).TaskCount);
        Assert.Equal([resident, .. sources], await MoveWire.ColumnAsync(database, owner.SpaceId, projectId, defaults[1].Id));
        Assert.Equal(residentKey, await MoveWire.KeyOfAsync(database, owner.SpaceId, resident));
        Assert.All(sources, id => Assert.Equal(viewsBefore[id], viewsAfter[id]));
        Assert.Equal(assignee, assigneeAfter);

        var keys = (await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId)).Where(row => row.StatusId == defaults[1].Id).Select(row => row.PositionKey).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys.Where(key => key != residentKey), key => Assert.True(string.CompareOrdinal(key, residentKey) > 0, $"{key} is not after the resident's {residentKey}."));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Removing an occupied Status with no destination is refused <c>409 status-destination-required</c>; the Status stays in
    /// the set, its Task stays at its key, and the delta is untouched.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AR-24")]
    public async Task An_occupied_Status_removed_without_a_destination_is_refused_409_and_nothing_changes()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? refused = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        IReadOnlyList<ProjectStatusEntry> set = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var first = (await Wire.DefaultsAsync(database, owner.SpaceId))[0].Id;

            projectId = IdOf(await CreateAsync(owner, "Occupied"));
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Sitting in it"));
            before = await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId);
            refused = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, first, destinationStatusId: null);
            set = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);
        Assert.Equal(3, set.Count);
        Assert.Equal(before, await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId));
        Assert.Equal(0, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The destination is checked against the set the removal leaves: the removed Status itself, another Space's real
    /// Status, a sibling Project's added Status and a default this Project already removed are each refused with the same
    /// <c>400 invalid-status-destination</c> body as a random id, byte for byte; an unreadable body is the same type; nothing
    /// is written by any of them.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "NFR-1")]
    public async Task Every_destination_outside_the_set_after_the_removal_is_refused_with_one_identical_400()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var refusals = new List<WireResponse>();
        WireResponse? random = null;
        WireResponse? unreadable = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> before = [];
        var deltaBefore = -1;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            projectId = IdOf(await CreateAsync(owner, "Mapped carefully"));

            var sibling = IdOf(await CreateAsync(owner, "Sibling"));
            var siblings = await Wire.AddedAsync(actor, owner.SpaceId, sibling, "Theirs only");
            var foreign = (await Wire.DefaultsAsync(database, other.SpaceId))[1].Id;

            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Needs a home"));
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[2].Id);
            before = await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId);
            deltaBefore = await Wire.DeltaCountAsync(database, owner.SpaceId, projectId);

            foreach (var destination in new[] { defaults[0].Id, foreign, siblings, defaults[2].Id })
            {
                refusals.Add(await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, destination));
            }

            random = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, Guid.NewGuid());
            unreadable = await RemovalWire.RemoveRawAsync(actor, owner.SpaceId, projectId, defaults[0].Id, "{ not json");
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(random, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
        Assert.Equal(4, refusals.Count);
        Assert.All(refusals, refusal => RemovalWire.AssertRefused(refusal, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination, random));
        RemovalWire.AssertRefused(unreadable, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
        Assert.Equal(before, await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId));
        Assert.Equal(deltaBefore, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A request means one thing whatever the occupancy: an unoccupied Status removed with a valid destination is removed;
    /// one removed with a random destination is refused <c>400 invalid-status-destination</c> and stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    public async Task An_unoccupied_Status_with_a_valid_destination_is_removed_and_with_an_invalid_one_is_refused()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? valid = null;
        WireResponse? invalid = null;
        IReadOnlyList<StatusRowOf> defaults = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Mapped needlessly"));

            defaults = [.. (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => new StatusRowOf(row.Id, row.Name))];
            invalid = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[1].Id, Guid.NewGuid());
            valid = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[2].Id, defaults[0].Id);
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(invalid, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
        Assert.True(valid?.StatusCode == HttpStatusCode.OK, $"{valid?.StatusCode} {valid?.Body}");
        Assert.Equal([defaults[0].Id, defaults[1].Id], ProjectStatusWire.EntriesOf(valid!.Body).Select(entry => entry.Id));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A set is never empty: with two defaults removed, removing the last is refused <c>409 status-set-empty</c> before any
    /// destination check (a random destination does not turn it into a <c>400</c>), and nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "FR-26")]
    public async Task Removing_the_last_Status_is_refused_409_status_set_empty_before_any_destination_check()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? bare = null;
        WireResponse? mapped = null;
        var deltaBefore = -1;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            projectId = IdOf(await CreateAsync(owner, "Down to one"));
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[1].Id);
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[2].Id);
            deltaBefore = await Wire.DeltaCountAsync(database, owner.SpaceId, projectId);
            bare = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, destinationStatusId: null);
            mapped = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, Guid.NewGuid());
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(bare, HttpStatusCode.Conflict, ProblemTypes.StatusSetEmpty);
        RemovalWire.AssertRefused(mapped, HttpStatusCode.Conflict, ProblemTypes.StatusSetEmpty);
        Assert.Equal(deltaBefore, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A removed default comes back by id: renamed and placed first here, removed, then added back, it returns under its
    /// Space name at its Space slot with the same id; a Task can be moved into it; removed again (to the first default) and
    /// added back again, each answering the whole set; and no Task is ever outside the set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-16")]
    public async Task A_removed_default_is_added_back_with_its_id_at_its_Space_slot_under_its_Space_name()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRowOf> defaults = [];
        var answers = new List<(string Step, WireResponse Answer)>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Comes back"));
            var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Travels"));

            defaults = [.. (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => new StatusRowOf(row.Id, row.Name))];
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[1].Id, "Underway")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, projectId, defaults[1].Id, afterStatusId: null)).StatusCode);

            answers.Add(("remove", await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[1].Id, destinationStatusId: null)));
            await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
            answers.Add(("restore", await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, defaults[1].Id)));
            await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
            answers.Add(("move in", await MoveWire.MoveTaskAsync(actor, owner.SpaceId, taskId, defaults[1].Id, afterTaskId: null)));
            answers.Add(("remove again", await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[1].Id, defaults[0].Id)));
            await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
            answers.Add(("restore again", await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, defaults[1].Id)));
        });

        Assert.NotNull(owner);
        Assert.All(answers, step => Assert.True(step.Answer.StatusCode == HttpStatusCode.OK, $"{step.Step}: {step.Answer.StatusCode} {step.Answer.Body}"));
        Assert.Equal([defaults[0].Id, defaults[2].Id], ProjectStatusWire.EntriesOf(answers[0].Answer.Body).Select(entry => entry.Id));

        foreach (var restored in new[] { answers[1].Answer, answers[4].Answer })
        {
            var entries = ProjectStatusWire.EntriesOf(restored.Body);

            Assert.Equal(defaults.Select(row => (row.Id, row.Name)), entries.Select(entry => (entry.Id, entry.Name)));
            Assert.Equal(ProjectStatusSources.SpaceDefault, entries[1].Source);
            Assert.Null(entries[1].DefaultName);
        }

        Assert.Equal(1, ProjectStatusWire.EntriesOf(answers[3].Answer.Body).Single(entry => entry.Id == defaults[0].Id).TaskCount);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Adding back is refused where it would break the set: a removed default whose name the Project has since given to an
    /// added Status is <c>409 status-name-taken</c>; a default this Project has not removed, and an added Status's id, each
    /// answer the in-Space <c>404 not-available</c> byte-identical to a random id; nothing is written by any of them.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-3")]
    public async Task Adding_back_a_name_the_set_now_holds_is_409_and_an_id_that_is_not_a_removed_default_is_404()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? clash = null;
        var notRemoved = new List<(WireResponse Answer, WireResponse Random)>();
        var deltaBefore = -1;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            projectId = IdOf(await CreateAsync(owner, "Name reused"));
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[2].Id);

            var reused = await Wire.AddedAsync(actor, owner.SpaceId, projectId, defaults[2].Name);

            deltaBefore = await Wire.DeltaCountAsync(database, owner.SpaceId, projectId);
            clash = await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, defaults[2].Id);

            foreach (var statusId in new[] { defaults[0].Id, reused })
            {
                notRemoved.Add((
                    await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, statusId),
                    await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, Guid.NewGuid())));
            }
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(clash, HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken);
        Assert.All(notRemoved, pair => AssertSameNotAvailable(pair.Answer, pair.Random));
        Assert.Equal(deltaBefore, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The rest of the order does not move: with the third default placed after the first and an added Status placed after
    /// the third (order: first, third, added, second), removing the third leaves first, added, second - the added Status does
    /// not fall back to the end because its anchor left. The removed Status's own rename and place are deleted, and no
    /// operation is anchored on it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AD-16")]
    public async Task Removing_a_Status_that_others_are_placed_after_leaves_the_rest_in_their_order()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var parked = Guid.Empty;
        IReadOnlyList<StatusRowOf> defaults = [];
        IReadOnlyList<Guid> orderBefore = [];
        WireResponse? removed = null;
        IReadOnlyList<(Guid StatusId, string Name)> columns = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Anchored"));
            defaults = [.. (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => new StatusRowOf(row.Id, row.Name))];
            parked = await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked");
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[2].Id, "Shipped")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, projectId, defaults[2].Id, defaults[0].Id)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, projectId, parked, defaults[2].Id)).StatusCode);
            orderBefore = [.. (await Wire.EffectiveAsync(actor, owner.SpaceId, projectId)).Select(entry => entry.Id)];
            removed = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[2].Id, destinationStatusId: null);
            columns = await Wire.BoardColumnsAsync(actor, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.Equal([defaults[0].Id, defaults[2].Id, parked, defaults[1].Id], orderBefore);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");

        Guid[] expected = [defaults[0].Id, parked, defaults[1].Id];

        Assert.Equal(expected, ProjectStatusWire.EntriesOf(removed!.Body).Select(entry => entry.Id));
        Assert.Equal(expected, columns.Select(column => column.StatusId));

        var delta = await RemovalWire.DeltaRowsAsync(database, owner.SpaceId, projectId);

        Assert.Equal([RemovalWire.RemoveKind], delta.Where(row => row.StatusId == defaults[2].Id).Select(row => row.Kind));
        Assert.DoesNotContain(delta, row => row.AfterStatusId == defaults[2].Id);
        Assert.Equal(delta.Count, delta.Select(row => row.Sequence).Distinct().Count());
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Member and a Viewer are each refused the removal and the add-back on their Session and their Token: <c>403
    /// insufficient-role</c> naming <c>configure-project-status-delta</c>, four refusals recorded as <c>InsufficientRole</c>,
    /// and nothing written. Anchor: the same caller reads the set.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Member")]
    [InlineData("Viewer")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Member_or_a_Viewer_is_refused_403_on_removal_and_add_back_on_both_surfaces(string role)
    {
        var database = Available();
        Actor? owner = null;
        Actor? caller = null;
        var projectId = Guid.Empty;
        var refused = new List<WireResponse>();
        var deltaBefore = -1;
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            caller = await RegisterAsync(client, database);
            await AddMemberAsync(database, owner.SpaceId, caller.AccountId, role);

            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            projectId = IdOf(await CreateAsync(owner, "Guarded removal"));
            _ = await RemovalWire.RemovedAsync(AsTaskActor(owner), owner.SpaceId, projectId, defaults[2].Id);
            deltaBefore = await Wire.DeltaCountAsync(database, owner.SpaceId, projectId);

            var actor = AsTaskActor(caller);
            var token = await IssueTokenAsync(caller, owner.SpaceId);

            refused.Add(await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[1].Id, destinationStatusId: null));
            refused.Add(await RemovalWire.RemoveWithTokenAsync(actor, token, owner.SpaceId, projectId, defaults[1].Id, destinationStatusId: null));
            refused.Add(await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, defaults[2].Id));
            refused.Add(await RemovalWire.RestoreWithTokenAsync(actor, token, owner.SpaceId, projectId, defaults[2].Id));
            anchor = await Wire.ReadWithTokenAsync(actor, token, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(caller);
        Assert.Equal(4, refused.Count);
        Assert.All(refused, answer =>
        {
            Assert.True(answer.StatusCode == HttpStatusCode.Forbidden, $"{answer.StatusCode} {answer.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(answer)?.Type);
            Assert.Equal(ConfigureCapability, Problem(answer)?.Capability);
        });
        Assert.Equal(Enumerable.Repeat(InsufficientRoleKind, 4), await RefusalKindsAsync(database, caller.AccountId, owner.SpaceId));
        Assert.Equal(deltaBefore, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// A Project id that matches nothing in the caller's own Space answers the removal and the add-back with the boundary's
    /// <c>404 not-available</c>, byte-identical to a Space that does not exist; in the caller's own Project path a sibling
    /// Project's added Status and another Space's real Status answer both routes as a random id does; nothing is recorded or
    /// written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "FR-20")]
    public async Task An_unknown_Project_or_a_foreign_Status_in_the_path_answers_the_404_byte_identical()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var answers = new List<(WireResponse Answer, WireResponse Anchor)>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var first = (await Wire.DefaultsAsync(database, owner.SpaceId))[0].Id;

            projectId = IdOf(await CreateAsync(owner, "Own"));

            var sibling = IdOf(await CreateAsync(owner, "Sibling"));
            var siblings = await Wire.AddedAsync(actor, owner.SpaceId, sibling, "Theirs only");
            var foreign = (await Wire.DefaultsAsync(database, other.SpaceId))[0].Id;

            answers.Add((
                await RemovalWire.RemoveAsync(actor, owner.SpaceId, Guid.NewGuid(), first, destinationStatusId: null),
                await RemovalWire.RemoveAsync(actor, Guid.NewGuid(), Guid.NewGuid(), first, destinationStatusId: null)));
            answers.Add((
                await RemovalWire.RestoreAsync(actor, owner.SpaceId, Guid.NewGuid(), first),
                await RemovalWire.RestoreAsync(actor, Guid.NewGuid(), Guid.NewGuid(), first)));

            foreach (var statusId in new[] { siblings, foreign })
            {
                answers.Add((
                    await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, statusId, destinationStatusId: null),
                    await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, Guid.NewGuid(), destinationStatusId: null)));
                answers.Add((
                    await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, statusId),
                    await RemovalWire.RestoreAsync(actor, owner.SpaceId, projectId, Guid.NewGuid())));
            }
        });

        Assert.NotNull(owner);
        Assert.Equal(6, answers.Count);
        Assert.All(answers, pair => AssertSameNotAvailable(pair.Answer, pair.Anchor));
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Equal(0, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        Assert.Equal(4, (await Wire.DefinitionsAsync(database, owner.SpaceId)).Count);
    }

    /// <summary>
    /// A removal sent twice with one <c>Idempotency-Key</c> answers the original <c>200</c> and body the second time, and
    /// migrates once: the Tasks keep the keys the first answer gave them and the delta holds one <c>Remove</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    public async Task A_replayed_removal_returns_the_original_answer_and_migrates_once()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? original = null;
        WireResponse? replay = null;
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> afterOriginal = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            var key = Guid.NewGuid().ToString("D");

            projectId = IdOf(await CreateAsync(owner, "Replayed removal"));
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Moved once"));
            original = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, defaults[1].Id, key);
            afterOriginal = await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId);
            replay = await RemovalWire.RemoveAsync(actor, owner.SpaceId, projectId, defaults[0].Id, defaults[1].Id, key);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.OK, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(original!.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Equal(afterOriginal, await MoveWire.PlacementsAsync(database, owner.SpaceId, projectId));
        Assert.Single(await RemovalWire.DeltaRowsAsync(database, owner.SpaceId, projectId), row => row.Kind == RemovalWire.RemoveKind);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A <c>Remove</c> operation goes with the rest of the delta: deleting a Project that removed a default and holds Tasks
    /// answers <c>204</c> and leaves none of its Tasks or operations; deleting a Space holding such a Project answers
    /// <c>204</c> in its one statement and leaves nothing of the Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "AD-17")]
    public async Task Deleting_a_Project_or_a_Space_holding_a_removed_default_and_Tasks_succeeds()
    {
        var database = Available();
        Actor? owner = null;
        var doomed = Guid.Empty;
        WireResponse? projectDeleted = null;
        WireResponse? spaceDeleted = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            doomed = IdOf(await CreateAsync(owner, "Torn down"));

            var kept = IdOf(await CreateAsync(owner, "Torn down with the Space"));

            foreach (var projectId in new[] { doomed, kept })
            {
                _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Migrated, then deleted"));
                _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[0].Id, defaults[2].Id);
            }

            await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
            projectDeleted = await DeleteWire.DeleteProjectAsync(actor, owner.SpaceId, doomed);
            Assert.Equal(0, await Wire.DeltaCountAsync(database, owner.SpaceId, doomed));
            Assert.Equal(0, await TaskWire.TaskCountAsync(database, owner.SpaceId, doomed));
            spaceDeleted = await owner.Session.SendWithHeadersAsync(HttpMethod.Delete, Yello.Contracts.Spaces.SpaceRoutes.ActiveOf(owner.SpaceId), content: null, TaskWire.NoHeaders);
        });

        Assert.NotNull(owner);
        Assert.True(projectDeleted?.StatusCode == HttpStatusCode.NoContent, $"{projectDeleted?.StatusCode} {projectDeleted?.Body}");
        Assert.True(spaceDeleted?.StatusCode == HttpStatusCode.NoContent, $"{spaceDeleted?.StatusCode} {spaceDeleted?.Body}");
        Assert.Empty(await Wire.DefinitionsAsync(database, owner.SpaceId));
        Assert.Empty(await TaskWire.TaskRowsAsync(database, owner.SpaceId));
        Assert.Empty(await ProjectRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// A List walk by Status whose cursor names a Status the Project has since removed answers <c>400 invalid-list-cursor</c>
    /// rather than an empty page that silently ends the walk; a fresh walk returns every Task once, all in the destination.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-30")]
    [Trait("Requirement", "FR-26")]
    public async Task A_Status_sort_cursor_on_a_removed_Status_is_refused_400_and_a_fresh_walk_returns_every_Task_once()
    {
        const int seeded = 60;
        var database = Available();
        Actor? owner = null;
        WireResponse? stale = null;
        IReadOnlyList<Guid> walked = [];
        IReadOnlyList<StatusRowOf> defaults = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Walked by Status"));
            var query = SliceListQuery.By(ListSorts.Status);

            defaults = [.. (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => new StatusRowOf(row.Id, row.Name))];
            await TaskWire.SeedTasksAsync(database, owner.SpaceId, projectId, defaults[0].Id, seeded);

            var first = ListWire.PageOf(await ListWire.PageAsync(actor, owner.SpaceId, projectId, query));

            Assert.NotNull(first.NextAfter);
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[0].Id, defaults[1].Id);
            stale = await ListWire.PageAsync(actor, owner.SpaceId, projectId, query, first.NextAfter);
            walked = [.. (await ListWire.WalkAsync(actor, owner.SpaceId, projectId, query)).SelectMany(page => page.Tasks).Select(task => task.Id)];
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(stale, HttpStatusCode.BadRequest, ProblemTypes.InvalidListCursor);
        Assert.Equal(seeded, walked.Count);
        Assert.Equal(seeded, walked.Distinct().Count());
        Assert.All(await TaskWire.TaskRowsAsync(database, owner.SpaceId), row => Assert.Equal(defaults[1].Id, row.StatusId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Task's view as sorted <c>member=value</c> lines without its Status and key: what a migration must leave unchanged.
    /// </summary>
    private static IReadOnlyList<string> WithoutPlacement(string body)
    {
        using var document = JsonDocument.Parse(body);

        return
        [
            .. document.RootElement.EnumerateObject()
                .Where(member => !PlacementMembers.Contains(member.Name, StringComparer.Ordinal))
                .Select(member => $"{member.Name}={member.Value.GetRawText()}")
                .Order(StringComparer.Ordinal),
        ];
    }

    // Members is a list, which a record compares by reference.
    private static string Shown(ProjectStatusEntry entry) => $"{entry.Id}|{entry.Name}|{entry.Source}|{entry.DefaultName}|{entry.TaskCount}|{string.Join(',', entry.Members)}";

    private sealed record StatusRowOf(Guid Id, string Name);
}
