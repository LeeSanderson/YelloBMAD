namespace Yello.Contracts.Tasks;

/// <summary>
/// What a batch of description changes came to: the changes refused, the server's version, and the changes the editor's
/// version lacks.
/// </summary>
/// <remarks>
/// <b>Never text for the editor to adopt.</b> The editor withdraws what was refused, latest first, and applies what it lacks,
/// so its replica converges by the merge alone.
/// </remarks>
/// <param name="Refused">The positions, from zero, in the batch as sent, of the changes refused.</param>
/// <param name="Version">The server's encoded version after the batch.</param>
/// <param name="Missing">The encoded changes the editor's version lacks.</param>
public sealed record TaskDescriptionChangesResponse(IReadOnlyList<int> Refused, ReadOnlyMemory<byte> Version, IReadOnlyList<ReadOnlyMemory<byte>> Missing);