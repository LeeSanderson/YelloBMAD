using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Contracts.Lists;
using Yello.Contracts.Projects;
using Yello.Tests.Shared;
using ListWire = Yello.Tests.Slices.Lists.ListWire;
using SliceListQuery = Yello.Tests.Slices.Lists.ListQuery;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// A Project's Status delta over the wire: the read of its effective set, and add, rename and place, on both surfaces,
/// through the pipeline that already resolves the Space, checks <c>configure-project-status-delta</c> and replays keys - and
/// every consumer of the effective set (the Board, its column page, the List, Task creation) following the Project's own set.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded against the recommended options</b>: open question 1 (a) (an owned <c>StatusDefinition</c> plus a delta of
/// <c>Add</c>, <c>Rename</c> and <c>Place</c> operations), 5 (a) (the entry <c>{ defaultName, id, name, source, taskCount }</c>),
/// 6 (a) (case-insensitive, trimmed, 1 to 100), 12 (a) (the four routes). An add answers <c>201</c>, rename and place
/// <c>200</c>, each with the whole effective set. A skip that rests on one of them names it.
/// </para>
/// <para>
/// <b>Every case ends with S-2</b>: no Task of the Space holds a Status outside its own Project's set.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private const string ConfigureCapability = "configure-project-status-delta";

    /// <summary>
    /// An Owner adds "Parked": <c>201</c>, the answer is the whole set - the three Space defaults in Space order marked
    /// <c>space-default</c>, then "Parked" marked <c>added-here</c> with no default name and no Tasks - each entry exactly the
    /// five members, and the read answers the same set; the Board gains a fourth column, and the Space's own read still holds
    /// its three defaults.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "UX-DR15")]
    public async Task Adding_a_Status_appends_it_as_added_here_and_the_Board_gains_its_column()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? added = null;
        WireResponse? read = null;
        IReadOnlyList<(Guid StatusId, string Name)> columns = [];
        IReadOnlyList<(Guid Id, string Name)> spaceRead = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Harbour"));

            added = await Wire.AddAsync(actor, owner.SpaceId, projectId, "  Parked  ");
            read = await Wire.ReadAsync(actor, owner.SpaceId, projectId);
            columns = await Wire.BoardColumnsAsync(actor, owner.SpaceId, projectId);
            spaceRead = await Wire.SpaceReadAsync(actor, owner.SpaceId);
        });

        Assert.NotNull(owner);
        Assert.True(added?.StatusCode == HttpStatusCode.Created, $"{added?.StatusCode} {added?.Body}");
        Assert.Equal(HttpStatusCode.OK, read?.StatusCode);
        Assert.Equal(read!.Body, added!.Body);

        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
        var entries = ProjectStatusWire.EntriesOf(added.Body);

        Assert.Equal(4, entries.Count);
        Assert.All(entries, entry => Assert.Equal(ProjectStatusWire.EntryMembers, entry.Members));
        Assert.Equal(defaults.Select(row => row.Id), entries.Take(3).Select(entry => entry.Id));
        Assert.All(entries.Take(3), entry => Assert.Equal(ProjectStatusSources.SpaceDefault, entry.Source));
        Assert.Equal(("Parked", ProjectStatusSources.AddedHere, 0), (entries[3].Name, entries[3].Source, entries[3].TaskCount));
        Assert.Null(entries[3].DefaultName);
        Assert.Equal(entries.Select(entry => (entry.Id, entry.Name)), columns);
        Assert.Equal(defaults.Select(row => (row.Id, row.Name)), spaceRead);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// An add that names <c>afterStatusId</c> lands right after that Status in one write: added after the first default, the
    /// set is the first default, the new Status, then the other two.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public async Task Adding_after_a_default_places_the_new_Status_right_after_it()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? added = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var projectId = IdOf(await CreateAsync(owner, "Atlas"));
            var first = (await Wire.DefaultsAsync(database, owner.SpaceId))[0].Id;

            added = await Wire.AddAfterAsync(AsTaskActor(owner), owner.SpaceId, projectId, "Triage", first);
        });

        Assert.NotNull(owner);
        Assert.True(added?.StatusCode == HttpStatusCode.Created, $"{added?.StatusCode} {added?.Body}");

        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

        Assert.Equal(
            [defaults[0].Name, "Triage", defaults[1].Name, defaults[2].Name],
            ProjectStatusWire.EntriesOf(added!.Body).Select(entry => entry.Name));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Project renames a Space default: <c>200</c>; the entry keeps the default's id, takes the new name, stays
    /// <c>space-default</c> and states the default's own name; the Space's read still answers the original name and id; the
    /// Board column has the new name under the same id; the Task in it keeps its <c>StatusId</c>; the column page for that id
    /// still answers; and a sibling Project still shows the original name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "AR-23")]
    public async Task Renaming_a_default_in_a_Project_keeps_its_id_and_leaves_the_Space_default_as_it_was()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        Guid? statusBefore = null;
        WireResponse? renamed = null;
        WireResponse? columnPage = null;
        IReadOnlyList<(Guid StatusId, string Name)> columns = [];
        IReadOnlyList<(Guid StatusId, string Name)> siblingColumns = [];
        IReadOnlyList<(Guid Id, string Name)> spaceRead = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Renamed here"));
            var sibling = IdOf(await CreateAsync(owner, "Left alone"));
            var first = (await Wire.DefaultsAsync(database, owner.SpaceId))[0].Id;

            taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Waiting"));
            statusBefore = (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == taskId).StatusId;
            renamed = await Wire.RenameAsync(actor, owner.SpaceId, projectId, first, "  Intake  ");
            columns = await Wire.BoardColumnsAsync(actor, owner.SpaceId, projectId);
            siblingColumns = await Wire.BoardColumnsAsync(actor, owner.SpaceId, sibling);
            spaceRead = await Wire.SpaceReadAsync(actor, owner.SpaceId);
            columnPage = await owner.Session.GetAsync(BoardRoutes.ColumnOf(owner.SpaceId, projectId, first));
        });

        Assert.NotNull(owner);
        Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"{renamed?.StatusCode} {renamed?.Body}");

        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
        var entry = ProjectStatusWire.EntriesOf(renamed!.Body)[0];

        Assert.Equal((defaults[0].Id, "Intake", ProjectStatusSources.SpaceDefault), (entry.Id, entry.Name, entry.Source));
        Assert.Equal(defaults[0].Name, entry.DefaultName);
        Assert.Equal(defaults.Select(row => (row.Id, row.Name)), spaceRead);
        Assert.Equal((defaults[0].Id, "Intake"), columns[0]);
        Assert.Equal((defaults[0].Id, defaults[0].Name), siblingColumns[0]);
        Assert.Equal(statusBefore, (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Single(row => row.Id == taskId).StatusId);
        Assert.Equal(HttpStatusCode.OK, columnPage?.StatusCode);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Renaming a Status the Project added keeps its id and source, changes its effective name, and leaves the owned
    /// definition's own name as it was added (the rename is an operation, uniform for both kinds of Status).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-16")]
    public async Task Renaming_an_added_Status_changes_its_effective_name_and_keeps_its_id()
    {
        var database = Available();
        Actor? owner = null;
        var parked = Guid.Empty;
        WireResponse? renamed = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Beacon"));

            parked = await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked");
            renamed = await Wire.RenameAsync(actor, owner.SpaceId, projectId, parked, "On ice");
        });

        Assert.NotNull(owner);
        Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"{renamed?.StatusCode} {renamed?.Body}");

        var entry = ProjectStatusWire.EntriesOf(renamed!.Body)[^1];

        Assert.Equal((parked, "On ice", ProjectStatusSources.AddedHere), (entry.Id, entry.Name, entry.Source));
        Assert.Equal("Parked", (await Wire.DefinitionsAsync(database, owner.SpaceId)).Single(row => row.Id == parked).Name);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Placing an added Status first, then placing the third default after it, reorders the effective set; the Board's
    /// columns and the List page's Status order both follow it, and the Space's own read keeps Space order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "FR-30")]
    [Trait("Requirement", "AD-16")]
    public async Task Placing_first_and_placing_after_reorders_the_Board_and_the_List()
    {
        var database = Available();
        Actor? owner = null;
        var parked = Guid.Empty;
        WireResponse? first = null;
        WireResponse? after = null;
        IReadOnlyList<(Guid StatusId, string Name)> columns = [];
        IReadOnlyList<Guid> listOrder = [];
        IReadOnlyList<(Guid Id, string Name)> spaceRead = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Reordered"));
            var third = (await Wire.DefaultsAsync(database, owner.SpaceId))[2].Id;

            parked = await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked");
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Listed"));
            first = await Wire.PlaceAsync(actor, owner.SpaceId, projectId, parked, afterStatusId: null);
            after = await Wire.PlaceAsync(actor, owner.SpaceId, projectId, third, parked);
            columns = await Wire.BoardColumnsAsync(actor, owner.SpaceId, projectId);
            listOrder = ListWire.PageOf(await ListWire.PageAsync(actor, owner.SpaceId, projectId, new SliceListQuery(ListSorts.Status, [], []))).StatusIds;
            spaceRead = await Wire.SpaceReadAsync(actor, owner.SpaceId);
        });

        Assert.NotNull(owner);
        Assert.True(first?.StatusCode == HttpStatusCode.OK, $"{first?.StatusCode} {first?.Body}");
        Assert.True(after?.StatusCode == HttpStatusCode.OK, $"{after?.StatusCode} {after?.Body}");

        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
        Guid[] expected = [parked, defaults[2].Id, defaults[0].Id, defaults[1].Id];

        Assert.Equal(expected, ProjectStatusWire.EntriesOf(after!.Body).Select(entry => entry.Id));
        Assert.Equal(expected, columns.Select(column => column.StatusId));
        Assert.Equal(expected, listOrder);
        Assert.Equal(defaults.Select(row => row.Id), spaceRead.Select(row => row.Id));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Task creation takes the first Status of its own Project's set: after the third default is placed first in one
    /// Project, a Task created there holds it, while a Task created in a sibling Project still holds the first default.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-17")]
    public async Task Task_creation_takes_the_first_Status_of_its_own_Projects_set()
    {
        var database = Available();
        Actor? owner = null;
        var shaped = Guid.Empty;
        var plain = Guid.Empty;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var projectId = IdOf(await CreateAsync(owner, "Starts at the end"));
            var sibling = IdOf(await CreateAsync(owner, "Starts at the start"));
            var third = (await Wire.DefaultsAsync(database, owner.SpaceId))[2].Id;

            Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, projectId, third, afterStatusId: null)).StatusCode);
            shaped = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Lands first"));
            plain = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, sibling, "Lands as before"));
        });

        Assert.NotNull(owner);

        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
        var rows = await TaskWire.TaskRowsAsync(database, owner.SpaceId);

        Assert.Equal(defaults[2].Id, rows.Single(row => row.Id == shaped).StatusId);
        Assert.Equal(defaults[0].Id, rows.Single(row => row.Id == plain).StatusId);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Two Projects with different deltas hold both sets at once: P1 adds "Parked" and P2 renames the second default. P1's
    /// Board has "Parked" and the default's own name; P2's Board has the rename and no "Parked"; and P2's column page for
    /// "Parked" answers <c>404</c> byte-identical to a random Status id's, while P1's answers <c>200</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AD-17")]
    public async Task Two_Projects_hold_different_sets_at_once()
    {
        var database = Available();
        Actor? owner = null;
        var parked = Guid.Empty;
        IReadOnlyList<(Guid StatusId, string Name)> first = [];
        IReadOnlyList<(Guid StatusId, string Name)> second = [];
        WireResponse? ownPage = null;
        WireResponse? siblingPage = null;
        WireResponse? randomPage = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var p1 = IdOf(await CreateAsync(owner, "P1"));
            var p2 = IdOf(await CreateAsync(owner, "P2"));
            var middle = (await Wire.DefaultsAsync(database, owner.SpaceId))[1].Id;

            parked = await Wire.AddedAsync(actor, owner.SpaceId, p1, "Parked");
            Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, p2, middle, "Underway")).StatusCode);

            first = await Wire.BoardColumnsAsync(actor, owner.SpaceId, p1);
            second = await Wire.BoardColumnsAsync(actor, owner.SpaceId, p2);
            ownPage = await owner.Session.GetAsync(BoardRoutes.ColumnOf(owner.SpaceId, p1, parked));
            siblingPage = await owner.Session.GetAsync(BoardRoutes.ColumnOf(owner.SpaceId, p2, parked));
            randomPage = await owner.Session.GetAsync(BoardRoutes.ColumnOf(owner.SpaceId, p2, Guid.NewGuid()));
        });

        Assert.NotNull(owner);

        var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

        Assert.Equal([.. defaults.Select(row => (row.Id, row.Name)), (parked, "Parked")], first);
        Assert.Equal([(defaults[0].Id, defaults[0].Name), (defaults[1].Id, "Underway"), (defaults[2].Id, defaults[2].Name)], second);
        Assert.Equal(HttpStatusCode.OK, ownPage?.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, siblingPage?.StatusCode);
        Assert.Equal(randomPage?.StatusCode, siblingPage?.StatusCode);
        Assert.Equal(randomPage?.ContentType, siblingPage?.ContentType);
        Assert.Equal(randomPage?.Body, siblingPage?.Body);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Names are trimmed, 1 to 100 characters and unique within the Project's set, compared case-insensitively: adding the
    /// first default's name in lower case, or renaming an added Status to the second default's name in upper case, is
    /// <c>409 status-name-taken</c>; a blank or 101-character name is <c>400 invalid-status-name</c>; nothing is written by a
    /// refusal. Renaming a Status to its own name in another case is admitted, and 100 characters is admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    public async Task A_name_the_set_holds_or_a_blank_or_overlong_name_is_refused_and_a_change_of_case_is_admitted()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var refusals = new List<(WireResponse Answer, string Type)>();
        var deltaAfterRefusals = -1;
        WireResponse? ownNameInCase = null;
        WireResponse? longest = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            projectId = IdOf(await CreateAsync(owner, "Named"));

            var parked = await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked");
            var before = await Wire.DeltaCountAsync(database, owner.SpaceId, projectId);

            refusals.Add((await Wire.AddAsync(actor, owner.SpaceId, projectId, defaults[0].Name.ToUpperInvariant()), ProblemTypes.StatusNameTaken));
            refusals.Add((await Wire.AddAsync(actor, owner.SpaceId, projectId, " parked "), ProblemTypes.StatusNameTaken));
            refusals.Add((await Wire.RenameAsync(actor, owner.SpaceId, projectId, parked, defaults[1].Name.ToUpperInvariant()), ProblemTypes.StatusNameTaken));
            refusals.Add((await Wire.AddAsync(actor, owner.SpaceId, projectId, "   "), ProblemTypes.InvalidStatusName));
            refusals.Add((await Wire.AddAsync(actor, owner.SpaceId, projectId, new string('n', 101)), ProblemTypes.InvalidStatusName));
            refusals.Add((await Wire.RenameAsync(actor, owner.SpaceId, projectId, parked, string.Empty), ProblemTypes.InvalidStatusName));

            deltaAfterRefusals = await Wire.DeltaCountAsync(database, owner.SpaceId, projectId) - before;
            ownNameInCase = await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[2].Id, defaults[2].Name.ToUpperInvariant());
            longest = await Wire.AddAsync(actor, owner.SpaceId, projectId, new string('n', 100));
        });

        Assert.NotNull(owner);

        foreach (var (answer, type) in refusals)
        {
            var expected = type == ProblemTypes.StatusNameTaken ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest;

            Assert.True(answer.StatusCode == expected, $"{answer.StatusCode} {answer.Body}");
            Assert.Equal(type, ProjectStatusWire.ProblemTypeOf(answer.Body));
        }

        Assert.Equal(0, deltaAfterRefusals);
        Assert.True(ownNameInCase?.StatusCode == HttpStatusCode.OK, $"{ownNameInCase?.StatusCode} {ownNameInCase?.Body}");
        Assert.True(longest?.StatusCode == HttpStatusCode.Created, $"{longest?.StatusCode} {longest?.Body}");
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Member and a Viewer of an Owner's Space are each refused every write - add, rename, place - on their Session and on
    /// their Token: <c>403 insufficient-role</c> naming <c>configure-project-status-delta</c>, six refusals recorded as
    /// <c>InsufficientRole</c> and none as <c>CrossSpace</c>, and nothing written. Anchor: the same caller reads the set.
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Member")]
    [InlineData("Viewer")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-7")]
    public async Task A_Member_or_a_Viewer_is_refused_403_on_every_write_on_both_surfaces(string role)
    {
        var database = Available();
        Actor? owner = null;
        Actor? caller = null;
        var projectId = Guid.Empty;
        var refused = new List<WireResponse>();
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            caller = await RegisterAsync(client, database);
            await AddMemberAsync(database, owner.SpaceId, caller.AccountId, role);

            projectId = IdOf(await CreateAsync(owner, "Guarded"));

            var actor = AsTaskActor(caller);
            var token = await IssueTokenAsync(caller, owner.SpaceId);
            var first = (await Wire.DefaultsAsync(database, owner.SpaceId))[0].Id;

            refused.Add(await Wire.AddAsync(actor, owner.SpaceId, projectId, "Parked"));
            refused.Add(await Wire.AddWithTokenAsync(actor, token, owner.SpaceId, projectId, "Parked"));
            refused.Add(await Wire.RenameAsync(actor, owner.SpaceId, projectId, first, "Intake"));
            refused.Add(await Wire.RenameWithTokenAsync(actor, token, owner.SpaceId, projectId, first, "Intake"));
            refused.Add(await Wire.PlaceAsync(actor, owner.SpaceId, projectId, first, afterStatusId: null));
            refused.Add(await Wire.PlaceWithTokenAsync(actor, token, owner.SpaceId, projectId, first, afterStatusId: null));
            anchor = await Wire.ReadWithTokenAsync(actor, token, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(caller);
        Assert.Equal(6, refused.Count);
        Assert.All(refused, answer =>
        {
            Assert.True(answer.StatusCode == HttpStatusCode.Forbidden, $"{answer.StatusCode} {answer.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(answer)?.Type);
            Assert.Equal(ConfigureCapability, Problem(answer)?.Capability);
        });
        Assert.Equal(Enumerable.Repeat(InsufficientRoleKind, 6), await RefusalKindsAsync(database, caller.AccountId, owner.SpaceId));
        Assert.Equal(0, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        Assert.Equal(3, (await Wire.DefinitionsAsync(database, owner.SpaceId)).Count);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// A Project id that matches nothing in the caller's own Space answers the read and the add with the boundary's
    /// <c>404 not-available</c>, byte-identical to a Space that does not exist, and records nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task An_unknown_Project_answers_the_boundary_404_byte_identical_and_records_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var answers = new List<(WireResponse InSpace, WireResponse AbsentSpace)>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            _ = IdOf(await CreateAsync(owner, "Present"));

            answers.Add((await Wire.ReadAsync(actor, owner.SpaceId, Guid.NewGuid()), await Wire.ReadAsync(actor, Guid.NewGuid(), Guid.NewGuid())));
            answers.Add((await Wire.AddAsync(actor, owner.SpaceId, Guid.NewGuid(), "Parked"), await Wire.AddAsync(actor, Guid.NewGuid(), Guid.NewGuid(), "Parked")));
        });

        Assert.NotNull(owner);
        Assert.All(answers, pair => AssertSameNotAvailable(pair.InSpace, pair.AbsentSpace));
        Assert.Equal(3, (await Wire.DefinitionsAsync(database, owner.SpaceId)).Count);
    }

    /// <summary>
    /// In the caller's own Project path, a sibling Project's added Status id and another Space's real Status id are each
    /// refused by rename and by place exactly as a random id is - the in-Space <c>404 not-available</c>, byte-identical - and
    /// nothing is written in either Project. A place anchor outside the set is <c>400 invalid-status-position</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_sibling_Projects_or_another_Spaces_Status_in_the_path_answers_the_in_Space_404()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var answers = new List<(WireResponse Foreign, WireResponse Random)>();
        WireResponse? outsideAnchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Own"));

            var sibling = IdOf(await CreateAsync(owner, "Sibling"));
            var siblings = await Wire.AddedAsync(actor, owner.SpaceId, sibling, "Theirs only");
            var foreign = (await Wire.DefaultsAsync(database, other.SpaceId))[0].Id;
            var first = (await Wire.DefaultsAsync(database, owner.SpaceId))[0].Id;

            foreach (var statusId in new[] { siblings, foreign })
            {
                answers.Add((
                    await Wire.RenameAsync(actor, owner.SpaceId, projectId, statusId, "Taken over"),
                    await Wire.RenameAsync(actor, owner.SpaceId, projectId, Guid.NewGuid(), "Taken over")));
                answers.Add((
                    await Wire.PlaceAsync(actor, owner.SpaceId, projectId, statusId, afterStatusId: null),
                    await Wire.PlaceAsync(actor, owner.SpaceId, projectId, Guid.NewGuid(), afterStatusId: null)));
            }

            outsideAnchor = await Wire.PlaceAsync(actor, owner.SpaceId, projectId, first, siblings);
        });

        Assert.NotNull(owner);
        Assert.All(answers, pair => AssertSameNotAvailable(pair.Foreign, pair.Random));
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.True(outsideAnchor?.StatusCode == HttpStatusCode.BadRequest, $"{outsideAnchor?.StatusCode} {outsideAnchor?.Body}");
        Assert.Equal(ProblemTypes.InvalidStatusPosition, ProjectStatusWire.ProblemTypeOf(outsideAnchor!.Body));
        Assert.Equal(0, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
    }

    /// <summary>
    /// An add sent twice with one <c>Idempotency-Key</c> answers the original <c>201</c> and body the second time, and the
    /// Status is added once: one owned definition, one <c>Add</c> operation.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    public async Task A_replayed_add_returns_the_original_answer_and_adds_once()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? original = null;
        WireResponse? replay = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var key = Guid.NewGuid().ToString("D");

            projectId = IdOf(await CreateAsync(owner, "Replayed"));
            original = await Wire.AddAsync(actor, owner.SpaceId, projectId, "Parked", key);
            replay = await Wire.AddAsync(actor, owner.SpaceId, projectId, "Parked", key);
        });

        Assert.NotNull(owner);
        Assert.True(original?.StatusCode == HttpStatusCode.Created, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(original!.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Single(await Wire.DefinitionsAsync(database, owner.SpaceId), row => row.OwnerProjectId == projectId);
        Assert.Equal(1, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    private static void AssertSameNotAvailable(WireResponse answer, WireResponse anchor)
    {
        Assert.True(answer.StatusCode == HttpStatusCode.NotFound, $"{answer.StatusCode} {answer.Body}");
        Assert.Equal(anchor.StatusCode, answer.StatusCode);
        Assert.Equal(anchor.ContentType, answer.ContentType);
        Assert.Equal(anchor.Body, answer.Body);
        Assert.Equal(anchor.HeaderNames, answer.HeaderNames);
        Assert.Equal(ProblemTypes.NotAvailable, Problem(answer)?.Type);
    }
}
