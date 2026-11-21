using Xunit;
using Yello.Application.Accounts.DeleteAccount;
using Yello.Application.Spaces;

namespace Yello.Tests.Slices.Accounts.DeleteAccount;

/// <summary>
/// The deletion slice's ordering of its three steps: check, leave each Space, erase - and when it stops.
/// </summary>
/// <remarks>
/// No container and no database. The steps are a fake that scripts what each returns and records what was asked, because
/// what is under test is the sequence and its exits; the wire cases in <c>ActiveSpaceEndpointTests.DeleteAccount</c> hold
/// what the steps do to the database.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-3")]
[Trait("Requirement", "AD-5")]
[Trait("Requirement", "AD-24")]
public sealed class DeleteAccountHandlerTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    /// <summary>
    /// An Account that owns a Space is refused naming what it owns, and no Space is left and nothing is erased.
    /// </summary>
    [Fact]
    public async Task An_owner_is_refused_before_any_Space_is_left()
    {
        var owned = new SpaceIdentity(Guid.NewGuid(), "Owned");
        var steps = new ScriptedSteps { Checks = { new AccountHoldings([owned], [owned.Id, Guid.NewGuid()]) } };

        var result = await HandleAsync(steps);

        Assert.Equal(AccountDeletionOutcome.OwnsSpaces, result.Outcome);
        Assert.Equal([owned], result.Owned);
        Assert.Equal(["check"], steps.Calls);
    }

    /// <summary>
    /// An Account that owns nothing leaves every Space it holds, in the order the check gave, then is erased.
    /// </summary>
    [Fact]
    public async Task An_Account_that_owns_nothing_leaves_each_Space_in_order_then_is_erased()
    {
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        var steps = new ScriptedSteps
        {
            Checks = { new AccountHoldings([], [first, second]) },
            Erasures = { new AccountErasure(Erased: true, Empty) },
        };

        var result = await HandleAsync(steps);

        Assert.Equal(AccountDeletionOutcome.Deleted, result.Outcome);
        Assert.Equal(["check", $"leave {first:D}", $"leave {second:D}", "erase"], steps.Calls);
    }

    /// <summary>
    /// A leave that meets an Owner's row - an offer accepted after the check - stops the sequence: the check is read again
    /// and the refusal names what is owned now. Later Spaces are not left and nothing is erased.
    /// </summary>
    [Fact]
    public async Task A_Space_owned_part_way_stops_the_sequence_and_names_what_is_owned_now()
    {
        var (first, second, third) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var nowOwned = new SpaceIdentity(second, "Accepted meanwhile");
        var steps = new ScriptedSteps
        {
            Checks = { new AccountHoldings([], [first, second, third]), new AccountHoldings([nowOwned], [second, third]) },
            OwnedOnLeave = { second },
        };

        var result = await HandleAsync(steps);

        Assert.Equal(AccountDeletionOutcome.OwnsSpaces, result.Outcome);
        Assert.Equal([nowOwned], result.Owned);
        Assert.Equal(["check", $"leave {first:D}", $"leave {second:D}", "check"], steps.Calls);
    }

    /// <summary>
    /// A Membership that arrives between the check and the erase is left in one extra pass, and the second erase finishes.
    /// </summary>
    [Fact]
    public async Task A_Membership_found_by_the_erase_is_left_in_one_extra_pass()
    {
        var (first, arrived) = (Guid.NewGuid(), Guid.NewGuid());
        var steps = new ScriptedSteps
        {
            Checks = { new AccountHoldings([], [first]) },
            Erasures = { new AccountErasure(Erased: false, new AccountHoldings([], [arrived])), new AccountErasure(Erased: true, Empty) },
        };

        var result = await HandleAsync(steps);

        Assert.Equal(AccountDeletionOutcome.Deleted, result.Outcome);
        Assert.Equal(["check", $"leave {first:D}", "erase", $"leave {arrived:D}", "erase"], steps.Calls);
    }

    /// <summary>
    /// Memberships that keep arriving after the extra pass are not chased: the deletion answers interrupted after two
    /// erases, with nothing more left.
    /// </summary>
    [Fact]
    public async Task Memberships_that_keep_arriving_interrupt_the_deletion_after_one_extra_pass()
    {
        var steps = new ScriptedSteps
        {
            Checks = { new AccountHoldings([], [Guid.NewGuid()]) },
            Erasures =
            {
                new AccountErasure(Erased: false, new AccountHoldings([], [Guid.NewGuid()])),
                new AccountErasure(Erased: false, new AccountHoldings([], [Guid.NewGuid()])),
            },
        };

        var result = await HandleAsync(steps);

        Assert.Equal(AccountDeletionOutcome.Interrupted, result.Outcome);
        Assert.Equal(2, steps.Calls.Count(call => call == "erase"));
        Assert.Equal(2, steps.Calls.Count(call => call.StartsWith("leave", StringComparison.Ordinal)));
    }

    /// <summary>
    /// A Space created for the Account after the check is found owned by the erase, which refuses naming it.
    /// </summary>
    [Fact]
    public async Task A_Space_owned_by_the_time_of_the_erase_is_refused_by_name()
    {
        var created = new SpaceIdentity(Guid.NewGuid(), "Created meanwhile");
        var steps = new ScriptedSteps
        {
            Checks = { new AccountHoldings([], []) },
            Erasures = { new AccountErasure(Erased: false, new AccountHoldings([created], [created.Id])) },
        };

        var result = await HandleAsync(steps);

        Assert.Equal(AccountDeletionOutcome.OwnsSpaces, result.Outcome);
        Assert.Equal([created], result.Owned);
        Assert.Equal(["check", "erase"], steps.Calls);
    }

    private static AccountHoldings Empty => new([], []);

    private static Task<AccountDeletionResult> HandleAsync(ScriptedSteps steps) =>
        new DeleteAccountHandler(steps).HandleAsync(new AccountScopedContext(AccountId), TestContext.Current.CancellationToken);

    /// <summary>
    /// Answers each step from its script, in order, and records every call.
    /// </summary>
    private sealed class ScriptedSteps : IAccountDeletion
    {
        private int _checks;
        private int _erasures;

        public List<AccountHoldings> Checks { get; } = [];

        public List<AccountErasure> Erasures { get; } = [];

        public HashSet<Guid> OwnedOnLeave { get; } = [];

        public List<string> Calls { get; } = [];

        public Task<AccountHoldings> CheckAsync(Guid accountId, CancellationToken cancellationToken)
        {
            Assert.Equal(AccountId, accountId);
            Calls.Add("check");

            return Task.FromResult(Checks[_checks++]);
        }

        public Task<SpaceLeave> LeaveAsync(Guid accountId, Guid spaceId, CancellationToken cancellationToken)
        {
            Assert.Equal(AccountId, accountId);
            Calls.Add($"leave {spaceId:D}");

            return Task.FromResult(OwnedOnLeave.Contains(spaceId) ? SpaceLeave.OwnershipHeld : SpaceLeave.Left);
        }

        public Task<AccountErasure> EraseAsync(Guid accountId, CancellationToken cancellationToken)
        {
            Assert.Equal(AccountId, accountId);
            Calls.Add("erase");

            return Task.FromResult(Erasures[_erasures++]);
        }
    }
}
