using Yello.Domain.Memberships;
using Yello.Domain.Spaces;
using Yello.Domain.Statuses;

namespace Yello.Domain.Accounts;

/// <summary>
/// Everything registration writes, written in one transaction. AD-22.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole registration is one method because AD-22 makes it one transaction.</b>
/// "Registration completing with anything other than exactly one owned Space is a failed
/// transaction, not a repairable state" (<c>ARCHITECTURE-SPINE.md:210</c>). A port offering
/// <c>AddAccount</c>, <c>AddSpace</c> and <c>AddMembership</c> separately would let a caller
/// commit two of the three and leave precisely the state AC3 says cannot exist, so the port does
/// not offer that shape at all.
/// </para>
/// <para>
/// <b>The implementation sets the row-level security session context inside the
/// transaction</b>, from the <c>SpaceId</c> the slice generated - see
/// <see cref="IIdentifierGenerator"/> for why that honours AD-2 rather than working around it.
/// The slice never sets it, and never sees it.
/// </para>
/// </remarks>
public interface IAccountRegistrationStore
{
    /// <summary>
    /// Commits an Account, its Space, its Owner Membership and its default Statuses together, or
    /// commits none of them.
    /// </summary>
    /// <param name="registration">The four sets of rows, already fully formed.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>
    /// Whether the rows were written, or the address was already taken. An outcome rather than an
    /// exception because the caller's response is identical either way and an exception would
    /// tempt a handler into telling them apart.
    /// </returns>
    Task<RegistrationOutcome> RegisterAsync(
        AccountRegistration registration,
        CancellationToken cancellationToken);
}

/// <summary>
/// The rows one registration writes.
/// </summary>
/// <param name="Account">The new Account.</param>
/// <param name="Space">The Space provisioned for it - an ordinary Space in every respect.</param>
/// <param name="OwnerMembership">The single Membership at Role Owner joining the two.</param>
/// <param name="Statuses">The Space's default Status set.</param>
public sealed record AccountRegistration(
    Account Account,
    Space Space,
    Membership OwnerMembership,
    IReadOnlyList<StatusDefinition> Statuses);

/// <summary>
/// What happened, for the slice's own bookkeeping only.
/// </summary>
/// <remarks>
/// <b>This never reaches the wire.</b> AD-23 requires the response to a duplicate registration to
/// be identical to a successful one in status, body, shape and duration, and
/// <c>ARCHITECTURE-SPINE.md:215</c> names a <c>409 Conflict</c> on a duplicate email as "the exact
/// defect AD-23 exists to prevent". This enum exists so the slice can decide whether it has rows
/// to write - not so a caller can decide what to say.
/// </remarks>
public enum RegistrationOutcome
{
    /// <summary>
    /// The address was new, and every row was committed.
    /// </summary>
    Registered,

    /// <summary>
    /// An Account already existed for the address, so nothing was written. The password was
    /// hashed anyway, before this method was ever called.
    /// </summary>
    AddressAlreadyRegistered,
}
