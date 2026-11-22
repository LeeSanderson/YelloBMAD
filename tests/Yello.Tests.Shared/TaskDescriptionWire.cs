using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Tasks;

namespace Yello.Tests.Shared;

/// <summary>
/// A Task's description as every suite reads it on the wire: the change route takes a batch of encoded merge changes and
/// the client's encoded version, never text; the read route answers the projection's text and the encoded state an editor
/// bootstraps from. Bytes travel as base64 strings.
/// </summary>
/// <remarks>
/// <para>
/// The routes and problem types are the contract's; the member names are the wire's camel case.
/// </para>
/// <para>
/// This project does not reference <c>Yello.Merge</c>, so everything here speaks bytes. A suite that makes changes takes
/// the strategy itself.
/// </para>
/// </remarks>
public static class TaskDescriptionWire
{
    /// <summary>
    /// The capability the change route declares, as the refusal names it.
    /// </summary>
    public static string EditCapabilityCode => "edit-task-description-collaboratively";

    /// <summary>
    /// The batch cap: changes per request.
    /// </summary>
    public static int MaximumChangesPerBatch => 256;

    /// <summary>
    /// The batch cap: bytes per request body.
    /// </summary>
    public static int MaximumBodyBytes => 256 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// <c>POST</c> a batch of changes here.
    /// </summary>
    public static string ChangesRoute => TaskRoutes.DescriptionChanges;

    /// <summary>
    /// <c>GET</c> the projection and the encoded state here.
    /// </summary>
    public static string Route => TaskRoutes.Description;

    /// <summary>
    /// The problem type for a change batch that does not parse.
    /// </summary>
    public static string InvalidChangesType => ProblemTypes.InvalidDescriptionChanges;

    /// <summary>
    /// The problem type for a batch over the cap.
    /// </summary>
    public static string BatchTooLargeType => ProblemTypes.DescriptionBatchTooLarge;

    /// <summary>
    /// The Host setting, by environment-variable name, that lowers the compaction threshold for a test.
    /// </summary>
    public static string CompactionThresholdSetting => "TaskDescription__CompactionThreshold";

    /// <summary>
    /// The change path of one Task.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="taskId">The Task.</param>
    /// <returns>The path.</returns>
    public static string ChangesOf(Guid spaceId, Guid taskId) => TaskRoutes.DescriptionChangesOf(spaceId, taskId);

    /// <summary>
    /// The read path of one Task.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="taskId">The Task.</param>
    /// <returns>The path.</returns>
    public static string Of(Guid spaceId, Guid taskId) => TaskRoutes.DescriptionOf(spaceId, taskId);

    /// <summary>
    /// A change batch: each encoded change and the client replica's encoded version.
    /// </summary>
    /// <param name="changes">The encoded changes, in the order the client made them.</param>
    /// <param name="version">The client replica's encoded version.</param>
    /// <returns>The body.</returns>
    public static JsonContent Body(IEnumerable<byte[]> changes, byte[] version) =>
        JsonContent.Create(
            new { changes = changes.Select(Convert.ToBase64String).ToArray(), version = Convert.ToBase64String(version) },
            options: JsonOptions);

    /// <summary>
    /// A body sent as written, for the cases about what does not parse.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The body.</returns>
    public static StringContent RawBody(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>
    /// The number of changes an answer lists as refused, or <c>null</c> when it lists none.
    /// </summary>
    /// <param name="body">A change answer.</param>
    /// <returns>The count.</returns>
    public static int? RefusedCountIn(string body) => ArrayLengthIn(body, "refused");

    /// <summary>
    /// The encoded changes an answer says the client lacks.
    /// </summary>
    /// <param name="body">A change answer.</param>
    /// <returns>Each change's bytes.</returns>
    public static IReadOnlyList<byte[]> MissingIn(string body) => BytesListIn(body, "missing");

    /// <summary>
    /// The server's encoded version an answer states.
    /// </summary>
    /// <param name="body">A change or read answer.</param>
    /// <returns>The bytes, or <c>null</c>.</returns>
    public static byte[]? VersionIn(string body) => BytesIn(body, "version");

    /// <summary>
    /// The encoded replica state a read answer states.
    /// </summary>
    /// <param name="body">A read answer.</param>
    /// <returns>The bytes, or <c>null</c>.</returns>
    public static byte[]? StateIn(string body) => BytesIn(body, "state");

    /// <summary>
    /// The projection's text a read answer states.
    /// </summary>
    /// <param name="body">A read answer.</param>
    /// <returns>The text, or <c>null</c>.</returns>
    public static string? TextIn(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("text", out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    /// <summary>
    /// The top-level member names of an answer, ordinal order.
    /// </summary>
    /// <param name="body">An answer.</param>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> MembersOf(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal)];
    }

    private static int? ArrayLengthIn(string body, string name)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : null;
    }

    private static byte[]? BytesIn(string body, string name)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetBytesFromBase64()
            : null;
    }

    private static List<byte[]> BytesListIn(string body, string name)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Select(item => item.GetBytesFromBase64())]
            : [];
    }
}
