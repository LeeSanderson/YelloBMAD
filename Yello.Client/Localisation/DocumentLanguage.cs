using Microsoft.JSInterop;

namespace Yello.Client.Localisation;

/// <summary>
/// Writes the active culture onto the document's <c>lang</c> attribute at startup.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the thing that makes <c>base.css</c>'s locale rules do anything at all.</b>
/// <c>base.css:141-172</c> withholds <c>text-transform: uppercase</c> from Turkish, Azeri and
/// Greek - where uppercasing changes the word - and withholds letter-spacing from 24 case-less
/// scripts, where it severs connected joins. Every one of those rules is scoped by
/// <c>:lang()</c>, which resolves against the document's <c>lang</c> attribute and nothing else.
/// <c>index.html</c> hard-coded <c>lang="en"</c>, so the entire exclusion list was <i>inert</i>:
/// it could not fire for any locale, and <c>deferred-work.md:32</c> recorded that "no gate
/// detects the inertness either".
/// </para>
/// <para>
/// <b>No JavaScript file, and no new dependency.</b> Blazor's interop resolves a dotted
/// identifier from <c>globalThis</c> and invokes it with the preceding object as <c>this</c>, so
/// <c>document.documentElement.setAttribute</c> is callable directly. This repository has zero
/// JavaScript of its own and no bundler; adding a module for one attribute would have been a
/// build step to maintain forever.
/// </para>
/// <para>
/// <b>Gated, not trusted.</b> <c>The_document_language_is_set_from_the_active_culture</c> scans
/// <c>Yello.Client</c>'s compiled IL for a call to <see cref="ApplyAsync"/>, so deleting the call
/// in <c>Program.cs</c> - or deleting this class - fails the build rather than quietly restoring
/// the inertness. It was proved against exactly that planted violation.
/// </para>
/// </remarks>
public static class DocumentLanguage
{
    /// <summary>
    /// The JavaScript function path, resolved from <c>globalThis</c> by Blazor's interop.
    /// </summary>
    public static string SetAttributeFunction => "document.documentElement.setAttribute";

    /// <summary>
    /// The attribute <c>:lang()</c> selectors resolve against.
    /// </summary>
    public static string LanguageAttribute => "lang";

    /// <summary>
    /// Sets the document's language to the given culture.
    /// </summary>
    /// <param name="jsRuntime">Blazor's interop.</param>
    /// <param name="cultureName">
    /// A BCP 47 tag, e.g. <c>en</c> or <c>de-AT</c>. The culture actually being rendered, not the
    /// one the browser asked for - a page that claims a language it is not written in is worse
    /// for a screen reader than one that claims the wrong region.
    /// </param>
    /// <returns>A task that completes once the attribute is set.</returns>
    public static ValueTask ApplyAsync(IJSRuntime jsRuntime, string cultureName) =>
        jsRuntime.InvokeVoidAsync(SetAttributeFunction, LanguageAttribute, cultureName);
}
