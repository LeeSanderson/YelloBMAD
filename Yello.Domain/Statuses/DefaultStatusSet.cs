namespace Yello.Domain.Statuses;

/// <summary>
/// The three Statuses every new Space starts with. FR-24.
/// </summary>
/// <remarks>
/// <para>
/// <b>The names are stated only in <see cref="Names"/>, and deliberately not in prose anywhere in
/// this repository.</b> The first of the three is a word SonarAnalyzer's S1135 treats as a task
/// marker, so naming it in any comment fails the build - <c>TreatWarningsAsErrors</c> makes that
/// an error rather than a note. It is not a false positive worth suppressing: the rule is right
/// about comments in general, and the remedy costs nothing because the values belong in code
/// rather than in prose. Epic 6 builds the Status editor and will meet this constantly.
/// </para>
/// <para>
/// <b>Seeded here, owned by Epic 6.</b> Story 1.3 writes these rows and nothing else about them:
/// the Status editor, removal by migrating a Status's Tasks, the Space-default cascade and the
/// rename are stories 6.1 to 6.4. Adding any of that here is the scope creep those stories would
/// then have to unpick.
/// </para>
/// <para>
/// <b>These are seeded data, not user-visible copy, and the distinction is deliberate.</b> They
/// are not read from a localisation resource, and that is a reading of the requirement rather
/// than an oversight. FR-24 names the three literally; the moment a Space exists these are
/// ordinary rows a person may rename (FR-25), so a localised default would mean the same Space
/// showed different Status names to two members in different locales while the rows never
/// changed. The copy gate does not reach them either - it scans <c>.razor</c> markup and
/// component C#, which is where copy that a translator owns actually lives.
/// </para>
/// </remarks>
public static class DefaultStatusSet
{
    /// <summary>
    /// The default Status names, in the order a Board draws its columns.
    /// </summary>
    /// <remarks>
    /// The order is the sequence itself; <see cref="Statuses.StatusDefinition.Position"/> is
    /// assigned from the index, so this is the one place the ordering is stated.
    /// </remarks>
    public static IReadOnlyList<string> Names { get; } = ["Todo", "In Progress", "Done"];
}
