using Microsoft.Extensions.Localization;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Localisation;

/// <summary>
/// The resource set for text registration writes to the database.
/// </summary>
/// <remarks>
/// <para>
/// An empty type used only as <c>IStringLocalizer&lt;T&gt;</c>'s key. The resource file sits
/// beside it as <c>RegistrationCopy.resx</c>, so the manifest name the localiser looks for -
/// <c>Yello.Infrastructure.Localisation.RegistrationCopy</c> - is the type's own full name with
/// no <c>ResourcesPath</c> indirection to keep in step.
/// </para>
/// <para>
/// <b>This is a small resource set on purpose.</b> Almost all of Yello's copy is on the client
/// and is gated there; the only text the <i>server</i> composes is the Personal Space's name,
/// which is written into a row rather than rendered. Translating a Space's name after the fact
/// would be wrong - it is data the owner may rename - so the culture that matters is the one the
/// request arrived under, which is what <c>RequestLocalization</c> supplies.
/// </para>
/// </remarks>
public sealed class RegistrationCopy
{
    /// <summary>
    /// The resource key holding the Personal Space name's composite format string, e.g.
    /// <c>"{0}'s Space"</c>.
    /// </summary>
    /// <remarks>
    /// The template is a resource rather than a literal because the possessive is different in
    /// every language and several have none at all - Finnish inflects the noun, Japanese uses a
    /// particle, and a hard-coded <c>"'s "</c> would be untranslatable in all of them.
    /// <para>
    /// It lives on the resource set's own type so the key and the file that answers it cannot
    /// drift apart, and so this class carries a member rather than being the empty marker S2094
    /// objects to.
    /// </para>
    /// </remarks>
    public static string PersonalSpaceNameKey => "PersonalSpaceName";
}

/// <summary>
/// <see cref="IPersonalSpaceNaming"/> backed by <see cref="RegistrationCopy"/>.
/// </summary>
/// <remarks>
/// The adapter is three lines because the decision it serves is one function and one string -
/// which is the whole point of <see cref="PersonalSpaceName"/> being a choke point. If Lee's
/// answer to "what is the Personal Space called" changes, it changes the resource entry below and
/// nothing else: not the slice, not the schema, not the tests.
/// </remarks>
/// <param name="localizer">The resource set, resolved for the request's culture.</param>
internal sealed class ResourcePersonalSpaceNaming(IStringLocalizer<RegistrationCopy> localizer)
    : IPersonalSpaceNaming
{
    /// <inheritdoc />
    public string NameFor(string displayName) =>
        PersonalSpaceName.From(displayName, localizer[RegistrationCopy.PersonalSpaceNameKey].Value);
}
