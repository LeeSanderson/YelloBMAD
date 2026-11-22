namespace Yello.Merge;

/// <summary>
/// Who made a change. One per editing session or server-side replica, never shared and never reused for a fresh replica.
/// </summary>
public readonly record struct ReplicaId(Guid Value);
