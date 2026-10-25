using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yello.Host.Sessions;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// <c>CredentialHashingRateLimit.PartitionOf</c> normalises the client's address: an IPv4-mapped IPv6
/// address keys as its IPv4 form, IPv6 keys on its /64, and a missing address keeps the shared empty
/// partition. Story 1.10 AC11 (AR-25 / AD-18); Task 4, <c>deferred-work.md:304</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written by the ATDD run on 2026-10-25.</b> The normalisation cases are red-phase scaffolds. The
/// third case states what must not change (Q10's sub-question: keep the shared partition for a
/// missing address) and is green today, a standing gate.
/// </para>
/// <para>
/// <b>Equality, not a format.</b> The cases assert which addresses share a key and that a key is never
/// the full IPv6 address, not how a /64 is spelled - that is the implementation's choice.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-25")]
[Trait("Requirement", "AD-18")]
public sealed class RateLimitPartitionTests
{
    /// <summary>
    /// 1.10-UNIT-001 - an IPv4-mapped IPv6 address keys as its IPv4 form.
    /// </summary>
    [Fact(Skip = "red: AC11 - PartitionOf does not map IPv4-mapped addresses to IPv4 (Task 4)")]
    public void An_ipv4_mapped_address_keys_as_its_ipv4_form()
    {
        Assert.Equal(PartitionOf("203.0.113.7"), PartitionOf("::ffff:203.0.113.7"));
    }

    /// <summary>
    /// 1.10-UNIT-002 - IPv6 addresses key on their /64: two in one prefix share a key, one in another
    /// prefix does not, and the key is not the full address.
    /// </summary>
    [Fact(Skip = "red: AC11 - PartitionOf keys IPv6 on the whole address (Task 4)")]
    public void Ipv6_addresses_key_on_their_64_bit_prefix()
    {
        var first = PartitionOf("2001:db8:1:2::1");

        Assert.Equal(first, PartitionOf("2001:db8:1:2:ffff:ffff:ffff:ffff"));
        Assert.NotEqual(first, PartitionOf("2001:db8:1:3::1"));
        Assert.NotEqual("2001:db8:1:2::1", first);
    }

    /// <summary>
    /// 1.10-UNIT-003 - an IPv4 address keys as itself, and a request with no address keys in the shared
    /// empty partition.
    /// </summary>
    /// <remarks><b>Active - green today, a standing gate.</b></remarks>
    [Fact]
    [Trait("Priority", "P1")]
    public void An_ipv4_address_keys_as_itself_and_a_missing_one_shares_the_empty_partition()
    {
        Assert.Equal("203.0.113.7", PartitionOf("203.0.113.7"));
        Assert.Equal(string.Empty, CredentialHashingRateLimit.PartitionOf(new DefaultHttpContext()));
    }

    private static string PartitionOf(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        return CredentialHashingRateLimit.PartitionOf(context);
    }
}
