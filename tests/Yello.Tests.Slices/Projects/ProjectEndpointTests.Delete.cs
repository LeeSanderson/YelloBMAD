using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Contracts.Projects;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using TaskActor = Yello.Tests.Slices.Tasks.Actor;
using TaskEditWire = Yello.Tests.Slices.Tasks.TaskEditWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Story 2.5 over the wire: deleting a Project, and with it every Task in it and every application of those Tasks -
/// immediately and irreversibly. FR-17's delete clause, AC2 (the count's read), AC3, AC7; AD-3, AD-20, AD-29, AR-10,
/// AR-11, NFR-7, PRD §6.2. Written red by the story 2.5 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Activated by story 2.5's dev phase</b> once <c>DELETE</c> was mapped on the Project's item route (story Task 5) -
/// the path 2.1's rename serves (<see cref="ProjectRoutes.Project"/>). 2.1's
/// <c>An_unmapped_method_on_a_Project_route_answers_404_with_no_Allow_header</c> sent <c>DELETE</c> there; it now sends
/// <c>DELETE</c> to the collection, which stays unmapped.
/// </para>
/// <para>
/// <b>Tasks are created through the product with 2.2's <see cref="TaskWire"/></b>, which takes the Tasks namespace's
/// <see cref="TaskActor"/>; <see cref="AsTaskActor"/> adapts this class's own actor (same session, Account and Space).
/// Project-irreversibility cases carry <c>Assumption=PRD-12-5</c> (<c>prd.md:896</c>, R10); Task irreversibility is §6.2,
/// not §12, and takes none.
/// </para>
/// <para>
/// <b>Decisions these cases rest on</b> (story Open questions, recommended options): Q2 (a) - the listing gains a true
/// <c>taskCount</c> per Project, read again when the confirm opens; Q4 - absent, deleted or foreign is the byte-identical
/// <c>404</c>; Q6 - no <c>BEFORE DELETE</c> predicate. If Q2 is decided otherwise, correct
/// <see cref="The_Project_listing_states_each_Projects_true_Task_count"/> first and record it.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    /// <summary>
    /// AC2 (a): the listing's row shape once Q2 (a) appends the count (AD-19: appending is legal in v1).
    /// </summary>
    private static readonly string[] ProjectPropertiesWithCount = ["createdAt", "id", "name", "taskCount"];

    /// <summary>
    /// AC3, AC7 - an Owner, Admin or Member deletes a Project holding Tasks in three Statuses with Labels applied, on
    /// either surface: <c>204</c> with no body; the listing omits it; its Board is <c>404</c>; read under the Space's own
    /// key, not one of its Tasks and not one of their applications remains (gone, not hidden); and another Project's Task,
    /// its application and every Label are untouched. Nothing is recorded as a refusal.
    /// </summary>
    /// <param name="role">The deleter's Role in the Owner's Space.</param>
    /// <param name="surface">The surface the deletion is sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Owner", IsolationSurface.Session)]
    [InlineData("Admin", IsolationSurface.ApiToken)]
    [InlineData("Member", IsolationSurface.Session)]
    [InlineData("Member", IsolationSurface.ApiToken)]
    [Trait("Requirement", "FR-23")]
    [Trait("Requirement", "AD-4")]
    [Trait("Assumption", "PRD-12-5")]
    public async Task An_Owner_Admin_or_Member_deletes_a_Project_and_every_Task_and_application_in_it_goes(string role, IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        Actor? deleter = null;
        var doomed = Guid.Empty;
        var other = Guid.Empty;
        var otherTask = Guid.Empty;
        var labelId = Guid.Empty;
        List<Guid> doomedTasks = [];
        WireResponse? deleted = null;
        WireResponse? listing = null;
        WireResponse? board = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            deleter = owner;

            if (role != "Owner")
            {
                deleter = await RegisterAsync(client, database);
                await AddMemberAsync(database, owner.SpaceId, deleter.AccountId, role);
            }

            var token = await IssueTokenAsync(deleter, owner.SpaceId);
            var seeder = AsTaskActor(owner);
            var statuses = await TaskWire.StatusesAsync(database, owner.SpaceId);

            doomed = IdOf(await CreateAsync(owner, "Being torn down"));
            other = IdOf(await CreateAsync(owner, "Staying up"));
            labelId = await LabelWire.SeedAsync(seeder, owner.SpaceId, "Shared Label");

            foreach (var (title, status) in new[] { ("In the first", 0), ("In the second", 1), ("In the third", 2) })
            {
                var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(seeder, owner.SpaceId, doomed, title));

                if (status > 0)
                {
                    Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(seeder, owner.SpaceId, taskId, TaskEditWire.Body(new { statusId = statuses[status].Id }))).StatusCode);
                }

                Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(seeder, owner.SpaceId, taskId, LabelWire.ApplyBody(labelId))).StatusCode);
                doomedTasks.Add(taskId);
            }

            otherTask = TaskWire.IdOf(await TaskWire.CreateTaskAsync(seeder, owner.SpaceId, other, "Elsewhere"));
            Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(seeder, owner.SpaceId, otherTask, LabelWire.ApplyBody(labelId))).StatusCode);

            deleted = await DeleteWire.DeleteProjectOnAsync(surface, AsTaskActor(deleter), token, owner.SpaceId, doomed);
            listing = await owner.Session.GetAsync(ProjectRoutes.ListIn(owner.SpaceId));
            board = await owner.Session.GetAsync(BoardRoutes.BoardOf(owner.SpaceId, doomed));
        });

        Assert.NotNull(owner);
        Assert.NotNull(deleter);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Equal(string.Empty, deleted!.Body);
        Assert.Equal([other], DeleteWire.ListedProjectIds(listing!));
        Assert.Equal(HttpStatusCode.NotFound, board?.StatusCode);

        // Gone, not hidden: read under the Space's own key, which sees every row the Space holds.
        Assert.Equal([other], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Equal(0, await TaskWire.TaskCountAsync(database, owner.SpaceId, doomed));
        Assert.Equal(0, await DeleteWire.ApplicationsOfAsync(database, owner.SpaceId, doomedTasks));

        // Another Project, its Task, its application and the Space's Labels are untouched.
        Assert.Equal([otherTask], (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Equal([labelId], await LabelWire.AppliedToAsync(database, owner.SpaceId, otherTask));
        Assert.Equal([labelId], (await LabelWire.LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Empty(await RefusalKindsAsync(database, deleter.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// AC7, V-1, C-6, NFR-7 - a Viewer of an Owner's Space is refused <c>403 insufficient-role</c> naming
    /// <c>manage-projects</c> on its Session and on its Token, each recorded once as <c>InsufficientRole</c>; the Project
    /// and its Task stay. Anchor: a Member deletes it (<c>204</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "NFR-7")]
    public async Task A_Viewer_is_refused_403_manage_projects_on_both_surfaces_and_the_Project_stays()
    {
        var database = Available();
        Actor? owner = null;
        Actor? viewer = null;
        var projectId = Guid.Empty;
        WireResponse? bySession = null;
        WireResponse? byToken = null;
        IReadOnlyList<ProjectRow> afterRefusals = [];
        var tasksAfterRefusals = 0;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            viewer = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            projectId = IdOf(await CreateAsync(owner, "Viewer cannot remove"));
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(AsTaskActor(owner), owner.SpaceId, projectId, "Inside"));

            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            bySession = await DeleteWire.DeleteProjectAsync(AsTaskActor(viewer), owner.SpaceId, projectId);
            byToken = await DeleteWire.DeleteProjectWithTokenAsync(AsTaskActor(viewer), token, owner.SpaceId, projectId);
            afterRefusals = await ProjectRowsAsync(database, owner.SpaceId);
            tasksAfterRefusals = await TaskWire.TaskCountAsync(database, owner.SpaceId, projectId);
            anchor = await DeleteWire.DeleteProjectAsync(AsTaskActor(member), owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(viewer);

        foreach (var refused in new[] { bySession!, byToken! })
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused)?.Type);
            Assert.Equal(StableCode.Of(Capability.ManageProjects), Problem(refused)?.Capability);
        }

        Assert.Equal([InsufficientRoleKind, InsufficientRoleKind], await RefusalKindsAsync(database, viewer.AccountId, owner.SpaceId));
        Assert.Equal([projectId], afterRefusals.Select(row => row.Id));
        Assert.Equal(1, tasksAfterRefusals);
        Assert.Equal(HttpStatusCode.NoContent, anchor?.StatusCode);
    }

    /// <summary>
    /// AC7, AD-3, AD-20, Q4 - a random id, an id already deleted, and a real Project id from another Space, each sent in
    /// one's own Space, answer the byte-identical <c>404 not-available</c> of a Space that does not exist, on either
    /// surface, and record nothing; the other Space's Project and its Task are still there.
    /// </summary>
    /// <param name="surface">The surface every request is sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "AR-10")]
    public async Task An_absent_deleted_or_foreign_Project_answers_the_identical_404_and_records_nothing(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        Actor? stranger = null;
        var foreign = Guid.Empty;
        var answers = new List<WireResponse>();
        WireResponse? first = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            stranger = await RegisterAsync(client, database);

            var token = await IssueTokenAsync(owner, owner.SpaceId);
            var deleter = AsTaskActor(owner);
            var own = IdOf(await CreateAsync(owner, "Deleted once"));

            foreign = IdOf(await CreateAsync(stranger, "Someone else's"));
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(AsTaskActor(stranger), stranger.SpaceId, foreign, "Theirs"));

            first = await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, own);

            answers.Add(await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, Guid.NewGuid()));
            answers.Add(await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, own));
            answers.Add(await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, owner.SpaceId, foreign));

            anchor = await DeleteWire.DeleteProjectOnAsync(surface, deleter, token, Guid.NewGuid(), Guid.NewGuid());
        });

        Assert.NotNull(owner);
        Assert.NotNull(stranger);
        Assert.Equal(HttpStatusCode.NoContent, first?.StatusCode);
        Assert.NotNull(anchor);
        Assert.Equal(HttpStatusCode.NotFound, anchor.StatusCode);

        Assert.All(answers, answer =>
        {
            Assert.Equal(anchor.StatusCode, answer.StatusCode);
            Assert.Equal(anchor.ContentType, answer.ContentType);
            Assert.Equal(anchor.Body, answer.Body);
            Assert.Equal(anchor.HeaderNames, answer.HeaderNames);
            Assert.Equal(ProblemTypes.NotAvailable, Problem(answer)?.Type);
        });

        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Equal([foreign], (await ProjectRowsAsync(database, stranger.SpaceId)).Select(row => row.Id));
        Assert.Equal(1, await TaskWire.TaskCountAsync(database, stranger.SpaceId, foreign));
    }

    /// <summary>
    /// AC7, AR-30, AD-25, story Task 7 - deleting a Project frees the Projects-per-Space bound: at 50 the next creation is
    /// refused <c>409 bound-reached</c>; one Project is deleted; the next creation is <c>201</c>. Reuses 2.1's
    /// <see cref="SeedProjectsAsync"/>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    [Trait("Priority", "P1")]
    public async Task Deleting_a_Project_from_a_full_Space_lets_the_next_creation_through()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;
        WireResponse? deleted = null;
        WireResponse? admitted = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            await SeedProjectsAsync(database, owner.SpaceId, ProjectsPerSpace - 1);

            var last = IdOf(await CreateAsync(owner, "The fiftieth"));

            refused = await CreateAsync(owner, "Refused while full");
            deleted = await DeleteWire.DeleteProjectAsync(AsTaskActor(owner), owner.SpaceId, last);
            admitted = await CreateAsync(owner, "Admitted after a deletion");
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Conflict, refused?.StatusCode);
        Assert.Equal(ProblemTypes.BoundReached, Problem(refused!)?.Type);
        Assert.Equal(HttpStatusCode.NoContent, deleted?.StatusCode);
        Assert.True(admitted?.StatusCode == HttpStatusCode.Created, $"{admitted?.StatusCode} {admitted?.Body}");
        Assert.Equal(ProjectsPerSpace, (await ProjectRowsAsync(database, owner.SpaceId)).Count);
    }

    /// <summary>
    /// AC2, AC12, AD-29, Q2 (a) - the Project listing states each Project's true Task count (every Status, not the Tasks
    /// one column happens to list), zero included, so the confirm can name it when it opens; the row is exactly
    /// <c>{createdAt, id, name, taskCount}</c> - 2.1's three fields and the one appended - and carries no Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-29")]
    [Trait("Requirement", "AD-19")]
    public async Task The_Project_listing_states_each_Projects_true_Task_count()
    {
        var database = Available();
        Actor? owner = null;
        var busy = Guid.Empty;
        var empty = Guid.Empty;
        WireResponse? listing = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var seeder = AsTaskActor(owner);
            var statuses = await TaskWire.StatusesAsync(database, owner.SpaceId);

            busy = IdOf(await CreateAsync(owner, "Three Tasks"));
            empty = IdOf(await CreateAsync(owner, "No Tasks"));

            for (var index = 0; index < 3; index++)
            {
                var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(seeder, owner.SpaceId, busy, $"Task {index}"));

                Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(seeder, owner.SpaceId, taskId, TaskEditWire.Body(new { statusId = statuses[index].Id }))).StatusCode);
            }

            listing = await owner.Session.GetAsync(ProjectRoutes.ListIn(owner.SpaceId));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, listing?.StatusCode);

        using var document = JsonDocument.Parse(listing!.Body);
        var rows = document.RootElement.EnumerateArray().ToDictionary(row => row.GetProperty("id").GetGuid());

        Assert.All(rows.Values, row => Assert.Equal(ProjectPropertiesWithCount, row.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
        Assert.Equal(3, rows[busy].GetProperty("taskCount").GetInt32());
        Assert.Equal(0, rows[empty].GetProperty("taskCount").GetInt32());
        Assert.DoesNotContain(owner.SpaceId.ToString("D"), listing.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC3, story Task 3 (measure, do not fix), Q13 - a Project holding 5,000 Tasks is deleted whole by one request:
    /// <c>204</c> and not one Task left, never half a Project. The elapsed time is written to the case's output for the
    /// Debug Log and ledgered to 2.9 (lock escalation on <c>Task</c>); nothing here asserts a duration, so the case needs
    /// no serial collection.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Assumption", "PRD-12-5")]
    [Trait("Priority", "P1")]
    public async Task A_Project_of_5000_Tasks_is_deleted_whole_by_one_request()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? deleted = null;
        var elapsed = TimeSpan.Zero;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = IdOf(await CreateAsync(owner, "Large"));

            var first = (await TaskWire.StatusesAsync(database, owner.SpaceId))[0];

            await TaskWire.SeedTasksAsync(database, owner.SpaceId, projectId, first.Id, BoundRegistry.For(Bound.TasksPerProject).Limit);

            var clock = Stopwatch.StartNew();

            deleted = await DeleteWire.DeleteProjectAsync(AsTaskActor(owner), owner.SpaceId, projectId);
            elapsed = clock.Elapsed;
        });

        TestContext.Current.TestOutputHelper?.WriteLine($"Deleting a Project of {BoundRegistry.For(Bound.TasksPerProject).Limit} Tasks took {elapsed.TotalMilliseconds:F0} ms over the wire.");

        Assert.NotNull(owner);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Equal(0, await TaskWire.TaskCountAsync(database, owner.SpaceId, projectId));
        Assert.Empty(await ProjectRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// This class's actor as the Tasks namespace's (same session, Account and Space), for 2.2-2.5's Task, Label and
    /// deletion helpers.
    /// </summary>
    private static TaskActor AsTaskActor(Actor actor) => new(actor.Session, actor.AccountId, actor.SpaceId);
}
