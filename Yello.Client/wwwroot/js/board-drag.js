/*
    THE BOARD'S POINTER AND TOUCH DRAG - story 2.7. AC1-AC6, AC10, AC11, AC13, AC15; UX-DR22, UX-DR23, UX-DR6;
    EXPERIENCE.md:297-308; DESIGN.md:506-521.

    The repository's first JavaScript of its own, and the reason it exists: Blazor's event system cannot capture a pointer,
    cannot decide to preventDefault a touchmove halfway through a gesture, and would cross into .NET on every pointermove.
    So the gesture lives here, and .NET is told only four things - a lift, a change of destination slot, a drop and a
    cancel (AC15). Nothing reaches the network from this file: the drop is story 2.6's move, sent by the Board.

    IT NEVER CHANGES WHAT BLAZOR RENDERS. It writes custom properties on the dragged card and nothing else: --drag-x and
    --drag-y, the translation that keeps it under the pointer, and --drag-top, --drag-start and --drag-width, the box it
    had when it lifted. No node is inserted, removed or reordered, and no class is added. The lifted look and the drop zone
    are rendered by Blazor from the state this module reports, so the two never fight over the DOM.

    THE LIFTED CARD LEAVES ITS COLUMN. A column scrolls inside itself, so it clips whatever overflows it - a card that only
    translated would vanish at its own column's edge on every cross-column drag. The lifted card is therefore fixed to the
    view (components.css), placed at the box it lifted from and translated from there: it is drawn above every column, its
    slot in its own column closes, and the drop zone alone marks where it will land.

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
    excluded - direction-neutral, so RTL needs nothing here: story 2.11 (AC13, UX-DR42) proved it with a right-to-left drag
    to the inline-end column at 1024px. The slot is the number of cards whose middle is above the pointer; the pointer inside
    the drop zone keeps the slot it has, so the zone cannot chase itself. Outside every column there is no drop zone, and a
    release there moves nothing (AC6).

    BELOW 768PX (story 2.11, AC13, open question 6 (a); deferred-work.md:574) only the column the Status pager shows is a
    drop target: a hidden column - no box, the hidden attribute, or not the pager's selected column - is never hit-tested,
    previewed or dropped into, and the Board's row never auto-scrolls sideways. A cross-Status move at that width is the
    Move control's. The query is the one js/board-keys.js and components.css use.

    AUTO-SCROLL (AC5, story 2.7 Q13): while lifted, a requestAnimationFrame loop scrolls the hovered column near its block
    edges, the Board's row near the view's inline edges, and the page near the view's block edges - in proportion to the
    depth into an edge zone of twice --target-min. Direct manipulation, not animation, so reduced motion leaves it alone.

    LAYOUT IS READ ONLY WHEN SOMETHING MOVED (story 2.9, AC14; deferred-work.md:578). The rects hit-testing reads - the
    Board's, each column's, the drop zone's and the middle of every card - are measured once and cached until something
    moves them: a scroll anywhere, a resize, or Blazor adding or removing a node in the row of columns (a drop zone drawn, a
    page of cards appended). A frame in which the pointer has not moved, nothing scrolled and nothing changed reads no
    layout at all, so a lifted card over a column of 5,000 costs nothing while the pointer rests.
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
const SELECTED = 'data-pager-selected';
const NARROW = '(max-width: 47.9375em)';
const OPENER = '[data-task-id]';
const DRAG_PROPERTIES = ['--drag-x', '--drag-y', '--drag-top', '--drag-start', '--drag-width'];

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

/**
 * Whether a column can take a drop: it is drawn, and - below 768px - it is the column the Status pager shows.
 */
function isTarget(column, narrow) {
    if (column.hidden || (narrow && !column.hasAttribute(SELECTED))) {
        return false;
    }

    const rect = column.getBoundingClientRect();

    return rect.width > 0 && rect.height > 0;
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
    #clearing = 0;

    constructor(root, board) {
        this.#root = root;
        this.#board = board;

        root.addEventListener('pointerdown', this.#onPress);

        // Not passive: once a touch lift has committed, the finger's moves must not pan the page. Registered before any
        // touch begins, which is when the browser decides whether a touchmove can be prevented at all.
        root.addEventListener('touchmove', this.#onTouchMove, { passive: false });

        window.addEventListener('pointerdown', this.#onAnyPress, true);
        window.addEventListener('pointerup', this.#onAnyRelease, true);
        window.addEventListener('keydown', this.#onAnyKey, true);
        window.addEventListener('click', this.#onClick, true);
        window.addEventListener('contextmenu', this.#onContextMenu, true);
    }

    /**
     * Ends any drag, pending or lifted, silently - the Board has purged, read itself again, lost the dragged Task or opened
     * the panel, and already knows.
     */
    cancel() {
        if (this.#gesture) {
            this.#gesture.silenced = true;
        }

        this.#end(false);
    }

    /**
     * Ends any drag and lets go of every listener.
     */
    dispose() {
        this.#end(false);
        clearTimeout(this.#clearing);
        this.#root.removeEventListener('pointerdown', this.#onPress);
        this.#root.removeEventListener('touchmove', this.#onTouchMove, { passive: false });
        window.removeEventListener('pointerdown', this.#onAnyPress, true);
        window.removeEventListener('pointerup', this.#onAnyRelease, true);
        window.removeEventListener('keydown', this.#onAnyKey, true);
        window.removeEventListener('click', this.#onClick, true);
        window.removeEventListener('contextmenu', this.#onContextMenu, true);
    }

    #onAnyPress = event => {
        // A new press starts afresh: the click and context menu a finished drag would have raised are long gone.
        if (!this.#gesture) {
            this.#stopSuppressing();
            return;
        }

        // A second pointer - another finger, a pinch - ends the drag (AC13).
        if (event.pointerId !== this.#gesture.pointerId) {
            this.#end(true);
        }
    };

    /**
     * The release of any pointer. The click a release raises is dispatched before a timer can run, so a click the finished
     * drag owes is still swallowed - and nothing after it is.
     */
    #onAnyRelease = event => {
        if (event.pointerType !== 'touch') {
            this.#stopSuppressingSoon();
        }
    };

    /**
     * Any key: a keyboard or assistive-technology activation is never a drag's leftover click or menu (AC8, AC11).
     */
    #onAnyKey = () => {
        if (!this.#gesture) {
            this.#stopSuppressing();
        }
    };

    #stopSuppressing() {
        clearTimeout(this.#clearing);
        this.#suppressClick = false;
        this.#suppressMenu = false;
    }

    /**
     * Stops swallowing once the current mouse or pen input has been dispatched. A touch long-press raises its click and
     * its context menu after the release, as gestures of their own (Chromium does), so after a touch both stay swallowed
     * until the next press or key.
     */
    #stopSuppressingSoon() {
        clearTimeout(this.#clearing);
        this.#clearing = setTimeout(() => {
            if (!this.#gesture) {
                this.#suppressClick = false;
                this.#suppressMenu = false;
            }
        }, 0);
    }

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
            dirty: true,
            layout: null,
            watcher: null,
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
            gesture.dirty = true;
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

        const box = gesture.card.getBoundingClientRect();
        const centre = centreOf(box);
        const rightToLeft = getComputedStyle(gesture.card).direction === 'rtl';
        const style = gesture.card.style;

        gesture.grabX = gesture.startX - centre.x;
        gesture.grabY = gesture.startY - centre.y;

        // The box the fixed, lifted card is placed at, in logical terms: in a right-to-left Board its inline start is its
        // right edge's distance from the view's.
        style.setProperty('--drag-top', `${box.top}px`);
        style.setProperty('--drag-start', `${rightToLeft ? document.documentElement.clientWidth - box.right : box.left}px`);
        style.setProperty('--drag-width', `${box.width}px`);

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

        // A lift .NET accepts after the Board has already ended the gesture would leave a lifted card with nothing behind
        // it, so the Board is told the gesture was abandoned.
        this.#board.invokeMethodAsync('Lifted', gesture.taskId)
            .then(accepted => {
                if (accepted && gesture.silenced) {
                    this.#tell('Abandoned', gesture.taskId);
                }
            })
            .catch(() => {
                // The Board has gone: there is nobody left to tell.
            });

        // Anything that moves the layout under a still pointer - a scroll, a resize, a node Blazor adds or removes - makes the
        // cached rects stale (story 2.9, AC14).
        gesture.watcher = new MutationObserver(this.#invalidate);
        gesture.watcher.observe(this.#root, { childList: true, subtree: true });
        window.addEventListener('scroll', this.#invalidate, true);
        window.addEventListener('resize', this.#invalidate);

        this.#frame = requestAnimationFrame(this.#step);
    };

    /**
     * The layout the next frame reads is no longer the cached one.
     */
    #invalidate = () => {
        if (this.#gesture) {
            this.#gesture.layout = null;
            this.#gesture.dirty = true;
        }
    };

    #step = () => {
        const gesture = this.#gesture;

        if (!gesture || !gesture.lifted) {
            return;
        }

        // A still pointer over a still Board reads nothing (story 2.9, AC14): the target it had is the target it has.
        // An auto-scroll that moved anything raises a scroll event, which marks the next frame dirty, so it goes on while
        // it scrolls; one that hit the end of what can scroll raises none, and the frames go still.
        if (gesture.dirty) {
            gesture.dirty = false;
            this.#autoScroll(gesture);
            this.#follow(gesture);

            const target = this.#hitTest(gesture);

            if (!this.#same(target, gesture.target)) {
                gesture.target = target;
                this.#tell('Targeted', target ? target.statusId : null, target ? target.index : -1);
            }
        }

        this.#frame = requestAnimationFrame(this.#step);
    };

    /**
     * The rects hit-testing and auto-scroll read, measured once and kept until #invalidate: the Board's, and each column's
     * with its drop zone's and the middle of each of its cards other than the dragged one.
     */
    #layoutOf(gesture) {
        if (!gesture.layout) {
            const narrow = window.matchMedia(NARROW).matches;

            gesture.layout = {
                root: this.#root.getBoundingClientRect(),
                narrow,
                columns: [...this.#root.querySelectorAll(COLUMN)].filter(column => isTarget(column, narrow)).map(column => {
                    const zone = column.querySelector(DROP_ZONE);

                    return {
                        element: column,
                        statusId: column.getAttribute('data-status-id'),
                        rect: column.getBoundingClientRect(),
                        zone: zone ? zone.getBoundingClientRect() : null,
                        middles: [...column.querySelectorAll(CARD)]
                            .filter(card => card !== gesture.card)
                            .map(card => centreOf(card.getBoundingClientRect()).y),
                    };
                }),
            };
        }

        return gesture.layout;
    }

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

    /**
     * Scrolls whatever the pointer is near the edge of.
     */
    #autoScroll(gesture) {
        const { x, y, edge } = gesture;

        for (const column of this.#layoutOf(gesture).columns) {
            if (inside(column.rect, x, y)) {
                const step = edgeStep(y, column.rect.top, column.rect.bottom, edge);

                if (step !== 0) {
                    column.element.scrollBy({ top: step, behavior: 'instant' });
                }

                break;
            }
        }

        // Physical on both sides, and so consistent under RTL: clientX's edge step, then scrollBy's physical left, whose
        // negative scrollLeft Chromium uses in a right-to-left row (story 2.11, AC13). Never below 768px (open question 6).
        const sideways = this.#layoutOf(gesture).narrow ? 0 : edgeStep(x, 0, window.innerWidth, edge);

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
        const layout = this.#layoutOf(gesture);

        if (!inside(layout.root, x, y)) {
            return null;
        }

        for (const column of layout.columns) {
            if (!inside(column.rect, x, y)) {
                continue;
            }

            const statusId = column.statusId;

            if (column.zone && gesture.target && gesture.target.statusId === statusId && inside(column.zone, x, y)) {
                return gesture.target;
            }

            return { statusId, index: column.middles.filter(middle => middle < y).length };
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
        gesture.layout = null;

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

            // The platform took a mouse or pen pointer: no click follows.
            if (event.pointerType !== 'touch') {
                this.#stopSuppressingSoon();
            }
        }
    };

    #onKey = event => {
        const gesture = this.#gesture;

        if (event.key === 'Escape' && gesture) {
            // The innermost meaning wins: Esc ends the drag and nothing else - and a mouse press it ends before any lift
            // does not become a click on release.
            event.preventDefault();
            event.stopPropagation();

            if (!gesture.lifted && !gesture.touch) {
                this.#suppressClick = true;
            }

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
        // the drag's - never 2.6's card menu, never the browser's (story 2.7 Q11 (a)). A mouse right-click is untouched,
        // except in the middle of a mouse drag, where it would open a menu on a card that is still travelling.
        if ((this.#gesture && (this.#gesture.touch || this.#gesture.lifted)) || this.#suppressMenu) {
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
        window.removeEventListener('scroll', this.#invalidate, true);
        window.removeEventListener('resize', this.#invalidate);
        document.removeEventListener('visibilitychange', this.#onVisibility);
        document.removeEventListener('selectstart', this.#onSelectStart, true);

        if (gesture.watcher) {
            gesture.watcher.disconnect();
        }

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

            for (const property of DRAG_PROPERTIES) {
                gesture.card.style.removeProperty(property);
            }
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
