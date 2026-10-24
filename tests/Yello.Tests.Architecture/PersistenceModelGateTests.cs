using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
    /// Every Space-scoped entity carries a global query filter derived from application state.
    /// AD-2 / AR-7, AC4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AD-2 (<c>ARCHITECTURE-SPINE.md:84</c>): the query filters "restate every policy as an
    /// independent second layer, derived from application state rather than session context, so
    /// neither layer alone carries NFR-1". Two ways to break that, and this catches both. An entity
    /// with a <c>SpaceId</c> and no filter is one layer where there should be two. A filter that
    /// calls anything - <c>SESSION_CONTEXT</c> through a mapped function, raw SQL - is the database's
    /// layer restated, which fails the moment the session context is wrong, which is exactly when
    /// the second layer exists to hold.
    /// </para>
    /// <para>
    /// <b>The shape required is the one the product uses</b>: the entity's <c>SpaceId</c> compared
    /// with a member of <see cref="YelloDbContext"/> - the request's <c>SpaceScope</c> - and no method
    /// call anywhere in the expression. Derived from the model, so an entity added by a later story
    /// is held to it without anyone extending this gate. Behaviour is asserted separately, against a
    /// real database, by <c>SpaceIsolationLayerTests.The_query_filter_alone_excludes_another_Spaces_rows</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_Space_scoped_entity_has_a_query_filter_from_application_state()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var scoped = context.Model.GetEntityTypes()
            .Where(entity => entity.FindProperty(SchemaNames.SpaceIdColumn) is not null)
            .ToList();

        // Vacuity guard: two Space-scoped entities exist today, and a gate that found none would be
        // green over a model it never looked at.
        Assert.NotEmpty(scoped);

        var problems = new List<string>();

        foreach (var entity in scoped)
        {
            var filters = entity.GetDeclaredQueryFilters();

            if (filters.Count == 0)
            {
                problems.Add(
                    $"{entity.ClrType.Name} carries {SchemaNames.SpaceIdColumn} and no query filter, so "
                    + "row-level security is its only layer.");

                continue;
            }

            foreach (var filter in filters)
            {
                var shape = new FilterShape();
                shape.Visit(filter.Expression);

                var isEquality = filter.Expression is { } expression && IsSpaceIdEqualToContextState(expression);

                if (!isEquality || shape.CallsAMethod)
                {
                    problems.Add(
                        $"{entity.ClrType.Name}'s query filter `{filter.Expression}` is not the "
                        + $"application-state shape: its whole body must be {SchemaNames.SpaceIdColumn} == a "
                        + "member of YelloDbContext, and it must call nothing. "
                        + $"(is that equality: {isEquality}, calls a method: {shape.CallsAMethod})");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// A reference from one Space-scoped entity to another carries <c>SpaceId</c> on both sides.
    /// AD-2 / AR-8, AC6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AD-2: "cross-entity references inside a Space use composite foreign keys carrying
    /// <c>SpaceId</c>, so a same-Space constraint holds by construction rather than by validation."
    /// A single-column reference to a Membership - <c>AssigneeId → Membership.Id</c> - lets a Task in
    /// Space A name a Membership in Space B, and nothing in the database refuses it. The composite
    /// <c>(SpaceId, AssigneeId) → Membership(SpaceId, Id)</c> makes that row impossible to write.
    /// </para>
    /// <para>
    /// <b>No such reference exists yet</b> - the first is FR-21's Assignee in story 4.6 - so this
    /// passes today by having nothing to inspect. A reference to
    /// <c>Space</c> itself is outside it: <c>Space</c> is the Space, and its key already is the
    /// <c>SpaceId</c>. Held against a single-column reference and a mis-paired composite one before
    /// it was trusted.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_reference_between_Space_scoped_entities_carries_SpaceId()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var foreignKeys = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetDeclaredForeignKeys())
            .ToList();

        // Vacuity guard on the scan rather than on its findings: the model has foreign keys, so a
        // green result means they were inspected and none crosses two Space-scoped entities badly.
        Assert.NotEmpty(foreignKeys);

        var problems = foreignKeys
            .Where(foreignKey =>
                foreignKey.DeclaringEntityType.FindProperty(SchemaNames.SpaceIdColumn) is not null
                && foreignKey.PrincipalEntityType.FindProperty(SchemaNames.SpaceIdColumn) is not null)
            .Where(foreignKey => !PairsSpaceIdWithSpaceId(foreignKey))
            .Select(foreignKey =>
                $"{foreignKey.DeclaringEntityType.ClrType.Name}({string.Join(", ", foreignKey.Properties.Select(p => p.Name))}) → "
                + $"{foreignKey.PrincipalEntityType.ClrType.Name}({string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => p.Name))})")
            .ToList();

        Assert.True(
            problems.Count == 0,
            "A reference between two Space-scoped entities does not carry SpaceId on both sides, so "
            + "nothing in the database stops it pointing into another Space. Make it composite, "
            + "targeting the principal's (SpaceId, Id) key:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, problems.Select(problem => $"  - {problem}")));
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

    /// <summary>
    /// Whether a foreign key maps the dependent's <c>SpaceId</c> onto the principal's <c>SpaceId</c>.
    /// </summary>
    /// <remarks>
    /// By position, because a composite key pairs its columns in order: <c>SpaceId</c> present on
    /// both sides is not enough, since <c>(AssigneeId, SpaceId) → (SpaceId, Id)</c> carries it on both
    /// and maps it onto <c>Id</c>.
    /// </remarks>
    private static bool PairsSpaceIdWithSpaceId(IReadOnlyForeignKey foreignKey)
    {
        var dependent = foreignKey.Properties.Select(property => property.Name).ToList().IndexOf(SchemaNames.SpaceIdColumn);
        var principal = foreignKey.PrincipalKey.Properties.Select(property => property.Name).ToList().IndexOf(SchemaNames.SpaceIdColumn);

        return dependent >= 0 && dependent == principal;
    }

    /// <summary>
    /// Whether a filter's entire body is <c>entity.SpaceId == context.Member</c>, in either order.
    /// </summary>
    /// <remarks>
    /// The body as a whole, not a part of it: a filter that merely mentions both sides admits
    /// <c>!=</c>, and <c>... || context.ScopedSpaceId == null</c>, which returns every Space's rows
    /// when no Space is in scope - the opposite of failing closed.
    /// </remarks>
    private static bool IsSpaceIdEqualToContextState(LambdaExpression filter)
    {
        if (StripConversions(filter.Body) is not BinaryExpression { NodeType: ExpressionType.Equal } equality)
        {
            return false;
        }

        var spaceIdFirst = IsEntitySpaceId(equality.Left) && IsContextState(equality.Right);
        var contextFirst = IsContextState(equality.Left) && IsEntitySpaceId(equality.Right);

        return spaceIdFirst || contextFirst;
    }

    private static bool IsEntitySpaceId(Expression side) =>
        StripConversions(side) is MemberExpression { Expression: ParameterExpression } member
        && member.Member.Name == SchemaNames.SpaceIdColumn;

    private static bool IsContextState(Expression side) =>
        StripConversions(side) is MemberExpression member
        && member.Member.DeclaringType == typeof(YelloDbContext);

    /// <summary>
    /// Removes the conversion C# inserts when a <c>Guid</c> column is compared with a <c>Guid?</c>.
    /// </summary>
    private static Expression StripConversions(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Convert } conversion
            ? StripConversions(conversion.Operand)
            : expression;

    /// <summary>
    /// Whether a query filter's expression calls anything.
    /// </summary>
    private sealed class FilterShape : ExpressionVisitor
    {
        public bool CallsAMethod { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            CallsAMethod = true;

            return base.VisitMethodCall(node);
        }
    }
}
