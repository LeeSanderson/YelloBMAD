namespace Yello.Tests.Shared;

/// <summary>
/// The Label routes, the five problem types and the palette keys story 2.4 adds, stated once on the test side
/// because <c>Yello.Contracts</c> and <c>Yello.Domain</c> do not hold them yet. Written by the story 2.4 ATDD run.
/// <b>Temporary</b>: on activation each use is replaced by the contract or Domain member (<c>LabelRoutes.List</c>,
/// <c>LabelRoutes.Create</c>, <c>LabelRoutes.Label</c>, <c>LabelRoutes.LabelsOf</c>, <c>LabelRoutes.LabelOf</c>,
/// <c>ProblemTypes.InvalidLabelName</c> ..., <c>LabelName.MaxLength</c>, <c>LabelPalette</c>) and this file is
/// deleted, as story 2.3's <c>TaskEditScaffold</c> was.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded against the story's recommended options.</b> Q1 (a): applying is a <c>labelIds</c> member on the
/// Task <c>PATCH</c>, so no route of its own. Q2: names trimmed, 1-50 characters, unique per Space. Q3: the colour
/// is a palette key. Q6: no NFR-8 bound, the <c>labelIds</c> array length guarded as input shape. If a decision
/// differs, correct this file first and record it.
/// </para>
/// <para>
/// <b>The routes sit under the Space, never a Project</b> (FR-22 "available to every Project";
/// <c>The_routes_under_a_Project_are_its_rename_its_Task_creation_and_its_Board</c> stays green).
/// </para>
/// </remarks>
public static class LabelScaffold
{
    /// <summary>
    /// <c>GET</c> (list) and <c>POST</c> (define) the Space's Labels: story 2.4 Task 4's <c>LabelRoutes.List</c> and
    /// <c>LabelRoutes.Create</c>.
    /// </summary>
    public static string Labels => "/api/v1/spaces/{spaceId:guid}/labels";

    /// <summary>
    /// <c>DELETE</c> one Label: story 2.4 Task 4's <c>LabelRoutes.Label</c>.
    /// </summary>
    public static string Label => "/api/v1/spaces/{spaceId:guid}/labels/{labelId:guid}";

    /// <summary>
    /// <c>400</c>: a Label name that is blank after trimming or longer than <see cref="NameMaxLength"/> (Q2).
    /// </summary>
    public static string InvalidLabelName => "urn:yello:problem:invalid-label-name";

    /// <summary>
    /// <c>400</c>: a colour that is not one of the palette keys (Q3) - a hex string included.
    /// </summary>
    public static string InvalidLabelColour => "urn:yello:problem:invalid-label-colour";

    /// <summary>
    /// <c>409</c>: the Space already holds a Label of that name, compared case-insensitively (Q2).
    /// </summary>
    public static string LabelNameTaken => "urn:yello:problem:label-name-taken";

    /// <summary>
    /// <c>409</c>: the Label is applied to at least one Task, so it cannot be deleted (AC3, Q11). No count, no title.
    /// </summary>
    public static string LabelInUse => "urn:yello:problem:label-in-use";

    /// <summary>
    /// <c>400</c>: a <c>labelIds</c> entry that is unknown, another Space's, duplicated, not a GUID, or deleted under
    /// the request (AC10, AC11). A random id and a foreign one answer the same bytes.
    /// </summary>
    public static string InvalidTaskLabels => "urn:yello:problem:invalid-task-labels";

    /// <summary>
    /// The longest Label name Q2 accepts, after trimming.
    /// </summary>
    public static int NameMaxLength => 50;

    /// <summary>
    /// A <c>labelIds</c> array longer than any input-shape guard Q6 recommends ("e.g. 500"). If the dev phase sets
    /// the guard higher, raise this above it.
    /// </summary>
    public static int OversizeLabelIds => 1001;

    /// <summary>
    /// The eight palette keys Q3 recommends, in palette order.
    /// </summary>
    public static IReadOnlyList<string> PaletteKeys { get; } = ["rust", "ochre", "olive", "grass", "jade", "teal", "blue", "orchid"];

    /// <summary>
    /// The Space's Labels: <c>LabelRoutes.LabelsOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string LabelsOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/labels";

    /// <summary>
    /// One Label: <c>LabelRoutes.LabelOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="labelId">The Label.</param>
    /// <returns>The path.</returns>
    public static string LabelOf(Guid spaceId, Guid labelId) => $"/api/v1/spaces/{spaceId:D}/labels/{labelId:D}";
}
