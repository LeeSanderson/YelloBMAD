using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;
using Yello.Domain.Statuses;

namespace Yello.Application.Accounts.RegisterAccount;

/// <summary>
/// The one slice that creates an Account. AD-22 / AR-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exactly one slice creates an Account, and it provisions the Space and the Owner Membership
/// in the same transaction</b> (<c>ARCHITECTURE-SPINE.md:210</c>). Story 4.3 -
/// registration-while-accepting-an-Invitation - delegates to this exact handler and then adds the
/// invited Space's Membership as a <i>separate, additional</i> row
/// (<c>epics.md:1703-1706</c>). So this must provision once and only once, and 4.3 must not need
/// a second provisioning path. That is why the naming, the default Statuses and the Owner
/// Membership are all assembled here rather than at the endpoint.
/// </para>
/// <para>
/// <b>This handler returns nothing, and that is the enforcement mechanism for AD-23.</b> The
/// store knows whether the address was new; the handler discards that knowledge deliberately, so
/// there is no value for an endpoint, a log line or a future story to branch on. A
/// <c>bool</c> return here would be the whole defect: <c>ARCHITECTURE-SPINE.md:215</c> names a
/// <c>409 Conflict</c> on a duplicate email as "the exact defect AD-23 exists to prevent", and
/// the cheapest way to arrive at one is to hand a caller the fact and trust it not to use it.
/// </para>
/// <para>
/// <b>Nothing cross-cutting is re-implemented here</b> (AR-3). No authorisation, no Space
/// resolution, no refusal recording, no idempotency and no bound check. Those belong to the
/// request pipeline, and stories 1.5, 1.6 and 1.7 build it. Registration is unauthenticated, so
/// there is nothing for authorisation to decide; and no NFR-8 bound applies - the bound registry
/// is built in 1.6 and assigns Spaces-per-Account to story 3.1.
/// </para>
/// </remarks>
/// <param name="passwordHasher">Turns the password into the only form Yello stores.</param>
/// <param name="registrationStore">Commits every row, or none.</param>
/// <param name="personalSpaceNaming">Names the provisioned Space, from one resource string.</param>
/// <param name="identifiers">Generates ids before the insert, which is what AD-2 needs.</param>
/// <param name="clock">
/// The time source. <c>TimeProvider</c> rather than <c>DateTimeOffset.UtcNow</c> so a test can
/// assert that all five rows carry the same instant - and because
/// <c>DateTimeOffset.Now</c> is a banned API at build.
/// </param>
public sealed class RegisterAccountHandler(
    IPasswordHasher passwordHasher,
    IAccountRegistrationStore registrationStore,
    IPersonalSpaceNaming personalSpaceNaming,
    IIdentifierGenerator identifiers,
    TimeProvider clock)
{
    /// <summary>
    /// Registers an Account, or does nothing at all - and never says which.
    /// </summary>
    /// <param name="command">The submission, already validated.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    public async Task HandleAsync(RegisterAccountCommand command, CancellationToken cancellationToken)
    {
        // FIRST, AND UNCONDITIONALLY. AD-23 requires the duplicate path to be indistinguishable
        // from the new-address path in duration, and the hash is where essentially all of that
        // duration lives - measured at ~273 ms p50 against single-digit milliseconds for
        // everything below. Moving this after any check of stored state, or skipping it when the
        // address is known, is the timing oracle the decision exists to close. It is above the
        // store call rather than inside it so that this ordering is visible in the slice that
        // owns the use case.
        var passwordHash = passwordHasher.Hash(command.Password);

        var displayName = command.DisplayName.Trim();
        var emailAddress = command.EmailAddress.Trim();

        // One instant for every row, read once. Five rows created "now" by five separate reads
        // of the clock would carry five slightly different instants, which makes any later
        // ordering question - which came first, the Space or its Owner - answerable only by
        // luck.
        var createdAt = clock.GetUtcNow();

        var accountId = identifiers.Next();
        var spaceId = identifiers.Next();

        var account = new Account
        {
            Id = accountId,
            EmailAddress = emailAddress,
            NormalizedEmailAddress = EmailAddressNormalisation.Normalise(emailAddress),
            DisplayName = displayName,
            PasswordHash = passwordHash,
            CreatedAt = createdAt,
        };

        // An ordinary Space. Nothing marks it as provisioned rather than created, because AC4
        // requires that no attribute distinguishes it from a Space made by any other route.
        var space = new Space
        {
            Id = spaceId,
            Name = personalSpaceNaming.NameFor(displayName),
            CreatedAt = createdAt,
        };

        // Ownership is a Membership at Role Owner, never an OwnerId column on Space
        // (addendum.md:33). That is what makes FR-3's erasure work: ownership cannot be forced
        // onto an Account, so deleting one removes rows rather than orphaning a Space.
        var ownerMembership = new Membership
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            AccountId = accountId,
            Role = Role.Owner,
            CreatedAt = createdAt,
        };

        // FR-24's three defaults, identity-bearing so a later rename moves no Task (AR-23).
        // Position comes from the index, so DefaultStatusSet.Names is the one place the order is
        // stated. No per-Project effective Status set is materialised - no table stores one.
        var statuses = DefaultStatusSet.Names
            .Select((name, index) => new StatusDefinition
            {
                Id = identifiers.Next(),
                SpaceId = spaceId,
                Name = name,
                Position = index,
                CreatedAt = createdAt,
            })
            .ToList();

        var registration = new AccountRegistration(account, space, ownerMembership, statuses);

        // The outcome is discarded, deliberately and permanently. See the class remarks: the
        // caller must not be able to tell a new address from a known one, and the surest way to
        // guarantee that is for this method to have nothing to tell it.
        _ = await registrationStore.RegisterAsync(registration, cancellationToken).ConfigureAwait(false);
    }
}
