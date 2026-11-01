using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Application.Invitations;
using Yello.Application.Invitations.IssueInvitation;
using Yello.Application.Outbox;
using Yello.Application.Spaces;
using Yello.Domain.Invitations;
using Yello.Domain.Memberships;
using Yello.Infrastructure.Email;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Slices.Invitations.IssueInvitation;

/// <summary>
/// Story 4.1 Tasks 5 and 6 - the issue slice enqueues exactly one email for an issued Invitation and none for a refusal,
/// renders it from the Space, the Role, the issuer, the link and the expiry alone, and never issues at Owner; the shared
/// actionable predicate translates to the database clock (AC8 gate (ii), the behavioural half); and the ACS adapter's
/// signing and connection parsing hold their documented shape. AC5, AC6, AC7, AC8, AC11; FR-10, FR-39, AD-23, AD-27.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>ActiveSpaceEndpointTests.IssueInvitation*</c> hold the lock, the reads, the rows,
/// the outbox and the drop.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-10")]
[Trait("Requirement", "FR-39")]
public sealed class IssueInvitationHandlerTests
{
    private static readonly Uri Origin = new("https://client.yello.test");

    /// <summary>
    /// AC6, AC7, AD-2 - an issued Invitation enqueues one <c>invitation-issued</c> email to the address as stored, naming
    /// the Space, the Role's display name and the issuer, carrying the link to the token and the expiry as a UTC date; the
    /// store is asked about the resolved Space and Account, never ones the request named.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Assumption", "PRD-12-9")]
    public async Task An_issued_Invitation_enqueues_one_email_naming_the_Space_the_Role_and_the_issuer()
    {
        var context = new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), Role.Admin);
        var expiresAt = new DateTimeOffset(2026, 11, 8, 23, 30, 0, TimeSpan.FromHours(-2));
        var store = new RecordingStore(InvitationIssueOutcome.Issued, expiresAt);
        var outbox = new RecordingOutbox();

        var result = await new IssueInvitationHandler(store, new FixedName("Roadmap"), new InvitationLinks(Origin), outbox)
            .HandleAsync(context, "  invitee@example.test ", Role.Viewer, TestContext.Current.CancellationToken);

        Assert.Equal(InvitationIssueOutcome.Issued, result.Outcome);
        Assert.Equal((context.SpaceId, context.AccountId, Role.Viewer), Assert.Single(store.Calls));

        var email = Assert.Single(outbox.Enqueued);

        Assert.Equal(OutboxKinds.InvitationIssued, email.Kind);
        Assert.Equal("invitee@example.test", email.Recipient);
        Assert.Equal("Ada invited you to Roadmap on Yello", email.Subject);
        Assert.Contains("Ada invited you to join Roadmap as Viewer.", email.Body, StringComparison.Ordinal);
        Assert.Contains("https://client.yello.test/invitations/" + RecordingStore.Token + "\n", email.Body, StringComparison.Ordinal);
        Assert.Contains("The link expires on 2026-11-09 (UTC).", email.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC4, AC13 - a refusal enqueues nothing.
    /// </summary>
    /// <param name="outcome">The store's refusal.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(InvitationIssueOutcome.AlreadyAMember)]
    [InlineData(InvitationIssueOutcome.AlreadyPending)]
    public async Task A_refused_issue_enqueues_nothing(InvitationIssueOutcome outcome)
    {
        var outbox = new RecordingOutbox();

        var result = await new IssueInvitationHandler(new RecordingStore(outcome, DateTimeOffset.UnixEpoch), new FixedName("Roadmap"), new InvitationLinks(Origin), outbox)
            .HandleAsync(new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), Role.Owner), "invitee@example.test", Role.Member, TestContext.Current.CancellationToken);

        Assert.Equal(outcome, result.Outcome);
        Assert.Empty(outbox.Enqueued);
    }

    /// <summary>
    /// Story 3.3's race - a Space deleted after the pipeline resolved it answers <see cref="IssueInvitationResult.SpaceGone"/>
    /// and enqueues nothing (the endpoint's boundary <c>404</c> rolls the Invitation back).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Space_deleted_under_the_issue_enqueues_nothing()
    {
        var outbox = new RecordingOutbox();

        var result = await new IssueInvitationHandler(new RecordingStore(InvitationIssueOutcome.Issued, DateTimeOffset.UnixEpoch), new FixedName(null), new InvitationLinks(Origin), outbox)
            .HandleAsync(new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), Role.Owner), "invitee@example.test", Role.Member, TestContext.Current.CancellationToken);

        Assert.Same(IssueInvitationResult.SpaceGone, result);
        Assert.Empty(outbox.Enqueued);
    }

    /// <summary>
    /// AC2, FR-42 - the slice never issues at Owner, whatever reaches it: the store is never asked.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    public async Task The_slice_never_issues_at_Owner()
    {
        var store = new RecordingStore(InvitationIssueOutcome.Issued, DateTimeOffset.UnixEpoch);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new IssueInvitationHandler(store, new FixedName("Roadmap"), new InvitationLinks(Origin), new RecordingOutbox())
            .HandleAsync(new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), Role.Owner), "invitee@example.test", Role.Owner, TestContext.Current.CancellationToken));

        Assert.Empty(store.Calls);
    }

    /// <summary>
    /// AC7, UX-DR18 - the link is the client origin, <c>/invitations/</c> and the token, and nothing else; with no usable
    /// origin it is the path alone.
    /// </summary>
    [Fact]
    public void The_link_is_the_client_origin_and_the_token_and_nothing_else()
    {
        Assert.Equal("https://client.yello.test/invitations/yello_inv_abc", new InvitationLinks(new Uri("https://client.yello.test/")).For("yello_inv_abc").OriginalString);
        Assert.Equal("/invitations/yello_inv_abc", new InvitationLinks(null).For("yello_inv_abc").OriginalString);
    }

    /// <summary>
    /// AC8 gate (ii), the behavioural half; AR-31, AD-27, A-15 - the shared actionable predicate translates to SQL that
    /// compares <c>ExpiresAt</c> with <c>SYSDATETIMEOFFSET()</c> and <c>State</c> with <c>Pending</c>: the database clock,
    /// inside the statement, never a C# instant. Plant 8 (the predicate rewritten with a C# clock) turns it red.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    public void The_shared_actionable_predicate_translates_to_the_database_clock()
    {
        var options = new DbContextOptionsBuilder<YelloDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true")
            .Options;

        using var context = new YelloDbContext(options);

        // The Space filter is set aside: with no Space in scope it matches nothing, and EF would fold the whole WHERE away.
        var sql = context.Set<Invitation>().IgnoreQueryFilters().Where(OfferPredicates.Actionable).ToQueryString();

        Assert.Contains("SYSDATETIMEOFFSET()", sql, StringComparison.Ordinal);
        Assert.Contains("[i].[ExpiresAt] > SYSDATETIMEOFFSET()", sql, StringComparison.Ordinal);
        Assert.Contains("N'Pending'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DECLARE", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC11, Q4 (a) - the ACS request is signed as Microsoft's HMAC tutorial states: <c>x-ms-date</c> in RFC 1123, the
    /// body's SHA-256 in base64, and a signature over <c>"POST\n{path and query}\n{date};{host};{hash}"</c> keyed by the
    /// decoded access key. The expected values are computed here from the tutorial's recipe, independently of the signer.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-37")]
    public void The_ACS_request_is_signed_as_the_HMAC_scheme_states()
    {
        var key = Convert.ToBase64String(Encoding.UTF8.GetBytes("a-test-access-key-of-thirty-two!"));
        var uri = new Uri("https://yello-acs.communication.azure.com/emails:send?api-version=2023-03-31");
        var body = Encoding.UTF8.GetBytes("""{"senderAddress":"DoNotReply@example.test"}""");
        var now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        var signed = AcsRequestSigner.Sign("POST", uri, body, key, now);

        const string date = "Sun, 01 Nov 2026 12:00:00 GMT";
        var hash = Convert.ToBase64String(SHA256.HashData(body));
        var stringToSign = $"POST\n/emails:send?api-version=2023-03-31\n{date};yello-acs.communication.azure.com;{hash}";
        var signature = Convert.ToBase64String(HMACSHA256.HashData(Convert.FromBase64String(key), Encoding.UTF8.GetBytes(stringToSign)));

        Assert.Equal(date, signed.Date);
        Assert.Equal(hash, signed.ContentHash);
        Assert.Equal($"HMAC-SHA256 SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature={signature}", signed.Authorization);
    }

    /// <summary>
    /// AC11 - the connection string's endpoint and key are read, and an unusable one is refused naming the problem and
    /// never the key.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-37")]
    public void The_ACS_connection_string_is_parsed_and_an_unusable_one_is_refused_without_quoting_the_key()
    {
        Assert.Null(AcsConnection.TryParse("endpoint=https://yello-acs.communication.azure.com/;accesskey=c2VjcmV0", out var parsed));
        Assert.Equal(new Uri("https://yello-acs.communication.azure.com/"), parsed?.Endpoint);
        Assert.Equal("c2VjcmV0", parsed?.AccessKey);

        var problem = AcsConnection.TryParse("endpoint=http://plain.example.test/;accesskey=c2VjcmV0", out var refused);

        Assert.NotNull(problem);
        Assert.Null(refused);
        Assert.DoesNotContain("c2VjcmV0", problem, StringComparison.Ordinal);
        Assert.NotNull(AcsConnection.TryParse("endpoint=https://yello-acs.communication.azure.com/;accesskey=not base64!", out _));
    }

    private sealed class RecordingStore(InvitationIssueOutcome outcome, DateTimeOffset expiresAt) : IInvitationStore
    {
        public const string Token = "yello_inv_token";

        public List<(Guid SpaceId, Guid AccountId, Role Role)> Calls { get; } = [];

        public Task<InvitationIssue> IssueAsync(Guid spaceId, Guid issuerAccountId, string emailAddress, Role role, CancellationToken cancellationToken)
        {
            Calls.Add((spaceId, issuerAccountId, role));

            return Task.FromResult(outcome switch
            {
                InvitationIssueOutcome.Issued => InvitationIssue.Issued(
                    new Invitation
                    {
                        Id = Guid.NewGuid(),
                        SpaceId = spaceId,
                        EmailAddress = emailAddress.Trim(),
                        NormalizedEmailAddress = emailAddress.Trim().ToUpperInvariant(),
                        Role = role,
                        State = InvitationState.Pending,
                        ExpiresAt = expiresAt,
                        TokenHash = new byte[32],
                        IssuedByAccountId = issuerAccountId,
                        IssuerName = "Ada",
                        CreatedAt = DateTimeOffset.UnixEpoch,
                    },
                    Token),
                InvitationIssueOutcome.AlreadyAMember => InvitationIssue.AlreadyAMember,
                _ => InvitationIssue.AlreadyPending,
            });
        }
    }

    private sealed class RecordingOutbox : IOutbox
    {
        public List<OutboxEmail> Enqueued { get; } = [];

        public Task EnqueueAsync(OutboxEmail email, CancellationToken cancellationToken)
        {
            Enqueued.Add(email);

            return Task.CompletedTask;
        }
    }

    private sealed class FixedName(string? name) : IActiveSpaceNameReader
    {
        public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(name);
    }
}
