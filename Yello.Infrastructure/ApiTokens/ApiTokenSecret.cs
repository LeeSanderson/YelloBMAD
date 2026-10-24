using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Yello.Infrastructure.ApiTokens;

/// <summary>
/// An API Token's secret: how one is made, how it is hashed for storage, and what a well-formed one
/// looks like. NFR-6; AD-6; story 1.8, open question 9.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>yello_pat_</c> followed by the base64url of 32 random bytes.</b> The prefix makes a leaked
/// Token recognisable to a secret scanner. Nothing else - no id, no Space, no Role - is encoded in
/// it; everything a Token means is read off its row.
/// </para>
/// <para>
/// <b>SHA-256, deliberately fast.</b> A password is hashed slowly because it has little entropy to
/// search. This secret carries 256 random bits, so there is nothing to brute-force, and a slow hash
/// would cost every API request the ~270 ms a sign-in costs (NFR-5). The planning set names no
/// algorithm; this is the choice, recorded.
/// </para>
/// </remarks>
internal static class ApiTokenSecret
{
    /// <summary>
    /// The fixed, readable prefix.
    /// </summary>
    internal const string Prefix = "yello_pat_";

    /// <summary>
    /// The random part's length, in bytes.
    /// </summary>
    private const int SecretBytes = 32;

    /// <summary>
    /// The random part's length once encoded: 43 base64url characters, unpadded.
    /// </summary>
    private static readonly int EncodedLength = Base64Url.GetEncodedLength(SecretBytes);

    /// <summary>
    /// Makes a new secret.
    /// </summary>
    /// <returns>The plaintext.</returns>
    internal static string Generate() =>
        Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <summary>
    /// The value stored for a plaintext, and looked up for a presented one.
    /// </summary>
    /// <param name="plaintext">The full plaintext, prefix included.</param>
    /// <returns>The SHA-256 of its UTF-8 bytes - 32 bytes.</returns>
    internal static byte[] Hash(string plaintext) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));

    /// <summary>
    /// Whether a presented value could be a Token at all. A value that could not is refused without
    /// a database read - and answered exactly as an unknown one is.
    /// </summary>
    /// <param name="presented">The bearer value.</param>
    /// <returns><c>true</c> for the prefix followed by 43 base64url characters that decode to 32 bytes.</returns>
    internal static bool IsWellFormed(string? presented)
    {
        if (presented is null
            || presented.Length != Prefix.Length + EncodedLength
            || !presented.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return Base64Url.IsValid(presented.AsSpan(Prefix.Length), out var decodedLength)
            && decodedLength == SecretBytes;
    }
}
