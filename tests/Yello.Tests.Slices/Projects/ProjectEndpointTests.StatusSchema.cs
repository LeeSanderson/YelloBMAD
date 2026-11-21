using System.Net;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using TaskEditWire = Yello.Tests.Slices.Tasks.TaskEditWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// The schema a Project's Status delta rests on, over the wire and in the catalogue: a Status a Project adds is a
/// <c>StatusDefinition</c> row owned by that Project through a NO ACTION key, names are unique under two filtered indexes,
/// the delta is a Space-scoped table of operations with no materialised order, deleting a Project or a Space that holds
/// such rows still succeeds, and the name columns compare case-insensitively.
/// </summary>
/// <remarks>
/// <para>
/// <b>The riskiest assumption of the story is the first case here</b>: SQL Server refuses a second cascade path into one
/// table (1785), so the owning key from <c>StatusDefinition</c> to <c>Project</c> must be NO ACTION, and Project deletion
/// becomes ordered statements (its Tasks, its delta operations, its owned definitions, then the Project) instead of one
/// cascading <c>DELETE</c>. If the migration cannot take that shape, open question 2 is revisited before anything else is
/// built on it.
/// </para>
/// <para>
/// <b>Names the recommended schema</b> (open questions 1 (a), 2 (a), 6 (a)): <c>StatusDefinition.OwnerProjectId</c>, the table
/// <c>dbo.StatusDeltaOperation</c>, and <c>SQL_Latin1_General_CP1_CI_AS</c> on the three name columns. See
/// <see cref="ProjectStatusSliceWire"/> for the seed constraints.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private const string DeltaTable = "StatusDeltaOperation";

    private const string NameCollation = "SQL_Latin1_General_CP1_CI_AS";

    private static readonly string[] NameColumnTables = ["StatusDefinition", "Label", DeltaTable];

    private static readonly string[] TwoProjectNames = ["North", "South"];

    /// <summary>
    /// The owning key, the two filtered unique indexes and the delta table's keys and policy, read from the catalogue: an
    /// owned definition is keyed <c>(SpaceId, OwnerProjectId)</c> to <c>Project</c> with NO ACTION; no unfiltered unique index
    /// covers <c>Name</c>; defaults are unique by <c>(SpaceId, Name)</c> where unowned and owned rows by
    /// <c>(SpaceId, OwnerProjectId, Name)</c>; the delta cascades from <c>Project</c>, never cascades from
    /// <c>StatusDefinition</c>, has no key to <c>Space</c>, is unique per <c>(SpaceId, ProjectId, StatusId, Kind)</c>, and carries
    /// the Space policy's FILTER and two BLOCK predicates.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4, AC11, AC13 - Q1 (a), Q2 (a): StatusDefinition has no OwnerProjectId and there is no StatusDeltaOperation table yet")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "AR-23")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_Project_added_Status_is_a_StatusDefinition_keyed_NO_ACTION_to_its_Project_under_filtered_unique_names()
    {
        var database = Available();

        Assert.Equal(["1"], await CatalogueAsync(
            database,
            "SELECT CAST(c.is_nullable AS int) FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.StatusDefinition') AND c.name = N'OwnerProjectId'"));

        Assert.Equal(["SpaceId,OwnerProjectId|NO_ACTION"], await CatalogueAsync(
            database,
            """
            SELECT CONCAT(
                (SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                 FROM sys.foreign_key_columns fkc JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                 WHERE fkc.constraint_object_id = fk.object_id),
                '|', fk.delete_referential_action_desc)
            FROM sys.foreign_keys fk
            WHERE fk.parent_object_id = OBJECT_ID(N'dbo.StatusDefinition') AND fk.referenced_object_id = OBJECT_ID(N'dbo.Project')
            """));

        var nameIndexes = (await UniqueIndexesAsync(database, "StatusDefinition")).Where(index => index.Key.Contains("Name", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, nameIndexes.Count);
        Assert.Contains(nameIndexes, index => index.Key == "SpaceId,Name" && index.Filter.Contains("[OwnerProjectId] IS NULL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nameIndexes, index => index.Key == "SpaceId,OwnerProjectId,Name" && index.Filter.Contains("[OwnerProjectId] IS NOT NULL", StringComparison.OrdinalIgnoreCase));

        var deltaKeys = await CatalogueAsync(
            database,
            $"SELECT CONCAT(OBJECT_NAME(fk.referenced_object_id), '|', fk.delete_referential_action_desc) FROM sys.foreign_keys fk WHERE fk.parent_object_id = OBJECT_ID(N'dbo.{DeltaTable}')");

        Assert.Equal(["Project|CASCADE"], deltaKeys.Where(key => key.StartsWith("Project|", StringComparison.Ordinal)));
        Assert.Contains("StatusDefinition|NO_ACTION", deltaKeys);
        Assert.All(deltaKeys.Where(key => key.StartsWith("StatusDefinition|", StringComparison.Ordinal)), key => Assert.Equal("StatusDefinition|NO_ACTION", key));
        Assert.DoesNotContain(deltaKeys, key => key.StartsWith("Space|", StringComparison.Ordinal));

        Assert.Contains((await UniqueIndexesAsync(database, DeltaTable)).Select(index => index.Key), key => key == "SpaceId,ProjectId,StatusId,Kind");

        Assert.Equal(
            ["BLOCK|AFTER INSERT", "BLOCK|AFTER UPDATE", "FILTER|"],
            (await CatalogueAsync(
                database,
                $"SELECT CONCAT(p.predicate_type_desc, '|', ISNULL(p.operation_desc, N'')) FROM sys.security_predicates p WHERE p.target_object_id = OBJECT_ID(N'dbo.{DeltaTable}')"))
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Two Projects of one Space each add a Status with the same name (the filtered index permits it); a Task is moved into
    /// the first Project's; deleting that Project answers <c>204</c> and removes its Tasks, its delta and its owned
    /// definition, while the second Project's added Status, the three Space defaults and the Space's own read are untouched,
    /// and no Task is left outside its Project's set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - Q2 (a): no Project can own a Status, and Project deletion is still one cascading statement")]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-17")]
    public async Task Two_Projects_add_the_same_name_and_deleting_one_holding_Tasks_in_it_removes_only_its_own()
    {
        var database = Available();
        Actor? owner = null;
        var doomed = Guid.Empty;
        var kept = Guid.Empty;
        var doomedParked = Guid.Empty;
        var keptParked = Guid.Empty;
        IReadOnlyList<(Guid Id, string Name)> spaceReadBefore = [];
        IReadOnlyList<(Guid Id, string Name)> spaceReadAfter = [];
        WireResponse? deleted = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            doomed = IdOf(await CreateAsync(owner, "Being torn down"));
            kept = IdOf(await CreateAsync(owner, "Staying up"));
            spaceReadBefore = await Wire.SpaceReadAsync(actor, owner.SpaceId);

            doomedParked = await Wire.AddedAsync(actor, owner.SpaceId, doomed, "Parked");
            keptParked = await Wire.AddedAsync(actor, owner.SpaceId, kept, "Parked");

            var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, doomed, "Parked work"));

            Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(actor, owner.SpaceId, taskId, TaskEditWire.Body(new { statusId = doomedParked }))).StatusCode);
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, kept, "Kept work"));

            deleted = await DeleteWire.DeleteProjectAsync(actor, owner.SpaceId, doomed);
            spaceReadAfter = await Wire.SpaceReadAsync(actor, owner.SpaceId);
        });

        Assert.NotNull(owner);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");

        var definitions = await Wire.DefinitionsAsync(database, owner.SpaceId);

        Assert.DoesNotContain(definitions, row => row.Id == doomedParked);
        Assert.Contains(definitions, row => row.Id == keptParked && row.OwnerProjectId == kept);
        Assert.Equal(3, definitions.Count(row => row.OwnerProjectId is null));
        Assert.Equal(0, await Wire.DeltaCountAsync(database, owner.SpaceId, doomed));
        Assert.Equal(0, await TaskWire.TaskCountAsync(database, owner.SpaceId, doomed));
        Assert.Equal(1, await TaskWire.TaskCountAsync(database, owner.SpaceId, kept));
        Assert.Equal(spaceReadBefore, spaceReadAfter);
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Nothing stores a Project's effective set: <c>Project</c> has no Status column, no table is named
    /// <c>%EffectiveStatus%</c>, the delta table holds no position, ordinal, order or rank column, and renaming one default
    /// writes exactly one operation row - not one per Status of the Project. The successor of
    /// <c>Creating_a_Project_leaves_the_Space_default_Statuses_unchanged_and_stores_no_effective_set</c>, whose
    /// <c>%Delta%</c> clause the delta table turns red by design.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - Q1 (a): there is no delta table and no rename operation yet")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "FR-25")]
    public async Task The_delta_is_stored_as_operations_and_no_table_stores_an_effective_set()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? renamed = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = IdOf(await CreateAsync(owner, "Shaped"));

            var second = (await Wire.DefaultsAsync(database, owner.SpaceId))[1].Id;

            renamed = await Wire.RenameAsync(AsTaskActor(owner), owner.SpaceId, projectId, second, "Underway");
        });

        Assert.NotNull(owner);
        Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"{renamed?.StatusCode} {renamed?.Body}");

        var projectColumns = await CatalogueAsync(
            database,
            "SELECT c.name FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id WHERE t.name = N'Project'");
        var deltaColumns = await CatalogueAsync(
            database,
            $"SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.{DeltaTable}')");

        Assert.Contains("SpaceId", projectColumns);
        Assert.DoesNotContain(projectColumns, column => column.Contains("Status", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await CatalogueAsync(database, "SELECT t.name FROM sys.tables t WHERE t.name LIKE N'%EffectiveStatus%'"));
        Assert.Contains("Kind", deltaColumns);
        Assert.Contains("StatusId", deltaColumns);
        Assert.DoesNotContain(deltaColumns, column =>
            column.Contains("Position", StringComparison.OrdinalIgnoreCase)
            || column.Contains("Ordinal", StringComparison.OrdinalIgnoreCase)
            || column.Contains("Order", StringComparison.OrdinalIgnoreCase)
            || column.Contains("Rank", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
    }

    /// <summary>
    /// Deleting a Space that holds two Projects, each with a delta, an added Status and a Task in it, answers <c>204</c> in
    /// the Space deletion's one statement: the NO ACTION keys into <c>StatusDefinition</c> and <c>Project</c> are checked at
    /// the end of the statement, after the cascades have emptied both sides. Nothing of the Space remains.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - Q2 (a): no Project can own a Status or hold a delta yet")]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "AD-17")]
    public async Task Deleting_a_Space_holding_Projects_with_deltas_and_added_Statuses_succeeds_in_one_statement()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? deleted = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            foreach (var name in TwoProjectNames)
            {
                var projectId = IdOf(await CreateAsync(owner, name));
                var parked = await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked");
                var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

                Assert.Equal(HttpStatusCode.OK, (await Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[0].Id, $"{name} intake")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await Wire.PlaceAsync(actor, owner.SpaceId, projectId, parked, afterStatusId: null)).StatusCode);

                var taskId = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, $"{name} work"));

                Assert.Equal(HttpStatusCode.OK, (await TaskEditWire.EditTaskAsync(actor, owner.SpaceId, taskId, TaskEditWire.Body(new { statusId = parked }))).StatusCode);
            }

            await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
            deleted = await owner.Session.SendWithHeadersAsync(HttpMethod.Delete, SpaceRoutes.ActiveOf(owner.SpaceId), content: null, NoHeaders);
        });

        Assert.NotNull(owner);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Empty(await Wire.DefinitionsAsync(database, owner.SpaceId));
        Assert.Empty(await TaskWire.TaskRowsAsync(database, owner.SpaceId));
        Assert.Empty(await ProjectRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// The three name columns - <c>StatusDefinition.Name</c>, <c>Label.Name</c> and the delta's <c>Name</c> - carry the
    /// case-insensitive collation the application's comparison agrees with, so a uniqueness backstop never disagrees with the
    /// check made under the lock. Anchored: each column exists. The explicit declaration in the model is
    /// <c>RefusalBoundaryGateTests.The_three_name_columns_declare_their_case_insensitive_collation_in_the_model</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - Q6 (a): the delta table and its Name column do not exist yet")]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "FR-22")]
    public async Task The_Status_and_Label_name_columns_compare_case_insensitively()
    {
        var database = Available();

        foreach (var table in NameColumnTables)
        {
            Assert.Equal(
                [NameCollation],
                await CatalogueAsync(database, $"SELECT c.collation_name FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.{table}') AND c.name = N'Name'"));
        }
    }

    /// <summary>
    /// The unique, non-primary-key indexes of one table as (key columns in key order, filter definition or empty).
    /// </summary>
    private static async Task<IReadOnlyList<(string Key, string Filter)>> UniqueIndexesAsync(RegistrationDatabase database, string table)
    {
        var rows = await CatalogueAsync(
            database,
            $"""
            SELECT CONCAT(
                (SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
                 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                 WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0),
                '|', ISNULL(i.filter_definition, N''))
            FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID(N'dbo.{table}') AND i.is_unique = 1 AND i.is_primary_key = 0
            """);

        return [.. rows.Select(row => row.Split('|')).Select(parts => (parts[0], parts[1]))];
    }
}
