using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Domain.Accounts;
using Yello.Domain.Invitations;
using Yello.Host.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Architecture;

/// <summary>
/// Structural gates for deleting an Account: the route is Account-scoped on the Account settings surface and nothing else,
/// the refusal names owned Spaces through a stable type and an appended <c>Spaces</c> member of identity alone, and the
/// Invitation's issuer key lets an Invitation outlive its issuer's Account.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each gate finds its subject by name</b> (route pattern, reflection, the EF model), so a subject built under another
/// name turns it red: <c>DELETE /api/v1/account</c>, <c>ProblemTypes.AccountOwnsSpaces</c>, <c>ProblemResponse.Spaces</c>,
/// <c>Invitation.IssuedByAccountId</c>.
/// </para>
/// <para>
/// <b>Other gates that list the route:</b>
/// <c>Every_Account_scoped_endpoint_names_an_AD_24_surface</c> (one more <c>AccountSettings</c> entry) and the
/// <c>AccountScopedReads</c> / <c>Readers</c> / <c>Endpoints</c> lists in <c>SpaceSwitcherGateTests.Isolation</c>.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string AccountDeletionPattern = "/api/v1/account";

    /// <summary>
    /// <c>DELETE /api/v1/account</c> is mapped exactly once, for <c>DELETE</c> only, Account-scoped on the
    /// <c>AccountSettings</c> surface - AD-24's second Account-scoped surface, with no new member - and declares no Space
    /// capability and no bound, because it is decided by identity, not by a Role in a Space.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public void The_Account_deletion_route_is_Account_scoped_on_Account_settings_and_nothing_else()
    {
        var endpoints = RouteTable().Where(endpoint => endpoint.RoutePattern.RawText == AccountDeletionPattern).ToList();
        var endpoint = Assert.Single(endpoints);

        Assert.Equal(["DELETE"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);

        var marker = endpoint.Metadata.FirstOrDefault(item => item.GetType().FullName == AccountScopedMarkerName);

        Assert.NotNull(marker);
        Assert.Equal("AccountSettings", marker.GetType().GetProperty("Surface")?.GetValue(marker)?.ToString());
        Assert.Null(endpoint.Metadata.GetMetadata<RequiredCapability>());
        Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>());
    }

    /// <summary>
    /// The owned-Space refusal has a stable type, <c>ProblemTypes.AccountOwnsSpaces</c> =
    /// <c>urn:yello:problem:account-owns-spaces</c>, and <c>ProblemResponse</c> gains <c>Spaces</c> after its existing
    /// members, typed as a list of <c>SpaceIdentityResponse</c> - identity only, nothing a Role or a count could ride on.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AD-24")]
    public void The_owned_Spaces_refusal_has_a_stable_type_and_an_appended_identity_only_Spaces_member()
    {
        var type = typeof(Yello.Contracts.ProblemTypes).GetProperty("AccountOwnsSpaces", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(type);
        Assert.Equal("urn:yello:problem:account-owns-spaces", type.GetValue(null));

        var parameters = typeof(Yello.Contracts.ProblemResponse).GetConstructors().Single().GetParameters();

        // Conflicts, Projects and Removal follow it: the Space default Status refusals' members, appended after it.
        Assert.Equal(["Type", "Errors", "Capability", "Bound", "Limit", "Spaces", "Conflicts", "Projects", "Removal"], parameters.Select(parameter => parameter.Name));
        Assert.Equal(typeof(IReadOnlyList<Yello.Contracts.Spaces.SpaceIdentityResponse>), parameters.Single(parameter => parameter.Name == "Spaces").ParameterType);
        Assert.Equal(["Id", "Name"], PropertyNames(typeof(Yello.Contracts.Spaces.SpaceIdentityResponse)));
    }

    /// <summary>
    /// In the EF model, <c>Invitation.IssuedByAccountId</c> is a <c>Guid?</c> whose reference to <c>Account</c> is
    /// <c>SetNull</c>: deleting the issuer's Account keeps every Invitation it issued, in every Space, with no issuer - a
    /// Space it was removed from included, where no Space-scoped step of the deletion runs. Not <c>Cascade</c>, which would
    /// delete another Account's Space's work; not <c>Restrict</c>, which would refuse the deletion.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-5")]
    public void The_Invitation_issuer_key_is_nullable_and_set_null_when_the_Account_is_deleted()
    {
        Assert.Equal(typeof(Guid?), typeof(Invitation).GetProperty(nameof(Invitation.IssuedByAccountId))?.PropertyType);

        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var invitation = context.Model.FindEntityType(typeof(Invitation));

        Assert.NotNull(invitation);

        var toAccount = Assert.Single(invitation.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Account));

        Assert.Equal([nameof(Invitation.IssuedByAccountId)], toAccount.Properties.Select(property => property.Name));
        Assert.False(toAccount.IsRequired);
        Assert.Equal(DeleteBehavior.SetNull, toAccount.DeleteBehavior);
    }

    /// <summary>
    /// Both Invitation contracts serve the issuer's name as nullable: <c>null</c> once the issuer's Account is deleted, so
    /// the deleted Account's name is never served again and the client renders the tombstone instead.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "UX-DR10")]
    public void The_Invitation_contracts_serve_a_nullable_issuer_name()
    {
        var nullability = new NullabilityInfoContext();

        foreach (var contract in new[] { typeof(Yello.Contracts.Invitations.InvitationResponse), typeof(Yello.Contracts.Invitations.InvitationOfferResponse) })
        {
            var issuer = contract.GetProperty("IssuerName");

            Assert.NotNull(issuer);
            Assert.True(
                nullability.Create(issuer).ReadState == NullabilityState.Nullable,
                $"{contract.Name}.IssuerName is not nullable: a deleted issuer would have to be served a name.");
        }
    }
}
