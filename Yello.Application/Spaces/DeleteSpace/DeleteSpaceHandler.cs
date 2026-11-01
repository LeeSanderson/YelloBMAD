namespace Yello.Application.Spaces.DeleteSpace;

/// <summary>
/// Deletes the active Space, immediately and irreversibly, with every Membership, API Token, Project, Task, Label,
/// Status, Invitation, presence row and idempotency record in it. FR-7, FR-36, AD-6, PRD §6.2; story 3.3, AC1, AC4, AC10.
/// </summary>
/// <remarks>
/// <para>
/// <b>No trash and no restore</b> (PRD §6.2): the rows are gone when this answers. The <c>AccessRefusal</c> rows naming
/// the Space stay - they carry no foreign key, and NFR-7 keeps them 90 days.
/// </para>
/// <para>
/// <b>Invitations.</b> FR-7 says a Space's Invitations are destroyed with it, and they are: <c>Invitation</c> references
/// <c>Space</c> with <c>ON DELETE CASCADE</c> (story 4.1, Lee's Q1; story 3.3's C1), so the deletion stays one statement.
/// An invitation email already enqueued is not deleted - the outbox has no Space reference (Lee's Q6) - so it may still be
/// delivered, and its link answers story 4.3's uniform "no longer valid".
/// </para>
/// <para>
/// <b>No Role, no Space from the request</b> (AR-3): the pipeline resolved the Space and checked <c>DeleteSpace</c>, which
/// the matrix grants the Owner alone. The typed name the confirm asks for is a guard against accident in the client and
/// is not sent (story 3.3, open question 3 (a)).
/// </para>
/// </remarks>
/// <param name="spaces">Deletes the Space.</param>
public sealed class DeleteSpaceHandler(ISpaceStore spaces)
{
    /// <summary>
    /// Deletes the resolved Space.
    /// </summary>
    /// <param name="context">The context the pipeline resolved.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    public Task<SpaceDeletion> HandleAsync(ActiveSpaceContext context, CancellationToken cancellationToken) =>
        spaces.DeleteAsync(context, cancellationToken);
}
