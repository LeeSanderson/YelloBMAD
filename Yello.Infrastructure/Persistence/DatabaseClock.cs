using Microsoft.EntityFrameworkCore;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The database's clock, <c>SYSDATETIMEOFFSET()</c>, as a function a LINQ query can name. AD-27, A-15; story 4.1, AC8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mapped, never evaluated here.</b> <see cref="Map"/> registers <see cref="Now"/> with <c>HasDbFunction</c> as the built-in <c>SYSDATETIMEOFFSET</c>, so a query that names it compares against the clock of
/// the server that runs the statement, inside the statement's own <c>WHERE</c> clause. Called outside a query it throws:
/// a C# clock in an expiry comparison is exactly what AD-27 forbids.
/// </para>
/// <para>
/// <b>One clock for the stamp and the comparison.</b> <c>Invitation.ExpiresAt</c> is stamped by a column default built
/// on the same function, so an Invitation expires when the database says so, whatever the Host's clock reads.
/// </para>
/// </remarks>
public static class DatabaseClock
{
    /// <summary>
    /// The SQL Server built-in this maps to.
    /// </summary>
    internal const string FunctionName = "SYSDATETIMEOFFSET";

    /// <summary>
    /// Registers <see cref="Now"/> on the model as the built-in <c>SYSDATETIMEOFFSET()</c>. Called once from
    /// <see cref="YelloDbContext"/>.
    /// </summary>
    /// <param name="modelBuilder">The model.</param>
    internal static void Map(ModelBuilder modelBuilder) =>
        modelBuilder.HasDbFunction(typeof(DatabaseClock).GetMethod(nameof(Now))!)
            .HasName(FunctionName)
            .IsBuiltIn();

    /// <summary>
    /// The database's current instant. Translated to <c>SYSDATETIMEOFFSET()</c>; never called in process.
    /// </summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="InvalidOperationException">Always: this exists only to be translated.</exception>
    public static DateTimeOffset Now() =>
        throw new InvalidOperationException("DatabaseClock.Now is translated to SYSDATETIMEOFFSET() and has no in-process value.");
}
