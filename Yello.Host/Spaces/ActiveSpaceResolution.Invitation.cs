using Yello.Application.Invitations;
using Yello.Application.Spaces;
using Yello.Contracts.Invitations;
using Yello.Host.Endpoints;
using Yello.Host.Sessions;

namespace Yello.Host.Spaces;

/// <summary>
/// The Invitation step of the request pipeline: the view and the accept, on the Invitation unit of work keyed to the Space
/// the token resolved to on the server. Story 4.3; Lee's Q1 (a), Q2 (a), Q4 (a), Q6 (a) and Q7 (a); AD-2 and AD-24 as
/// amended on 2026-11-02, AD-18, AD-25, AD-28.
/// </summary>
/// <remarks>
/// <para>
/// <b>Part of <see cref="ActiveSpaceResolution"/>, deliberately</b>, beside the Space creation step: AR-3 makes the
/// transaction, the session key, the bound and idempotency the pipeline's, and every A-12 gate holds that only the
/// pipeline reads the bound registry, the bound-count port and the idempotency ports. The slices
/// (<c>ViewInvitationHandler</c>, <c>AcceptInvitationHandler</c>) only read and accept.
/// </para>
/// <para>
/// <b>Opted into by the endpoint's metadata</b> (<see cref="InvitationTokenEndpoint"/>): neither route carries
/// <c>{spaceId}</c> (story 4.3, C7), because the link holder does not know the Space until the view answers. The token
/// travels in the <c>Yello-Invitation-Token</c> header on the view and in the body on the accept, never in a URL.
/// </para>
/// <para>
/// <b>The view</b> opens the unit read-only and never completes it, so it commits nothing - no Invitation column, no
/// Membership, no refusal record, no idempotency record. It answers the same offer to no Session, any Session and a bearer
/// Token; the addressee indicator reads a Session only (Lee's Q5 (a)).
/// </para>
/// <para>
/// <b>The accept</b> is Session-only (Lee's Q7 (a)): <see cref="RequireAccountAsync"/> has already answered <c>401</c>
/// without one, before anti-forgery, recording nothing - a bearer Token included, since a Token is bound to one Space and
/// must never join its Account to another. In order: the token is read from the body and resolved; the unit of work opens
/// under the <c>SpaceId</c> key alone; an <c>Idempotency-Key</c> already answered is replayed (open question 11 (a), the
/// Space-scoped record keyed with the caller's Account); the screen takes the Space's Invitations lock and reads whether
/// the accept would be admitted (open questions 8 (a), 9 (a)); only then is the Memberships-per-Space bound consulted, so a
/// holder of someone else's or a dead token never learns that the Space is full; the endpoint runs; a materialised success
/// is recorded for its key and committed, and anything else rolls back. Nothing on either route is recorded as an
/// <c>AccessRefusal</c>: none of its answers is one of AD-20's two kinds.
/// </para>
/// </remarks>
internal static partial class ActiveSpaceResolution
{
    /// <summary>
    /// The Invitation step itself - nested, as <see cref="SpaceCreation"/> is, so it stays the pipeline's own for every
    /// gate while the resolution stays inside S1200's coupling limit.
    /// </summary>
    internal static class InvitationResolution
    {
        /// <summary>
        /// Runs the Invitation view or accept on the Invitation unit of work, and passes every other request straight
        /// through. Registered once on the route group, after the Space creation step.
        /// </summary>
        /// <param name="context">The request.</param>
        /// <param name="next">The rest of the pipeline.</param>
        /// <returns>The refusal, the replay, or whatever the endpoint returns.</returns>
        internal static ValueTask<object?> ResolveTokenAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<InvitationTokenEndpoint>()?.Use switch
            {
                InvitationTokenUse.Offer => ViewAsync(context, next),
                InvitationTokenUse.Acceptance => AcceptAsync(context, next),
                _ => next(context),
            };
        }

        /// <summary>
        /// Whether the endpoint serving this request reaches a Space through an Invitation token.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <returns><c>true</c> for the view and the accept.</returns>
        internal static bool IsResolvedByToken(HttpContext httpContext) =>
            httpContext.GetEndpoint()?.Metadata.GetMetadata<InvitationTokenEndpoint>() is not null;

        private static async ValueTask<object?> ViewAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;
            var services = httpContext.RequestServices;

            // Absent, the empty string: it is hashed and looked up like any other, so it costs what a real token does.
            var token = httpContext.Request.Headers[InvitationRoutes.TokenHeader].ToString();

            var unitOfWork = await services.GetRequiredService<IInvitationScope>()
                .BeginAsync(token, SessionPrincipal.AccountIdOf(httpContext.User), httpContext.RequestAborted)
                .ConfigureAwait(false);

            await using (unitOfWork.ConfigureAwait(false))
            {
                services.GetRequiredService<InvitationRequest>().Establish(unitOfWork.Context);

                var result = await next(context).ConfigureAwait(false);

                UnitOfWorkResults.EnsureMaterialised(result);

                // Never completed: the view is a safe read, and disposing the unit rolls back whatever it opened.
                return result;
            }
        }

        private static async ValueTask<object?> AcceptAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;
            var services = httpContext.RequestServices;
            var cancellationToken = httpContext.RequestAborted;
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);

            // AccountScopedResolution answered a request without a Session already; this fails closed if that is bypassed.
            if (SessionPrincipal.AccountIdOf(httpContext.User) is not { } accountId)
            {
                return BoundaryProblems.Unauthenticated();
            }

            var token = await ReadTokenAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

            var unitOfWork = await services.GetRequiredService<IInvitationScope>()
                .BeginAsync(token, accountId, cancellationToken)
                .ConfigureAwait(false);

            await using (unitOfWork.ConfigureAwait(false))
            {
                var invitation = unitOfWork.Context;

                services.GetRequiredService<InvitationRequest>().Establish(invitation);

                var idempotent = await InvitationIdempotency.BeginAsync(httpContext, logger, invitation, accountId, cancellationToken).ConfigureAwait(false);

                var result = idempotent.Answer
                    ?? await ScreenAndConsultBoundAsync(httpContext, logger, invitation, cancellationToken).ConfigureAwait(false)
                    ?? await RunAsync(context, next, logger, invitation, accountId, idempotent).ConfigureAwait(false);

                if (UnitOfWorkResults.IsFailure(result))
                {
                    return await InvitationIdempotency.LateReplayAsync(httpContext, logger, invitation, accountId, idempotent, cancellationToken).ConfigureAwait(false)
                        ?? result;
                }

                // Not the request's token: the endpoint has finished, so a client that disconnects now must not decide
                // whether its completed work is kept.
                await unitOfWork.CompleteAsync(CancellationToken.None).ConfigureAwait(false);

                return result;
            }
        }

        /// <summary>
        /// The accept's token, read from the body without consuming it - the fingerprint and the endpoint read it again.
        /// A body that is not the request's shape is the empty token: resolved like any other, it names nothing.
        /// </summary>
        private static async Task<string> ReadTokenAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            request.EnableBuffering();

            var body = await JsonRequestBody.ReadAsync(request, typeof(AcceptInvitationRequest), cancellationToken)
                .ConfigureAwait(false) as AcceptInvitationRequest;

            request.Body.Position = 0;

            return body?.InvitationToken ?? string.Empty;
        }

        /// <summary>
        /// Open question 9 (a): the bound is consulted only for an accept the screen would admit - after the lock, the
        /// actionable predicate and the caller's address - so a stranger's or a dead token's holder never meets it.
        /// </summary>
        private static async Task<IResult?> ScreenAndConsultBoundAsync(
            HttpContext httpContext,
            ILogger logger,
            InvitationContext invitation,
            CancellationToken cancellationToken)
        {
            var screened = await httpContext.RequestServices.GetRequiredService<IInvitationAcceptanceScreen>()
                .ScreenAsync(invitation, cancellationToken)
                .ConfigureAwait(false);

            return screened == InvitationAcceptanceOutcome.Accepted
                ? await CreationBound.ConsultAsync(httpContext, logger, invitation.SpaceId, cancellationToken).ConfigureAwait(false)
                : null;
        }

        private static async Task<object?> RunAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next,
            ILogger logger,
            InvitationContext invitation,
            Guid accountId,
            IdempotentRequest idempotent)
        {
            var result = await next(context).ConfigureAwait(false);

            UnitOfWorkResults.EnsureMaterialised(result);

            if (UnitOfWorkResults.IsFailure(result) || idempotent.Key is null)
            {
                return result;
            }

            return await InvitationIdempotency.RecordFirstAnswerAsync(context.HttpContext, logger, invitation, accountId, idempotent, result)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <c>Idempotency-Key</c> on the accept (open question 11 (a)): the Space-scoped record, under the invited Space and
    /// keyed with the caller's Account - the Space-scoped step's rules, on the Invitation unit of work. Nested beside
    /// <see cref="InvitationResolution"/> only to keep both inside S1200's coupling limit.
    /// </summary>
    private static class InvitationIdempotency
    {
        /// <summary>
        /// A refused accept whose <c>Idempotency-Key</c> was first used by a request that committed while this one waited on
        /// the Space's Invitations lock (code review of story 4.3): both looked the key up before the lock and found
        /// nothing, the first accepted and recorded its <c>200</c>, and this one's screen then saw the Invitation
        /// accepted. Read again now that the lock is held - read committed snapshot sees what the first committed - and
        /// replay the first answer instead of the uniform <c>404</c>, so a resend after a lost answer is never told the
        /// Invitation it just accepted is no longer valid. <c>null</c> when there is no key or still no record.
        /// </summary>
        internal static async Task<object?> LateReplayAsync(
            HttpContext httpContext,
            ILogger logger,
            InvitationContext invitation,
            Guid accountId,
            IdempotentRequest idempotent,
            CancellationToken cancellationToken)
        {
            if (idempotent.Key is null || idempotent.Answer is not null)
            {
                return null;
            }

            var recorded = await httpContext.RequestServices.GetRequiredService<IIdempotencyStore>()
                .FindAsync(accountId, idempotent.Key, cancellationToken)
                .ConfigureAwait(false);

            // The fingerprint taken before the endpoint read the body; a record for another body is the uniform refusal.
            if (recorded is null || !string.Equals(recorded.Fingerprint, idempotent.Fingerprint, StringComparison.Ordinal))
            {
                return null;
            }

            SpaceLog.IdempotentReplay(logger, invitation.SpaceId);

            return IdempotentResponses.Replay(httpContext, recorded);
        }

        /// <summary>
        /// Records a materialised success for the request's key, in the same unit of work as its effect.
        /// </summary>
        /// <returns>The answer, or <c>409</c> when a concurrent first use of the key committed first.</returns>
        internal static async Task<object?> RecordFirstAnswerAsync(
            HttpContext httpContext,
            ILogger logger,
            InvitationContext invitation,
            Guid accountId,
            IdempotentRequest idempotent,
            object? result)
        {
            var record = IdempotentResponses.Capture(httpContext, idempotent.Fingerprint!, result);

            var recorded = await httpContext.RequestServices.GetRequiredService<IIdempotencyStore>()
                .TryRecordAsync(invitation.SpaceId, accountId, idempotent.Key!, record, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (recorded)
            {
                return IdempotentResponses.FirstAnswer(httpContext, record, result);
            }

            SpaceLog.IdempotencyKeyRefused(logger, invitation.SpaceId, "in flight");

            return BoundaryProblems.IdempotencyKeyInFlight();
        }

        /// <summary>
        /// Reads the request's key and answers it from the record under the invited Space and the caller's Account when it
        /// has one - the Space-scoped step's rules, on the Invitation unit of work.
        /// </summary>
        internal static async Task<IdempotentRequest> BeginAsync(
            HttpContext httpContext,
            ILogger logger,
            InvitationContext invitation,
            Guid accountId,
            CancellationToken cancellationToken)
        {
            if (!IdempotentResponses.TryReadKey(httpContext.Request, out var key))
            {
                SpaceLog.IdempotencyKeyRefused(logger, invitation.SpaceId, "malformed");

                return IdempotentRequest.Answered(BoundaryProblems.InvalidIdempotencyKey());
            }

            if (key is null)
            {
                return IdempotentRequest.None;
            }

            var fingerprint = await IdempotentResponses.FingerprintAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

            var recorded = await httpContext.RequestServices.GetRequiredService<IIdempotencyStore>()
                .FindAsync(accountId, key, cancellationToken)
                .ConfigureAwait(false);

            if (recorded is null)
            {
                return new IdempotentRequest(key, fingerprint, Answer: null);
            }

            if (!string.Equals(recorded.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                SpaceLog.IdempotencyKeyRefused(logger, invitation.SpaceId, "reused");

                return IdempotentRequest.Answered(BoundaryProblems.IdempotencyKeyReused());
            }

            SpaceLog.IdempotentReplay(logger, invitation.SpaceId);

            return IdempotentRequest.Answered(IdempotentResponses.Replay(httpContext, recorded));
        }
    }
}

/// <summary>
/// Which of the two Invitation-token routes an endpoint is. Story 4.3.
/// </summary>
internal enum InvitationTokenUse
{
    /// <summary>
    /// The view: <c>GET</c>, read-only, any credential or none.
    /// </summary>
    Offer,

    /// <summary>
    /// The accept: <c>POST</c>, Session-only, idempotent, bound-consulting.
    /// </summary>
    Acceptance,
}

/// <summary>
/// The metadata that puts an endpoint on the Invitation unit of work: the pipeline resolves its token to a Space on the
/// server and opens the unit keyed to that Space alone. Story 4.3.
/// </summary>
/// <param name="Use">Which route it is.</param>
internal sealed record InvitationTokenEndpoint(InvitationTokenUse Use);

/// <summary>
/// Marks the Invitation-token endpoints. Story 4.3.
/// </summary>
internal static class InvitationTokenEndpoints
{
    /// <summary>
    /// Puts the endpoint on the Invitation unit of work.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <param name="use">Which route it is.</param>
    /// <returns>The same builder, for chaining.</returns>
    internal static RouteHandlerBuilder ResolvedByInvitationToken(this RouteHandlerBuilder builder, InvitationTokenUse use) =>
        builder.WithMetadata(new InvitationTokenEndpoint(use));
}

/// <summary>
/// The Invitation context established for this request, held for the endpoint to read - the Invitation step's twin of
/// <see cref="SpaceCreationRequest"/>, for the same reason: minimal APIs bind arguments before the filters run.
/// </summary>
public sealed class InvitationRequest
{
    private InvitationContext? _context;

    /// <summary>
    /// The context the pipeline established: the Space and Invitation the token resolved to, and the Session's Account.
    /// Throws on an endpoint the pipeline did not establish one for.
    /// </summary>
    public InvitationContext Context =>
        _context ?? throw new InvalidOperationException(
            "No Invitation context has been established on this request. It exists only on the Invitation view and "
            + "accept, inside the Invitation unit of work.");

    /// <summary>
    /// Records the context. The pipeline's Invitation step is the only caller.
    /// </summary>
    /// <param name="context">The context.</param>
    internal void Establish(InvitationContext context) => _context = context;
}
