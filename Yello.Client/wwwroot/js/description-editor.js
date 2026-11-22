/*
    THE DESCRIPTION EDITOR'S CARET. Blazor can set a textarea's value but cannot read or place its selection, and a change
    from another editor rewrites the value: without this the caret would jump to the end on every remote change. The
    component reads the selection before it applies the change, maps it through the change, and places it afterwards.
*/

/**
 * The selection of a textarea as [start, end], in UTF-16 code units.
 */
export function selectionOf(field) {
    return field instanceof HTMLTextAreaElement ? [field.selectionStart, field.selectionEnd] : [0, 0];
}

/**
 * Places the selection of a textarea, if it still has focus; a caret is never placed in a field the person has left.
 */
export function select(field, start, end) {
    if (field instanceof HTMLTextAreaElement && document.activeElement === field) {
        field.setSelectionRange(start, end);
    }
}
