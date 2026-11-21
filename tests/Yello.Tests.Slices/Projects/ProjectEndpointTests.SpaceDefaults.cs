using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Contracts.Lists;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;
using ListWire = Yello.Tests.Slices.Lists.ListWire;
using MoveWire = Yello.Tests.Slices.Tasks.MoveWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using SliceListQuery = Yello.Tests.Slices.Lists.ListQuery;
using Space = Yello.Tests.Slices.Projects.SpaceDefaultStatusWire;
using StatusRow = Yello.Tests.Slices.Tasks.StatusRow;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// The Space's default Statuses changed at Space level - add, rename, place - on both surfaces, and every Project following
/// through the one derivation: a new default reaches every Project, a renamed one shows its new name wherever no Project
/// renamed it, a reordered one moves wherever no Project placed it. Where a Project did rename it, the rename is reported and
/// decided once for all of them; where a Project's set would hold a name twice, the write is refused and names the Projects.
/// </summary>
/// <remarks>
/// <para>
/// <b>A Space write writes no Project row</b>, except that a rename deletes the <c>Rename</c> ops it names. Every case reads the
/// Space's delta count or its rename ops around the write to prove it.
/// </para>
/// <para>
/// <b>Every case ends with the Status invariant</b>: no Task of the Space holds a Status outside its own Project's set.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private const string ConfigureDefaultsCapability = "configure-space-default-statuses";

    /// <summary>
    /// An Owner adds "Review" to the defaults: <c>201</c> with a <c>Location</c> on the collection and the four defaults in
    /// Space order, positions 0 to 3, the read answering the same. A plain Project, one that removed the third default and
    /// one that added "Parked" each show "Review" in its Space slot - after the defaults it holds, before "Parked" - the plain
    /// Board gains a fourth column, and the Space's delta holds exactly the operations it held before.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC15 - no Space add route yet (Q1, Q8)")]
    [Trait("Requirement", "FR-24")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-16")]
    public async Task Adding_a_default_reaches_every_Project_in_its_Space_slot_and_writes_no_Project_row()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? added = null;
        IReadOnlyList<SpaceStatusRow> read = [];
        var deltaBefore = -1;
        var deltaAfter = -1;
        IReadOnlyList<ProjectStatusEntry> plain = [];
        IReadOnlyList<ProjectStatusEntry> removing = [];
        IReadOnlyList<ProjectStatusEntry> adding = [];
        IReadOnlyList<(Guid StatusId, string Name)> columns = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var plainId = IdOf(await CreateAsync(owner, "Plain"));
            var removingId = IdOf(await CreateAsync(owner, "Removed the third"));
            var addingId = IdOf(await CreateAsync(owner, "Added its own"));

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, removingId, defaults[2].Id);
            _ = await Wire.AddedAsync(actor, owner.SpaceId, addingId, "Parked");

            deltaBefore = await Space.SpaceDeltaCountAsync(database, owner.SpaceId);
            added = await Space.AddAsync(actor, owner.SpaceId, "Review");
            deltaAfter = await Space.SpaceDeltaCountAsync(database, owner.SpaceId);
            read = SpaceStatusWire.DefaultsOf((await owner.Session.GetAsync(SpaceRoutes.StatusesOf(owner.SpaceId))).Body);
            plain = await Wire.EffectiveAsync(actor, owner.SpaceId, plainId);
            removing = await Wire.EffectiveAsync(actor, owner.SpaceId, removingId);
            adding = await Wire.EffectiveAsync(actor, owner.SpaceId, addingId);
            columns = await Wire.BoardColumnsAsync(actor, owner.SpaceId, plainId);
        });

        Assert.NotNull(owner);
        Assert.True(added?.StatusCode == HttpStatusCode.Created, $"{added?.StatusCode} {added?.Body}");
        Assert.EndsWith(SpaceRoutes.StatusesOf(owner.SpaceId), added!.Header("Location") ?? string.Empty, StringComparison.Ordinal);

        var answered = SpaceStatusWire.DefaultsOf(added.Body);
        string[] names = [defaults[0].Name, defaults[1].Name, defaults[2].Name, "Review"];

        Assert.Equal(names, answered.Select(row => row.Name));
        Assert.Equal([0, 1, 2, 3], answered.Select(row => row.Position));
        Assert.Equal(answered, read);
        Assert.Equal(names, plain.Select(entry => entry.Name));
        Assert.Equal([defaults[0].Name, defaults[1].Name, "Review"], removing.Select(entry => entry.Name));
        Assert.Equal([.. names, "Parked"], adding.Select(entry => entry.Name));
        Assert.Equal(ProjectStatusSources.SpaceDefault, plain[3].Source);
        Assert.Equal(4, columns.Count);
        Assert.Equal(deltaBefore, deltaAfter);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// An add that names the default it goes after lands there in one write: added after the first default, the defaults are
    /// the first, "Triage", the second and the third at positions 0 to 3, and a plain Project shows the same order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - no Space add route yet (Q8)")]
    [Trait("Requirement", "FR-24")]
    [Trait("Requirement", "FR-27")]
    public async Task Adding_a_default_after_another_places_it_there_in_the_Space_and_in_a_plain_Project()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? added = null;
        IReadOnlyList<ProjectStatusEntry> plain = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Follows the Space"));

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            added = await Space.AddAsync(actor, owner.SpaceId, "Triage", defaults[0].Id);
            plain = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.True(added?.StatusCode == HttpStatusCode.Created, $"{added?.StatusCode} {added?.Body}");

        string[] expected = [defaults[0].Name, "Triage", defaults[1].Name, defaults[2].Name];

        Assert.Equal(expected, SpaceStatusWire.DefaultsOf(added!.Body).Select(row => row.Name));
        Assert.Equal([0, 1, 2, 3], (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => row.Position));
        Assert.Equal(expected, plain.Select(entry => entry.Name));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Renaming the second default "Underway" writes only its name: its id is unchanged, a Task holding it keeps its
    /// <c>StatusId</c> and its column page still answers, and the new name shows in a plain Project, in one that placed it
    /// first, and in one that renamed the first default - and the Space's delta is untouched.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - no Space rename route yet (Q1)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "AR-23")]
    public async Task Renaming_a_default_reaches_plain_placed_and_other_renamed_Projects_and_keeps_its_id()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var taskId = Guid.Empty;
        WireResponse? renamed = null;
        WireResponse? columnPage = null;
        var deltaBefore = -1;
        var deltaAfter = -1;
        IReadOnlyList<ProjectStatusEntry> plain = [];
        IReadOnlyList<ProjectStatusEntry> placed = [];
        IReadOnlyList<ProjectStatusEntry> other = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var plainId = IdOf(await CreateAsync(owner, "Plain"));
            var placedId = IdOf(await CreateAsync(owner, "Placed it first"));
            var otherId = IdOf(await CreateAsync(owner, "Renamed another"));

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, plainId, "In the second"));
            Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(actor, owner.SpaceId, taskId, defaults[1].Id, afterTaskId: null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, placedId, defaults[1].Id, afterStatusId: null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, otherId, defaults[0].Id, "Intake")).StatusCode);

            deltaBefore = await Space.SpaceDeltaCountAsync(database, owner.SpaceId);
            renamed = await Space.RenameAsync(actor, owner.SpaceId, defaults[1].Id, "Underway");
            deltaAfter = await Space.SpaceDeltaCountAsync(database, owner.SpaceId);
            plain = await Wire.EffectiveAsync(actor, owner.SpaceId, plainId);
            placed = await Wire.EffectiveAsync(actor, owner.SpaceId, placedId);
            other = await Wire.EffectiveAsync(actor, owner.SpaceId, otherId);
            columnPage = await owner.Session.GetAsync(BoardRoutes.ColumnOf(owner.SpaceId, plainId, defaults[1].Id));
        });

        Assert.NotNull(owner);
        Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"{renamed?.StatusCode} {renamed?.Body}");
        Assert.Equal(
            [(defaults[0].Id, defaults[0].Name), (defaults[1].Id, "Underway"), (defaults[2].Id, defaults[2].Name)],
            SpaceStatusWire.DefaultsOf(renamed!.Body).Select(row => (row.Id, row.Name)));
        Assert.Equal([defaults[0].Name, "Underway", defaults[2].Name], plain.Select(entry => entry.Name));
        Assert.Equal(["Underway", defaults[0].Name, defaults[2].Name], placed.Select(entry => entry.Name));
        Assert.Equal(["Intake", "Underway", defaults[2].Name], other.Select(entry => entry.Name));
        Assert.Null(plain[1].DefaultName);
        Assert.Equal(defaults[1].Id, (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == taskId).StatusId);
        Assert.Equal(HttpStatusCode.OK, columnPage?.StatusCode);
        Assert.Equal(deltaBefore, deltaAfter);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Two Projects renamed the first default themselves ("Intake" holding two Tasks, "Backlog" holding none) and a third did
    /// not. A Space rename of it to "Queued" with no decision is <c>409 status-rename-conflict</c> whose report names exactly
    /// the two, each with its id, its name, its own name for the Status and its Task count; the default, the rename ops and
    /// every set are as they were.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - no Space rename route and no conflict report yet (Q2, Q3)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "UX-DR16")]
    public async Task A_rename_Projects_conflict_with_is_refused_409_naming_each_Project_its_name_and_its_Task_count()
    {
        var database = Available();
        Actor? owner = null;
        Conflicted? seeded = null;
        WireResponse? refused = null;
        IReadOnlyList<(Guid ProjectId, Guid StatusId, string Name)> opsBefore = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            seeded = await SeedConflictsAsync(owner, database);
            opsBefore = await Space.RenameOperationsAsync(database, owner.SpaceId);
            refused = await Space.RenameAsync(AsTaskActor(owner), owner.SpaceId, seeded.Defaults[0].Id, "Queued");
        });

        Assert.NotNull(owner);
        Assert.NotNull(seeded);
        RemovalWire.AssertRefused(refused, HttpStatusCode.Conflict, SpaceStatusWire.StatusRenameConflict);
        Assert.Equal(
            [(seeded.Backlog, "Backlog project", "Backlog", 0), (seeded.Intake, "Intake project", "Intake", 2)],
            SpaceStatusWire.ConflictsOf(refused!.Body).Select(conflict => (conflict.ProjectId, conflict.ProjectName, conflict.Name, conflict.TaskCount)).OrderBy(conflict => conflict.ProjectName, StringComparer.Ordinal));
        Assert.Equal(seeded.Defaults.Select(row => row.Name), (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => row.Name));
        Assert.Equal(opsBefore, await Space.RenameOperationsAsync(database, owner.SpaceId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The same rename sent with one decision for every conflicting Project: with <c>true</c> both show "Queued" with no default
    /// name and their <c>Rename</c> ops of the first default are gone; with <c>false</c> they keep "Intake" and "Backlog" and show
    /// "Queued" as the default's name, their ops untouched. Either way the plain Project shows "Queued", a Project's rename of
    /// another default is untouched, and the answer is <c>200</c> with the defaults.
    /// </summary>
    /// <param name="replaceProjectNames">The decision.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC5 - no Space rename route and no cascade yet (Q3, Q4)")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "UX-DR16")]
    [Trait("Requirement", "AD-16")]
    public async Task One_decision_applies_to_every_conflicting_Project_and_the_rename_reaches_the_rest(bool replaceProjectNames)
    {
        var database = Available();
        Actor? owner = null;
        Conflicted? seeded = null;
        WireResponse? renamed = null;
        IReadOnlyList<(Guid ProjectId, Guid StatusId, string Name)> opsBefore = [];
        IReadOnlyList<ProjectStatusEntry> intake = [];
        IReadOnlyList<ProjectStatusEntry> backlog = [];
        IReadOnlyList<ProjectStatusEntry> plain = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            seeded = await SeedConflictsAsync(owner, database);
            opsBefore = await Space.RenameOperationsAsync(database, owner.SpaceId);
            renamed = await Space.RenameAsync(actor, owner.SpaceId, seeded.Defaults[0].Id, "Queued", replaceProjectNames);
            intake = await Wire.EffectiveAsync(actor, owner.SpaceId, seeded.Intake);
            backlog = await Wire.EffectiveAsync(actor, owner.SpaceId, seeded.Backlog);
            plain = await Wire.EffectiveAsync(actor, owner.SpaceId, seeded.Plain);
        });

        Assert.NotNull(owner);
        Assert.NotNull(seeded);
        Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"{renamed?.StatusCode} {renamed?.Body}");
        Assert.Equal("Queued", SpaceStatusWire.DefaultsOf(renamed!.Body)[0].Name);
        Assert.Equal(("Queued", null), (plain[0].Name, plain[0].DefaultName));
        Assert.Equal("Underway", plain[1].Name);

        var opsAfter = await Space.RenameOperationsAsync(database, owner.SpaceId);
        var first = seeded.Defaults[0].Id;

        if (replaceProjectNames)
        {
            Assert.Equal(("Queued", null), (intake[0].Name, intake[0].DefaultName));
            Assert.Equal(("Queued", null), (backlog[0].Name, backlog[0].DefaultName));
            Assert.DoesNotContain(opsAfter, op => op.StatusId == first);
            Assert.Equal(opsBefore.Where(op => op.StatusId != first), opsAfter);
        }
        else
        {
            Assert.Equal(("Intake", "Queued"), (intake[0].Name, intake[0].DefaultName));
            Assert.Equal(("Backlog", "Queued"), (backlog[0].Name, backlog[0].DefaultName));
            Assert.Equal(opsBefore, opsAfter);
        }

        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Project whose own name for the first default equals its old Space name or the requested new one exactly is not a
    /// conflict, and neither is a Project that removed it: a rename to "Queued" with no decision is admitted, every one of
    /// them that holds it shows "Queued" with no default name and no <c>Rename</c> op of it is left, and the removing Project,
    /// adding it back, gets it under "Queued". A Project whose name differs only in case is still reported.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - no Space rename route yet (Q3)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "UX-DR16")]
    public async Task An_own_name_equal_to_the_old_or_new_name_or_a_removal_is_no_conflict_and_follows_the_rename()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? caseOnly = null;
        WireResponse? renamed = null;
        IReadOnlyList<ProjectStatusEntry> sameAsOld = [];
        IReadOnlyList<ProjectStatusEntry> sameAsNew = [];
        IReadOnlyList<ProjectStatusEntry> restored = [];
        var caseOnlyProject = Guid.Empty;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var oldId = IdOf(await CreateAsync(owner, "Named it back"));
            var newId = IdOf(await CreateAsync(owner, "Named it ahead"));
            var removingId = IdOf(await CreateAsync(owner, "Removed it"));

            caseOnlyProject = IdOf(await CreateAsync(owner, "Changed its case"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, oldId, defaults[0].Id, "Temporary")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, oldId, defaults[0].Id, defaults[0].Name)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, newId, defaults[0].Id, "Queued")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, caseOnlyProject, defaults[0].Id, defaults[0].Name.ToUpperInvariant())).StatusCode);
            _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, removingId, defaults[0].Id);

            caseOnly = await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Queued");
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, caseOnlyProject, defaults[0].Id, defaults[0].Name)).StatusCode);

            renamed = await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Queued");
            sameAsOld = await Wire.EffectiveAsync(actor, owner.SpaceId, oldId);
            sameAsNew = await Wire.EffectiveAsync(actor, owner.SpaceId, newId);

            var restore = await RemovalWire.RestoreAsync(actor, owner.SpaceId, removingId, defaults[0].Id);

            Assert.True(restore.StatusCode == HttpStatusCode.OK, $"{restore.StatusCode} {restore.Body}");
            restored = ProjectStatusWire.EntriesOf(restore.Body);
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(caseOnly, HttpStatusCode.Conflict, SpaceStatusWire.StatusRenameConflict);
        Assert.Equal([caseOnlyProject], SpaceStatusWire.ConflictsOf(caseOnly!.Body).Select(conflict => conflict.ProjectId));
        Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"{renamed?.StatusCode} {renamed?.Body}");
        Assert.Equal(("Queued", null), (sameAsOld[0].Name, sameAsOld[0].DefaultName));
        Assert.Equal(("Queued", null), (sameAsNew[0].Name, sameAsNew[0].DefaultName));
        Assert.Equal(("Queued", null), (restored[0].Name, restored[0].DefaultName));
        Assert.DoesNotContain(await Space.RenameOperationsAsync(database, owner.SpaceId), op => op.StatusId == defaults[0].Id);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Space write that would leave some Project's set holding a name twice is <c>409 status-name-taken</c> naming exactly
    /// those Projects, and writes nothing: an add of "parked" where one Project added "Parked", and a rename of the third
    /// default to "Review" where one Project renamed the second to "Review". A rename of a default to its own name in another
    /// case is admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - no Space add or rename route and no Space-wide name check yet (Q5)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "FR-25")]
    public async Task A_Space_add_or_rename_to_a_name_a_Projects_set_holds_is_refused_409_naming_those_Projects()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var parkedProject = Guid.Empty;
        var reviewProject = Guid.Empty;
        WireResponse? addClash = null;
        WireResponse? renameClash = null;
        WireResponse? caseChange = null;
        var deltaBefore = -1;
        var deltaAfterRefusals = -1;
        IReadOnlyList<StatusRow> afterRefusals = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            parkedProject = IdOf(await CreateAsync(owner, "Parks its work"));
            reviewProject = IdOf(await CreateAsync(owner, "Reviews its work"));
            _ = IdOf(await CreateAsync(owner, "Plain"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            _ = await Wire.AddedAsync(actor, owner.SpaceId, parkedProject, "Parked");
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, reviewProject, defaults[1].Id, "Review")).StatusCode);

            deltaBefore = await Space.SpaceDeltaCountAsync(database, owner.SpaceId);
            addClash = await Space.AddAsync(actor, owner.SpaceId, "parked");
            renameClash = await Space.RenameAsync(actor, owner.SpaceId, defaults[2].Id, "Review");
            deltaAfterRefusals = await Space.SpaceDeltaCountAsync(database, owner.SpaceId);
            afterRefusals = await Wire.DefaultsAsync(database, owner.SpaceId);
            caseChange = await Space.RenameAsync(actor, owner.SpaceId, defaults[2].Id, defaults[2].Name.ToUpperInvariant());
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(addClash, HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken);
        RemovalWire.AssertRefused(renameClash, HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken);
        Assert.Equal([(parkedProject, "Parks its work")], SpaceStatusWire.ProjectsOf(addClash!.Body));
        Assert.Equal([(reviewProject, "Reviews its work")], SpaceStatusWire.ProjectsOf(renameClash!.Body));
        Assert.Equal(defaults, afterRefusals);
        Assert.Equal(deltaBefore, deltaAfterRefusals);
        Assert.True(caseChange?.StatusCode == HttpStatusCode.OK, $"{caseChange?.StatusCode} {caseChange?.Body}");
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Project renamed the second default "Review" and the third "Mine"; a Space rename of the third to "Review" conflicts
    /// with "Mine". With <c>true</c> that Project's set would hold "Review" twice, so the rename is <c>409 status-name-taken</c>
    /// naming it and nothing changes; with <c>false</c> its own "Mine" remains, so the rename is admitted and its set holds
    /// "Review" once.
    /// </summary>
    /// <param name="replaceProjectNames">The decision.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC6, AC5 - no Space rename route and no Space-wide name check yet (Q3, Q5)")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "FR-25")]
    public async Task A_clash_through_a_conflicting_Projects_name_depends_on_the_decision(bool replaceProjectNames)
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var projectId = Guid.Empty;
        WireResponse? renamed = null;
        IReadOnlyList<ProjectStatusEntry> set = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Named two of them"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[1].Id, "Review")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[2].Id, "Mine")).StatusCode);

            renamed = await Space.RenameAsync(actor, owner.SpaceId, defaults[2].Id, "Review", replaceProjectNames);
            set = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
            await Space.AssertNoSetHoldsANameTwiceAsync(actor, owner.SpaceId, [projectId]);
        });

        Assert.NotNull(owner);

        var after = await Wire.DefaultsAsync(database, owner.SpaceId);

        if (replaceProjectNames)
        {
            RemovalWire.AssertRefused(renamed, HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken);
            Assert.Equal([projectId], SpaceStatusWire.ProjectsOf(renamed!.Body).Select(project => project.Id));
            Assert.Equal(defaults[2].Name, after[2].Name);
            Assert.Equal("Mine", set[2].Name);
        }
        else
        {
            Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"{renamed?.StatusCode} {renamed?.Body}");
            Assert.Equal("Review", after[2].Name);
            Assert.Equal(("Mine", "Review"), (set[2].Name, set[2].DefaultName));
        }

        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Names are trimmed and 1 to 100 characters, and unique among the defaults ignoring case: a blank, a 101-character or an
    /// empty rename is <c>400 invalid-status-name</c>, as is an unreadable add or rename body; an unreadable place body is
    /// <c>400 invalid-status-position</c>; another default's name in another case is <c>409 status-name-taken</c> naming no
    /// Project. Nothing is written by a refusal; 100 characters is admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - no Space add, rename or place route yet (Q1)")]
    [Trait("Requirement", "FR-24")]
    [Trait("Requirement", "AR-34")]
    public async Task A_blank_overlong_unreadable_or_default_held_name_is_refused_and_100_characters_is_admitted()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var refusals = new List<(WireResponse Answer, HttpStatusCode Status, string Type)>();
        IReadOnlyList<StatusRow> afterRefusals = [];
        WireResponse? longest = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            refusals.Add((await Space.AddAsync(actor, owner.SpaceId, "   "), HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusName));
            refusals.Add((await Space.AddAsync(actor, owner.SpaceId, new string('n', 101)), HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusName));
            refusals.Add((await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, string.Empty), HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusName));
            refusals.Add((await Space.RawAsync(actor, HttpMethod.Post, SpaceRoutes.StatusesOf(owner.SpaceId), "{ not json"), HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusName));
            refusals.Add((await Space.RawAsync(actor, HttpMethod.Patch, SpaceStatusWire.ItemOf(owner.SpaceId, defaults[0].Id), "[]"), HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusName));
            refusals.Add((await Space.RawAsync(actor, HttpMethod.Post, SpaceStatusWire.PlaceOf(owner.SpaceId, defaults[0].Id), "{ not json"), HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusPosition));
            refusals.Add((await Space.AddAsync(actor, owner.SpaceId, defaults[1].Name.ToUpperInvariant()), HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken));
            refusals.Add((await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, defaults[2].Name.ToUpperInvariant()), HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken));

            afterRefusals = await Wire.DefaultsAsync(database, owner.SpaceId);
            longest = await Space.AddAsync(actor, owner.SpaceId, new string('n', 100));
        });

        Assert.NotNull(owner);

        foreach (var (answer, status, type) in refusals)
        {
            RemovalWire.AssertRefused(answer, status, type);

            if (type == ProblemTypes.StatusNameTaken)
            {
                Assert.Empty(SpaceStatusWire.ProjectsOf(answer.Body));
            }
        }

        Assert.Equal(defaults, afterRefusals);
        Assert.True(longest?.StatusCode == HttpStatusCode.Created, $"{longest?.StatusCode} {longest?.Body}");
    }

    /// <summary>
    /// Placing the third default first rewrites the defaults' positions to the new order, 0 to 2, and writes no Project row: a
    /// plain Project's set, its List's Status order and its next Task's Status follow it, while a Project that placed the third
    /// after the first keeps its own placement.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8, AC15 - no Space place route yet (Q9)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "FR-30")]
    [Trait("Requirement", "AD-16")]
    public async Task Reordering_a_default_moves_it_in_every_Project_that_has_not_placed_it()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? placed = null;
        var deltaBefore = -1;
        var deltaAfter = -1;
        IReadOnlyList<ProjectStatusEntry> plain = [];
        IReadOnlyList<ProjectStatusEntry> ownPlace = [];
        IReadOnlyList<Guid> listOrder = [];
        var created = Guid.Empty;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var plainId = IdOf(await CreateAsync(owner, "Follows the order"));
            var placedId = IdOf(await CreateAsync(owner, "Keeps its own order"));

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, placedId, defaults[2].Id, defaults[0].Id)).StatusCode);

            deltaBefore = await Space.SpaceDeltaCountAsync(database, owner.SpaceId);
            placed = await Space.PlaceAsync(actor, owner.SpaceId, defaults[2].Id, afterStatusId: null);
            deltaAfter = await Space.SpaceDeltaCountAsync(database, owner.SpaceId);
            plain = await Wire.EffectiveAsync(actor, owner.SpaceId, plainId);
            ownPlace = await Wire.EffectiveAsync(actor, owner.SpaceId, placedId);
            created = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, plainId, "Lands in the new first"));
            listOrder = ListWire.PageOf(await ListWire.PageAsync(actor, owner.SpaceId, plainId, new SliceListQuery(ListSorts.Status, [], []))).StatusIds;
        });

        Assert.NotNull(owner);
        Assert.True(placed?.StatusCode == HttpStatusCode.OK, $"{placed?.StatusCode} {placed?.Body}");

        Guid[] spaceOrder = [defaults[2].Id, defaults[0].Id, defaults[1].Id];

        Assert.Equal(spaceOrder, SpaceStatusWire.DefaultsOf(placed!.Body).Select(row => row.Id));
        Assert.Equal([0, 1, 2], (await Wire.DefaultsAsync(database, owner.SpaceId)).Select(row => row.Position));
        Assert.Equal(spaceOrder, plain.Select(entry => entry.Id));
        Assert.Equal([defaults[0].Id, defaults[2].Id, defaults[1].Id], ownPlace.Select(entry => entry.Id));
        Assert.Equal(spaceOrder, listOrder);
        Assert.Equal(defaults[2].Id, (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == created).StatusId);
        Assert.Equal(deltaBefore, deltaAfter);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A place anchor that is not another default of this Space - another Space's default, a Project's added Status, the
    /// Status itself - is <c>400 invalid-status-position</c>; a Project's added Status in the path of a rename or a place is the
    /// in-Space <c>404</c>, byte-identical to a random id's. The defaults are unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8, AC12 - no Space rename or place route yet (Q1)")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "FR-20")]
    public async Task A_place_anchor_that_is_not_a_default_is_400_and_a_Project_added_Status_in_the_path_is_404()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var anchors = new List<WireResponse>();
        var paths = new List<(WireResponse Added, WireResponse Random)>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Added one"));
            var added = await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked");
            var foreign = (await Wire.DefaultsAsync(database, other.SpaceId))[0].Id;

            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            foreach (var anchor in new[] { foreign, added, defaults[1].Id })
            {
                anchors.Add(await Space.PlaceAsync(actor, owner.SpaceId, defaults[1].Id, anchor));
            }

            paths.Add((await Space.RenameAsync(actor, owner.SpaceId, added, "Taken over"), await Space.RenameAsync(actor, owner.SpaceId, Guid.NewGuid(), "Taken over")));
            paths.Add((await Space.PlaceAsync(actor, owner.SpaceId, added, afterStatusId: null), await Space.PlaceAsync(actor, owner.SpaceId, Guid.NewGuid(), afterStatusId: null)));
        });

        Assert.NotNull(owner);
        Assert.Equal(3, anchors.Count);
        Assert.All(anchors, answer => RemovalWire.AssertRefused(answer, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusPosition));
        Assert.All(paths, pair => AssertSameNotAvailable(pair.Added, pair.Random));
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Equal(defaults, await Wire.DefaultsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// A Member and a Viewer are each refused every Space default write - add, rename, place - on their Session and on their
    /// Token: <c>403 insufficient-role</c> naming <c>configure-space-default-statuses</c>, six refusals recorded as
    /// <c>InsufficientRole</c>, nothing written. Anchor: the same caller reads the Space's Statuses.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC2 - no Space write route declares ConfigureSpaceDefaultStatuses yet")]
    [InlineData("Member")]
    [InlineData("Viewer")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-7")]
    public async Task A_Member_or_a_Viewer_is_refused_403_on_every_Space_default_write_on_both_surfaces(string role)
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

            refused.Add(await Space.AddAsync(actor, owner.SpaceId, "Review"));
            refused.Add(await Space.AddWithTokenAsync(actor, token, owner.SpaceId, "Review"));
            refused.Add(await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Queued"));
            refused.Add(await Space.RenameWithTokenAsync(actor, token, owner.SpaceId, defaults[0].Id, "Queued"));
            refused.Add(await Space.PlaceAsync(actor, owner.SpaceId, defaults[2].Id, afterStatusId: null));
            refused.Add(await Space.PlaceWithTokenAsync(actor, token, owner.SpaceId, defaults[2].Id, afterStatusId: null));
            anchor = await actor.Session.GetWithTokenAsync(SpaceRoutes.StatusesOf(owner.SpaceId), token);
        });

        Assert.NotNull(owner);
        Assert.NotNull(caller);
        Assert.Equal(6, refused.Count);
        Assert.All(refused, answer =>
        {
            Assert.True(answer.StatusCode == HttpStatusCode.Forbidden, $"{answer.StatusCode} {answer.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(answer)?.Type);
            Assert.Equal(ConfigureDefaultsCapability, Problem(answer)?.Capability);
        });
        Assert.Equal(Enumerable.Repeat(InsufficientRoleKind, 6), await RefusalKindsAsync(database, caller.AccountId, owner.SpaceId));
        Assert.Equal(defaults, await Wire.DefaultsAsync(database, owner.SpaceId));
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// An Admin holds the capability as the Owner does: its add, rename and place of a default are each admitted, on its
    /// Session and on its Token.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - no Space write route yet")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-24")]
    public async Task An_Admin_adds_renames_and_places_a_default_on_both_surfaces()
    {
        var database = Available();
        Actor? owner = null;
        var answers = new List<(WireResponse Answer, HttpStatusCode Expected)>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var admin = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, admin.AccountId, "Admin");

            var actor = AsTaskActor(admin);
            var token = await IssueTokenAsync(admin, owner.SpaceId);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            answers.Add((await Space.AddAsync(actor, owner.SpaceId, "Review"), HttpStatusCode.Created));
            answers.Add((await Space.AddWithTokenAsync(actor, token, owner.SpaceId, "Parked"), HttpStatusCode.Created));
            answers.Add((await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Queued"), HttpStatusCode.OK));
            answers.Add((await Space.RenameWithTokenAsync(actor, token, owner.SpaceId, defaults[1].Id, "Underway"), HttpStatusCode.OK));
            answers.Add((await Space.PlaceAsync(actor, owner.SpaceId, defaults[2].Id, afterStatusId: null), HttpStatusCode.OK));
            answers.Add((await Space.PlaceWithTokenAsync(actor, token, owner.SpaceId, defaults[0].Id, afterStatusId: null), HttpStatusCode.OK));
        });

        Assert.NotNull(owner);
        Assert.All(answers, pair => Assert.True(pair.Answer.StatusCode == pair.Expected, $"{pair.Answer.StatusCode} {pair.Answer.Body}"));
        Assert.Equal(5, (await Wire.DefaultsAsync(database, owner.SpaceId)).Count);
    }

    /// <summary>
    /// Another Space's path (its id, its default) answers the boundary <c>404</c>, byte-identical to a Space that does not
    /// exist, and is recorded as <c>CrossSpace</c>; another Space's default in the caller's own Space path answers the in-Space
    /// <c>404</c>, byte-identical to a random id's, and records nothing. Neither Space's defaults change.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - no Space rename or place route yet")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task Another_Spaces_path_or_default_answers_the_404_byte_identical()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        IReadOnlyList<StatusRow> theirs = [];
        IReadOnlyList<StatusRow> ours = [];
        var foreignPaths = new List<(WireResponse Foreign, WireResponse Absent)>();
        var inSpace = new List<(WireResponse Foreign, WireResponse Random)>();
        IReadOnlyList<string> inSpaceRefusals = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            theirs = await Wire.DefaultsAsync(database, other.SpaceId);
            ours = await Wire.DefaultsAsync(database, owner.SpaceId);

            inSpace.Add((await Space.RenameAsync(actor, owner.SpaceId, theirs[0].Id, "Taken over"), await Space.RenameAsync(actor, owner.SpaceId, Guid.NewGuid(), "Taken over")));
            inSpace.Add((await Space.PlaceAsync(actor, owner.SpaceId, theirs[2].Id, afterStatusId: null), await Space.PlaceAsync(actor, owner.SpaceId, Guid.NewGuid(), afterStatusId: null)));
            inSpaceRefusals = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);

            foreignPaths.Add((await Space.AddAsync(actor, other.SpaceId, "Taken over"), await Space.AddAsync(actor, Guid.NewGuid(), "Taken over")));
            foreignPaths.Add((await Space.RenameAsync(actor, other.SpaceId, theirs[0].Id, "Taken over"), await Space.RenameAsync(actor, Guid.NewGuid(), theirs[0].Id, "Taken over")));
            foreignPaths.Add((await Space.PlaceAsync(actor, other.SpaceId, theirs[2].Id, afterStatusId: null), await Space.PlaceAsync(actor, Guid.NewGuid(), theirs[2].Id, afterStatusId: null)));
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);
        Assert.All(inSpace, pair => AssertSameNotAvailable(pair.Foreign, pair.Random));
        Assert.All(foreignPaths, pair => AssertSameNotAvailable(pair.Foreign, pair.Absent));
        Assert.Empty(inSpaceRefusals);
        Assert.Equal(Enumerable.Repeat(CrossSpaceKind, 3), await RefusalKindsAsync(database, owner.AccountId, other.SpaceId));
        Assert.Equal(theirs, await Wire.DefaultsAsync(database, other.SpaceId));
        Assert.Equal(ours, await Wire.DefaultsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// Another Space's Project renamed its own first default - the same Space name - and added "Parked". The caller's rename of
    /// its first default reports only its own conflicting Project, and nothing of the other's id or names; the caller's add of
    /// "Parked" is admitted, because no Project of the caller's Space holds it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12, AC4, AC6 - no Space rename or add route yet (Q2, Q5)")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "FR-27")]
    public async Task A_foreign_Project_is_never_named_in_a_conflict_report_or_a_clash_list()
    {
        var database = Available();
        Actor? owner = null;
        var ownProject = Guid.Empty;
        var foreignProject = Guid.Empty;
        WireResponse? report = null;
        WireResponse? add = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var otherActor = AsTaskActor(other);
            var ours = await Wire.DefaultsAsync(database, owner.SpaceId);
            var theirs = await Wire.DefaultsAsync(database, other.SpaceId);

            Assert.Equal(ours[0].Name, theirs[0].Name);

            ownProject = IdOf(await CreateAsync(owner, "Ours renamed it"));
            foreignProject = IdOf(await CreateAsync(other, "Theirs renamed it too"));

            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, ownProject, ours[0].Id, "Mine")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(otherActor, other.SpaceId, foreignProject, theirs[0].Id, "Theirs")).StatusCode);
            _ = await Wire.AddedAsync(otherActor, other.SpaceId, foreignProject, "Parked");

            report = await Space.RenameAsync(actor, owner.SpaceId, ours[0].Id, "Queued");
            add = await Space.AddAsync(actor, owner.SpaceId, "Parked");
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(report, HttpStatusCode.Conflict, SpaceStatusWire.StatusRenameConflict);
        Assert.Equal([ownProject], SpaceStatusWire.ConflictsOf(report!.Body).Select(conflict => conflict.ProjectId));
        Assert.DoesNotContain(foreignProject.ToString("D"), report.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Theirs", report.Body, StringComparison.Ordinal);
        Assert.True(add?.StatusCode == HttpStatusCode.Created, $"{add?.StatusCode} {add?.Body}");
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Each write sent twice with one <c>Idempotency-Key</c> answers the original status and body and applies once: one
    /// "Review", one order. A cascading rename replayed after the Project renamed the default again answers the original
    /// <c>200</c> and deletes nothing a second time: the Project's new op survives.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC14 - no Space add, rename or place route yet")]
    [Trait("Requirement", "AD-18")]
    public async Task A_replayed_write_answers_the_original_and_applies_once_and_a_replayed_cascade_deletes_nothing_again()
    {
        var database = Available();
        Actor? owner = null;
        var pairs = new List<(WireResponse Original, WireResponse Replay)>();
        var projectId = Guid.Empty;
        IReadOnlyList<ProjectStatusEntry> afterReplay = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            var addKey = Guid.NewGuid().ToString("D");
            var placeKey = Guid.NewGuid().ToString("D");
            var renameKey = Guid.NewGuid().ToString("D");

            projectId = IdOf(await CreateAsync(owner, "Renamed twice"));
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[0].Id, "Intake")).StatusCode);

            pairs.Add((await Space.AddAsync(actor, owner.SpaceId, "Review", key: addKey), await Space.AddAsync(actor, owner.SpaceId, "Review", key: addKey)));
            pairs.Add((await Space.PlaceAsync(actor, owner.SpaceId, defaults[2].Id, afterStatusId: null, key: placeKey), await Space.PlaceAsync(actor, owner.SpaceId, defaults[2].Id, afterStatusId: null, key: placeKey)));

            var cascade = await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Queued", replaceProjectNames: true, key: renameKey);

            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[0].Id, "Intake again")).StatusCode);
            pairs.Add((cascade, await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Queued", replaceProjectNames: true, key: renameKey)));
            afterReplay = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.Equal(3, pairs.Count);
        Assert.Equal(HttpStatusCode.Created, pairs[0].Original.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pairs[1].Original.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pairs[2].Original.StatusCode);
        Assert.All(pairs, pair =>
        {
            Assert.Equal(pair.Original.StatusCode, pair.Replay.StatusCode);
            Assert.Equal(pair.Original.Body, pair.Replay.Body);
        });
        Assert.Single(await Wire.DefinitionsAsync(database, owner.SpaceId), row => row.Name == "Review");
        Assert.Equal(("Intake again", "Queued"), (afterReplay[0].Name, afterReplay[0].DefaultName));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A cascading rename is one transaction: with a failure forced at whichever of its two statements - the op deletes and
    /// the default's rename - runs second, it answers <c>5xx</c>, the default keeps its name and the Project keeps its
    /// <c>Rename</c> op and its own name. Anchored on the same rename answering <c>200</c> once the triggers are dropped, with
    /// the op gone and the Project showing the new name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - no Space rename route and no cascade yet (Q10)")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-27")]
    public async Task A_failure_forced_inside_a_cascading_rename_rolls_both_statements_back()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<StatusRow> defaults = [];
        var projectId = Guid.Empty;
        WireResponse? forced = null;
        WireResponse? anchor = null;
        IReadOnlyList<(Guid ProjectId, Guid StatusId, string Name)> opsBefore = [];
        IReadOnlyList<(Guid ProjectId, Guid StatusId, string Name)> opsAfterFailure = [];
        IReadOnlyList<StatusRow> defaultsAfterFailure = [];
        IReadOnlyList<ProjectStatusEntry> setAfterFailure = [];
        IReadOnlyList<ProjectStatusEntry> setAfterAnchor = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Rolled back"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Held in the first"));
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[0].Id, "Intake")).StatusCode);

            opsBefore = await Space.RenameOperationsAsync(database, owner.SpaceId);

            var triggers = await Space.PlantFailingCascadeTriggersAsync(database, projectId, defaults[0].Id, "Queued");

            try
            {
                forced = await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Queued", replaceProjectNames: true);
            }
            finally
            {
                await Space.DropTriggersAsync(database, triggers);
            }

            opsAfterFailure = await Space.RenameOperationsAsync(database, owner.SpaceId);
            defaultsAfterFailure = await Wire.DefaultsAsync(database, owner.SpaceId);
            setAfterFailure = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
            anchor = await Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Queued", replaceProjectNames: true);
            setAfterAnchor = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(forced);
        Assert.True(forced.StatusCode is >= HttpStatusCode.InternalServerError, $"{forced.StatusCode} {forced.Body}");
        Assert.Single(opsBefore);
        Assert.Equal(opsBefore, opsAfterFailure);
        Assert.Equal(defaults, defaultsAfterFailure);
        Assert.Equal(("Intake", defaults[0].Name), (setAfterFailure[0].Name, setAfterFailure[0].DefaultName));
        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"{anchor?.StatusCode} {anchor?.Body}");
        Assert.Empty(await Space.RenameOperationsAsync(database, owner.SpaceId));
        Assert.Equal(("Queued", null), (setAfterAnchor[0].Name, setAfterAnchor[0].DefaultName));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Three Projects through the product: "Intake project" renamed the first default "Intake" and holds two Tasks in it,
    /// "Backlog project" renamed it "Backlog" and holds none, and "Plain" renamed only the second default "Underway".
    /// </summary>
    private static async Task<Conflicted> SeedConflictsAsync(Actor owner, Yello.Tests.Slices.Accounts.RegisterAccount.RegistrationDatabase database)
    {
        var actor = AsTaskActor(owner);
        var intake = IdOf(await CreateAsync(owner, "Intake project"));
        var backlog = IdOf(await CreateAsync(owner, "Backlog project"));
        var plain = IdOf(await CreateAsync(owner, "Plain"));
        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

        _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, intake, "First held"));
        _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, intake, "Second held"));

        Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, intake, defaults[0].Id, "Intake")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, backlog, defaults[0].Id, "Backlog")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, plain, defaults[1].Id, "Underway")).StatusCode);

        return new Conflicted(defaults, intake, backlog, plain);
    }

    /// <summary>
    /// The Space and Projects <see cref="SeedConflictsAsync"/> leaves.
    /// </summary>
    /// <param name="Defaults">The Space's defaults before any Space write.</param>
    /// <param name="Intake">The Project that renamed the first default "Intake" and holds two Tasks in it.</param>
    /// <param name="Backlog">The Project that renamed it "Backlog".</param>
    /// <param name="Plain">The Project that renamed only the second default.</param>
    private sealed record Conflicted(IReadOnlyList<StatusRow> Defaults, Guid Intake, Guid Backlog, Guid Plain);
}
