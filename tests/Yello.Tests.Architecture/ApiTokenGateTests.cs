using System.Buffers.Text;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Xunit;
using Yello.Domain.ApiTokens;
using Yello.Domain.Memberships;
using Yello.Host.ApiTokens;
using Yello.Host.Sessions;

namespace Yello.Tests.Architecture;

/// <summary>
/// What an API Token is, asserted against the types that decide it rather than against a running
/// Host. Story 1.8: AC1, AC3, AC4, AC6, AC9, AC10; AD-1, AD-4, AD-6, NFR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 1.8 ATDD run on 2026-10-24, activated by its
/// dev-story run.</b> <c>ApiTokenPrincipal</c> and <c>ApiToken</c> are typed references, so a rename
/// is a compile error. <c>ApiTokenSecret</c> is internal to <c>Yello.Infrastructure</c>, which grants
/// this project no internals, so it is still reached by name and its two methods by shape.
/// </para>
/// <para>
/// <b>No container.</b> Like <see cref="SessionContractTests"/>, these answer on a machine with the
/// container runtime down.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-36")]
[Trait("Requirement", "AD-6")]
public sealed class ApiTokenGateTests
{
    private const string TokenAccountClaim = "urn:yello:claim:token-account-id";
    private const string TokenSpaceClaim = "urn:yello:claim:token-space-id";
    private const string TokenIdClaim = "urn:yello:claim:token-id";
    private const string TokenAuthenticationType = "Yello.ApiToken";
    private const string TokenPrefix = "yello_pat_";

    private const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>
    /// The namespaces and types no <c>Yello.Application</c> type may reference: the claim model, the
    /// HTTP model, and anything in the Host. AC6 / AD-4 - a slice that cannot see a principal or a
    /// request cannot branch on which surface called it.
    /// </summary>
    private static readonly string[] SurfaceAwareNamespaces =
    [
        "System.Security.Claims",
        "Microsoft.AspNetCore",
        "Yello.Host",
    ];

    /// <summary>
    /// AC9 / AD-1 / Task 4 - a Token principal carries exactly its three claims, under its own
    /// authentication type, and <b>not</b> the Session's claim - so the switcher, <c>sessions/current</c>
    /// and sign-out, which read only <see cref="SessionPrincipal.AccountIdOf"/>, answer it <c>401</c>
    /// with no further code. No Role claim.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AR-4")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "AD-24")]
    public void The_Token_principal_carries_exactly_its_three_claims_and_not_the_Session_claim()
    {
        var (tokenId, accountId, spaceId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var principal = ApiTokenPrincipal.For(tokenId, accountId, spaceId);

        Assert.Equal(
            [TokenAccountClaim, TokenIdClaim, TokenSpaceClaim],
            principal.Claims.Select(claim => claim.Type).Order(StringComparer.Ordinal));

        Assert.Equal(accountId.ToString(), principal.FindFirst(TokenAccountClaim)?.Value, ignoreCase: true);
        Assert.Equal(spaceId.ToString(), principal.FindFirst(TokenSpaceClaim)?.Value, ignoreCase: true);
        Assert.Equal(tokenId.ToString(), principal.FindFirst(TokenIdClaim)?.Value, ignoreCase: true);

        Assert.Equal(
            [TokenAccountClaim, TokenSpaceClaim, TokenIdClaim, TokenAuthenticationType],
            [ApiTokenPrincipal.AccountIdClaimType, ApiTokenPrincipal.BoundSpaceIdClaimType, ApiTokenPrincipal.TokenIdClaimType, ApiTokenPrincipal.AuthenticationType]);

        Assert.Equal(TokenAuthenticationType, principal.Identity?.AuthenticationType);
        Assert.True(principal.Identity?.IsAuthenticated);

        // The property everything in AC9 rests on.
        Assert.Null(SessionPrincipal.AccountIdOf(principal));
        Assert.DoesNotContain(principal.Claims, claim => claim.Type == SessionPrincipal.AccountIdClaimType);
        Assert.DoesNotContain(principal.Claims, claim => claim.Type.Contains("role", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC1 / AC3 / AD-1 - the <c>ApiToken</c> entity's bound Space cannot be reassigned after
    /// construction, and the entity stores no Role: capability is the current Membership's, read per
    /// request.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-4")]
    [Trait("Requirement", "AD-1")]
    public void The_ApiToken_entity_stores_no_Role_and_its_bound_Space_cannot_be_reassigned()
    {
        var entity = typeof(ApiToken);

        var properties = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(
            ["AccountId", "BoundSpaceId", "CreatedAt", "Id", "Name", "SecretHash"],
            properties.Select(property => property.Name).Order(StringComparer.Ordinal));

        Assert.DoesNotContain(properties, property => property.PropertyType == typeof(Role) || property.PropertyType == typeof(Role?));

        var bound = entity.GetProperty("BoundSpaceId", BindingFlags.Public | BindingFlags.Instance);
        var setter = bound?.SetMethod;

        // Absent, non-public, or init-only: nothing can move a Token to another Space once it exists.
        var reassignable = setter is { IsPublic: true }
            && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

        Assert.False(reassignable, "ApiToken.BoundSpaceId has a public, non-init setter.");
    }

    /// <summary>
    /// AC6 / AD-4 - no <c>Yello.Application</c> type references the claim model, the HTTP model or the
    /// Host, so no slice can learn which surface called it.
    /// </summary>
    /// <remarks>
    /// <b>Active today - a standing gate.</b> It finds its subject first (the assembly has type
    /// references to scan), then asserts none falls in <see cref="SurfaceAwareNamespaces"/>. Planted on
    /// 2026-10-24 with a <see cref="ClaimsPrincipal"/> property on an Application type, and caught.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-2")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-21")]
    public void No_Application_type_references_claims_HTTP_or_the_Host()
    {
        using var module = ModuleDefinition.ReadModule(ProductionAssemblies.Application.Location);

        var references = module.GetTypeReferences().Select(reference => reference.FullName).ToList();

        Assert.NotEmpty(references);

        var offending = references
            .Where(name => Array.Exists(SurfaceAwareNamespaces, prefix => name.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(offending.Count == 0, "Yello.Application references: " + string.Join(", ", offending));
    }

    /// <summary>
    /// AC10 / NFR-6 - exactly one <c>Yello.Contracts</c> type carries a field that could hold a Token's
    /// plaintext (<c>Token</c>, <c>Secret</c> or <c>Plaintext</c>), and it is the issuance response in
    /// the API Token contracts. Finds its subject first, so it cannot pass over nothing.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AR-34")]
    public void Only_the_issuance_response_carries_a_Token_secret()
    {
        string[] secretNames = ["Token", "Secret", "Plaintext"];

        var carriers = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => Array.Exists(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => secretNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var carrier = Assert.Single(carriers);

        Assert.Equal("Yello.Contracts.ApiTokens", carrier.Namespace);
    }

    /// <summary>
    /// AC4 / NFR-6 / Q9 (a) - a generated secret is <c>yello_pat_</c> followed by the base64url of 32
    /// random bytes, two are never equal, and the stored hash is SHA-256 of the full plaintext's UTF-8
    /// bytes - 32 bytes, from which the plaintext cannot be read back.
    /// </summary>
    /// <remarks>
    /// The two methods are found by shape - a static <c>string ()</c> and a static
    /// <c>byte[] (string)</c> on <c>ApiTokenSecret</c> - because their names are Task 3's.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    public void A_Token_secret_is_256_random_bits_behind_a_prefix_and_is_stored_as_its_SHA_256()
    {
        var secret = ProductionAssemblies.Infrastructure.GetType("Yello.Infrastructure.ApiTokens.ApiTokenSecret");

        Assert.True(secret is not null, "Yello.Infrastructure.ApiTokens.ApiTokenSecret does not exist (story 1.8 Task 3).");

        var methods = secret.GetMethods(Everything).Where(method => method.IsStatic).ToList();
        var generate = Assert.Single(methods, method => method.ReturnType == typeof(string) && method.GetParameters().Length == 0);
        var hash = Assert.Single(methods, method => method.ReturnType == typeof(byte[])
            && method.GetParameters() is [{ ParameterType: var only }] && only == typeof(string));

        var first = Assert.IsType<string>(generate.Invoke(null, null));
        var second = Assert.IsType<string>(generate.Invoke(null, null));

        Assert.NotEqual(first, second);

        foreach (var plaintext in new[] { first, second })
        {
            Assert.StartsWith(TokenPrefix, plaintext, StringComparison.Ordinal);
            Assert.Equal(32, Base64Url.DecodeFromChars(plaintext.AsSpan(TokenPrefix.Length)).Length);
        }

        var stored = Assert.IsType<byte[]>(hash.Invoke(null, [first]));

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(first)), stored);
    }
}
