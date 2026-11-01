using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yello.Infrastructure.Email;

namespace Yello.Tests.Slices.Invitations;

/// <summary>
/// Story 4.1 AC11 and its code review (2026-11-01) - the email sender is chosen by configuration, and every branch that
/// refuses to start is seen: no sender at all, no or an implausible sender address, an unusable ACS connection string,
/// and (Lee's decision D1 (a)) a file drop configured on a Host that runs in Azure Container Apps.
/// </summary>
/// <remarks>
/// No database and no process: these call <see cref="EmailServices.AddYelloEmailSender"/> directly. That the Host passes
/// <c>CONTAINER_APP_NAME</c> through is held by the process case in <c>HostStartupTests</c>.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-37")]
[Trait("Requirement", "NFR-6")]
public sealed class EmailSenderSelectionTests
{
    private const string Acs = "endpoint=https://yello-acs.communication.azure.com/;accesskey=c2VjcmV0LWtleQ==";

    private const string Folder = "mail-drop";

    private const string Sender = "DoNotReply@yello.test";

    [Fact]
    public void The_ACS_adapter_is_chosen_when_its_connection_string_is_set() =>
        Assert.Equal("Azure Communication Services", new ServiceCollection().AddYelloEmailSender(Acs, null, Sender, null));

    [Fact]
    public void The_file_drop_is_chosen_off_Azure_when_only_a_drop_folder_is_set() =>
        Assert.Equal("file drop", new ServiceCollection().AddYelloEmailSender(null, Folder, Sender, null));

    [Fact]
    public void A_Host_with_no_sender_refuses_to_start() =>
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddYelloEmailSender(null, null, Sender, null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("Yello <DoNotReply@yello.test>")]
    public void A_Host_with_no_plausible_sender_address_refuses_to_start(string? sender) =>
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddYelloEmailSender(Acs, null, sender, null));

    [Theory]
    [InlineData("endpoint=http://yello-acs.communication.azure.com/;accesskey=c2VjcmV0LWtleQ==")]
    [InlineData("endpoint=https://yello-acs.communication.azure.com/;accesskey=not base64!")]
    [InlineData("accesskey=c2VjcmV0LWtleQ==")]
    public void A_Host_with_an_unusable_ACS_connection_string_refuses_to_start_without_quoting_it(string connectionString)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddYelloEmailSender(connectionString, null, Sender, null));

        Assert.DoesNotContain("c2VjcmV0LWtleQ", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lee's D1 (a): a drop folder on a Host in Azure Container Apps stops it, with or without an ACS connection string
    /// beside it, and the refusal does not name the folder. Anchor: the same configuration off Azure starts.
    /// </summary>
    /// <param name="acs">The ACS connection string, or <c>null</c>.</param>
    [Theory]
    [InlineData(null)]
    [InlineData(Acs)]
    public void A_Host_in_Azure_Container_Apps_refuses_a_drop_folder(string? acs)
    {
        Assert.NotNull(new ServiceCollection().AddYelloEmailSender(acs, Folder, Sender, null));

        var refusal = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddYelloEmailSender(acs, Folder, Sender, "yello-host"));

        Assert.Contains("Azure Container Apps", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Folder, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Host_in_Azure_Container_Apps_with_ACS_and_no_drop_folder_starts() =>
        Assert.Equal(
            "Azure Communication Services",
            new ServiceCollection().AddYelloEmailSender(Acs, null, Sender, "yello-host"));
}
