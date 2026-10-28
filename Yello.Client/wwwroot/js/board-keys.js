/*
    THE BOARD'S KEYBOARD - story 2.8. AC1, AC3, AC4, AC6, AC7, AC11, AC12, AC15; UX-DR24, UX-DR27, UX-DR42; EXPERIENCE.md:310-326.

    Why JavaScript: Blazor's preventDefault is static per element, so it cannot let Tab through while swallowing the arrows,
    and it cannot decide per key and per state whether Space is a pick-up or a button press. This module decides which keys
    it swallows; .NET decides what they mean. It is attached to the whole Board for EVERY Role - a Viewer traverses too -
    and is separate from the drag (js/board-drag.js), which stays movers-only (UX-DR27).

    IT NEVER CHANGES WHAT BLAZOR RENDERS. Every tabindex, class and node is Blazor's. This module calls preventDefault,
    stopPropagation, focus() and scrollIntoView, and reports to .NET - nothing else.

    THE COMPOSITE'S STOPS are a card's title button ([data-task-id]) and a column's section ([data-status-id], focused only
    when it is empty or when a card it held was deleted). On a stop:
      - the four arrows are prevented, so the column does not scroll, and reported with the Board's computed direction -
        .NET maps them to logical directions (UX-DR42);
      - Space is prevented ONLY on a mover's card (.task-card--movable, which Blazor renders for movers alone): it is the
        pick-up and the drop. Everywhere else - a Viewer's card, the actions control, "+", a column, a picker - Space keeps
        its native meaning (AC4, AC11). The keyup that follows is swallowed too, so the button never also clicks;
      - while a Task is carried (.task-card--carried), Esc is the carry's: prevented and stopped, so nothing around the
        Board also closes (AC6); Tab is reported and goes on, so the carry ends as focus leaves; Enter, the context-menu key
        and Shift+F10 do nothing;
      - any other key ends the traversal run (the sticky origin, AC2).
    Every key whose target is not a stop is untouched.

    THE PICKERS AND THE CARD MENU (AC15; WAI-ARIA listbox and menu patterns): inside a [role=listbox] or [role=menu] on the
    Board, ↑/↓ move focus between its options or items and wrap, Home/End go to the first and last - focus moves, nothing is
    chosen - and Space on an option is prevented so it never scrolls (its component chooses or toggles on the key). Blazor
    renders the one option that is a tab stop from where focus is.

    FOCUS is reported as it moves into a stop (or a card's actions control), so the Board keeps exactly one roving tab stop
    whichever route moved focus - a click, a panel closing, a move's arrival - and as it leaves the Board, so a carry never
    outlives focus.
*/

const CARD = '.task-card';
const MOVABLE_CARD = '.task-card--movable';
const CARRIED_CARD = '.task-card--carried';
const OPENER = '[data-task-id]';
const ACTIONS = '.task-card__actions';
const COLUMN = 'section[data-status-id]';
const DROP_ZONE = '.drop-zone';
const LIST = '[role="listbox"], [role="menu"]';
const ITEM = '[role="option"], [role="menuitem"]';

const ARROWS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);
const LIST_KEYS = new Set(['ArrowUp', 'ArrowDown', 'Home', 'End']);
const MODIFIERS = new Set(['Shift', 'Control', 'Alt', 'Meta', 'AltGraph', 'CapsLock']);

class BoardKeys {
    #root;
    #board;
    #swallowKeyUp = null;

    constructor(root, board) {
        this.#root = root;
        this.#board = board;

        root.addEventListener('keydown', this.#onKeyDown);
        root.addEventListener('keyup', this.#onKeyUp);
        root.addEventListener('focusin', this.#onFocusIn);
        root.addEventListener('focusout', this.#onFocusOut);
        root.addEventListener('contextmenu', this.#onContextMenu);
    }

    /**
     * Brings the drop zone of a carry into view, without moving focus from the carried card.
     */
    reveal() {
        const zone = this.#root.querySelector(DROP_ZONE);

        if (zone) {
            zone.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        }
    }

    /**
     * Lets go of every listener: the Board is gone.
     */
    dispose() {
        this.#root.removeEventListener('keydown', this.#onKeyDown);
        this.#root.removeEventListener('keyup', this.#onKeyUp);
        this.#root.removeEventListener('focusin', this.#onFocusIn);
        this.#root.removeEventListener('focusout', this.#onFocusOut);
        this.#root.removeEventListener('contextmenu', this.#onContextMenu);
    }

    #onKeyDown = event => {
        if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || MODIFIERS.has(event.key)) {
            return;
        }

        if (this.#inList(event)) {
            return;
        }

        const stop = this.#stopOf(event.target);

        if (stop) {
            this.#onStopKey(event, stop);
        }
    };

    #onStopKey(event, stop) {
        const key = event.key;
        const carrying = this.#root.querySelector(CARRIED_CARD) !== null;

        if (ARROWS.has(key)) {
            if (!event.shiftKey) {
                event.preventDefault();
                this.#tell(key, stop);
            }

            return;
        }

        if (key === ' ') {
            // A mover's card only: anywhere else Space keeps its native meaning (AC4, AC11).
            if (!event.shiftKey && stop.card && stop.card.matches(MOVABLE_CARD)) {
                event.preventDefault();
                this.#swallowKeyUp = event.target;

                if (!event.repeat) {
                    this.#tell(key, stop);
                }
            }

            return;
        }

        if (carrying) {
            this.#onCarryKey(event, stop);
            return;
        }

        this.#tell(key, stop);
    }

    #onCarryKey(event, stop) {
        const key = event.key;

        if (key === 'Escape') {
            // The innermost meaning wins: Esc cancels the carry and nothing else (AC6).
            event.preventDefault();
            event.stopPropagation();
            this.#tell(key, stop);
        } else if (key === 'Tab') {
            // Focus goes on; the Task does not go with it.
            this.#tell(key, stop);
        } else if (key === 'Enter' || key === 'ContextMenu' || (key === 'F10' && event.shiftKey)) {
            event.preventDefault();
            event.stopPropagation();
        }
    }

    #onKeyUp = event => {
        if (event.key === ' ' && this.#swallowKeyUp !== null) {
            if (event.target === this.#swallowKeyUp) {
                event.preventDefault();
            }

            // Spent either way: a drop may have drawn the card afresh, so the keyup lands on another element.
            this.#swallowKeyUp = null;
        }
    };

    // The context-menu key raises its contextmenu event apart from its keydown (on keyup, on Windows): while a Task is
    // carried, the one aimed at the carried card does nothing either, so the card menu never opens over a carry.
    #onContextMenu = event => {
        const target = event.target;

        if (target instanceof Element && target.closest(CARRIED_CARD) && this.#root.contains(target)) {
            event.preventDefault();
            event.stopPropagation();
        }
    };

    /**
     * A key inside a listbox or menu: the list's own pattern. Returns whether the target was in one.
     */
    #inList(event) {
        const target = event.target;
        const list = target.matches(LIST) ? target : target.matches(ITEM) ? target.closest(LIST) : null;

        if (!list) {
            return false;
        }

        // Space on an option, or on the list itself (the Move picker focuses it on opening), never scrolls (AC15).
        if (event.key === ' ' && (target === list || target.matches('[role="option"]'))) {
            event.preventDefault();
            return true;
        }

        if (!LIST_KEYS.has(event.key) || event.shiftKey) {
            return true;
        }

        const items = [...list.querySelectorAll(ITEM)].filter(item => item.closest(LIST) === list);

        if (items.length === 0) {
            return true;
        }

        event.preventDefault();

        const at = items.indexOf(target);
        const last = items.length - 1;
        let next;

        switch (event.key) {
            case 'Home':
                next = 0;
                break;
            case 'End':
                next = last;
                break;
            case 'ArrowDown':
                next = at < 0 || at === last ? 0 : at + 1;
                break;
            default:
                next = at <= 0 ? last : at - 1;
                break;
        }

        items[next].focus();

        return true;
    }

    /**
     * The composite stop an element is: a card's title button, or a column's section itself.
     */
    #stopOf(element) {
        if (!(element instanceof Element) || !this.#root.contains(element)) {
            return null;
        }

        if (element.matches(OPENER)) {
            const card = element.closest(CARD);

            return card ? { card, taskId: element.getAttribute('data-task-id'), statusId: null } : null;
        }

        if (element.matches(COLUMN)) {
            return { card: null, taskId: null, statusId: element.getAttribute('data-status-id') };
        }

        return null;
    }

    #onFocusIn = event => {
        const target = event.target;
        let stop = this.#stopOf(target);

        // A card's actions control makes its card the active one too (open question 3 (a)).
        if (!stop && target instanceof Element && target.matches(ACTIONS)) {
            const opener = target.closest(CARD)?.querySelector(OPENER);

            stop = opener ? this.#stopOf(opener) : null;
        }

        if (stop) {
            this.#invoke('Focused', stop.taskId, stop.statusId);
        }
    };

    #onFocusOut = event => {
        const next = event.relatedTarget;

        // The window losing focus (another application, the browser's own UI) leaves focus where it was in the document:
        // a carry outlives it, and the arrows go on from the same card when the window comes back.
        if (next === null && !document.hasFocus()) {
            return;
        }

        if (!(next instanceof Element) || !this.#root.contains(next)) {
            this.#invoke('Focused', null, null);
        }
    };

    #tell(key, stop) {
        const rightToLeft = getComputedStyle(this.#root).direction === 'rtl';

        this.#invoke('Key', key, rightToLeft, stop.taskId, stop.statusId);
    }

    #invoke(method, ...args) {
        this.#board.invokeMethodAsync(method, ...args).catch(() => {
            // The Board has gone (disposed or purged): there is nobody left to tell.
        });
    }
}

/**
 * Attaches the keyboard to the Board, reporting to <paramref name="board">, a DotNetObjectReference. Called for every Role:
 * a Viewer traverses the Board too, and the mover-only keys are decided by what Blazor rendered for the Role.
 */
export function attach(root, board) {
    return new BoardKeys(root, board);
}
