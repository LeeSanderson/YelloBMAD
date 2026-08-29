using System.Data.SqlTypes;
using Xunit;
using Yello.Domain;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// The identifier generator produces values that are sequential <i>in a SQL Server index</i>.
/// AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <b>This asserts the property rather than the name, and the distinction is the whole point.</b>
/// "Uses <c>SequentialGuidValueGenerator</c>" is a statement about which type was called;
/// "produces values that ascend under SQL Server's own comparison" is the thing the clustered
/// index actually needs, and only the second is checkable. A future EF Core release that changed
/// the generator's byte layout - to UUIDv7, say - would keep the first true and break the second
/// silently.
/// </para>
/// <para>
/// <b>Why UUIDv7 would break it, since that is the alternative the architecture names and
/// excludes.</b> <c>uniqueidentifier</c> does not compare bytes in the order they are written: it
/// orders by the last six bytes first, then the two before them, and so on. A UUIDv7 is monotonic
/// read left to right, which puts its timestamp in the bytes SQL Server sorts <i>last</i> - so a
/// run of them scatters across the index exactly as random values do.
/// </para>
/// <para>
/// <c>SqlGuid</c> is what makes this assertable without a database: it is the BCL's implementation
/// of SQL Server's own <c>uniqueidentifier</c> comparison semantics, so the comparison here is the
/// engine's rather than a reimplementation of it. The story's standing caveat asks for exactly
/// this - two plausible-but-wrong SQL Server claims have already been caught in this project,
/// "both in <i>index behaviour</i> specifically", and this is the first index the product has.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-34")]
public sealed class SequentialGuidIdentifierGeneratorTests
{
    /// <summary>
    /// Long enough that a generator relying on the clock alone would have to tick, and short
    /// enough to stay instant.
    /// </summary>
    private const int RunLength = 500;

    [Fact]
    public void Successive_identifiers_ascend_under_SQL_Servers_own_ordering()
    {
        IIdentifierGenerator generator = new SequentialGuidIdentifierGenerator();

        var identifiers = Enumerable.Range(0, RunLength)
            .Select(_ => generator.Next())
            .ToList();

        var outOfOrder = new List<string>();

        for (var index = 1; index < identifiers.Count; index++)
        {
            var previous = new SqlGuid(identifiers[index - 1]);
            var current = new SqlGuid(identifiers[index]);

            if (previous.CompareTo(current) >= 0)
            {
                outOfOrder.Add($"#{index}: {identifiers[index - 1]} then {identifiers[index]}");
            }
        }

        Assert.True(
            outOfOrder.Count == 0,
            "Identifiers did not ascend under SqlGuid ordering, so inserts would scatter across " +
            "the clustered index rather than appending to it. That is what AR-34 excludes " +
            "Guid.NewGuid() and Guid.CreateVersion7() for, and it is the property - not the type " +
            $"name - that matters:{Environment.NewLine}" +
            string.Join(Environment.NewLine, outOfOrder.Take(5)));
    }

    [Fact]
    public void Every_identifier_is_distinct()
    {
        IIdentifierGenerator generator = new SequentialGuidIdentifierGenerator();

        var identifiers = Enumerable.Range(0, RunLength)
            .Select(_ => generator.Next())
            .ToList();

        Assert.Equal(RunLength, identifiers.Distinct().Count());
    }

    /// <summary>
    /// The generator produces a usable value when called exactly as production calls it.
    /// </summary>
    /// <remarks>
    /// Production passes <c>null</c> for the <c>EntityEntry</c> the generator does not read, under
    /// a narrowly-scoped analyser suppression. That is safe today and is not guaranteed forever,
    /// so this calls the adapter the same way rather than calling EF Core's generator directly -
    /// which is what would fail here, loudly, if a later EF Core release started reading the
    /// entry.
    /// </remarks>
    [Fact]
    public void The_generator_answers_when_called_the_way_production_calls_it()
    {
        IIdentifierGenerator generator = new SequentialGuidIdentifierGenerator();

        Assert.NotEqual(Guid.Empty, generator.Next());
    }
}
