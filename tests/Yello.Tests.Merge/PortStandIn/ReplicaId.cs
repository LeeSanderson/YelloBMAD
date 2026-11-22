// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// Who made a change. One per editing session or server-side replica, never shared and never reused for a fresh replica.
/// </summary>
/// <remarks>
/// A stand-in declared in the test project; see <see cref="ITextMergeStrategy"/>'s remarks.
/// </remarks>
public readonly record struct ReplicaId(Guid Value);
