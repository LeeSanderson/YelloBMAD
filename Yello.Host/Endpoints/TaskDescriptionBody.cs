using System.Text.Json;
using Yello.Contracts;
using Yello.Domain.Tasks;

namespace Yello.Host.Endpoints;

/// <summary>
/// Reads a description change batch - <c>{ changes: [base64], version: base64 }</c> - inside the endpoint, after the
/// pipeline has run, and refuses what is not one before any of it reaches the merge.
/// </summary>
/// <remarks>
/// <para>
/// <b>A transport cap, not a product bound.</b> At most <see cref="MostChanges"/> changes and <see cref="MostBodyBytes"/>
/// bytes of body, so one request cannot make one transaction decode and apply without limit. No description length is
/// bounded: none is stated. The editor flushes long before either figure.
/// </para>
/// <para>
/// <b>Read as a document, never bound</b>, so a body naming text in place of changes is refused as a whole rather than read
/// as an empty batch. No refusal echoes what was sent.
/// </para>
/// </remarks>
internal static class TaskDescriptionBody
{
    /// <summary>
    /// The most changes one batch may carry.
    /// </summary>
    internal const int MostChanges = 256;

    /// <summary>
    /// The most bytes one batch's body may hold.
    /// </summary>
    internal const int MostBodyBytes = 256 * 1024;

    private const string ChangesMember = "changes";

    private const string VersionMember = "version";

    /// <summary>
    /// Reads the batch, or the refusal that answers it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The changes and the version, or a refusal - exactly one of the two.</returns>
    internal static async Task<(DescriptionBatch? Batch, IResult? Refusal)> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return (null, Invalid());
        }

        if (request.ContentLength > MostBodyBytes)
        {
            return (null, TooLarge());
        }

        var body = await ReadCappedAsync(request.Body, cancellationToken).ConfigureAwait(false);

        if (body is null)
        {
            return (null, TooLarge());
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            return Read(document.RootElement);
        }
        catch (JsonException)
        {
            return (null, Invalid());
        }
    }

    /// <summary>
    /// <c>400 invalid-description-changes</c>.
    /// </summary>
    /// <returns>The refusal.</returns>
    internal static IResult Invalid() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidDescriptionChanges,
            title: "A description edit is a batch of encoded changes and a version.");

    private static IResult TooLarge() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.DescriptionBatchTooLarge,
            title: "A description edit carries at most 256 changes and 256 KiB.");

    private static (DescriptionBatch? Batch, IResult? Refusal) Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(ChangesMember, out var changesElement)
            || changesElement.ValueKind != JsonValueKind.Array
            || ReadVersion(root) is not { } version)
        {
            return (null, Invalid());
        }

        if (changesElement.GetArrayLength() > MostChanges)
        {
            return (null, TooLarge());
        }

        var changes = new List<ReadOnlyMemory<byte>>(changesElement.GetArrayLength());

        foreach (var entry in changesElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String || !entry.TryGetBytesFromBase64(out var change))
            {
                return (null, Invalid());
            }

            changes.Add(change);
        }

        return (new DescriptionBatch(changes, version), null);
    }

    private static byte[]? ReadVersion(JsonElement root) =>
        root.TryGetProperty(VersionMember, out var element)
        && element.ValueKind == JsonValueKind.String
        && element.TryGetBytesFromBase64(out var version)
            ? version
            : null;

    /// <summary>
    /// The body's bytes, or <c>null</c> once it runs past the cap, whatever its declared length said.
    /// </summary>
    private static async Task<byte[]?> ReadCappedAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;

        while ((read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MostBodyBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }
}


