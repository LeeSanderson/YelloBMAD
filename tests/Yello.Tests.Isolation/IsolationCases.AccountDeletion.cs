using System.Net;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Deleting one's own Account, <c>DELETE /api/v1/account</c>, on both surfaces. The route is Account-scoped and Session
/// only, so a Token is the no-credential <c>401</c> on every row. A Session that owns a Space is refused naming exactly the
/// Spaces it owns, by identity alone; a Session that owns nothing is erased from the Space it belonged to and nobody
/// else's Space changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The refusal's list is the caller's own owned Spaces and nothing else</b> (I-11): an Account that owns two Spaces and
/// is a Member of a third is named its two, each item exactly <c>{ id, name }</c>; never the third, never world.A's or
/// world.B's.
/// </para>
/// <para>
/// <b>The actor is the joined view</b> - the Account seen from the Space it belongs to, with a Token bound there - so the
/// presence anchor reads a Space that exists after its own Personal Space has been deleted, and the Token surface sends a
/// Token that still works anywhere it is accepted.
/// </para>
/// <para>
/// <b>Pending until the route is mapped</b>; the develop phase makes these rows live before review (GATE-003) and swaps
/// <see cref="AccountDeletionWire.Route"/> for the contract's member.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private const string AccountDeletionPending = "DELETE /api/v1/account is not mapped until the dev phase of the story that deletes an Account.";

    private static IEnumerable<IsolationCase> AccountDeletionCases() =>
    [
        new IsolationCase(
            "ISO-DELETE-ACCOUNT-NAMES-ONLY-OWNED-SPACES", "I-11, AR-29", IsolationVector.Listing, HttpMethod.Delete, AccountDeletionWire.Route,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Dk");
                var host = await world.NewActorAsync("Dh");

                HoldsIdentity(host);

                var joined = await JoinedThroughTheProductAsync(world, host, first.Actor, "member");

                return new CaseRequest(joined, HttpMethod.Delete, [new(AccountDeletionWire.Route, AccountDeletionWire.Route)], TargetSpaceId: null, Other: second.Actor);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.Conflict, NamesExactlyTheCallersOwnedSpacesAsync, "409 account-owns-spaces naming exactly its two owned Spaces, each {id, name}; nothing changed")
                {
                    Then = TheCallerStillBelongsWhereItBelongedAsync,
                },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheCallerStillBelongsWhereItBelongedAsync },
            },
            PendingUntil: "5.4",
            PendingReason: AccountDeletionPending),

        new IsolationCase(
            "ISO-DELETE-ACCOUNT-OWN", "I-12, FR-3, V-3", IsolationVector.Api, HttpMethod.Delete, AccountDeletionWire.Route,
            OwnsNothingAndDeletesItselfAsync,
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.NoContent, ErasedFromTheHostsSpaceAloneAsync, "204, no body; gone from the host's Space; the host, A and B unchanged"),
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheCallerStillBelongsWhereItBelongedAsync },
            },
            PendingUntil: "5.4",
            PendingReason: AccountDeletionPending),
    ];

    /// <summary>
    /// A fresh Account joins a fresh host's Space as Member through the product (a Token bound there), then deletes its
    /// Personal Space, the only Space it owns, through the product.
    /// </summary>
    private static async Task<CaseRequest> OwnsNothingAndDeletesItselfAsync(IsolationWorld world)
    {
        var host = await world.NewActorAsync("Eh");

        HoldsIdentity(host);

        var leaver = await world.NewActorAsync("El");
        var joined = await JoinedThroughTheProductAsync(world, host, leaver, "member");
        var personal = await leaver.SendAsync(IsolationSurface.Session, HttpMethod.Delete, SpaceRoutes.ActiveOf(leaver.SpaceId));

        Assert.True(personal.StatusCode == HttpStatusCode.NoContent, $"Deleting the Personal Space through the product: {personal.StatusCode} {personal.Body}");

        return new CaseRequest(joined, HttpMethod.Delete, [new(AccountDeletionWire.Route, AccountDeletionWire.Route)], TargetSpaceId: null, Other: host);
    }

    /// <summary>
    /// I-11: the refusal is <c>account-owns-spaces</c> and lists exactly the caller's two owned Spaces, each exactly
    /// <c>{ id, name }</c>; it names neither the Space the caller merely belongs to nor A's or B's, and carries no address.
    /// </summary>
    private static Task NamesExactlyTheCallersOwnedSpacesAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var second = request.Other ?? throw new InvalidOperationException("The case names no second owned Space.");
        var named = AccountDeletionWire.OwnedSpacesIn(answer.Body);

        Assert.Equal(AccountDeletionWire.AccountOwnsSpacesType, AccountDeletionWire.ProblemTypeOf(answer.Body));
        Assert.Equal(new[] { request.Actor.Origin.SpaceId, second.SpaceId }.Order(), named.Select(space => space.Id).Order());
        Assert.All(named, space => Assert.Equal(AccountDeletionWire.OwnedSpaceMembers, space.Members));

        foreach (var foreign in new[] { request.Actor.SpaceId, world.A.SpaceId, world.B.SpaceId })
        {
            Assert.DoesNotContain(foreign.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("@", answer.Body, StringComparison.Ordinal);

        return Task.CompletedTask;
    }

    /// <summary>
    /// The caller still holds its Membership in the Space it was seen from: nothing was left and nothing erased.
    /// </summary>
    private static async Task TheCallerStillBelongsWhereItBelongedAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Contains(await world.Database.MembershipRowsInAsync(request.Actor.SpaceId), row => row.AccountId == request.Actor.AccountId);

    /// <summary>
    /// The answer has no body; the caller holds no Membership in the host's Space and no Token, and the host still owns its
    /// Space and A and B their own, each with exactly one Owner (S-2).
    /// </summary>
    private static async Task ErasedFromTheHostsSpaceAloneAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var host = request.Other ?? throw new InvalidOperationException("The case names no host.");

        Assert.Equal(string.Empty, answer.Body);
        Assert.DoesNotContain(await world.Database.MembershipRowsInAsync(host.SpaceId), row => row.AccountId == request.Actor.AccountId);
        Assert.Equal(0, await world.Database.TokenCountAsync(request.Actor.AccountId, host.SpaceId));
        Assert.Empty(await world.Database.SpacesNotHoldingExactlyOneOwnerAsync([host.SpaceId, world.A.SpaceId, world.B.SpaceId]));
        Assert.Equal("Owner", (await world.Database.MembershipOfAsync(host.SpaceId, host.AccountId)).Role);
    }
}
