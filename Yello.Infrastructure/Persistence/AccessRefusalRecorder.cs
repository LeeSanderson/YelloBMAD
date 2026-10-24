using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain;
using Yello.Domain.AccessRefusals;
using Yello.Domain.Memberships;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Writes one <see cref="AccessRefusal"/> row per refusal at the Space boundary, on a unit of work
/// of its own. AD-20 / AR-11.
/// </summary>
/// <remarks>
/// <para>
/// <b>A fresh context, never the request's.</b> The request's context has just been rolled back -
/// at a <c>404</c> by the resolver, at a <c>403</c> by the pipeline - and a context used again after
/// that runs on a reopened pooled connection with no session context. This table needs none (it is
/// not Space-scoped), but the rule is simpler kept than reasoned about per call site, and a second
/// <c>SpaceSessionContext.SetAsync</c> on the request's connection would be refused outright (error
/// 15664). A context of its own owns one short insert and nothing else.
/// </para>
/// <para>
/// <b>Not cancelled by the request.</b> A caller who disconnects as the refusal is written has still
/// been refused, and the record of it is the point.
/// </para>
/// </remarks>
/// <param name="options">The configured context options, from which a fresh context is built.</param>
/// <param name="identifiers">The application-side id generator.</param>
/// <param name="time">The clock.</param>
internal sealed class AccessRefusalRecorder(
    DbContextOptions<YelloDbContext> options,
    IIdentifierGenerator identifiers,
    TimeProvider time) : IAccessRefusalRecorder
{
    /// <summary>
    /// The boundary's not-found status, which a cross-Space refusal answered with.
    /// </summary>
    private const int NotFound = 404;

    /// <summary>
    /// The insufficient-Role status.
    /// </summary>
    private const int Forbidden = 403;

    /// <inheritdoc />
    public async Task RecordAsync(
        Guid accountId,
        Guid targetSpaceId,
        Capability capability,
        AccessRefusalKind kind,
        CancellationToken cancellationToken)
    {
        var refusal = new AccessRefusal
        {
            Id = identifiers.Next(),
            AccountId = accountId,
            TargetSpaceId = targetSpaceId,
            Capability = capability,
            Outcome = OutcomeOf(kind),
            Kind = kind,
            OccurredAt = time.GetUtcNow(),
        };

        var context = new YelloDbContext(options);

        await using (context.ConfigureAwait(false))
        {
            context.Set<AccessRefusal>().Add(refusal);

            await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The status the caller received, which follows from the kind: a cross-Space refusal is the
    /// boundary's <c>404</c>, an insufficient Role is a <c>403</c>.
    /// </summary>
    private static int OutcomeOf(AccessRefusalKind kind) => kind switch
    {
        AccessRefusalKind.CrossSpace => NotFound,
        AccessRefusalKind.InsufficientRole => Forbidden,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a refusal kind AD-20 records."),
    };
}
