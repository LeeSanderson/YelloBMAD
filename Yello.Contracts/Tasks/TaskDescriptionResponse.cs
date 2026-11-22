namespace Yello.Contracts.Tasks;

/// <summary>
/// One Task's description: the text as the server projects it, and the encoded state an editor bootstraps its replica from.
/// </summary>
/// <remarks>
/// Kept off <see cref="TaskResponse"/>, which every Board, List and Task view shares, so a page of Tasks carries no
/// description.
/// </remarks>
/// <param name="Text">The projection: the description as plain text, empty when there is none.</param>
/// <param name="State">The encoded replica state the description's log rebuilds to.</param>
/// <param name="Version">The server's encoded version.</param>
public sealed record TaskDescriptionResponse(string Text, ReadOnlyMemory<byte> State, ReadOnlyMemory<byte> Version);