using Xunit;
using Yello.Application.Accounts.AuthenticateAccount;

namespace Yello.Tests.Slices.Accounts.AuthenticateAccount;

/// <summary>
/// Structural validation of a sign-in submission - and the boundary between what a 400 may report
/// and what only the uniform 401 may. AD-23 / AR-28.
/// </summary>
/// <remarks>
/// No container and no database, deliberately: every rule under test reads only the submitted
/// values, which is the property that makes validating before the verification safe at all.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-2")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
public sealed class AuthenticateAccountValidatorTests
{
    [Fact]
    public void A_well_formed_submission_has_no_failures()
    {
        var failures = AuthenticateAccountValidator.Validate(
            new AuthenticateAccountCommand("ravi@example.test", "a-password"));

        Assert.Empty(failures);
    }

    [Fact]
    public void Both_fields_are_reported_at_once_rather_than_one_at_a_time()
    {
        var failures = AuthenticateAccountValidator.Validate(
            new AuthenticateAccountCommand(string.Empty, string.Empty));

        Assert.Equal(
            [
                AuthenticateAccountFailure.EmailAddressRequired,
                AuthenticateAccountFailure.PasswordRequired,
            ],
            failures);
    }

    [Fact]
    public void Whitespace_is_not_an_email_address()
    {
        var failures = AuthenticateAccountValidator.Validate(
            new AuthenticateAccountCommand("   ", "a-password"));

        Assert.Equal([AuthenticateAccountFailure.EmailAddressRequired], failures);
    }

    /// <summary>
    /// A password of one space is a password. Only <i>empty</i> is absent.
    /// </summary>
    /// <remarks>
    /// The email rule uses <c>IsNullOrWhiteSpace</c> and this one uses <c>IsNullOrEmpty</c>, and
    /// the asymmetry is deliberate rather than a slip: an address made of spaces cannot match a
    /// stored one, while a password made of spaces can perfectly well be somebody's password.
    /// Registration's validator draws the same line for the same reason, so an account that can be
    /// registered with such a password can always sign in with it.
    /// </remarks>
    [Fact]
    public void A_password_of_spaces_is_accepted_because_it_is_a_password()
    {
        var failures = AuthenticateAccountValidator.Validate(
            new AuthenticateAccountCommand("ravi@example.test", "   "));

        Assert.Empty(failures);
    }

    [Fact]
    public void An_email_address_beyond_the_column_is_refused()
    {
        var address = new string('a', 250) + "@example.test";

        var failures = AuthenticateAccountValidator.Validate(
            new AuthenticateAccountCommand(address, "a-password"));

        Assert.Equal([AuthenticateAccountFailure.EmailAddressTooLong], failures);
    }

    [Fact]
    public void A_password_beyond_the_request_ceiling_is_refused()
    {
        var failures = AuthenticateAccountValidator.Validate(
            new AuthenticateAccountCommand("ravi@example.test", new string('p', 4097)));

        Assert.Equal([AuthenticateAccountFailure.PasswordTooLong], failures);
    }

    /// <summary>
    /// AD-23: a malformed address is NOT refused here, because refusing it would answer a subset of
    /// non-existent addresses differently from the rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the assertion that keeps the validator honest, and it asserts an
    /// <i>absence</i>.</b> Registration refuses <c>not-an-address</c> with a 400 and is right to:
    /// nothing about that refusal depends on stored state. Sign-in refusing it would not leak
    /// either - but it would split the space of failing addresses into two answers, a 400 for the
    /// implausible ones and a 401 for the rest, and every such split is a place a later story
    /// widens. The uniform answer is cheaper to keep than to restore.
    /// </para>
    /// <para>
    /// Written as an equality against the empty list rather than as "does not contain a malformed
    /// code", so a NEW rule added here fails this test rather than passing it.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_malformed_address_is_not_refused_here_but_left_to_the_uniform_answer()
    {
        var failures = AuthenticateAccountValidator.Validate(
            new AuthenticateAccountCommand("not-an-address", "a-password"));

        Assert.Empty(failures);
    }

    /// <summary>
    /// No code this validator can emit says anything about whether an Account exists.
    /// </summary>
    /// <remarks>
    /// <b>An absence assertion, so it is validated against a planted signal.</b> The plant is the
    /// vocabulary list itself: a code named for a stored fact - <c>account-not-found</c>,
    /// <c>no-such-account</c>, <c>password-incorrect</c> - fails this by name. The same shape as the
    /// registration endpoint's log-vocabulary assertion, and for the same reason: the words are how
    /// this defect actually arrives.
    /// </remarks>
    [Fact]
    public void No_failure_code_names_a_stored_fact()
    {
        var codes = new[]
        {
            AuthenticateAccountFailure.EmailAddressRequired,
            AuthenticateAccountFailure.EmailAddressTooLong,
            AuthenticateAccountFailure.PasswordRequired,
            AuthenticateAccountFailure.PasswordTooLong,
        };

        var disclosures = new[]
        {
            "not-found", "notfound", "unknown", "no-such", "exists", "incorrect", "wrong",
            "invalid-credential", "unregistered", "mismatch",
        };

        var leaked = (
            from code in codes
            from word in disclosures
            where code.Contains(word, StringComparison.OrdinalIgnoreCase)
            select $"'{word}' in the failure code '{code}'")
            .ToList();

        Assert.True(
            leaked.Count == 0,
            "A sign-in failure code names a stored fact. Every code this validator emits is a "
            + "statement about the submission; the moment one is a statement about what is stored, "
            + "the 400 becomes an enumeration oracle beside a 401 built not to be one."
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, leaked)}");
    }
}
