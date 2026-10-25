using Microsoft.Data.SqlClient;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Whether a failure means the database could not be reached or is not available right now - paused,
/// resuming, offline, or out of reach - as opposed to a defect in what was asked of it. Story 1.10
/// AC14; Lee's decision (a) at code review, 2026-10-25.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only availability is retryable.</b> The Host answers an availability failure with a uniform
/// <c>503</c> and <c>Retry-After</c>. Every other provider error - a constraint or row-level security
/// violation, a deadlock, a permission denial, an object a migration did not create - is a defect or a
/// refusal, and it stays an unhandled <c>500</c> where it is seen, rather than being told to retry.
/// </para>
/// <para>
/// <b>Classified here, so the Host does not learn the provider.</b> The numbers are SQL Server's and
/// Azure SQL Database's: the connection-level network errors, "cannot open database" and "database is
/// offline", Azure SQL's "database not currently available" (40613, the paused serverless database),
/// the service-busy and resource-limit errors, and a session killed under the request. A severity of 20
/// or above is a fatal error that ends the connection, and counts as unavailability too.
/// </para>
/// </remarks>
public static class DatabaseUnavailability
{
    private const byte FatalSeverity = 20;

    private static readonly HashSet<int> AvailabilityNumbers =
    [
        -2,     // the connection or command timed out
        -1,     // a network-related or instance-specific error while connecting
        2,      // the server was not found or was not accessible
        53,     // the network path was not found
        64,     // the specified network name is no longer available
        233,    // no process is on the other end of the pipe
        596,    // the session is in the kill state
        942,    // the database cannot be opened because it is offline
        4060,   // cannot open the database requested by the login
        10053,  // the connection was aborted by the host
        10054,  // the connection was reset by the peer
        10060,  // the connection attempt timed out
        10061,  // the connection was refused
        10928,  // Azure SQL: a resource limit was reached
        10929,  // Azure SQL: the minimum guarantee could not be met
        11001,  // the host was not found
        40143,  // Azure SQL: the service is processing the request
        40197,  // Azure SQL: the service encountered an error processing the request
        40501,  // Azure SQL: the service is busy
        40613,  // Azure SQL: the database is not currently available (paused or resuming)
        49918,  // Azure SQL: not enough resources to process the request
        49919,  // Azure SQL: too many operations in progress
        49920,  // Azure SQL: too many operations in progress for the subscription
    ];

    /// <summary>
    /// Whether <paramref name="exception"/>, or one it wraps, is the database being unavailable.
    /// </summary>
    /// <param name="exception">The exception a request ended with.</param>
    /// <returns><c>true</c> for an availability failure anywhere in the chain; <c>false</c> otherwise.</returns>
    /// <remarks>
    /// EF Core wraps a provider failure in an <see cref="InvalidOperationException"/>, and the Data
    /// Protection system wraps a key-ring failure in a <c>CryptographicException</c>; the provider's own
    /// exception is inside both.
    /// </remarks>
    public static bool IsUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && IsAvailabilityError(sql))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAvailabilityError(SqlException exception)
    {
        if (exception.Class >= FatalSeverity || AvailabilityNumbers.Contains(exception.Number))
        {
            return true;
        }

        foreach (SqlError error in exception.Errors)
        {
            if (error.Class >= FatalSeverity || AvailabilityNumbers.Contains(error.Number))
            {
                return true;
            }
        }

        return false;
    }
}
