using Yello.Application.Spaces;
using Yello.Domain.Labels;

namespace Yello.Application.Labels.DefineLabel;

/// <summary>
/// Defines a Label in the active Space, available to every Project in it. FR-22; story 2.4, AC1.
/// </summary>
/// <remarks>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space, checked
/// <c>ManageLabels</c> against the matrix and replayed an <c>Idempotency-Key</c> it had already seen. This slice
/// reads the Space off the resolved context - never its Role, and never a Space from the request - and consults no
/// bound (story 2.4, open question 6).
/// </remarks>
/// <param name="store">Writes the Label.</param>
public sealed class DefineLabelHandler(ILabelStore store)
{
    /// <summary>
    /// Defines the Label.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="name">The name, already normalised.</param>
    /// <param name="colour">The colour, already a palette key.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Label when it was defined.</returns>
    public Task<DefineLabelResult> HandleAsync(ActiveSpaceContext context, string name, string colour, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return DefineAsync(context.SpaceId, name, colour, cancellationToken);
    }

    private async Task<DefineLabelResult> DefineAsync(Guid spaceId, string name, string colour, CancellationToken cancellationToken)
    {
        var label = await store.CreateAsync(spaceId, name, colour, cancellationToken).ConfigureAwait(false);

        return label is null ? DefineLabelResult.NameTaken : DefineLabelResult.Defined(label);
    }
}

/// <summary>
/// The outcome of <see cref="DefineLabelHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Label">The Label, when <see cref="Outcome"/> is <see cref="DefineLabelOutcome.Defined"/>.</param>
public sealed record DefineLabelResult(DefineLabelOutcome Outcome, Label? Label)
{
    /// <summary>
    /// The Space already holds a Label of that name; nothing was written.
    /// </summary>
    public static DefineLabelResult NameTaken { get; } = new(DefineLabelOutcome.NameTaken, null);

    /// <summary>
    /// The Label was defined.
    /// </summary>
    /// <param name="label">The Label.</param>
    /// <returns>The result.</returns>
    public static DefineLabelResult Defined(Label label) => new(DefineLabelOutcome.Defined, label);
}

/// <summary>
/// What a definition came to.
/// </summary>
public enum DefineLabelOutcome
{
    /// <summary>
    /// The Label was defined.
    /// </summary>
    Defined,

    /// <summary>
    /// The Space already holds a Label of that name, compared case-insensitively.
    /// </summary>
    NameTaken,
}
