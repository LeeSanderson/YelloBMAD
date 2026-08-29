using Xunit;
using Yello.Application.Accounts.RegisterAccount;
using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// The registration slice's own behaviour, with every port faked. AD-22, AD-23, AC1, AC2, AC4.
/// </summary>
/// <remarks>
/// <para>
/// No container: these assert what the slice <i>does</i> - what it builds, in what order, and
/// what it refuses to tell its caller. That the rows actually land in one transaction is a
/// database fact and is asserted against a real SQL Server in
/// <see cref="RegisterAccountIntegrationTests"/>.
/// </para>
/// <para>
/// Every email address is randomised. FR-1's uniqueness makes a shared literal a cross-suite
/// flake (<c>TESTING-CONVENTIONS.md:85</c>) - and while these particular tests never touch a
/// database, following the convention everywhere is what stops the exception becoming the habit.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-27")]
[Trait("Requirement", "AD-22")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
public sealed class RegisterAccountHandlerTests
{
    private const int ExpectedDefaultStatusCount = 3;

    [Fact]
    public async Task Registration_writes_one_Account_one_Space_and_one_Owner_Membership()
    {
        var harness = new Harness();

        await harness.RegisterAsync();

        var registration = Assert.Single(harness.Store.Registrations);

        Assert.Equal(registration.Account.Id, registration.OwnerMembership.AccountId);
        Assert.Equal(registration.Space.Id, registration.OwnerMembership.SpaceId);
        Assert.Equal(Role.Owner, registration.OwnerMembership.Role);
    }

    /// <summary>
    /// AC1's second clause: the Space carries the default Status set and no Projects.
    /// </summary>
    /// <remarks>
    /// The Projects half needs no assertion here and would be a vacuous one if written: no
    /// Project entity exists until epic 2, so "no Projects" is true by there being no table. It
    /// is recorded rather than faked.
    /// </remarks>
    [Fact]
    public async Task The_provisioned_Space_carries_the_default_Status_set_in_order()
    {
        var harness = new Harness();

        await harness.RegisterAsync();

        var registration = Assert.Single(harness.Store.Registrations);

        Assert.Equal(ExpectedDefaultStatusCount, registration.Statuses.Count);
        Assert.Equal(
            Yello.Domain.Statuses.DefaultStatusSet.Names,
            registration.Statuses.Select(status => status.Name).ToList());

        // Position comes from the index, so the order is stated in exactly one place.
        Assert.Equal([0, 1, 2], registration.Statuses.Select(status => status.Position).ToList());

        // Every Status belongs to the Space that was just provisioned - AD-2's non-nullable
        // SpaceId is only useful if the value is the right one.
        Assert.All(
            registration.Statuses,
            status => Assert.Equal(registration.Space.Id, status.SpaceId));
    }

    /// <summary>
    /// AD-23's load-bearing ordering: the hash happens before anything reads stored state.
    /// </summary>
    /// <remarks>
    /// This is the assertion that keeps the duplicate path honest. If the handler ever consulted
    /// the store first and hashed only for a new address, the duplicate path would return in
    /// microseconds while a real registration took a quarter of a second - which is the timing
    /// oracle AD-23 exists to close, and no response-shape test would notice.
    /// </remarks>
    [Fact]
    public async Task The_password_is_hashed_before_the_store_is_consulted()
    {
        var harness = new Harness();

        await harness.RegisterAsync();

        Assert.Equal(["hash", "store"], harness.Calls);
    }

    /// <summary>
    /// AC2: identical in duration, "because the password hash is performed anyway rather than
    /// skipped".
    /// </summary>
    [Fact]
    public async Task The_hash_is_performed_even_when_the_address_is_already_registered()
    {
        var harness = new Harness { Store = { Outcome = RegistrationOutcome.AddressAlreadyRegistered } };

        await harness.RegisterAsync();

        Assert.Equal(1, harness.Hasher.Calls);
        Assert.Equal(["hash", "store"], harness.Calls);
    }

    /// <summary>
    /// AC2, and the reason the handler returns <c>Task</c> rather than <c>Task&lt;bool&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Asserted against the type rather than against a value, because the point is that there is
    /// no value. A test that called the handler twice and compared results would pass just as
    /// happily if the method returned an outcome nobody happened to read yet - and the next story
    /// to add an endpoint would read it.
    /// </remarks>
    [Fact]
    public void The_handler_returns_nothing_that_could_distinguish_the_two_paths()
    {
        var handle = typeof(RegisterAccountHandler).GetMethod(nameof(RegisterAccountHandler.HandleAsync));

        Assert.NotNull(handle);
        Assert.Equal(typeof(Task), handle.ReturnType);
    }

    [Fact]
    public async Task Every_row_one_registration_writes_carries_the_same_instant()
    {
        var harness = new Harness();

        await harness.RegisterAsync();

        var registration = Assert.Single(harness.Store.Registrations);

        var instants = new List<DateTimeOffset>
        {
            registration.Account.CreatedAt,
            registration.Space.CreatedAt,
            registration.OwnerMembership.CreatedAt,
        };

        instants.AddRange(registration.Statuses.Select(status => status.CreatedAt));

        Assert.All(instants, instant => Assert.Equal(Harness.Now, instant));
    }

    [Fact]
    public async Task The_address_is_stored_as_typed_and_indexed_normalised()
    {
        var address = Harness.RandomAddress();
        var harness = new Harness();

        await harness.RegisterAsync(emailAddress: "  " + address.ToUpperInvariant() + "  ");

        var registration = Assert.Single(harness.Store.Registrations);

        // Trimmed but otherwise untouched: this is what correspondence would use.
        Assert.Equal(address.ToUpperInvariant(), registration.Account.EmailAddress);

        // And the comparable form the unique index is built on.
        Assert.Equal(
            EmailAddressNormalisation.Normalise(address),
            registration.Account.NormalizedEmailAddress);
    }

    /// <summary>
    /// The Space's name comes from the choke point, never from the handler.
    /// </summary>
    /// <remarks>
    /// Asserted through the port rather than against the string "Lee's Space", deliberately.
    /// Whichever answer the naming question settles on, this test stays true - which is the whole
    /// reason the decision sits behind one function and one resource string.
    /// </remarks>
    [Trait("Assumption", "PRD-12-1")]
    [Fact]
    public async Task The_Space_is_named_through_the_naming_port_from_the_display_name()
    {
        var harness = new Harness();

        await harness.RegisterAsync(displayName: "  Ravi  ");

        var registration = Assert.Single(harness.Store.Registrations);

        Assert.Equal("Ravi", harness.Naming.LastDisplayName);
        Assert.Equal(harness.Naming.NameFor("Ravi"), registration.Space.Name);
    }

    /// <summary>
    /// AC4: no attribute distinguishes the provisioned Space from one created by any other route.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The comparison target - a Space created by FR-5 - does not exist until Epic 3, so this
    /// asserts against the SHAPE that comparison would use rather than pretending to make it. A
    /// Space has exactly three members, and none of them can say how it came to exist.
    /// </para>
    /// <para>
    /// <b>This is the assertion that catches the defect the story title invites.</b> AD-22 and the
    /// story both say "the Personal Space", and <c>decisions-settled.md:26</c> records "a distinct
    /// undeletable type" and "a permanently private type" as explicitly rejected. An
    /// <c>IsPersonal</c> flag, a discriminator, a <c>ProvisionedAtRegistration</c> timestamp or a
    /// subtype would all read as harmless provenance and would all fail here.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_attribute_on_a_Space_can_distinguish_how_it_was_created()
    {
        var members = typeof(Space)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["CreatedAt", "Id", "Name"], members);
    }

    private sealed class Harness
    {
        public static DateTimeOffset Now => new(2026, 8, 29, 9, 30, 0, TimeSpan.Zero);

        public List<string> Calls { get; } = [];

        public FakeHasher Hasher { get; }

        public FakeStore Store { get; }

        public FakeNaming Naming { get; } = new();

        public Harness()
        {
            Hasher = new FakeHasher(Calls);
            Store = new FakeStore(Calls);
        }

        public static string RandomAddress() =>
            $"person-{Guid.NewGuid():N}@example.test";

        public Task RegisterAsync(string? displayName = null, string? emailAddress = null)
        {
            var handler = new RegisterAccountHandler(
                Hasher,
                Store,
                Naming,
                new SequentialIdentifiers(),
                new FixedClock());

            var command = new RegisterAccountCommand(
                displayName ?? "Ravi",
                emailAddress ?? RandomAddress(),
                "a-password-that-is-only-ever-hashed");

            return handler.HandleAsync(command, TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeHasher(List<string> calls) : IPasswordHasher
    {
        public int Calls { get; private set; }

        public string Hash(string password)
        {
            Calls++;
            calls.Add("hash");

            return "hashed:" + password.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private sealed class FakeStore(List<string> calls) : IAccountRegistrationStore
    {
        public List<AccountRegistration> Registrations { get; } = [];

        public RegistrationOutcome Outcome { get; set; } = RegistrationOutcome.Registered;

        public Task<RegistrationOutcome> RegisterAsync(
            AccountRegistration registration,
            CancellationToken cancellationToken)
        {
            calls.Add("store");
            Registrations.Add(registration);

            return Task.FromResult(Outcome);
        }
    }

    private sealed class FakeNaming : IPersonalSpaceNaming
    {
        public string? LastDisplayName { get; private set; }

        public string NameFor(string displayName)
        {
            LastDisplayName = displayName;

            return displayName + "'s Space";
        }
    }

    /// <summary>
    /// Deterministic ids, so a test can tell one from another without asserting on randomness.
    /// </summary>
    private sealed class SequentialIdentifiers : IIdentifierGenerator
    {
        private int _next;

        public Guid Next()
        {
            _next++;

            return new Guid(_next, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Harness.Now;
    }
}
