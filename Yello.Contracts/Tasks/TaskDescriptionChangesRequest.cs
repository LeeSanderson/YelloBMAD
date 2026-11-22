namespace Yello.Contracts.Tasks;

/// <summary>
/// A batch of changes to one Task's description: each encoded merge change, in the order the editor made them, and the
/// editor replica's encoded version. Bytes travel as base64.
/// </summary>
/// <remarks>
/// <b>Never text.</b> The server never takes a client's text as the description; it admits or refuses each change, and the
/// version tells it which changes to send back.
/// </remarks>
/// <param name="Changes">The encoded changes.</param>
/// <param name="Version">The editor replica's encoded version.</param>
public sealed record TaskDescriptionChangesRequest(IReadOnlyList<ReadOnlyMemory<byte>> Changes, ReadOnlyMemory<byte> Version);