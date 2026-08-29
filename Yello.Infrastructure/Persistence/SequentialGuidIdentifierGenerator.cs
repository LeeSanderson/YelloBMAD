using Microsoft.EntityFrameworkCore.ValueGeneration;
using Yello.Domain;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// <see cref="IIdentifierGenerator"/> over EF Core's <see cref="SequentialGuidValueGenerator"/>.
/// </summary>
/// <remarks>
/// <para>
/// The adapter exists because the generator is an EF Core type and the port is declared in
/// <c>Yello.Domain</c>, which references nothing at all - a rule Gate B enforces against compiled
/// bytecode, so an unused <c>using</c> would not save it.
/// </para>
/// <para>
/// <b>Why this generator and not the two obvious alternatives</b>, per AR-34 and
/// <c>ARCHITECTURE-SPINE.md:268</c>: a database identity column would generate after the insert,
/// which defeats the whole reason registration can set the row-level security session context to
/// its own new <c>SpaceId</c>; and <c>Guid.NewGuid()</c> scatters inserts across a clustered
/// index. <c>Guid.CreateVersion7()</c> is named and excluded too, and the reason is specific to
/// SQL Server rather than to UUIDv7: <c>uniqueidentifier</c> does not compare bytes in the order
/// they are written. It orders by the last six bytes first, so a value that is monotonic when
/// read left to right - which UUIDv7 is, by construction - is <i>not</i> monotonic in the index.
/// </para>
/// <para>
/// <b>That property is asserted rather than believed.</b>
/// <c>SequentialGuidIdentifierGeneratorTests</c> generates a run of ids and compares them under
/// <c>System.Data.SqlTypes.SqlGuid</c>, which implements SQL Server's own ordering. The story's
/// standing caveat is that two plausible-but-wrong SQL Server claims have already been caught in
/// this project, "both in <i>index behaviour</i> specifically" - and this is the first index the
/// product has, resting on the first ordering claim it makes.
/// </para>
/// </remarks>
internal sealed class SequentialGuidIdentifierGenerator : IIdentifierGenerator
{
    private readonly SequentialGuidValueGenerator _generator = new();

    /// <inheritdoc />
    public Guid Next()
    {
        // MA0191 disabled here, deliberately and for one expression. The rule is right in
        // general: a null-forgiving operator is usually a claim the compiler cannot check. This
        // is the case it is wrong about, and the alternatives are all worse.
        //
        // `EntityEntry` is part of the `ValueGenerator<T>` contract and this generator never
        // reads it - it composes a value from a counter and a fresh Guid, with no reference to
        // the entity being tracked - and there is no overload that omits the parameter.
        // Fabricating a real entry would need a live change tracker and an entity instance,
        // which is a DbContext and a transaction's worth of machinery to satisfy an argument
        // that is discarded.
        //
        // What makes this safe to state rather than to hope: SequentialGuidIdentifierGeneratorTests
        // calls this method exactly as production does, so a later EF Core release that started
        // reading the entry would fail the suite here rather than in a request.
#pragma warning disable MA0191 // Do not use the null-forgiving operator
        return _generator.Next(entry: null!);
#pragma warning restore MA0191
    }
}
