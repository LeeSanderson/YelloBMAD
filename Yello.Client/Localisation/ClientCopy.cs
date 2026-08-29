namespace Yello.Client.Localisation;

/// <summary>
/// The resource set every component's copy comes from.
/// </summary>
/// <remarks>
/// <para>
/// An empty-by-design key type for <c>IStringLocalizer&lt;T&gt;</c>. The resource file sits beside
/// it as <c>ClientCopy.resx</c>, so the manifest name the localiser looks for -
/// <c>Yello.Client.Localisation.ClientCopy</c> - is this type's own full name, with no
/// <c>ResourcesPath</c> indirection to keep in step.
/// </para>
/// <para>
/// <b>Nothing in this repository may render a literal.</b>
/// <c>No_user_visible_string_literal_appears_in_a_component</c> fails the build on any word of two
/// or more letters that is not <c>Yello</c> in a <c>.razor</c> text node or in one of ten
/// attributes - a single <c>Email</c> label is a build failure - and its sibling gate scans
/// <c>@code</c> blocks and <c>*.razor.cs</c> for sentence-shaped C# literals. That is not a style
/// rule: German and Finnish run 30-40% longer than English, and a layout sized to an English
/// string breaks on contact with either.
/// </para>
/// <para>
/// <b>Failure codes are used directly as resource keys.</b> The Host's
/// <c>RegisterAccountFailure</c> codes - <c>email-address-malformed</c> and its siblings - are
/// looked up here as they arrive, so there is no code-to-key mapping table to fall out of step
/// with the server. A code with no entry renders as the code itself, which is ugly and therefore
/// noticed; a mapping table with a missing row renders as blank, which is not.
/// </para>
/// <para>
/// <b>Every value is sentence case.</b> Uppercasing comes from <c>text-transform</c> in
/// <c>base.css</c>, never from a resource: a resource holding <c>VIEWER</c> makes the accessible
/// name "V-I-E-W-E-R", spelled letter by letter by JAWS and VoiceOver
/// (<c>DESIGN.md:396-400</c>).
/// </para>
/// </remarks>
public sealed class ClientCopy
{
    /// <summary>
    /// The resource name, for anything that needs to locate the set rather than resolve a key.
    /// </summary>
    /// <remarks>
    /// Also keeps this class from being the empty marker S2094 objects to.
    /// </remarks>
    public static string ResourceName => typeof(ClientCopy).FullName ?? nameof(ClientCopy);
}
