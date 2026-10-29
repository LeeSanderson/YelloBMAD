/*
    SINGLE-LINE TEXT THAT WRAPS - the Task detail panel's Title field.

    Why a textarea at all: an <input> cannot wrap, so a long title under WCAG 1.4.12 text spacing or a wide font was
    cut off by its own box. A textarea wraps and, with `field-sizing: content`, grows to fit. But a title is one line,
    so the field must not take one: Enter keeps its meaning (commit), and a pasted line break becomes a space.

    Why JavaScript: Blazor's preventDefault is static per element, so it cannot swallow Enter while letting every other
    key through. This module only stops the newline; Blazor's own keydown handler still sees Enter and commits.

    IT NEVER CHANGES WHAT BLAZOR RENDERS. The target is any textarea[data-single-line]. Both listeners are on the document
    in the capture phase, so they run before Blazor's delegated handlers read the key or the value, and installing twice
    is a no-op - there is no per-element attach to keep in step with renders.
*/

const FIELD = 'textarea[data-single-line]';

let installed = false;

function onKeyDown(event) {
    if (event.key === 'Enter' && !event.isComposing && event.target instanceof Element && event.target.matches(FIELD)) {
        event.preventDefault();
    }
}

function onInput(event) {
    const field = event.target;

    if (!(field instanceof HTMLTextAreaElement) || !field.matches(FIELD) || !/[\r\n]/.test(field.value)) {
        return;
    }

    const caret = field.selectionStart;

    field.value = field.value.replace(/\r\n|[\r\n]/g, ' ');
    field.setSelectionRange(caret, caret);
}

/**
 * Makes every textarea[data-single-line] on the page single-line. Idempotent.
 */
export function install() {
    if (installed) {
        return;
    }

    installed = true;
    document.addEventListener('keydown', onKeyDown, true);
    document.addEventListener('input', onInput, true);
}
