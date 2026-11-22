using System.Data.SqlTypes;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// SQL Server's ordering of <c>uniqueidentifier</c>, which is not <see cref="Guid"/>'s own.
/// </summary>
/// <remarks>
/// Apart from <see cref="TaskStore"/> only to keep that class within its dependency budget (S1200).
/// </remarks>
internal static class ServerGuidOrder
{
    /// <summary>
    /// Compares two ids as the server orders them.
    /// </summary>
    internal static IComparer<Guid> Comparer { get; } =
        Comparer<Guid>.Create((first, second) => new SqlGuid(first).CompareTo(new SqlGuid(second)));
}
