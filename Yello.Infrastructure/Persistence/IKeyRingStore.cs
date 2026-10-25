namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Where the Host's Data Protection key ring is read from and written to: the application database.
/// Story 1.10 AC12; Finding F1, open question 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Serialised XML in, serialised XML out.</b> The framework's key-repository contract lives in the
/// ASP.NET Core shared framework, which this library does not reference, so the Host adapts it and
/// this side deals only in strings.
/// </para>
/// <para>
/// <b>A failure is never swallowed here.</b> A database that is paused or unreachable makes a read
/// throw, and the Host answers that as a <c>503</c>. A store that answered "no keys" instead would
/// let the framework create a fresh key in memory and void every outstanding Session.
/// </para>
/// </remarks>
public interface IKeyRingStore
{
    /// <summary>
    /// Every stored key, as XML.
    /// </summary>
    /// <returns>The keys' XML, in no particular order.</returns>
    IReadOnlyList<string> ReadAll();

    /// <summary>
    /// Stores one new key.
    /// </summary>
    /// <param name="friendlyName">The name the framework gave the key.</param>
    /// <param name="xml">The key's XML.</param>
    void Add(string? friendlyName, string xml);
}
