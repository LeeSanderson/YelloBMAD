using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The one statement row-level security rests on: <c>sp_set_session_context 'SpaceId', ...,
/// @read_only = 1</c>. AD-2 / AR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only place in the product that executes it.</b> Registration and every Space-scoped
/// request both come through here, and
/// <c>SpaceResolutionSeamTests.Exactly_one_production_call_site_sets_the_Space_session_context</c>
/// fails the build on a second copy. A second copy is a live defect as well as a duplicate: the key
/// is <c>@read_only = 1</c>, so two setters on one unit of work do not merely repeat each other -
/// SQL Server refuses the second with error 15664 and the request fails.
/// </para>
/// <para>
/// <b>What <c>@read_only = 1</c> costs, measured rather than read.</b> Both claims this design
/// rests on are executed against a live engine on every build
/// (<c>SpaceIsolationLayerTests.A_read_only_session_key_is_cleared_by_the_pool_and_refuses_a_second_set</c>):
/// the key cannot be set again on the same logical connection, and <c>sp_reset_connection</c>
/// clears it when the pool hands the physical connection to its next consumer. Together they make
/// the rule: <b>a unit of work owns its connection from open to close</b>, which in EF Core means an
/// explicit transaction - without one the context closes the connection between statements, and a
/// reopened connection has no session context at all.
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
    internal static async Task SetAsync(
        DatabaseFacade database,
        Guid spaceId,
        CancellationToken cancellationToken)
    {
        var key = new SqlParameter("@key", SqlDbType.NVarChar, SessionKeyMaxLength)
        {
            Value = SchemaNames.SpaceIdSessionKey,
        };

        var value = new SqlParameter("@value", SqlDbType.Variant) { Value = spaceId };

        await database
            .ExecuteSqlRawAsync(
                "EXEC sp_set_session_context @key = @key, @value = @value, @read_only = 1",
                [key, value],
                cancellationToken)
            .ConfigureAwait(false);
    }
}
