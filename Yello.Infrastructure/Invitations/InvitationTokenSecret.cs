using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Yello.Infrastructure.Invitations;

/// <summary>
/// An Invitation's token: how one is made and how it is hashed for storage. NFR-6, AD-28; story 4.1, Task 3 (C8).
/// </summary>
/// <remarks>
/// <para>
/// <b><c>yello_inv_</c> followed by the base64url of 32 random bytes</b> - the API Token's shape
/// (<c>ApiTokenSecret</c>) behind a prefix of its own, so a leaked token is recognisable to a secret scanner and never
/// mistaken for an API Token. Nothing - no Space, no Role, no address - is encoded in it; the link it travels in carries
/// nothing else either (UX-DR18).
/// </para>
/// <para>
/// <b>Only its SHA-256 is stored</b>, under a unique index story 4.3 resolves an Invitation through. The plaintext exists
/// only in the rendered outbox row until the email is sent, and then only in the email.
/// </para>
/// </remarks>
internal static class InvitationTokenSecret
{
    /// <summary>
    /// The fixed, readable prefix.
    /// </summary>
    internal const string Prefix = "yello_inv_";

    /// <summary>
    /// The random part's length, in bytes.
    /// </summary>
    private const int SecretBytes = 32;

    /// <summary>
    /// Makes a new token.
    /// </summary>
    /// <returns>The plaintext.</returns>
    internal static string Generate() =>
        Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <summary>
    /// The value stored for a plaintext.
    /// </summary>
    /// <param name="plaintext">The full plaintext, prefix included.</param>
    /// <returns>The SHA-256 of its UTF-8 bytes - 32 bytes.</returns>
    internal static byte[] Hash(string plaintext) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
}
