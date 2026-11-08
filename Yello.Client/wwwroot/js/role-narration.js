/*
    FOCUS AROUND A ROLE DROP - the narration element under the context bar.

    Blazor cannot ask which element holds focus, and a drop must move focus to the narration BEFORE the control holding
    it is removed: a removed element drops focus to the body, and a screen reader then starts again from the top.

    A control a lower Role loses sits inside an element marked data-role-gated. When focus is inside one as the drop is
    narrated, focus moves to the narration first. When focus was somewhere unmarked that the drop removed all the same,
    recoverFocus puts it on the narration once the lower Role has rendered, rather than leaving it on the body.
*/

export function focusIfGated(narration) {
    const active = document.activeElement;
    const focused = !!active && active !== document.body;

    if (focused && active.closest('[data-role-gated]')) {
        narration.focus();
    }

    return focused;
}

export function recoverFocus(narration) {
    const active = document.activeElement;

    if (!active || active === document.body) {
        narration.focus();
    }
}
