using Microsoft.Playwright;

namespace Yello.Tests.Client;

/// <summary>
/// A refusal page's structure, for comparing two of them: one line per element under <c>main</c> -
/// depth, tag, <c>role</c>, <c>href</c>, <c>tabindex</c>, <c>hidden</c> and <c>aria-*</c> attributes,
/// classes and own text - then the focused element's path from <c>main</c>, or where focus is when it
/// is not inside <c>main</c>.
/// </summary>
/// <remarks>
/// Story 1.9 Task 8 (<c>deferred-work.md:348</c>): story 1.7 compared <c>main</c>'s text, which an extra
/// wrapper element or a different role would pass. Both refusal comparisons in this suite read this.
/// </remarks>
internal static class RefusalFingerprint
{
    /// <summary>
    /// Reads the fingerprint of the page as it stands.
    /// </summary>
    /// <param name="page">The page, showing a refusal.</param>
    /// <returns>The fingerprint.</returns>
    public static Task<string> ReadAsync(IPage page) =>
        page.EvaluateAsync<string>("""
        () => {
            const main = document.querySelector('main');
            if (!main) {
                return 'no main element';
            }
            const lines = [];
            const compared = ['role', 'href', 'tabindex', 'hidden'];
            const walk = (element, depth) => {
                const aria = [...element.attributes]
                    .filter(a => compared.includes(a.name) || a.name.startsWith('aria-'))
                    .map(a => `${a.name}=${a.value}`).sort().join(' ');
                const text = [...element.childNodes]
                    .filter(n => n.nodeType === Node.TEXT_NODE)
                    .map(n => n.textContent.trim()).filter(t => t).join(' ');
                lines.push(`${depth}|${element.tagName}|${aria}|${[...element.classList].sort().join('.')}|${text}`);
                [...element.children].forEach(child => walk(child, depth + 1));
            };
            walk(main, 0);
            const focusPath = [];
            const focused = document.activeElement;
            if (focused && main.contains(focused)) {
                for (let e = focused; e && e !== main; e = e.parentElement) {
                    focusPath.unshift(`${e.tagName}:${[...e.parentElement.children].indexOf(e)}`);
                }
            } else {
                focusPath.push('outside main:' + (focused ? focused.tagName : 'none'));
            }
            lines.push('focus:' + focusPath.join('/'));
            return lines.join('\n');
        }
        """);
}
