/*
    THE LIST VIEW'S KEYBOARD - story 2.10, Task 6, open question 8 (a). AC7, AC18; UX-DR24.

    Why JavaScript, and why its own module: Blazor's preventDefault is static per element, so it cannot swallow the arrows,
    Home and End while letting Tab and Space through. The Board's module (board-keys.js) carries a two-axis grammar, a carry
    and the pickers; the list needs one axis and nothing else, so it is not bent to carry a mode it does not need. This
    module decides which keys it swallows; .NET decides what they mean (ListTraversal).

    IT NEVER CHANGES WHAT BLAZOR RENDERS. Every tabindex and node is Blazor's. This module calls preventDefault, focus()
    and scrollIntoView, and reports to .NET - nothing else.

    THE STOP is a row's title cell ([data-list-stop], inside tr[data-task-id]); the rows are one roving tab stop. On a
    stop, ArrowUp, ArrowDown, Home and End are prevented - the page does not scroll - and reported with the row's Task id.
    Every other key, and every key whose target is not a stop, is untouched: Tab and Space keep their native meaning, and
    the native table keeps a screen reader's table commands: no grid role and no application mode.

    FOCUS is reported as it moves into a stop, so the list keeps exactly one tab stop whichever route moved focus.
*/

const STOP = '[data-list-stop]';
const ROW = 'tr[data-task-id]';
const KEYS = new Set(['ArrowUp', 'ArrowDown', 'Home', 'End']);

class ListKeys {
    #root;
    #list;

    constructor(root, list) {
        this.#root = root;
        this.#list = list;

        root.addEventListener('keydown', this.#onKeyDown);
        root.addEventListener('focusin', this.#onFocusIn);
    }

    /**
     * Moves focus to the stop of the row that holds the Task, by its identity, and brings it into view.
     */
    focus(taskId) {
        const row = [...this.#root.querySelectorAll(ROW)].find(candidate => candidate.dataset.taskId === taskId);
        const stop = row ? row.querySelector(STOP) : null;

        if (stop) {
            stop.focus();
            stop.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        }
    }

    /**
     * Lets go of every listener: the list is gone.
     */
    dispose() {
        this.#root.removeEventListener('keydown', this.#onKeyDown);
        this.#root.removeEventListener('focusin', this.#onFocusIn);
    }

    #onKeyDown = event => {
        if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey || !KEYS.has(event.key)) {
            return;
        }

        const taskId = this.#taskOf(event.target);

        if (taskId === null) {
            return;
        }

        event.preventDefault();
        this.#invoke('Key', event.key, taskId);
    };

    #onFocusIn = event => {
        const taskId = this.#taskOf(event.target);

        if (taskId !== null) {
            this.#invoke('Focused', taskId);
        }
    };

    #taskOf(element) {
        if (!(element instanceof Element) || !element.matches(STOP) || !this.#root.contains(element)) {
            return null;
        }

        return element.closest(ROW)?.dataset.taskId ?? null;
    }

    #invoke(method, ...args) {
        this.#list.invokeMethodAsync(method, ...args).catch(() => {
            // The list has gone (disposed or purged): there is nobody left to tell.
        });
    }
}

/**
 * Attaches the keyboard to the list, reporting to <paramref name="list">, a DotNetObjectReference. Every Role: the list is
 * read-only for all of them.
 */
export function attach(root, list) {
    return new ListKeys(root, list);
}
