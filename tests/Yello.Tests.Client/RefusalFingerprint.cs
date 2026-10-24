using Microsoft.Playwright;

namespace Yello.Tests.Client;

/// <summary>
/// A refusal page's structure, for comparing two of them: one line per element under <c>main</c> -
/// depth, tag, role, <c>aria-*</c> attributes, classes and own text - then the focused element's path
/// from <c>main</c>.
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
            const lines = [];
            const walk = (element, depth) => {
                const aria = [...element.attributes]
                    .filter(a => a.name === 'role' || a.name.startsWith('aria-'))
                    .map(a => `${a.name}=${a.value}`).sort().join(' ');
                const text = [...element.childNodes]
                    .filter(n => n.nodeType === Node.TEXT_NODE)
                    .map(n => n.textContent.trim()).filter(t => t).join(' ');
                lines.push(`${depth}|${element.tagName}|${aria}|${[...element.classList].sort().join('.')}|${text}`);
                [...element.children].forEach(child => walk(child, depth + 1));
            };
            walk(main, 0);
            const focusPath = [];
            for (let e = document.activeElement; e && e !== main; e = e.parentElement) {
                focusPath.unshift(`${e.tagName}:${[...e.parentElement.children].indexOf(e)}`);
            }
            lines.push('focus:' + focusPath.join('/'));
            return lines.join('\n');
        }
        """);
}
