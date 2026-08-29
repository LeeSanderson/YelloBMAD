using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Domain.Accounts;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Writes an Account, its Space, its Owner Membership and its default Statuses in one
/// transaction, with the row-level security session context set inside it. AD-22, AD-2.
/// </summary>
/// <remarks>
/// <para>
/// <b>This class is where AD-2's registration seam is actually resolved</b>, and the resolution
/// is worth stating because it is not obvious. AD-2 requires
/// <c>sp_set_session_context 'SpaceId', ..., @read_only = 1</c> at the start of every unit of
/// work "from <c>ActiveSpaceContext</c> and never from a client-supplied value". Registration is
/// unauthenticated, has no <c>ActiveSpaceContext</c>, and creates the very Space whose id the
/// context needs.
/// </para>
/// <para>
/// Because ids are generated application-side (<see cref="SequentialGuidIdentifierGenerator"/>),
/// the slice produces the <c>SpaceId</c> before anything is written; this class sets the session
/// context to it inside the transaction and only then inserts. The value is server-generated, so
/// AD-2's prohibition is honoured rather than bypassed. <b>Never</b> resolve this by disabling a
/// policy, opening a second connection or inventing an exemption - AD-24
/// (<c>ARCHITECTURE-SPINE.md:222</c>) names precisely that as the failure it exists to prevent,
/// "and that bypass then spreading".
/// </para>
/// <para>
/// <b>The whole registration is one transaction because AD-22 makes partial success
/// unrepresentable:</b> "Registration completing with anything other than exactly one owned Space
/// is a failed transaction, not a repairable state". Nothing here compensates, retries a part, or
/// leaves an Account holding zero Spaces or two.
/// </para>
/// <para>
/// <b>Built to be called by story 4.3.</b> Registration-while-accepting-an-Invitation delegates
/// to this same path and then adds the invited Space's Membership as a separate, additional row
/// (<c>epics.md:1703-1706</c>). So this must provision exactly once and must not grow a second
/// provisioning route for that case.
/// </para>
/// </remarks>
/// <param name="dbContext">The unit of work this registration is written through.</param>
internal sealed class AccountRegistrationStore(YelloDbContext dbContext) : IAccountRegistrationStore
{
    /// <summary>
    /// SQL Server's "duplicate key row in object with unique index".
    /// </summary>
    private const int DuplicateKeyRowErrorNumber = 2601;

    /// <summary>
    /// SQL Server's "violation of UNIQUE KEY constraint".
    /// </summary>
    private const int UniqueConstraintViolationErrorNumber = 2627;

    /// <summary>
    /// <c>sp_set_session_context</c>'s key parameter is <c>sysname</c>, which is
    /// <c>nvarchar(128)</c>.
    /// </summary>
    private const int SessionKeyMaxLength = 128;

    /// <inheritdoc />
    public async Task<RegistrationOutcome> RegisterAsync(
        AccountRegistration registration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await SetSpaceSessionContextAsync(registration.Space.Id, cancellationToken).ConfigureAwait(false);

        // Inside the transaction, so the check and the insert see one consistent state. It is
        // not the guarantee, though - the unique index below is. Under READ COMMITTED two
        // concurrent registrations for the same address can both pass this, which is why the
        // duplicate-key handler exists rather than being defensive padding.
        var alreadyRegistered = await dbContext.Accounts
            .AnyAsync(
                account => account.NormalizedEmailAddress == registration.Account.NormalizedEmailAddress,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyRegistered)
        {
            // Refused server-side and silently. The caller has already hashed the password, and
            // the response it produces is identical to the success path's in status, body, shape
            // and duration - AD-23. Nothing is logged that distinguishes this from a new
            // address either.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return RegistrationOutcome.AddressAlreadyRegistered;
        }

        dbContext.Accounts.Add(registration.Account);
        dbContext.Spaces.Add(registration.Space);
        dbContext.Memberships.Add(registration.OwnerMembership);
        dbContext.StatusDefinitions.AddRange(registration.Statuses);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return RegistrationOutcome.Registered;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // The race the check above cannot close. Reported as the same outcome, so the
            // response stays uniform even when two people register the same address at once.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return RegistrationOutcome.AddressAlreadyRegistered;
        }
    }

    /// <summary>
    /// Sets the row-level security session context for this transaction.
    /// </summary>
    /// <remarks>
    /// <c>@read_only = 1</c> is AD-2's, and it is the half that makes the context a boundary
    /// rather than a suggestion: once set, nothing later in the same session can move it, so a
    /// query that ran after some other code changed the Space cannot exist.
    /// <para>
    /// Parameterised with explicit types rather than interpolated. The key parameter is
    /// <c>sysname</c> and the value is <c>sql_variant</c>; letting the provider infer them sends
    /// an <c>nvarchar(4000)</c> key into a 128-character parameter, which is a conversion nobody
    /// asked for in the one statement the whole isolation model rests on.
    /// </para>
    /// </remarks>
    private async Task SetSpaceSessionContextAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        var key = new SqlParameter("@key", SqlDbType.NVarChar, SessionKeyMaxLength)
        {
            Value = SchemaNames.SpaceIdSessionKey,
        };

        var value = new SqlParameter("@value", SqlDbType.Variant) { Value = spaceId };

        await dbContext.Database
            .ExecuteSqlRawAsync(
                "EXEC sp_set_session_context @key = @key, @value = @value, @read_only = 1",
                [key, value],
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// True when a failed save was a unique-index or unique-constraint violation.
    /// </summary>
    /// <remarks>
    /// Both numbers, because SQL Server uses 2601 for a unique <i>index</i> and 2627 for a
    /// unique <i>constraint</i> and this schema has one of each shape. Matching on the message
    /// text was the alternative and is not one: it is localised by the server's language
    /// setting, so a gate resting on it would pass on an English container and fail in
    /// production.
    /// </remarks>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Errors.Cast<SqlError>().Any(error =>
            error.Number is DuplicateKeyRowErrorNumber or UniqueConstraintViolationErrorNumber);
}
