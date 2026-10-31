using Microsoft.Data.SqlClient;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Whether a failure is a statement refused by a foreign-key reference (SQL Server error 547) - the failure a Space-scoped
/// write meets when its Space was deleted under it. Story 3.3, AC13, open question 4 (a).
/// </summary>
/// <remarks>
/// <b>Classified here, so the Host does not learn the provider</b> - as <see cref="DatabaseUnavailability"/> is. The
/// pipeline does not act on this alone: it resolves the request's Account in its Space again, and rethrows when the
/// Membership is still there, so a reference conflict that is a defect stays a <c>500</c>.
/// </remarks>
public static class ReferenceConflict
{
    /// <summary>
    /// SQL Server's "statement conflicted with a FOREIGN KEY / REFERENCE constraint" - and also its CHECK constraint
    /// conflict, which shares the number. That is safe here only because the pipeline re-resolves before it answers: a
    /// CHECK conflict under a Membership that still exists is rethrown.
    /// </summary>
    private const int ReferenceConflictErrorNumber = 547;

    /// <summary>
    /// Whether <paramref name="exception"/>, or one it wraps, is a reference conflict.
    /// </summary>
    /// <param name="exception">The exception a unit of work ended with.</param>
    /// <returns><c>true</c> for a reference conflict anywhere in the chain.</returns>
    /// <remarks>EF Core wraps the provider's exception in a <c>DbUpdateException</c>; it is inside.</remarks>
    public static bool IsReferenceConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql
                && (sql.Number == ReferenceConflictErrorNumber
                    || sql.Errors.Cast<SqlError>().Any(error => error.Number == ReferenceConflictErrorNumber)))
            {
                return true;
            }
        }

        return false;
    }
}
