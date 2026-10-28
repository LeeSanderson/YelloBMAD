/*
    THE BOARD'S POINTER AND TOUCH DRAG - story 2.7. AC1-AC6, AC10, AC11, AC13, AC15; UX-DR22, UX-DR23, UX-DR6;
    EXPERIENCE.md:297-308; DESIGN.md:506-521.

    The repository's first JavaScript of its own, and the reason it exists: Blazor's event system cannot capture a pointer,
    cannot decide to preventDefault a touchmove halfway through a gesture, and would cross into .NET on every pointermove.
    So the gesture lives here, and .NET is told only four things - a lift, a change of destination slot, a drop and a
    cancel (AC15). Nothing reaches the network from this file: the drop is story 2.6's move, sent by the Board.

    IT NEVER CHANGES WHAT BLAZOR RENDERS. It writes two custom properties on the dragged card (--drag-x, --drag-y) and
    nothing else: no node inserted, removed or reordered, no class added. The lifted look and the drop zone are rendered
    by Blazor from the state this module reports, so the two never fight over the DOM.

    POINTER (mouse, pen): primary button only; a press lifts once the pointer has moved past the drag-start distance - the
    same --motion-long-press-slop as touch (story 2.7 Q3 (a)), so a click that wobbles a few pixels is still a click.

    TOUCH: a press is a PENDING lift for --motion-long-press-threshold. A finger that moves beyond --motion-long-press-slop
    in ANY direction before then - the distance is Euclidean, so the rule is axis-agnostic with a real dead zone (AC3) - is
    a pan: the pending lift is cancelled and the browser scrolls. Once the lift commits the finger drives the drag, so its
    touchmoves are prevented from panning the page. A scroll the browser starts anyway (pointercancel), a second finger,
    Esc, losing focus or the page hiding end the drag with nothing moved (AC4, AC13).

    Both tokens are read from the root's computed style at each press (story 2.7 Q2 (a)): the token layer stays the single
    source, and a test can lengthen the threshold without racing the clock.

    HIT-TESTING is by bounding rects of the [data-status-id] columns and the .task-card elements in them, the dragged card
    excluded - direction-neutral, so RTL needs nothing here (UX-DR42 is story 2.11's). The slot is the number of cards
    whose middle is above the pointer; the pointer inside the drop zone keeps the slot it has, so the zone cannot chase
    itself. Outside every column there is no drop zone, and a release there moves nothing (AC6).

    AUTO-SCROLL (AC5, story 2.7 Q13): while lifted, a requestAnimationFrame loop scrolls the hovered column near its block
    edges, the Board's row near the view's inline edges, and the page near the view's block edges - in proportion to the
    depth into an edge zone of twice --target-min. Direct manipulation, not animation, so reduced motion leaves it alone.
*/

const FALLBACK_THRESHOLD_MS = 320;
const FALLBACK_SLOP_PX = 10;
const FALLBACK_TARGET_PX = 24;
const MOST_SCROLL_PER_FRAME_PX = 16;
const HAPTIC_PULSE_MS = 10;

const MOVABLE_CARD = '.task-card--movable';
const CARD = '.task-card';
const COLUMN = '[data-status-id]';
const DROP_ZONE = '.drop-zone';
const OPENER = '[data-task-id]';

// Never a handle: the actions control and anything inside an open menu or picker (story 2.7 Q4 (a)).
const NOT_A_HANDLE = '.task-card__actions, .task-menu, .move-picker, [role="menu"], [role="listbox"]';

/**
 * Reads a length or a duration token from the root, as px or ms; the fallback when it cannot be read.
 */
function readToken(name, fallback) {
    const root = document.documentElement;
    const text = getComputedStyle(root).getPropertyValue(name).trim();
    const value = parseFloat(text);

    if (!Number.isFinite(value)) {
        return fallback;
    }

    if (text.endsWith('ms')) {
        return value;
    }

    if (text.endsWith('s')) {
        return value * 1000;
    }

    if (text.endsWith('rem')) {
        return value * parseFloat(getComputedStyle(root).fontSize);
    }

    return value;
}

function centreOf(rect) {
    return { x: (rect.left + rect.right) / 2, y: (rect.top + rect.bottom) / 2 };
}

function inside(rect, x, y) {
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
}

/**
 * How far into an edge zone a coordinate is: negative near the start edge, positive near the end, zero elsewhere -
 * scaled to at most the largest step a frame scrolls.
 */
function edgeStep(position, start, end, zone) {
    if (position < start + zone) {
        return -Math.ceil(MOST_SCROLL_PER_FRAME_PX * Math.min(1, (start + zone - position) / zone));
    }

    if (position > end - zone) {
        return Math.ceil(MOST_SCROLL_PER_FRAME_PX * Math.min(1, (position - (end - zone)) / zone));
    }

    return 0;
}

class BoardDrag {
    #root;
    #board;
    #gesture = null;
    #frame = 0;
    #suppressClick = false;
    #suppressMenu = false;

    constructor(root, board) {
        this.#root = root;
        this.#board = board;

        root.addEventListener('pointerdown', this.#onPress);

        // Not passive: once a touch lift has committed, the finger's moves must not pan the page. Registered before any
        // touch begins, which is when the browser decides whether a touchmove can be prevented at all.
        root.addEventListener('touchmove', this.#onTouchMove, { passive: false });

        window.addEventListener('pointerdown', this.#onAnyPress, true);
        window.addEventListener('click', this.#onClick, true);
        window.addEventListener('contextmenu', this.#onContextMenu, true);
    }

    /**
     * Ends any drag silently - the Board has purged, read itself again, lost the dragged Task or opened the panel, and
     * already knows.
     */
    cancel() {
        this.#end(false);
    }

    /**
     * Ends any drag and lets go of every listener.
     */
    dispose() {
        this.#end(false);
        this.#root.removeEventListener('pointerdown', this.#onPress);
        this.#root.removeEventListener('touchmove', this.#onTouchMove, { passive: false });
        window.removeEventListener('pointerdown', this.#onAnyPress, true);
        window.removeEventListener('click', this.#onClick, true);
        window.removeEventListener('contextmenu', this.#onContextMenu, true);
    }

    #onAnyPress = event => {
        // A new press starts afresh: the click and context menu a finished drag would have raised are long gone.
        if (!this.#gesture) {
            this.#suppressClick = false;
            this.#suppressMenu = false;
            return;
        }

        // A second pointer - another finger, a pinch - ends the drag (AC13).
        if (event.pointerId !== this.#gesture.pointerId) {
            this.#end(true);
        }
    };

    #onPress = event => {
        if (this.#gesture || !event.isPrimary || (event.pointerType !== 'touch' && event.button !== 0)) {
            return;
        }

        const card = event.target.closest(MOVABLE_CARD);
        const opener = card && card.querySelector(OPENER);

        if (!card || !opener || !this.#root.contains(card) || event.target.closest(NOT_A_HANDLE)) {
            return;
        }

        const touch = event.pointerType === 'touch';

        this.#gesture = {
            pointerId: event.pointerId,
            touch,
            card,
            taskId: opener.getAttribute('data-task-id'),
            startX: event.clientX,
            startY: event.clientY,
            x: event.clientX,
            y: event.clientY,
            slop: readToken('--motion-long-press-slop', FALLBACK_SLOP_PX),
            edge: 2 * readToken('--target-min', FALLBACK_TARGET_PX),
            lifted: false,
            timer: 0,
            grabX: 0,
            grabY: 0,
            dragX: 0,
            dragY: 0,
            target: null,
        };

        window.addEventListener('pointermove', this.#onMove, true);
        window.addEventListener('pointerup', this.#onRelease, true);
        window.addEventListener('pointercancel', this.#onPointerCancel, true);
        window.addEventListener('keydown', this.#onKey, true);
        window.addEventListener('blur', this.#onBlur);
        document.addEventListener('visibilitychange', this.#onVisibility);
        document.addEventListener('selectstart', this.#onSelectStart, true);

        if (touch) {
            this.#gesture.timer = setTimeout(this.#lift, readToken('--motion-long-press-threshold', FALLBACK_THRESHOLD_MS));
        }
    };

    #onMove = event => {
        const gesture = this.#gesture;

        if (!gesture || event.pointerId !== gesture.pointerId) {
            return;
        }

        gesture.x = event.clientX;
        gesture.y = event.clientY;

        if (gesture.lifted) {
            return;
        }

        if (Math.hypot(gesture.x - gesture.startX, gesture.y - gesture.startY) <= gesture.slop) {
            return;
        }

        if (gesture.touch) {
            // Beyond the slop before the threshold: a pan, and it wins (AC3, AC4). Nothing was lifted, nothing is said.
            this.#end(false);
        } else {
            this.#lift();
        }
    };

    #lift = () => {
        const gesture = this.#gesture;

        if (!gesture || gesture.lifted) {
            return;
        }

        clearTimeout(gesture.timer);
        gesture.lifted = true;

        const centre = centreOf(gesture.card.getBoundingClientRect());

        gesture.grabX = gesture.startX - centre.x;
        gesture.grabY = gesture.startY - centre.y;

        if (gesture.touch) {
            if (typeof navigator.vibrate === 'function') {
                navigator.vibrate(HAPTIC_PULSE_MS);
            }
        } else {
            try {
                gesture.card.setPointerCapture(gesture.pointerId);
            } catch {
                // The pointer is already gone; the release or cancel that follows ends the drag.
            }
        }

        this.#tell('Lifted', gesture.taskId);
        this.#frame = requestAnimationFrame(this.#step);
    };

    #step = () => {
        const gesture = this.#gesture;

        if (!gesture || !gesture.lifted) {
            return;
        }

        this.#autoScroll(gesture);
        this.#follow(gesture);

        const target = this.#hitTest(gesture);

        if (!this.#same(target, gesture.target)) {
            gesture.target = target;
            this.#tell('Targeted', target ? target.statusId : null, target ? target.index : -1);
        }

        this.#frame = requestAnimationFrame(this.#step);
    };

    /**
     * Keeps the card under the pointer where it was grabbed. Its layout slot can move under it - a drop zone drawn above
     * it, its column scrolled - so the offset is worked out from where the card is now: rotation about the centre leaves
     * the centre where the translation put it.
     */
    #follow(gesture) {
        const centre = centreOf(gesture.card.getBoundingClientRect());

        gesture.dragX = gesture.x - gesture.grabX - (centre.x - gesture.dragX);
        gesture.dragY = gesture.y - gesture.grabY - (centre.y - gesture.dragY);

        gesture.card.style.setProperty('--drag-x', `${gesture.dragX}px`);
        gesture.card.style.setProperty('--drag-y', `${gesture.dragY}px`);
    }

    #autoScroll(gesture) {
        const { x, y, edge } = gesture;

        for (const column of this.#root.querySelectorAll(COLUMN)) {
            const rect = column.getBoundingClientRect();

            if (inside(rect, x, y)) {
                const step = edgeStep(y, rect.top, rect.bottom, edge);

                if (step !== 0) {
                    column.scrollBy({ top: step, behavior: 'instant' });
                }

                break;
            }
        }

        const sideways = edgeStep(x, 0, window.innerWidth, edge);

        if (sideways !== 0) {
            this.#root.scrollBy({ left: sideways, behavior: 'instant' });
        }

        const vertical = edgeStep(y, 0, window.innerHeight, edge);

        if (vertical !== 0) {
            window.scrollBy({ top: vertical, behavior: 'instant' });
        }
    }

    #hitTest(gesture) {
        const { x, y } = gesture;

        if (!inside(this.#root.getBoundingClientRect(), x, y)) {
            return null;
        }

        for (const column of this.#root.querySelectorAll(COLUMN)) {
            if (!inside(column.getBoundingClientRect(), x, y)) {
                continue;
            }

            const statusId = column.getAttribute('data-status-id');
            const zone = column.querySelector(DROP_ZONE);

            if (zone && gesture.target && gesture.target.statusId === statusId && inside(zone.getBoundingClientRect(), x, y)) {
                return gesture.target;
            }

            let index = 0;

            for (const card of column.querySelectorAll(CARD)) {
                if (card !== gesture.card && centreOf(card.getBoundingClientRect()).y < y) {
                    index++;
                }
            }

            return { statusId, index };
        }

        return null;
    }

    #same(first, second) {
        return first === second || (!!first && !!second && first.statusId === second.statusId && first.index === second.index);
    }

    #onRelease = event => {
        const gesture = this.#gesture;

        if (!gesture || event.pointerId !== gesture.pointerId) {
            return;
        }

        if (!gesture.lifted) {
            // Never lifted: a click, which goes ahead exactly as it would have.
            this.#end(false);
            return;
        }

        gesture.x = event.clientX;
        gesture.y = event.clientY;

        const target = this.#hitTest(gesture);

        this.#release(gesture);

        if (target) {
            this.#tell('Dropped', target.statusId, target.index);
        } else {
            this.#tell('Cancelled');
        }
    };

    #onPointerCancel = event => {
        if (this.#gesture && event.pointerId === this.#gesture.pointerId) {
            this.#end(true);
        }
    };

    #onKey = event => {
        if (event.key === 'Escape' && this.#gesture) {
            // The innermost meaning wins: Esc ends the drag and nothing else.
            event.preventDefault();
            event.stopPropagation();
            this.#end(true);
        }
    };

    #onBlur = () => this.#end(true);

    #onVisibility = () => {
        if (document.visibilityState === 'hidden') {
            this.#end(true);
        }
    };

    #onSelectStart = event => event.preventDefault();

    #onTouchMove = event => {
        if (this.#gesture && this.#gesture.touch && this.#gesture.lifted && event.cancelable) {
            event.preventDefault();
        }
    };

    #onClick = event => {
        // The click a finished drag raises on release never opens the Task or its menu (AC11).
        if (this.#suppressClick) {
            this.#suppressClick = false;
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    };

    #onContextMenu = event => {
        // A long-press raises the platform's context menu; while a touch lift is pending or held, and after it, the press is
        // the drag's - never 2.6's card menu, never the browser's (story 2.7 Q11 (a)). A mouse right-click is untouched.
        if ((this.#gesture && this.#gesture.touch) || this.#suppressMenu) {
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    };

    /**
     * Ends the gesture. A lifted one told .NET it was lifted, so a cancel the person made is reported; a pending one never
     * told .NET anything.
     */
    #end(report) {
        const gesture = this.#gesture;

        if (!gesture) {
            return;
        }

        const lifted = gesture.lifted;

        this.#release(gesture);

        if (lifted && report) {
            this.#tell('Cancelled');
        }
    }

    #release(gesture) {
        clearTimeout(gesture.timer);
        cancelAnimationFrame(this.#frame);
        this.#frame = 0;
        this.#gesture = null;

        window.removeEventListener('pointermove', this.#onMove, true);
        window.removeEventListener('pointerup', this.#onRelease, true);
        window.removeEventListener('pointercancel', this.#onPointerCancel, true);
        window.removeEventListener('keydown', this.#onKey, true);
        window.removeEventListener('blur', this.#onBlur);
        document.removeEventListener('visibilitychange', this.#onVisibility);
        document.removeEventListener('selectstart', this.#onSelectStart, true);

        if (gesture.lifted) {
            this.#suppressClick = true;
            this.#suppressMenu = gesture.touch;

            try {
                if (gesture.card.hasPointerCapture(gesture.pointerId)) {
                    gesture.card.releasePointerCapture(gesture.pointerId);
                }
            } catch {
                // Already released.
            }

            gesture.card.style.removeProperty('--drag-x');
            gesture.card.style.removeProperty('--drag-y');
        }
    }

    #tell(method, ...args) {
        this.#board.invokeMethodAsync(method, ...args).catch(() => {
            // The Board has gone (disposed or purged): there is nobody left to tell.
        });
    }
}

/**
 * Attaches the drag to the Board's row of columns, reporting to <paramref name="board">, a DotNetObjectReference.
 * Called for a Role that may move Tasks only (UX-DR27): a Viewer's Board never loads this module's listeners.
 */
export function attach(root, board) {
    return new BoardDrag(root, board);
}
