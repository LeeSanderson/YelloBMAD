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
/// <b>Reusable by story 4.3 for the provisioning half, and NOT for the Membership half.</b>
/// Registration-while-accepting-an-Invitation delegates to this same path and then adds the
/// invited Space's Membership as a separate, additional row (<c>epics.md:1703-1706</c>). So this
/// must provision exactly once and must not grow a second provisioning route for that case - that
/// part holds.
/// <para>
/// <b>What does not hold, found at code review on 2026-09-10.</b> This paragraph used to say
/// "built to be called by story 4.3" without qualification, and the invited Membership cannot be
/// written on this unit of work. <c>@read_only = 1</c> makes the session context key immutable for
/// the life of the <i>session</i>, not the transaction, and the <c>BLOCK PREDICATE ... AFTER
/// INSERT</c> on <c>Membership</c> refuses any row whose <c>SpaceId</c> the context does not name.
/// The context here names the <i>personal</i> Space. So 4.3 cannot insert the invited Space's
/// Membership on this connection at all: it needs its own unit of work, which makes
/// registration-while-accepting non-atomic across the two Spaces, or it reaches for the exemption
/// AD-24 exists to forbid. That is a real design question for 4.3 and it is recorded in
/// <c>deferred-work.md</c> rather than answered here - the ledger previously carried only the
/// <i>read</i> consequence of this context (the Space switcher, which story 1.7 built under its own
/// <c>AccountId</c> key), not the write one.
/// </para>
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

    /// <inheritdoc />
    public async Task<RegistrationOutcome> RegisterAsync(
        AccountRegistration registration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // The same setter every Space-scoped request goes through. Registration never meets that
        // path - it is unauthenticated and its route carries no {spaceId} - so the read-only key is
        // set exactly once on this connection.
        await SpaceSessionContext
            .SetAsync(dbContext.Database, registration.Space.Id, cancellationToken)
            .ConfigureAwait(false);

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

            // BUT a unique violation is not necessarily THIS unique violation, and until code
            // review on 2026-09-10 this branch assumed it was. Error 2601/2627 cover every unique
            // index and constraint in the schema, primary keys included - so a colliding
            // application-generated id would land here too and be answered as "that address is
            // taken": a 204 identical to success, with no Account written, nothing logged, and
            // the person told their registration completed. Silent data loss wearing AD-23's
            // uniform response as a disguise.
            //
            // Asking the database whether the address is actually taken is the check that does
            // not depend on index names or on a localised error message - the reason this class
            // already refuses to match on message text. It costs one indexed lookup on a path
            // that only a genuine race reaches, so it changes nothing about the duration AD-23
            // cares about: the ordinary duplicate path returns above, having never come here.
            var addressIsTaken = await dbContext.Accounts
                .AnyAsync(
                    account => account.NormalizedEmailAddress == registration.Account.NormalizedEmailAddress,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!addressIsTaken)
            {
                throw;
            }

            return RegistrationOutcome.AddressAlreadyRegistered;
        }
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
