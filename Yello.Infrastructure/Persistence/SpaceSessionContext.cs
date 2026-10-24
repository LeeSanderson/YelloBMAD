using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The one statement row-level security rests on: <c>sp_set_session_context &lt;key&gt;, ...,
/// @read_only = 1</c>. AD-2 / AR-6, AD-24 / AR-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only place in the product that executes it, for both keys.</b> Registration and every
/// Space-scoped request set <c>SpaceId</c> through <see cref="SetAsync"/>; the Space switcher, the
/// first of AD-24's two Account-scoped surfaces, sets <c>AccountId</c> through
/// <see cref="SetAccountAsync"/>. Both reach the one private line below, and
/// <c>SpaceResolutionSeamTests.Exactly_one_production_line_sets_a_session_context_key</c> fails the
/// build on a second copy. A second copy is a live defect as well as a duplicate: each key is
/// <c>@read_only = 1</c>, so two setters of one key on one unit of work do not merely repeat each
/// other - SQL Server refuses the second with error 15664 and the request fails.
/// </para>
/// <para>
/// <b>A unit of work sets exactly one key, and which one is decided by who calls which entry
/// point.</b> <c>Membership</c>'s filter admits a row when <i>either</i> key matches (story 1.7, open
/// question 1), so a connection carrying both would show a Space-scoped request the Account's
/// Memberships - and Roles - in every other Space it belongs to. The key set is closed
/// (<see cref="SessionContextKey"/>), and
/// <c>SpaceSwitcherGateTests.No_caller_sets_both_session_context_keys</c> holds the callers apart.
/// </para>
/// <para>
/// <b>What <c>@read_only = 1</c> costs, measured rather than read.</b> Both claims this design
/// rests on are executed against a live engine on every build
/// (<c>SpaceIsolationLayerTests.A_read_only_session_key_is_cleared_by_the_pool_and_refuses_a_second_set</c>,
/// and <c>PooledConnectionIsolationTests</c> for the <c>AccountId</c> key): the key cannot be set
/// again on the same logical connection, and <c>sp_reset_connection</c> clears it when the pool
/// hands the physical connection to its next consumer. Together they make the rule: <b>a unit of
/// work owns its connection from open to close</b>, which in EF Core means an explicit transaction -
/// without one the context closes the connection between statements, and a reopened connection has
/// no session context at all.
/// </para>
/// <para>
/// <b>Parameterised with explicit types rather than interpolated.</b> The key parameter is
/// <c>sysname</c> and the value is <c>sql_variant</c>; letting the provider infer them sends an
/// <c>nvarchar(4000)</c> key into a 128-character parameter, which is a conversion nobody asked for
/// in the one statement the whole isolation model rests on.
/// </para>
/// </remarks>
internal static class SpaceSessionContext
{
    /// <summary>
    /// <c>sp_set_session_context</c>'s key parameter is <c>sysname</c>, which is
    /// <c>nvarchar(128)</c>.
    /// </summary>
    private const int SessionKeyMaxLength = 128;

    /// <summary>
    /// Names a Space on the current connection, for the rest of that connection's life.
    /// </summary>
    /// <param name="database">
    /// The unit of work's database facade. The caller must already hold a transaction on it, so the
    /// connection this statement runs on is the one every later statement runs on.
    /// </param>
    /// <param name="spaceId">The Space the connection is scoped to.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task that completes once the context is set.</returns>
    internal static Task SetAsync(
        DatabaseFacade database,
        Guid spaceId,
        CancellationToken cancellationToken) =>
        SetKeyAsync(database, SessionContextKey.SpaceId, spaceId, cancellationToken);

    /// <summary>
    /// Names an Account on the current connection, for the rest of that connection's life - the
    /// session context an Account-scoped unit of work runs under (AD-24).
    /// </summary>
    /// <param name="database">
    /// The unit of work's database facade, already holding a transaction - see <see cref="SetAsync"/>.
    /// </param>
    /// <param name="accountId">
    /// The Account, from the Session the server issued - never from a client-supplied value (AD-2).
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task that completes once the context is set.</returns>
    internal static Task SetAccountAsync(
        DatabaseFacade database,
        Guid accountId,
        CancellationToken cancellationToken) =>
        SetKeyAsync(database, SessionContextKey.AccountId, accountId, cancellationToken);

    private static async Task SetKeyAsync(
        DatabaseFacade database,
        SessionContextKey key,
        Guid value,
        CancellationToken cancellationToken)
    {
        var keyParameter = new SqlParameter("@key", SqlDbType.NVarChar, SessionKeyMaxLength)
        {
            Value = key == SessionContextKey.AccountId ? SchemaNames.AccountIdSessionKey : SchemaNames.SpaceIdSessionKey,
        };

        var valueParameter = new SqlParameter("@value", SqlDbType.Variant) { Value = value };

        await database
            .ExecuteSqlRawAsync(
                "EXEC sp_set_session_context @key = @key, @value = @value, @read_only = 1",
                [keyParameter, valueParameter],
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// The session-context keys the product sets - exactly the two the spine names: <c>SpaceId</c>
/// (AD-2) and <c>AccountId</c> (AD-24). A third is an architecture amendment, not a code change.
/// </summary>
internal enum SessionContextKey
{
    /// <summary>
    /// <c>SESSION_CONTEXT('SpaceId')</c>, set by every Space-scoped unit of work.
    /// </summary>
    SpaceId,

    /// <summary>
    /// <c>SESSION_CONTEXT('AccountId')</c>, set only by an Account-scoped unit of work.
    /// </summary>
    AccountId,
}
