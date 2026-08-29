using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Architecture;

/// <summary>
/// The gates that keep the EF Core model honest without a database.
/// </summary>
/// <remarks>
/// These read the built model rather than a migrated schema, so they belong in the suite that
/// "takes seconds and should fail before anything slower starts". The migrated-schema assertions -
/// the row-level security policy, the filtered index - need a real SQL Server and live in
/// <c>Yello.Tests.Slices</c>, where the container fixture is reachable.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-5")]
[Trait("Requirement", "AD-2")]
public sealed class PersistenceModelGateTests
{
    /// <summary>
    /// Every entity configuration in the assembly reaches the model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the safety net for a deliberate trade.</b> <c>YelloDbContext</c> names its
    /// configurations one by one rather than calling
    /// <c>ApplyConfigurationsFromAssembly</c>, because an assembly scan leaves nothing in the
    /// solution statically referencing any configuration class - which MA0182 reported
    /// accurately, and which means a configuration deleted by mistake produces a silently
    /// different schema instead of a compile error.
    /// </para>
    /// <para>
    /// The cost of naming them is the opposite risk: a later story writes a configuration and
    /// forgets the line, so the entity quietly takes EF Core's conventions - no explicit table
    /// name, no <c>SpaceId</c> column mapping, no filtered index, and a pluralised table name
    /// that breaks the Glossary. That is not left to discipline.
    /// </para>
    /// <para>
    /// <b>It asserts the configuration's EFFECT, not the entity's presence, and the first version
    /// of this gate got that wrong.</b> Requiring only that the entity be in the model was
    /// satisfied by the <c>DbSet&lt;T&gt;</c> property alone - EF Core adds an entity type for
    /// every declared set - so planting a duplicated <c>ApplyConfiguration</c> line, which leaves
    /// one entity entirely unconfigured, passed. What is checked instead is the one thing every
    /// configuration here does and EF Core's conventions never would: an explicit singular table
    /// name matching the Glossary. Convention pluralises from the <c>DbSet</c> name, so an
    /// unapplied configuration gives <c>Accounts</c> where the Glossary says <c>Account</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_entity_configuration_reaches_the_model()
    {
        var configured = typeof(YelloDbContext).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
            .Select(contract => contract.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        // The reflection has to find something. An empty list would make the assertion below
        // vacuously true, which is the defect class this whole suite exists to catch - and it is
        // exactly what a renamed interface or a moved folder would produce.
        Assert.NotEmpty(configured);

        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var problems = new List<string>();

        foreach (var entity in configured)
        {
            var mapped = context.Model.FindEntityType(entity);

            if (mapped is null)
            {
                problems.Add(
                    $"{entity.Name} has an IEntityTypeConfiguration but is not in the model at " +
                    "all - it has neither a DbSet nor an applied configuration.");

                continue;
            }

            var table = mapped.GetTableName();

            if (!string.Equals(table, entity.Name, StringComparison.Ordinal))
            {
                problems.Add(
                    $"{entity.Name} is mapped to table '{table}', not '{entity.Name}'. Every " +
                    "configuration in this assembly sets an explicit singular table name, so a " +
                    "differing one means YelloDbContext.OnModelCreating never applied it - and " +
                    "the entity is taking EF Core's conventions instead: a pluralised table " +
                    "name, no explicit column mapping, and none of its indexes.");
            }
        }

        Assert.True(
            problems.Count == 0,
            $"An entity configuration does not reach the model:{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(problem => $"  - {problem}")));
    }

    /// <summary>
    /// Every Space-scoped entity carries a non-nullable <c>SpaceId</c>. AD-2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AD-2 requires a Space-scoped table's scoping column to be non-nullable, and the reason is
    /// not tidiness: a row whose <c>SpaceId</c> is null belongs to no Space, so no security
    /// predicate can place it. It would be filtered out of every query and visible to nobody -
    /// a leak in the other direction, and one that looks like data loss rather than a policy
    /// problem.
    /// </para>
    /// <para>
    /// Derived from the model rather than from a list of entity names, so an entity added by a
    /// later story is held to this without anyone extending the gate.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_Space_scoped_entity_has_a_non_nullable_SpaceId()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var problems = new List<string>();

        foreach (var entity in context.Model.GetEntityTypes())
        {
            var property = entity.FindProperty(SchemaNames.SpaceIdColumn);

            if (property is null)
            {
                continue;
            }

            if (property.IsNullable)
            {
                problems.Add(
                    $"{entity.ClrType.Name}.{SchemaNames.SpaceIdColumn} is nullable. A row that " +
                    "belongs to no Space cannot be placed by any row-level security predicate, " +
                    "so it would be invisible to everyone rather than scoped to someone.");
            }

            if (property.ClrType != typeof(Guid))
            {
                problems.Add(
                    $"{entity.ClrType.Name}.{SchemaNames.SpaceIdColumn} is " +
                    $"{property.ClrType.Name}, not Guid. The security predicate casts " +
                    "SESSION_CONTEXT to uniqueidentifier, and a comparison against another type " +
                    "would never be true.");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// No entity is store-generated: the application produces every id before the insert.
    /// </summary>
    /// <remarks>
    /// This is what makes AD-2's registration seam resolvable at all. The slice has to know the
    /// <c>SpaceId</c> before it can set the session context to it, and a database-generated key
    /// is not known until after the insert - so a later story switching one entity to an identity
    /// column would break isolation at registration, not merely change a convention.
    /// </remarks>
    [Fact]
    public void No_key_is_generated_by_the_database()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var generated = context.Model.GetEntityTypes()
            .Select(entity => new { entity, key = entity.FindPrimaryKey() })
            .Where(pair => pair.key is not null)
            .SelectMany(pair => pair.key!.Properties.Select(property => new { pair.entity, property }))
            .Where(pair => pair.property.ValueGenerated != Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never)
            .Select(pair => $"{pair.entity.ClrType.Name}.{pair.property.Name}")
            .ToList();

        Assert.True(
            generated.Count == 0,
            "A primary key is generated by the database. Ids are produced application-side, " +
            "before the insert, which is what lets the registration slice set the row-level " +
            "security session context to a SpaceId it has not written yet - see AD-2 and " +
            $"IIdentifierGenerator:{Environment.NewLine}" +
            string.Join(Environment.NewLine, generated.Select(name => $"  - {name}")));
    }
}
