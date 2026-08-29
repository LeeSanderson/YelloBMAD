using Xunit;
using Yello.Application.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// Structural validation of a registration submission.
/// </summary>
/// <remarks>
/// No container: every rule reads only the submitted values, which is exactly the property that
/// makes validating before hashing compatible with AD-23. The last test in this class asserts that
/// property rather than leaving it as a claim in a comment.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
public sealed class RegisterAccountValidatorTests
{
    private const string ValidPassword = "a-password-nobody-else-uses-1!";

    [Fact]
    public void A_well_formed_submission_has_no_failures() =>
        Assert.Empty(RegisterAccountValidator.Validate(
            new RegisterAccountCommand("Ravi", "ravi@anand.test", ValidPassword)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_display_name_is_refused(string displayName) =>
        Assert.Contains(
            RegisterAccountFailure.DisplayNameRequired,
            RegisterAccountValidator.Validate(
                new RegisterAccountCommand(displayName, "ravi@anand.test", ValidPassword)),
            StringComparer.Ordinal);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_email_address_is_refused(string address) =>
        Assert.Contains(
            RegisterAccountFailure.EmailAddressRequired,
            RegisterAccountValidator.Validate(
                new RegisterAccountCommand("Ravi", address, ValidPassword)),
            StringComparer.Ordinal);

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("@no-local-part")]
    [InlineData("no-domain@")]
    [InlineData("two@at@signs")]
    [InlineData("has space@example.test")]
    public void An_implausible_email_address_is_refused(string address) =>
        Assert.Contains(
            RegisterAccountFailure.EmailAddressMalformed,
            RegisterAccountValidator.Validate(
                new RegisterAccountCommand("Ravi", address, ValidPassword)),
            StringComparer.Ordinal);

    /// <summary>
    /// The check is shallow on purpose, and these are the addresses a stricter one would wrongly
    /// refuse.
    /// </summary>
    /// <remarks>
    /// Yello sends no mail at all - there is no email verification anywhere in the contract - so
    /// the only thing a stricter rule could achieve is refusing a real person's real address.
    /// Plus-addressing and long TLDs are the two forms that break the regular expressions in
    /// common circulation.
    /// </remarks>
    [Theory]
    [InlineData("ravi+yello@anand.test")]
    [InlineData("first.last@sub.domain.example")]
    [InlineData("r@a.io")]
    public void A_valid_but_unusual_email_address_is_accepted(string address) =>
        Assert.Empty(RegisterAccountValidator.Validate(
            new RegisterAccountCommand("Ravi", address, ValidPassword)));

    [Fact]
    public void A_missing_password_is_refused() =>
        Assert.Contains(
            RegisterAccountFailure.PasswordRequired,
            RegisterAccountValidator.Validate(
                new RegisterAccountCommand("Ravi", "ravi@anand.test", string.Empty)),
            StringComparer.Ordinal);

    /// <summary>
    /// Every rule is evaluated, not just the first to fail.
    /// </summary>
    /// <remarks>
    /// A validator that stopped at the first problem would make a person correcting the form
    /// discover the next one only after resubmitting - and each resubmission costs the
    /// deliberately-slow hash.
    /// </remarks>
    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var failures = RegisterAccountValidator.Validate(
            new RegisterAccountCommand(string.Empty, string.Empty, string.Empty));

        Assert.Equal(
            [
                RegisterAccountFailure.DisplayNameRequired,
                RegisterAccountFailure.EmailAddressRequired,
                RegisterAccountFailure.PasswordRequired,
            ],
            failures);
    }

    /// <summary>
    /// AD-23's precondition for validating before the hash: no rule can depend on stored state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the assertion that keeps early validation safe.</b> The story warns that "a
    /// server-side password-policy rejection that returns before the hash is performed
    /// reintroduces the same branch by another route", and the line between safe and unsafe is
    /// precisely whether a rule can see anything other than the submission. A validator that could
    /// consult the database would answer differently for a known address than for an unknown one -
    /// and it returns BEFORE the hash, so the difference would be visible in the response and in
    /// the timing.
    /// </para>
    /// <para>
    /// Asserted structurally, because a behavioural version cannot exist: there is no way to ask a
    /// pure function whether it would have consulted a database. A static class with no
    /// constructor and no injected dependency has nothing to consult, and that is checkable.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_validator_has_no_way_to_read_stored_state()
    {
        var type = typeof(RegisterAccountValidator);

        Assert.True(type.IsAbstract && type.IsSealed, "The validator must be a static class.");

        // No instance fields, no static fields beyond the length constants, and nothing that
        // could hold a connection, a context or a repository.
        var fields = type
            .GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Instance)
            .Where(field => !field.IsLiteral)
            .Select(field => field.Name)
            .ToList();

        Assert.Empty(fields);

        // And the entry point takes the submission and nothing else.
        var validate = type.GetMethod(nameof(RegisterAccountValidator.Validate));

        Assert.NotNull(validate);
        Assert.Equal(
            [typeof(RegisterAccountCommand)],
            validate.GetParameters().Select(parameter => parameter.ParameterType).ToList());
    }
}
