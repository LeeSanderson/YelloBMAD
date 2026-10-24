using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yello.Application.Spaces;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// What the Space-resolution filter does with its unit of work once the endpoint has answered.
/// AD-2 / AR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Driven directly, not over the wire.</b> The product's only Space-scoped endpoint is a read that
/// materialises its answer, so no request can reach the failure, streaming or disconnect paths. The
/// filter is called with a real route pattern and a unit of work that records what was done to it.
/// </para>
/// <para>
/// <b>Every rollback is anchored on a commit.</b> A unit of work that is never completed is also
/// what a filter that never opened one looks like, so the success case shows the same fake being
/// committed first.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-6")]
[Trait("Requirement", "AD-2")]
public sealed class ActiveSpaceUnitOfWorkTests
{
    private static readonly int[] One = [1];

    /// <summary>
    /// A success is committed, and the unit of work is closed afterwards.
    /// </summary>
    [Fact]
    public async Task A_success_is_committed()
    {
        var unitOfWork = new RecordingUnitOfWork();

        var result = await ResolveAsync(unitOfWork, TypedResults.Ok(new List<int> { 1 }), TestContext.Current.CancellationToken);

        Assert.IsType<Ok<List<int>>>(result);
        Assert.True(unitOfWork.Completed);
        Assert.True(unitOfWork.Disposed);
    }

    /// <summary>
    /// An endpoint that answers with a problem is rolled back, not committed, and its answer is
    /// passed on unchanged.
    /// </summary>
    [Fact]
    public async Task A_problem_answer_is_rolled_back()
    {
        var committed = new RecordingUnitOfWork();
        await ResolveAsync(committed, TypedResults.NoContent(), TestContext.Current.CancellationToken);

        var refused = new RecordingUnitOfWork();
        var problem = TypedResults.Problem(statusCode: StatusCodes.Status409Conflict);

        var result = await ResolveAsync(refused, problem, TestContext.Current.CancellationToken);

        Assert.True(committed.Completed);

        Assert.Same(problem, result);
        Assert.False(refused.Completed);
        Assert.True(refused.Disposed);
    }

    /// <summary>
    /// A query still to be run is refused inside the unit of work, which is rolled back, rather than
    /// being enumerated after the unit has closed.
    /// </summary>
    [Fact]
    public async Task A_query_still_to_be_run_is_refused_and_rolled_back()
    {
        var committed = new RecordingUnitOfWork();
        await ResolveAsync(committed, TypedResults.Ok(One.ToList()), TestContext.Current.CancellationToken);

        var queryable = new RecordingUnitOfWork();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolveAsync(queryable, TypedResults.Ok(One.AsQueryable()), TestContext.Current.CancellationToken).AsTask());

        var streamed = new RecordingUnitOfWork();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolveAsync(streamed, StreamedAsync(), TestContext.Current.CancellationToken).AsTask());

        Assert.True(committed.Completed);

        Assert.All([queryable, streamed], unitOfWork =>
        {
            Assert.False(unitOfWork.Completed);
            Assert.True(unitOfWork.Disposed);
        });
    }

    /// <summary>
    /// The commit is not cancelled by the request, so a client that disconnects after the endpoint
    /// has finished cannot undo work that completed.
    /// </summary>
    [Fact]
    public async Task A_disconnected_client_does_not_cancel_the_commit()
    {
        var unitOfWork = new RecordingUnitOfWork();

        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        await ResolveAsync(unitOfWork, TypedResults.NoContent(), aborted.Token);

        Assert.True(unitOfWork.Completed);
        Assert.False(unitOfWork.CommitToken.CanBeCanceled);
    }

    /// <summary>
    /// Registration's route is not Space-scoped, so the per-request setter never runs on the unit of
    /// work in which registration sets the read-only session key itself.
    /// </summary>
    [Fact]
    public void Registration_is_not_resolved_as_a_Space_scoped_request()
    {
        Assert.True(ActiveSpaceResolution.IsSpaceScoped(RequestTo(SpaceRoutes.Statuses)));
        Assert.False(ActiveSpaceResolution.IsSpaceScoped(RequestTo(AccountRoutes.Register)));
    }

    private static ValueTask<object?> ResolveAsync(
        RecordingUnitOfWork unitOfWork,
        object? endpointResult,
        CancellationToken requestAborted)
    {
        var httpContext = RequestTo(SpaceRoutes.Statuses);
        httpContext.RequestAborted = requestAborted;
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton<ISpaceRequestEstablishment>(new FixedEstablishment())
            .AddSingleton<IActiveSpaceResolver>(new FixedResolver(unitOfWork))
            .BuildServiceProvider();

        return ActiveSpaceResolution.ResolveAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult(endpointResult));
    }

    private static DefaultHttpContext RequestTo(string route)
    {
        var httpContext = new DefaultHttpContext();

        httpContext.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(route),
            order: 0,
            EndpointMetadataCollection.Empty,
            displayName: route));

        return httpContext;
    }

    private static async IAsyncEnumerable<int> StreamedAsync()
    {
        await Task.Yield();

        yield return 1;
    }

    private sealed class FixedEstablishment : ISpaceRequestEstablishment
    {
        public EstablishedSpaceRequest Establish(HttpContext httpContext) =>
            new(Guid.NewGuid(), Guid.NewGuid());
    }

    private sealed class FixedResolver(RecordingUnitOfWork unitOfWork) : IActiveSpaceResolver
    {
        public Task<IActiveSpaceUnitOfWork?> BeginAsync(
            Guid accountId,
            Guid spaceId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IActiveSpaceUnitOfWork?>(unitOfWork);
    }

    private sealed class RecordingUnitOfWork : IActiveSpaceUnitOfWork
    {
        public ActiveSpaceContext Context { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Role.Member);

        public bool Completed { get; private set; }

        public bool Disposed { get; private set; }

        public CancellationToken CommitToken { get; private set; }

        public Task CompleteAsync(CancellationToken cancellationToken)
        {
            Completed = true;
            CommitToken = cancellationToken;

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
