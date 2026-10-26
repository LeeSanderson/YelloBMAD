using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Labels;
using Yello.Domain;
using Yello.Domain.Labels;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.4 over the wire: Owners and Admins define Labels per Space, Owners, Admins and Members apply them through
/// the Task edit, and a Label in use cannot be deleted. AC1-AC3, AC7-AC9, AC11; FR-20 (Labels clause), FR-22, AD-3,
/// AD-20, AR-34. Written red by the story 2.4 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>One class, one database</b>: these are partials of 2.2's <see cref="TaskEndpointTests"/> on the Slices engine -
/// no new container-backed class (story Task 7). Routes and problem types are the contracts'; answers are read as raw
/// JSON, so an added property cannot hide.
/// </para>
/// <para>
/// <b>Scaffolded against the recommended options</b>: Q1 (a) <c>labelIds</c> on the Task <c>PATCH</c>, the whole set;
/// Q2 names trimmed 1-50, unique per Space case-insensitively; Q3 palette keys; Q11 refuse with no count; Q12 server
/// order by name then id. Seed constraints are stated on <see cref="LabelWire"/>.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    private static readonly string[] LabelProperties = ["colour", "id", "name"];

    /// <summary>
    /// AC1, AC11 - an Owner defines a Label on either surface: <c>201</c>, the body is exactly
    /// <c>{colour, id, name}</c> with the name trimmed and no <c>SpaceId</c>, <c>Location</c> is the Label's own path
    /// under its Space, it lists, and the row belongs to that Space.
    /// </summary>
    /// <param name="surface">The surface the definition is sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "AD-4")]
    public async Task An_Owner_defines_a_Label_and_it_lists_in_the_Space(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? created = null;
        WireResponse? listed = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            created = await DefineOnAsync(surface, owner, token, owner.SpaceId, "  Needs design  ", "teal");
            listed = await ListOnAsync(surface, owner, token, owner.SpaceId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(created);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");

        using var document = JsonDocument.Parse(created.Body);
        var id = document.RootElement.GetProperty("id").GetGuid();

        Assert.Equal(LabelProperties, document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Needs design", document.RootElement.GetProperty("name").GetString());
        Assert.Equal("teal", document.RootElement.GetProperty("colour").GetString());
        Assert.Equal(LabelRoutes.LabelOf(owner.SpaceId, id), created.Header("Location"));
        Assert.Equal(HttpStatusCode.OK, listed?.StatusCode);
        Assert.Equal([id], ListedIds(listed!.Body));
        Assert.Equal([new LabelRow(id, owner.SpaceId, "Needs design", "teal")], await LabelRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC1 ("available to every Project"), AC2 - an Admin defines a Label, and a Member applies it to a Task in each of
    /// two different Projects of the Space: both <c>200</c>, both recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    public async Task An_Admins_Label_is_applicable_to_Tasks_in_every_Project_of_the_Space()
    {
        var database = Available();
        Actor? owner = null;
        var labelId = Guid.Empty;
        var first = Guid.Empty;
        var second = Guid.Empty;
        WireResponse[] applied = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var admin = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, admin.AccountId, "Admin");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            (_, first) = await ProjectWithTaskAsync(owner, "In the first Project");
            (_, second) = await ProjectWithTaskAsync(owner, "In the second Project");

            labelId = await SeedAsync(admin, owner.SpaceId, "Cross-project");

            applied =
            [
                await EditTaskAsync(member, owner.SpaceId, first, ApplyBody(labelId)),
                await EditTaskAsync(member, owner.SpaceId, second, ApplyBody(labelId)),
            ];
        });

        Assert.NotNull(owner);
        Assert.All(applied, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}"));
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, first));
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, second));
    }

    /// <summary>
    /// AC1, AC9, FR-16, V-1 - a Member, and a Viewer, added to an Owner's Space, creating and deleting a Label on their
    /// Session and on their Token: four <c>403 insufficient-role</c> naming <c>manage-labels</c>, four
    /// <c>InsufficientRole</c> rows and no <c>CrossSpace</c>, nothing written, and the Owner's Label still listed.
    /// </summary>
    /// <param name="role">The refused Role, as stored.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Member")]
    [InlineData("Viewer")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Member_or_Viewer_defining_or_deleting_a_Label_is_refused_403_manage_labels_on_both_surfaces(string role)
    {
        var database = Available();
        Actor? owner = null;
        Actor? refusedActor = null;
        var labelId = Guid.Empty;
        var answers = new List<WireResponse>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            refusedActor = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, refusedActor.AccountId, role);

            labelId = await SeedAsync(owner, owner.SpaceId, "Owner's");
            var token = await IssueTokenAsync(refusedActor, owner.SpaceId);

            answers.Add(await DefineAsync(refusedActor, owner.SpaceId, "Attempted"));
            answers.Add(await DefineWithTokenAsync(refusedActor, token, owner.SpaceId, "Attempted by script"));
            answers.Add(await DeleteAsync(refusedActor, owner.SpaceId, labelId));
            answers.Add(await DeleteWithTokenAsync(refusedActor, token, owner.SpaceId, labelId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(refusedActor);

        foreach (var refused in answers)
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused)?.Type);
            Assert.Equal(StableCode.Of(Capability.ManageLabels), Problem(refused)?.Capability);
        }

        Assert.Equal(Enumerable.Repeat(InsufficientRoleKind, 4), await RefusalKindsAsync(database, refusedActor.AccountId, owner.SpaceId));
        Assert.Equal(["Owner's"], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC2, AC7, Q1 (a), Q12 - a Member applies two Labels in one edit: <c>200</c>, the answer's <c>labelIds</c> are the
    /// two in server order (name, then id) and the rows agree; <c>[]</c> clears them; an edit that does not name
    /// <c>labelIds</c> leaves them as they were.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "FR-22")]
    public async Task A_Member_applies_and_clears_Labels_and_an_edit_without_them_leaves_them()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var alpha = Guid.Empty;
        var bravo = Guid.Empty;
        WireResponse? applied = null;
        IReadOnlyList<Guid> afterApply = [];
        WireResponse? cleared = null;
        IReadOnlyList<Guid> afterClear = [];
        WireResponse? untouched = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            (_, taskId) = await ProjectWithTaskAsync(owner, "Labelled");
            bravo = await SeedAsync(owner, owner.SpaceId, "Bravo");
            alpha = await SeedAsync(owner, owner.SpaceId, "Alpha");

            applied = await EditTaskAsync(member, owner.SpaceId, taskId, ApplyBody(bravo, alpha));
            afterApply = await AppliedToAsync(database, owner.SpaceId, taskId);

            cleared = await EditTaskAsync(member, owner.SpaceId, taskId, ApplyBody());
            afterClear = await AppliedToAsync(database, owner.SpaceId, taskId);

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(member, owner.SpaceId, taskId, ApplyBody(alpha))).StatusCode);
            untouched = await EditTaskAsync(member, owner.SpaceId, taskId, Body(new { title = "Retitled" }));
        });

        Assert.NotNull(owner);
        Assert.True(applied?.StatusCode == HttpStatusCode.OK, $"{applied?.StatusCode} {applied?.Body}");
        Assert.Equal([alpha, bravo], LabelIdsOf(applied!.Body));
        Assert.Equal(new[] { alpha, bravo }.Order(), afterApply);

        Assert.Equal(HttpStatusCode.OK, cleared?.StatusCode);
        Assert.Empty(LabelIdsOf(cleared!.Body));
        Assert.Empty(afterClear);

        Assert.Equal(HttpStatusCode.OK, untouched?.StatusCode);
        Assert.Equal([alpha], LabelIdsOf(untouched!.Body));
        Assert.Equal([alpha], await AppliedToAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC7 - Labels survive a title, a Status and a due-date edit; and a Labels-only edit changes no other column - not
    /// the title, the Status, the due date or the position key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-20")]
    [Trait("Priority", "P1")]
    public async Task Labels_survive_other_edits_and_a_Labels_edit_touches_nothing_else()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var labelId = Guid.Empty;
        TaskRow? beforeLabels = null;
        TaskRow? afterLabels = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Survivor");
            labelId = await SeedAsync(owner, owner.SpaceId, "Sticky");

            beforeLabels = await RowAsync(database, owner.SpaceId, taskId);
            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(labelId))).StatusCode);
            afterLabels = await RowAsync(database, owner.SpaceId, taskId);

            var statuses = await StatusesAsync(database, owner.SpaceId);

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { title = "Renamed survivor" }))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = statuses[1].Id }))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { dueDate = "2026-12-01T00:00:00+00:00" }))).StatusCode);
        });

        Assert.NotNull(owner);
        Assert.Equal(beforeLabels, afterLabels);
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC2 ("a Viewer cannot apply one by any route including the API"), AC9, C-6 - a Viewer added to an Owner's Space
    /// sends <c>labelIds</c> on its Session and its Token: <c>403 insufficient-role</c> naming <c>manage-tasks</c> (a
    /// Task edit, not Label definition), two <c>InsufficientRole</c> rows, no application. Anchor: a Member's same edit
    /// is <c>200</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "NFR-7")]
    public async Task A_Viewer_applying_a_Label_is_refused_403_manage_tasks_on_both_surfaces()
    {
        var database = Available();
        Actor? owner = null;
        Actor? viewer = null;
        var taskId = Guid.Empty;
        var labelId = Guid.Empty;
        WireResponse? bySession = null;
        WireResponse? byToken = null;
        IReadOnlyList<Guid> afterRefusals = [];
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            viewer = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            (_, taskId) = await ProjectWithTaskAsync(owner, "Viewed");
            labelId = await SeedAsync(owner, owner.SpaceId, "Wanted");
            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            bySession = await EditTaskAsync(viewer, owner.SpaceId, taskId, ApplyBody(labelId));
            byToken = await EditTaskWithTokenAsync(viewer, token, owner.SpaceId, taskId, ApplyBody(labelId));
            afterRefusals = await AppliedToAsync(database, owner.SpaceId, taskId);
            anchor = await EditTaskAsync(member, owner.SpaceId, taskId, ApplyBody(labelId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(viewer);

        foreach (var refused in new[] { bySession!, byToken! })
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused)?.Type);
            Assert.Equal(StableCode.Of(Capability.ManageTasks), Problem(refused)?.Capability);
        }

        Assert.Equal([InsufficientRoleKind, InsufficientRoleKind], await RefusalKindsAsync(database, viewer.AccountId, owner.SpaceId));
        Assert.Empty(afterRefusals);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// AC11, Q2 - a name that is empty, blank, or longer than 50 characters is refused <c>400 invalid-label-name</c>,
    /// writes no refusal row and no Label. Anchor: exactly 50 characters is defined.
    /// </summary>
    /// <param name="length">The name's length.</param>
    /// <param name="blank">Whether it is spaces only.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(51, false)]
    [Trait("Requirement", "AR-34")]
    [Trait("Assumption", "FR-22-Q2")]
    [Trait("Priority", "P1")]
    public async Task An_invalid_Label_name_is_refused_400_and_nothing_is_written(int length, bool blank)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;
        WireResponse? anchor = null;
        var longest = new string('n', LabelName.MaxLength);

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            refused = await DefineAsync(owner, owner.SpaceId, new string(blank ? ' ' : 'n', length));
            anchor = await DefineAsync(owner, owner.SpaceId, longest);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.BadRequest, refused?.StatusCode);
        Assert.Equal(ProblemTypes.InvalidLabelName, Problem(refused!)?.Type);
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);
        Assert.Equal([longest], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC4, AC11, Q3 - a colour that is not a palette key - an unknown word, a hex value (the token layer's, never the
    /// wire's), or <c>null</c> - is refused <c>400 invalid-label-colour</c> and nothing is written. Anchor:
    /// <c>rust</c> is defined.
    /// </summary>
    /// <param name="colour">The colour sent.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("magenta")]
    [InlineData("#D2592D")]
    [InlineData(null)]
    [Trait("Requirement", "UX-DR13")]
    [Trait("Priority", "P1")]
    public async Task A_colour_outside_the_palette_is_refused_400_and_nothing_is_written(string? colour)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            refused = await DefineAsync(owner, owner.SpaceId, "Coloured", colour);
            anchor = await DefineAsync(owner, owner.SpaceId, "Coloured", Rust);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.BadRequest, refused?.StatusCode);
        Assert.Equal(ProblemTypes.InvalidLabelColour, Problem(refused!)?.Type);
        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);
        Assert.Equal([Rust], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Colour));
    }

    /// <summary>
    /// AC11, Q2 - defining a name the Space already holds, differently cased, is refused <c>409 label-name-taken</c>; the
    /// body echoes neither name; nothing is written and no refusal row is recorded. Uniqueness is per Space: another
    /// Space defines the same name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Assumption", "FR-22-Q2")]
    public async Task A_name_the_Space_already_holds_is_refused_409_and_another_Space_may_hold_it()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        WireResponse? taken = null;
        WireResponse? elsewhere = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);

            _ = await SeedAsync(owner, owner.SpaceId, "Bug");
            taken = await DefineAsync(owner, owner.SpaceId, "bug");
            elsewhere = await DefineAsync(other, other.SpaceId, "Bug");
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Conflict, taken?.StatusCode);
        Assert.Equal(ProblemTypes.LabelNameTaken, Problem(taken!)?.Type);
        Assert.DoesNotContain("bug", taken!.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Equal(["Bug"], (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
        Assert.Equal(HttpStatusCode.Created, elsewhere?.StatusCode);
    }

    /// <summary>
    /// AC11 - a definition body that is not JSON, is an empty object, or is a JSON array is a <c>400</c>, never a
    /// <c>500</c>, and writes nothing.
    /// </summary>
    /// <param name="body">The raw body.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("{ not json")]
    [InlineData("{}")]
    [InlineData("[\"rust\"]")]
    [Trait("Requirement", "AR-34")]
    [Trait("Priority", "P1")]
    public async Task An_unreadable_definition_is_refused_400_and_never_500(string body)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            refused = await DefineRawAsync(owner, owner.SpaceId, RawBody(body));
        });

        Assert.NotNull(owner);
        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Empty(await LabelRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC8, AC10, AC11, I-13 - a <c>labelIds</c> entry that is a random id, another Space's real Label id, a duplicate, or
    /// a Label deleted since is refused <c>400 invalid-task-labels</c>: the random and the foreign answers are
    /// byte-identical, no body echoes an id, nothing is applied in either Space, and no refusal row is written. Anchor:
    /// the own Label alone is applied.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "AR-34")]
    public async Task An_unknown_foreign_duplicate_or_deleted_Label_id_is_refused_400_and_nothing_changes()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        var taskId = Guid.Empty;
        var own = Guid.Empty;
        var foreign = Guid.Empty;
        var deleted = Guid.Empty;
        var random = Guid.NewGuid();
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);

            (_, taskId) = await ProjectWithTaskAsync(owner, "Guarded");
            own = await SeedAsync(owner, owner.SpaceId, "Own");
            deleted = await SeedAsync(owner, owner.SpaceId, "Gone");
            foreign = await SeedAsync(other, other.SpaceId, "Theirs");

            Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(owner, owner.SpaceId, deleted)).StatusCode);

            answers["random"] = await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(own, random));
            answers["foreign"] = await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(own, foreign));
            answers["duplicate"] = await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(own, own));
            answers["deleted"] = await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(own, deleted));

            Assert.Empty(await AppliedToAsync(database, owner.SpaceId, taskId));

            anchor = await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(own));
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);

        foreach (var (what, refused) in answers)
        {
            Assert.True(refused.StatusCode == HttpStatusCode.BadRequest, $"{what}: {refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InvalidTaskLabels, Problem(refused)?.Type);

            foreach (var id in new[] { own, foreign, deleted, random })
            {
                Assert.DoesNotContain(id.ToString("D"), refused.Body, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.Equal(answers["random"].Body, answers["foreign"].Body);
        Assert.Equal(answers["random"].ContentType, answers["foreign"].ContentType);
        Assert.Equal(answers["random"].HeaderNames, answers["foreign"].HeaderNames);
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, other.SpaceId));
        Assert.Empty(await ApplicationsAsync(database, other.SpaceId));
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
        Assert.Equal([own], await AppliedToAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC11, Q1 (a), Q6 - <c>labelIds</c> that is <c>null</c>, not an array, or longer than the input-shape guard is the
    /// edit's own <c>400 invalid-task-edit</c>; an element that is not a GUID is <c>400 invalid-task-labels</c>. Never a
    /// <c>500</c>, nothing applied.
    /// </summary>
    /// <param name="body">The raw body.</param>
    /// <param name="expected">The problem type.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("{\"labelIds\":null}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"labelIds\":\"rust\"}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("OVERSIZE", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"labelIds\":[\"not-a-guid\"]}", "urn:yello:problem:invalid-task-labels")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Priority", "P1")]
    public async Task A_labelIds_member_of_the_wrong_shape_is_refused_400_and_never_500(string body, string expected)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? refused = null;
        var sent = body == "OVERSIZE"
            ? JsonSerializer.Serialize(new { labelIds = Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()) })
            : body;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Shaped");

            refused = await EditTaskAsync(owner, owner.SpaceId, taskId, RawBody(sent));
        });

        Assert.NotNull(owner);
        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(expected, Problem(refused!)?.Type);
        Assert.Empty(await AppliedToAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC3, Q11 - an unused Label deletes (<c>204</c>, gone from the list); an applied Label is refused
    /// <c>409 label-in-use</c> with no count and no Task title in the body, stays listed, and its applications stay;
    /// once they are removed it deletes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    public async Task An_applied_Label_is_refused_deletion_until_its_applications_are_removed()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var applied = Guid.Empty;
        WireResponse? unusedDeleted = null;
        WireResponse? refused = null;
        IReadOnlyList<Guid> listedAfterRefusal = [];
        IReadOnlyList<Guid> appliedAfterRefusal = [];
        WireResponse? deletedAfterRemoval = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "A title nobody may read");
            applied = await SeedAsync(owner, owner.SpaceId, "Applied");
            var unused = await SeedAsync(owner, owner.SpaceId, "Unused");

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(applied))).StatusCode);

            unusedDeleted = await DeleteAsync(owner, owner.SpaceId, unused);
            refused = await DeleteAsync(owner, owner.SpaceId, applied);
            listedAfterRefusal = ListedIds((await ListAsync(owner, owner.SpaceId)).Body);
            appliedAfterRefusal = await AppliedToAsync(database, owner.SpaceId, taskId);

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody())).StatusCode);
            deletedAfterRemoval = await DeleteAsync(owner, owner.SpaceId, applied);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.NoContent, unusedDeleted?.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, refused?.StatusCode);
        Assert.Equal(ProblemTypes.LabelInUse, Problem(refused!)?.Type);
        Assert.DoesNotContain("A title nobody may read", refused!.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("count", refused.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([applied], listedAfterRefusal);
        Assert.Equal([applied], appliedAfterRefusal);
        Assert.Equal(HttpStatusCode.NoContent, deletedAfterRemoval?.StatusCode);
        Assert.Empty(await LabelRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC8, AD-3, AD-20 - deleting a Label id that matches nothing in the active Space answers the boundary's
    /// <c>404 not-available</c>, byte-identical to a Space that does not exist, and records nothing. Anchor: a real
    /// Label deletes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "AD-3")]
    public async Task Deleting_a_Label_that_matches_nothing_answers_the_boundary_404_and_records_nothing()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? inSpace = null;
        WireResponse? absentSpace = null;
        WireResponse? anchor = null;
        IReadOnlyList<string> refusalsAfterInSpace = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var real = await SeedAsync(owner, owner.SpaceId, "Real");

            inSpace = await DeleteAsync(owner, owner.SpaceId, Guid.NewGuid());
            refusalsAfterInSpace = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            absentSpace = await DeleteAsync(owner, Guid.NewGuid(), Guid.NewGuid());
            anchor = await DeleteAsync(owner, owner.SpaceId, real);
        });

        Assert.NotNull(inSpace);
        Assert.NotNull(absentSpace);
        Assert.Equal(HttpStatusCode.NotFound, inSpace.StatusCode);
        Assert.Equal(absentSpace.StatusCode, inSpace.StatusCode);
        Assert.Equal(absentSpace.ContentType, inSpace.ContentType);
        Assert.Equal(absentSpace.Body, inSpace.Body);
        Assert.Equal(absentSpace.HeaderNames, inSpace.HeaderNames);
        Assert.Equal(ProblemTypes.NotAvailable, Problem(inSpace)?.Type);
        Assert.Empty(refusalsAfterInSpace);
        Assert.Equal(HttpStatusCode.NoContent, anchor?.StatusCode);
    }

    /// <summary>
    /// AC1, AC14, Q12 - the list answers on both surfaces, ordered by name then id, each row exactly
    /// <c>{colour, id, name}</c>; a Viewer reads it (a Viewer sees chips).
    /// </summary>
    /// <param name="surface">The surface the Viewer reads on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "AD-4")]
    [Trait("Priority", "P1")]
    public async Task The_Label_list_is_ordered_by_name_and_a_Viewer_reads_it(IsolationSurface surface)
    {
        var database = Available();
        WireResponse? listed = null;
        var expected = new List<Guid>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var viewer = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");

            var charlie = await SeedAsync(owner, owner.SpaceId, "Charlie");
            var alpha = await SeedAsync(owner, owner.SpaceId, "Alpha");
            var bravo = await SeedAsync(owner, owner.SpaceId, "Bravo");

            expected.AddRange([alpha, bravo, charlie]);

            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            listed = await ListOnAsync(surface, viewer, token, owner.SpaceId);
        });

        Assert.True(listed?.StatusCode == HttpStatusCode.OK, $"{listed?.StatusCode} {listed?.Body}");
        Assert.Equal(expected, ListedIds(listed!.Body));

        using var document = JsonDocument.Parse(listed.Body);

        Assert.All(document.RootElement.EnumerateArray(), row => Assert.Equal(LabelProperties, row.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// AC7, AD-19, Q12 - the Board and the Task read carry each Task's <c>labelIds</c> in server order (name, then id);
    /// a Task with none carries <c>[]</c>, never an absent member.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AD-19")]
    public async Task The_Board_and_the_Task_read_carry_each_Tasks_Labels_in_server_order()
    {
        var database = Available();
        var labelled = Guid.Empty;
        var bare = Guid.Empty;
        Guid[] expected = [];
        WireResponse? board = null;
        WireResponse? read = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Read back");

            labelled = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Carries two"));
            bare = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Carries none"));

            var zulu = await SeedAsync(owner, owner.SpaceId, "Zulu");
            var mike = await SeedAsync(owner, owner.SpaceId, "Mike");

            expected = [mike, zulu];

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, labelled, ApplyBody(zulu, mike))).StatusCode);

            board = await BoardAsync(owner, owner.SpaceId, projectId);
            read = await ViewTaskAsync(owner, owner.SpaceId, labelled);
        });

        Assert.Equal(HttpStatusCode.OK, board?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read?.StatusCode);
        Assert.Equal(expected, LabelIdsOf(read!.Body));

        using var document = JsonDocument.Parse(board!.Body);
        var tasks = document.RootElement.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("tasks").EnumerateArray())
            .ToDictionary(task => task.GetProperty("id").GetGuid(), task => task.GetProperty("labelIds").EnumerateArray().Select(id => id.GetGuid()).ToList());

        Assert.Equal(expected, tasks[labelled]);
        Assert.Empty(tasks[bare]);
    }
}
