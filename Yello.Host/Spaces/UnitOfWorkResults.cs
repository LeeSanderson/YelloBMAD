namespace Yello.Host.Spaces;

/// <summary>
/// What the Space-scoped unit of work does with an endpoint's result: commit only a materialised
/// success. AD-2 / AR-6.
/// </summary>
internal static class UnitOfWorkResults
{
    /// <summary>
    /// Whether the endpoint answered with an error, which the unit of work must not commit.
    /// </summary>
    /// <param name="result">What the endpoint returned.</param>
    /// <returns><c>true</c> for a <c>4xx</c> or <c>5xx</c> result.</returns>
    internal static bool IsFailure(object? result) =>
        result is IStatusCodeHttpResult { StatusCode: >= StatusCodes.Status400BadRequest };

    /// <summary>
    /// Refuses a result that would do its work after the unit of work has closed.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core executes what an endpoint returns only after this filter has returned, and by then
    /// the transaction is committed and disposed. A query that runs at that point runs on a reopened
    /// connection with no session context, where row-level security fails closed and answers with
    /// nothing, silently. So a Space-scoped endpoint returns data it has already read; one that
    /// returns an <see cref="IQueryable"/> or an <see cref="IAsyncEnumerable{T}"/> fails loudly here,
    /// inside the unit of work, and is rolled back.
    /// </remarks>
    /// <param name="result">What the endpoint returned.</param>
    /// <exception cref="InvalidOperationException">The result is not materialised.</exception>
    internal static void EnsureMaterialised(object? result)
    {
        var value = result is IValueHttpResult { Value: var carried } ? carried : result;

        if (value is IQueryable || IsAsyncEnumerable(value))
        {
            throw new InvalidOperationException(
                $"A Space-scoped endpoint returned {value!.GetType().Name}, which would be enumerated after "
                + "its unit of work has closed. Materialise it before returning.");
        }
    }

    private static bool IsAsyncEnumerable(object? value) =>
        value is not null
        && Array.Exists(value.GetType().GetInterfaces(), type =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
}
