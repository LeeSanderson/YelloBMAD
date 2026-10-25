using Yello.Contracts;

namespace Yello.Client.Refusals;

/// <summary>
/// Which of the client's own strings a refusal is rendered with. AC9; UX-DR32, UX-DR35.
/// </summary>
/// <remarks>
/// <para>
/// <b>The <c>type</c> decides; prose never does.</b> The server's <c>title</c> and <c>detail</c> are
/// not even deserialised (<see cref="ProblemResponse"/>). A <c>403</c> is rendered from a resource
/// keyed on its capability code, a bound refusal from one keyed on its bound code with the limit as
/// the only argument, the two idempotency refusals from one each (story 2.2), and everything else
/// from the one not-available heading.
/// </para>
/// <para>
/// <b>No branch on which not-found occurred.</b> A Space the Account holds no Membership in, a
/// Space that does not exist, an unmatched address and an unrecognised problem all resolve to the
/// same key, because the client has nothing to tell them apart with and must not try.
/// </para>
/// </remarks>
public static class RefusalCopy
{
    /// <summary>
    /// The not-available heading's resource key.
    /// </summary>
    public static string NotAvailableKey => "NotAvailable";

    /// <summary>
    /// The resource key for a <c>403</c> whose capability code the client does not recognise.
    /// </summary>
    public static string RoleDoesNotPermitKey => "RoleDoesNotPermit";

    /// <summary>
    /// The refusal page's one link's resource key.
    /// </summary>
    public static string RouteBackKey => "RefusalRouteBack";

    /// <summary>
    /// The resource key for <c>409 idempotency-key-in-flight</c>: the request was already sent and its
    /// first use is still finishing (story 2.2, AC13).
    /// </summary>
    public static string IdempotencyKeyInFlightKey => "IdempotencyKeyInFlight";

    /// <summary>
    /// The resource key for <c>422 idempotency-key-reused</c>: the key was already used for a different
    /// request (story 2.2, AC13).
    /// </summary>
    public static string IdempotencyKeyReusedKey => "IdempotencyKeyReused";

    /// <summary>
    /// The resource key for <c>400 invalid-task-title</c>, met by a Task creation and a Task edit.
    /// </summary>
    public static string InvalidTaskTitleKey => "TaskTitleInvalid";

    /// <summary>
    /// The resource key for <c>400 invalid-task-status</c>: a Task edit's Status is not one of its Project's
    /// (story 2.3).
    /// </summary>
    public static string InvalidTaskStatusKey => "InvalidTaskStatus";

    /// <summary>
    /// The resource key for <c>400 invalid-due-date</c> (story 2.3).
    /// </summary>
    public static string InvalidDueDateKey => "InvalidDueDate";

    /// <summary>
    /// The resource key for <c>400 invalid-task-edit</c>: an edit body the server could not read (story 2.3).
    /// </summary>
    public static string InvalidTaskEditKey => "InvalidTaskEdit";

    /// <summary>
    /// The resource a refusal is rendered with.
    /// </summary>
    /// <param name="problem">The problem body, or <c>null</c> for an address the client cannot route.</param>
    /// <returns>The resource key, and the limit to format it with when it is a bound refusal.</returns>
    public static RefusalMessage For(ProblemResponse? problem) => For(problem, hasCopy: _ => true);

    /// <summary>
    /// The resource a refusal is rendered with, given which resource keys the client has copy for.
    /// </summary>
    /// <remarks>
    /// A capability code the client has no copy for - a server newer than a cached client - falls
    /// back to the generic capability statement, so the page never renders a bare code.
    /// </remarks>
    /// <param name="problem">The problem body, or <c>null</c> for an address the client cannot route.</param>
    /// <param name="hasCopy">Whether the client has a resource for a key.</param>
    /// <returns>The resource key, and the limit to format it with when it is a bound refusal.</returns>
    public static RefusalMessage For(ProblemResponse? problem, Func<string, bool> hasCopy)
    {
        ArgumentNullException.ThrowIfNull(hasCopy);

        var type = problem?.Type;

        if (string.Equals(type, ProblemTypes.InsufficientRole, StringComparison.Ordinal))
        {
            var capability = problem!.Capability;

            return new RefusalMessage(
                string.IsNullOrWhiteSpace(capability) || !hasCopy(capability) ? RoleDoesNotPermitKey : capability,
                Limit: null);
        }

        if (string.Equals(type, ProblemTypes.BoundReached, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(problem!.Bound)
            && problem.Limit is { } limit)
        {
            return new RefusalMessage(problem.Bound, limit);
        }

        // None of these is a not-found. A person told their request is still in flight must not be told it
        // does not exist (story 2.2, AC13; deferred-work.md:460), and a person whose edit had one field refused
        // must not be told their Task is gone (story 2.3, AC12).
        if (OwnKey(type) is { } own)
        {
            return new RefusalMessage(own, Limit: null);
        }

        return new RefusalMessage(NotAvailableKey, Limit: null);
    }

    private static string? OwnKey(string? type) => type switch
    {
        _ when string.Equals(type, ProblemTypes.IdempotencyKeyInFlight, StringComparison.Ordinal) => IdempotencyKeyInFlightKey,
        _ when string.Equals(type, ProblemTypes.IdempotencyKeyReused, StringComparison.Ordinal) => IdempotencyKeyReusedKey,
        _ when string.Equals(type, ProblemTypes.InvalidTaskTitle, StringComparison.Ordinal) => InvalidTaskTitleKey,
        _ when string.Equals(type, ProblemTypes.InvalidTaskStatus, StringComparison.Ordinal) => InvalidTaskStatusKey,
        _ when string.Equals(type, ProblemTypes.InvalidDueDate, StringComparison.Ordinal) => InvalidDueDateKey,
        _ when string.Equals(type, ProblemTypes.InvalidTaskEdit, StringComparison.Ordinal) => InvalidTaskEditKey,
        _ => null,
    };
}

/// <summary>
/// One refusal, as the client will render it.
/// </summary>
/// <param name="Key">The resource key in <c>ClientCopy.resx</c>.</param>
/// <param name="Limit">The bound's value, the resource's one format argument; <c>null</c> otherwise.</param>
public sealed record RefusalMessage(string Key, int? Limit);
