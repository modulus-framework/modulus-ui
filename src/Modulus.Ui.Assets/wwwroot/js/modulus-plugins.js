/*
 * Modulus plugins runtime — shared by Blazor, MVC and Razor Pages.
 *
 * Like modulus-shell.js it works from the markup alone: event delegation on document, plus a MutationObserver
 * that starts the widgets which need a library (maps, editors, spreadsheet, documents) when their element appears, so Blazor re-renders,
 * enhanced navigation and htmx swaps all work without calls from the page. Libraries load on first use from
 * /_content/Modulus.Ui.Assets/lib (served locally, no CDN).
 *
 *   [data-m-sortable]      drag to reorder (pointer, touch and keyboard), between lists of the same data-m-sort-group.
 *                          The result goes to the list's [data-m-sort-result] input as JSON { id, from, to, index },
 *                          followed by a bubbling change event (Blazor @onchange, htmx hx-trigger="change").
 *                          data-m-sort-mode="event" (Blazor) leaves the DOM alone; otherwise the item moves and its
 *                          [data-m-sort-value] inputs take the new list's name, so a form posts the order.
 *   [data-m-player]        custom controls for a <video> or <audio>: play, seek, time, mute, volume, speed,
 *                          captions (data-captions JSON cues), fullscreen and keyboard keys.
 *   [data-m-editor]        WYSIWYG editor on Tiptap; the HTML goes to [data-m-editor-value] with a change event.
 *   [data-m-map]           MapLibre GL tile map with markers (data-markers JSON) and popups; [data-m-map-go] buttons
 *                          fly to a marker.
 *   [data-m-vector-map]    jsVectorMap choropleth: region fills from data-fills, values in the tooltip.
 *   [data-m-sheet]         spreadsheet on Univer (ribbon, formula bar, filter, sort, conditional formatting, validation,
 *                          find and replace, links, notes, tables; a bare grid with sheet tabs with data-readonly); the
 *                          sheets JSON in data-sheets, edits back to [data-m-sheet-value] with a change event.
 *   [data-m-doc]           document editor on Univer Docs (Word-style ribbon, A4 pages; pages only with data-readonly);
 *                          the document JSON in data-doc, edits back to [data-m-doc-value] with a change event.
 *   [data-m-print]         prints the spreadsheet or document whose id it names (the editors' ribbons have a Print
 *                          button and take Ctrl+P too): the content as HTML in a hidden frame, not the canvas.
 *   [data-m-file-submit]   a file input that submits its form as soon as a file is picked.
 *   [data-m-copy]          copies its value to the clipboard on click.
 *   [data-m-uploader]      drop area on Uppy: files upload as they are dropped (one POST each, progress and limits
 *                          from data-accept, data-max-size and data-max-count); the result line says how many went up.
 *   [data-m-cropper]       image cropper on Cropper.js: aspect ratio buttons; the crop (natural pixels) goes to
 *                          [data-m-cropper-value] as JSON with a change event, and a preview of the crop is drawn.
 *   [data-m-code]          code editor on CodeMirror 6 (data-language, data-readonly); the text goes to
 *                          [data-m-code-value] with a debounced change event.
 */
(function () {
    'use strict';

    var LIB = '/_content/Modulus.Ui.Assets/lib/';
    var loading = {};

    function loadCss(href) {
        if (document.querySelector('link[href="' + href + '"]')) return;
        var link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        // Before the Modulus stylesheets, so their overrides win.
        var first = document.querySelector('link[href*="Modulus.Ui.Assets/css/"]');
        document.head.insertBefore(link, first || null);
    }

    function loadScript(src) {
        if (loading[src]) return loading[src];
        loading[src] = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = src;
            s.onload = function () { resolve(); };
            s.onerror = function () { delete loading[src]; reject(new Error('Could not load ' + src)); };
            document.head.appendChild(s);
        });
        return loading[src];
    }

    function libs(css, scripts) {
        css.forEach(loadCss);
        return scripts.reduce(function (p, src) { return p.then(function () { return loadScript(src); }); }, Promise.resolve());
    }

    function json(el, name, fallback) {
        try { var v = JSON.parse(el.getAttribute(name) || ''); return v == null ? fallback : v; } catch (e) { return fallback; }
    }

    function cssVar(el, name, fallback) {
        var v = getComputedStyle(el).getPropertyValue(name).trim();
        return v || fallback;
    }

    function raiseChange(input, value) {
        if (!input) return;
        input.value = value;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // ---- Widget registry -----------------------------------------------------------------------------------

    // One row per plugin widget: scan() inits the roots that appeared (roots mark themselves [data-m-ready])
    // and stops the ones whose element left the page (Blazor navigation, htmx swap). A widget that builds
    // something with listeners (an editor, a map) calls keepAlive(root, destroy) once it holds it.
    var registry = [];
    var liveRoots = [];

    function widget(selector, init) {
        registry.push({ selector: selector, init: init });
    }

    function keepAlive(root, destroy) {
        root._mDestroy = destroy;
        if (liveRoots.indexOf(root) < 0) liveRoots.push(root);
    }

    // ---- Sortable ------------------------------------------------------------------------------------------

    var drag = null;
    var DRAG_START = 4;

    // Table rows (the grids and the data table): the element around the table carries data-m-sort-rows and the
    // other options; a row is an item when one of its cells holds a [data-m-sort-row] handle. A detail row (an
    // expanded row, or a nested grid) is not an item: it travels with the row above it.
    function rowHandle(tr) {
        if (tr.tagName !== 'TR') return null;
        for (var i = 0; i < tr.children.length; i++) {
            var h = tr.children[i].querySelector('[data-m-sort-row]');
            if (h) return h.closest('tr') === tr ? h : null; // the first handle of a detail row is a nested table's
        }
        return null;
    }

    function isItem(c) { return c.hasAttribute('data-m-sort-id') || !!rowHandle(c); }

    function itemId(c) {
        if (c.hasAttribute('data-m-sort-id')) return c.getAttribute('data-m-sort-id');
        var h = rowHandle(c);
        return h ? h.getAttribute('data-m-sort-row') : '';
    }

    function isRows(list) { return !list.hasAttribute('data-m-sortable'); }

    // The element holding a list's options: the list itself, or (rows) the element around the table.
    function conf(list) { return isRows(list) ? list.closest('[data-m-sort-rows]') || list : list; }

    function shown(c) { return c.getClientRects().length > 0; }

    function sortItems(list) {
        return Array.prototype.filter.call(list.children, function (c) {
            return isItem(c) && !c.classList.contains('m-sort-source');
        });
    }

    // The expanded rows right after a row, which move with it.
    function detailRows(item) {
        var list = [];
        if (item.tagName !== 'TR') return list;
        for (var n = item.nextElementSibling; n && !isItem(n) && !n.classList.contains('m-sort-placeholder') &&
            (n.classList.contains('ant-table-expanded-row') || n.hasAttribute('data-detail-for')); n = n.nextElementSibling) list.push(n);
        return list;
    }

    function sortRoot(list) { return list.closest('[data-m-sort-root]') || list.parentElement; }

    // A root's own element (not one of a nested table's, further down).
    function own(root, selector) {
        var all = root.querySelectorAll(selector);
        for (var i = 0; i < all.length; i++) if (all[i].closest('[data-m-sort-root]') === root) return all[i];
        return null;
    }

    function sortName(list) { return list.getAttribute('data-m-sortable') || conf(list).getAttribute('data-m-sort-rows') || ''; }

    function sameGroup(a, b) {
        if (a === b) return true;
        var g = a.getAttribute('data-m-sort-group');
        return !!g && g === b.getAttribute('data-m-sort-group');
    }

    function isGrid(list) { return list.getAttribute('data-m-sort-layout') === 'grid'; }

    function announce(list, text) {
        var live = own(sortRoot(list), '[data-m-sort-live]');
        if (live) live.textContent = text;
    }

    function itemLabel(item) {
        var h = rowHandle(item);
        return (h && h.getAttribute('data-m-sort-label')) || item.getAttribute('data-m-sort-label') || item.textContent.trim().replace(/\s+/g, ' ').slice(0, 60);
    }

    // Moves `item` to position `index` of `target` (or reports it, in event mode), then raises the result.
    function commitSort(item, from, target, index, focusAfter) {
        var id = itemId(item);
        var oldIndex = sortItems(from).indexOf(item);
        if (from === target && index === oldIndex) return false;
        var eventMode = (conf(target).getAttribute('data-m-sort-mode') || conf(from).getAttribute('data-m-sort-mode')) === 'event';
        if (!eventMode) {
            var details = detailRows(item);
            var rest = sortItems(target).filter(function (c) { return c !== item; });
            target.insertBefore(item, rest[index] || null);
            var after = item;
            details.forEach(function (d) { target.insertBefore(d, after.nextSibling); after = d; });
            var name = target.getAttribute('data-m-sort-input') || '';
            if (name) item.querySelectorAll('[data-m-sort-value]').forEach(function (i) { i.name = name; });
            if (!isRows(from)) syncEmpty(from);
            if (!isRows(target)) syncEmpty(target);
        }
        var detail = { id: id, from: sortName(from), to: sortName(target), index: index };
        var count = sortItems(target).length + (eventMode && from !== target ? 1 : 0);
        var title = conf(target).getAttribute('data-m-sort-title');
        announce(target, itemLabel(item) + ' moved to position ' + (index + 1) + ' of ' + count + (title ? ' in ' + title : '') + '.');
        target.dispatchEvent(new CustomEvent('modulus:sort', { bubbles: true, detail: detail }));
        raiseChange(own(sortRoot(target), '[data-m-sort-result]'), JSON.stringify(detail));
        if (focusAfter) refocus(id);
        return true;
    }

    // Blazor re-renders the lists after the change event and moving a node drops its focus, so the handle is
    // focused again once it is back in the document.
    function refocus(id) {
        var tries = 0;
        (function again() {
            var handle = document.querySelector('[data-m-sort-id="' + CSS.escape(id) + '"] [data-m-sort-handle], [data-m-sort-id="' + CSS.escape(id) + '"][tabindex], [data-m-sort-row="' + CSS.escape(id) + '"]');
            if (handle && document.activeElement !== handle) handle.focus({ preventScroll: false });
            if (++tries < 12) requestAnimationFrame(again);
        })();
    }

    function syncEmpty(list) {
        list.classList.toggle('m-sortable-empty', sortItems(list).length === 0);
    }

    function dropSpot(list, x, y) {
        var items = sortItems(list);
        var grid = isGrid(list), horizontal = list.getAttribute('data-m-sort-layout') === 'horizontal';
        var best = null, bestDist = Infinity;
        items.forEach(function (c, i) {
            if (!shown(c)) return; // rows on another page, or filtered out
            var r = c.getBoundingClientRect();
            var cx = r.left + r.width / 2, cy = r.top + r.height / 2;
            var d = Math.hypot(cx - x, cy - y);
            if (d < bestDist) { bestDist = d; best = { i: i, r: r, cx: cx, cy: cy }; }
        });
        if (!best) return 0;
        var before;
        if (horizontal) before = x < best.cx;
        else if (grid) before = y >= best.r.top && y <= best.r.bottom ? x < best.cx : y < best.cy;
        else before = y < best.cy;
        return before ? best.i : best.i + 1;
    }

    function placePlaceholder(list, index) {
        var items = sortItems(list);
        var ref = items[index] || null;
        if (ref === drag.placeholder) return;
        if (drag.placeholder.parentNode === list && (ref ? ref.previousElementSibling === drag.placeholder : list.lastElementChild === drag.placeholder)) return;
        list.insertBefore(drag.placeholder, ref);
    }

    function startDrag(e) {
        var d = drag;
        var r = d.item.getBoundingClientRect();
        d.offsetX = d.x0 - r.left;
        d.offsetY = d.y0 - r.top;
        if (d.rows) {
            // A row can't float on its own: the ghost is a one-row table in the same Ant Design classes, cells as wide as the originals.
            var table = d.item.closest('.ant-table');
            var clone = d.item.cloneNode(true);
            Array.prototype.forEach.call(d.item.children, function (td, i) {
                var c = clone.children[i];
                if (c) { c.style.width = td.getBoundingClientRect().width + 'px'; c.style.position = 'static'; }
            });
            d.ghost = document.createElement('div');
            d.ghost.className = (table ? table.className : 'ant-table') + ' m-sort-ghost m-sort-ghost-row';
            d.ghost.setAttribute('x-ignore', ''); // Alpine leaves the copied widgets alone
            d.ghost.innerHTML = '<table style="table-layout: fixed; width: 100%;"><tbody class="ant-table-tbody"></tbody></table>';
            d.ghost.querySelector('tbody').appendChild(clone);
        } else {
            d.ghost = d.item.cloneNode(true);
            d.ghost.classList.add('m-sort-ghost');
            d.ghost.removeAttribute('data-m-sort-id');
        }
        d.ghost.querySelectorAll('[id]').forEach(function (n) { n.removeAttribute('id'); });
        d.ghost.querySelectorAll('[name]').forEach(function (n) { n.removeAttribute('name'); });
        d.ghost.setAttribute('aria-hidden', 'true');
        d.ghost.style.width = r.width + 'px';
        d.ghost.style.height = r.height + 'px';
        document.body.appendChild(d.ghost);
        if (d.rows) {
            d.placeholder = document.createElement('tr');
            d.placeholder.className = 'm-sort-placeholder m-sort-placeholder-row';
            var cell = document.createElement('td');
            cell.colSpan = d.item.children.length;
            cell.style.height = r.height + 'px';
            d.placeholder.appendChild(cell);
        } else {
            d.placeholder = document.createElement(d.item.tagName);
            d.placeholder.className = 'm-sort-placeholder';
            d.placeholder.style.height = r.height + 'px';
            if (isGrid(d.from) || d.from.getAttribute('data-m-sort-layout') === 'horizontal') d.placeholder.style.width = r.width + 'px';
        }
        d.placeholder.setAttribute('aria-hidden', 'true');
        d.item.parentNode.insertBefore(d.placeholder, d.item);
        d.details = detailRows(d.item);
        d.details.forEach(function (n) { n.classList.add('m-sort-source'); });
        d.item.classList.add('m-sort-source');
        document.documentElement.classList.add('m-sorting');
        d.started = true;
        moveDrag(e);
    }

    function moveDrag(e) {
        var d = drag;
        d.ghost.style.transform = 'translate(' + (e.clientX - d.offsetX) + 'px,' + (e.clientY - d.offsetY) + 'px)';
        if (d.rows) {
            // Rows stay in their own table: the pointer's height picks the spot, wherever it is across.
            placePlaceholder(d.from, dropSpot(d.from, e.clientX, e.clientY));
        }
        var under = d.rows ? null : document.elementFromPoint(e.clientX, e.clientY);
        var list = under && under.closest('[data-m-sortable]');
        if (list && sameGroup(d.from, list)) {
            if (d.over && d.over !== list) d.over.classList.remove('m-sortable-over');
            d.over = list;
            list.classList.add('m-sortable-over');
            placePlaceholder(list, dropSpot(list, e.clientX, e.clientY));
        }
        // Scroll the page near its top and bottom edges.
        var edge = 48, vh = window.innerHeight;
        if (e.clientY < edge) window.scrollBy(0, -12);
        else if (e.clientY > vh - edge) window.scrollBy(0, 12);
    }

    function endDrag(cancel) {
        var d = drag;
        drag = null;
        window.removeEventListener('pointermove', onPointerMove);
        window.removeEventListener('pointerup', onPointerUp);
        window.removeEventListener('pointercancel', onPointerCancel);
        document.removeEventListener('keydown', onDragKey, true);
        if (!d || !d.started) return;
        document.documentElement.classList.remove('m-sorting');
        if (d.over) d.over.classList.remove('m-sortable-over');
        var target = d.placeholder.parentNode;
        var index = target ? Array.prototype.filter.call(target.children, function (c) {
            return c === d.placeholder || (c !== d.item && isItem(c));
        }).indexOf(d.placeholder) : -1;
        d.placeholder.remove();
        d.ghost.remove();
        d.item.classList.remove('m-sort-source');
        (d.details || []).forEach(function (n) { n.classList.remove('m-sort-source'); });
        // A click fires after pointerup on the same element; swallow it so links and buttons in the item stay put.
        var swallow = function (ev) { ev.stopPropagation(); ev.preventDefault(); };
        window.addEventListener('click', swallow, { capture: true, once: true });
        setTimeout(function () { window.removeEventListener('click', swallow, true); }, 0);
        if (!cancel && target && index >= 0) commitSort(d.item, d.from, target, index, false);
    }

    function onPointerMove(e) {
        if (!drag) return;
        if (!drag.started) {
            if (Math.hypot(e.clientX - drag.x0, e.clientY - drag.y0) < DRAG_START) return;
            startDrag(e);
            return;
        }
        e.preventDefault();
        moveDrag(e);
    }
    function onPointerUp() { endDrag(false); }
    function onPointerCancel() { endDrag(true); }
    function onDragKey(e) {
        if (e.key === 'Escape' && drag) { e.preventDefault(); e.stopPropagation(); endDrag(true); }
    }

    document.addEventListener('pointerdown', function (e) {
        if (e.button !== 0 || drag || !(e.target instanceof Element)) return;
        var rowHandleEl = e.target.closest('[data-m-sort-row]');
        if (rowHandleEl) {
            // A table row, dragged by its handle only (its cells are editors, links and checkboxes).
            var row = rowHandleEl.closest('tr'), body = row && row.parentElement;
            if (!body || !rowsEnabled(body, rowHandleEl)) return;
            drag = { item: row, from: body, x0: e.clientX, y0: e.clientY, started: false, rows: true };
            e.preventDefault();
            window.addEventListener('pointermove', onPointerMove, { passive: false });
            window.addEventListener('pointerup', onPointerUp);
            window.addEventListener('pointercancel', onPointerCancel);
            document.addEventListener('keydown', onDragKey, true);
            return;
        }
        var item = e.target.closest('[data-m-sort-id]');
        var list = item && item.parentElement;
        if (!list || !list.hasAttribute('data-m-sortable') || list.hasAttribute('data-m-sort-disabled')) return;
        var handleOnly = list.hasAttribute('data-m-sort-handle-only');
        var handle = e.target.closest('[data-m-sort-handle]');
        if (handleOnly && !handle) return;
        if (!handle && e.target.closest('input, textarea, select, button, a, [contenteditable="true"], label')) return;
        drag = { item: item, from: list, x0: e.clientX, y0: e.clientY, started: false };
        if (handle) e.preventDefault(); // no text selection or native drag from the handle
        window.addEventListener('pointermove', onPointerMove, { passive: false });
        window.addEventListener('pointerup', onPointerUp);
        window.addEventListener('pointercancel', onPointerCancel);
        document.addEventListener('keydown', onDragKey, true);
    });

    // A row moves when the element around its table allows it (not while the rows are sorted by a column, say).
    function rowsEnabled(body, handle) {
        var owner = body.closest('[data-m-sort-rows]');
        return !!owner && !owner.hasAttribute('data-m-sort-disabled') && handle.getAttribute('aria-disabled') !== 'true';
    }

    // Touch: stop the page from scrolling while an item is dragged by its handle.
    document.addEventListener('touchmove', function (e) { if (drag && drag.started) e.preventDefault(); }, { passive: false });

    // Keyboard: arrow keys on a handle move the item; Left / Right in a vertical list move it to the neighbouring list.
    document.addEventListener('keydown', function (e) {
        if (!(e.target instanceof Element) || e.altKey || e.ctrlKey || e.metaKey) return;
        var handle = e.target.closest('[data-m-sort-handle]');
        if (handle && handle.hasAttribute('data-m-sort-row')) {
            // A row: Up and Down step over the rows shown (other pages and filtered rows stay put), Home and End.
            var row = handle.closest('tr'), body = row && row.parentElement;
            var rowStep = { ArrowUp: -1, ArrowDown: 1, Home: -Infinity, End: Infinity }[e.key];
            if (!body || rowStep === undefined || !rowsEnabled(body, handle)) return;
            e.preventDefault();
            e.stopPropagation();
            var all = sortItems(body), seen = all.filter(function (c) { return c === row || shown(c); });
            var at = seen.indexOf(row);
            var next = Math.max(0, Math.min(seen.length - 1, rowStep === -Infinity ? 0 : rowStep === Infinity ? seen.length - 1 : at + rowStep));
            if (next !== at) commitSort(row, body, body, all.indexOf(seen[next]), true);
            return;
        }
        var item = handle && handle.closest('[data-m-sort-id]');
        var list = item && item.parentElement;
        if (!list || !list.hasAttribute('data-m-sortable')) return;
        var items = sortItems(list), i = items.indexOf(item);
        var flat = isGrid(list) || list.getAttribute('data-m-sort-layout') === 'horizontal';
        var step = { ArrowUp: -1, ArrowDown: 1, Home: -Infinity, End: Infinity }[e.key];
        if (flat && e.key === 'ArrowLeft') step = -1;
        if (flat && e.key === 'ArrowRight') step = 1;
        if (step !== undefined) {
            e.preventDefault();
            var to = Math.max(0, Math.min(items.length - 1, step === -Infinity ? 0 : step === Infinity ? items.length - 1 : i + step));
            if (to !== i) commitSort(item, list, list, to, true);
            return;
        }
        if (!flat && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
            var group = list.getAttribute('data-m-sort-group');
            if (!group) return;
            var lists = Array.prototype.slice.call(document.querySelectorAll('[data-m-sortable][data-m-sort-group="' + CSS.escape(group) + '"]'));
            var next = lists[lists.indexOf(list) + (e.key === 'ArrowLeft' ? -1 : 1)];
            if (!next) return;
            e.preventDefault();
            commitSort(item, list, next, Math.min(i, sortItems(next).length), true);
        }
    });

    // ---- Media player --------------------------------------------------------------------------------------

    function playerOf(el) { return el instanceof Element ? el.closest('[data-m-player]') : null; }
    function mediaOf(p) { return p.querySelector('video, audio'); }

    function clock(t) {
        if (!isFinite(t) || t < 0) t = 0;
        var s = Math.floor(t % 60), m = Math.floor(t / 60) % 60, h = Math.floor(t / 3600);
        return (h ? h + ':' + (m < 10 ? '0' : '') : '') + m + ':' + (s < 10 ? '0' : '') + s;
    }

    function syncPlayer(p) {
        var m = mediaOf(p);
        if (!m) return;
        var playing = !m.paused && !m.ended;
        p.classList.toggle('m-media-playing', playing);
        p.classList.toggle('m-media-started', m.currentTime > 0 || playing);
        p.classList.toggle('m-media-muted', m.muted || m.volume === 0);
        p.querySelectorAll('[data-m-player-cmd="play"]').forEach(function (b) { b.setAttribute('aria-label', playing ? 'Pause' : 'Play'); b.title = playing ? 'Pause (k)' : 'Play (k)'; });
        var mute = p.querySelector('[data-m-player-cmd="mute"]');
        if (mute) { mute.setAttribute('aria-label', m.muted ? 'Unmute' : 'Mute'); mute.title = (m.muted ? 'Unmute' : 'Mute') + ' (m)'; }
        var time = p.querySelector('[data-m-player-time]');
        if (time) time.textContent = clock(m.currentTime);
        var dur = p.querySelector('[data-m-player-duration]');
        if (dur) dur.textContent = clock(m.duration);
        var seek = p.querySelector('[data-m-player-seek]');
        if (seek && !seek.matches(':active')) {
            seek.value = m.duration ? Math.round(m.currentTime / m.duration * 1000) : 0;
            seek.setAttribute('aria-valuetext', clock(m.currentTime) + ' of ' + clock(m.duration));
        }
        if (seek) seek.style.setProperty('--m-progress', (seek.value / 10) + '%');
        var buffered = p.querySelector('[data-m-player-buffered]');
        if (buffered && m.duration && m.buffered.length) buffered.style.width = (m.buffered.end(m.buffered.length - 1) / m.duration * 100) + '%';
        var vol = p.querySelector('[data-m-player-volume]');
        if (vol && !vol.matches(':active')) vol.value = m.muted ? 0 : m.volume;
        if (vol) vol.style.setProperty('--m-progress', (vol.value * 100) + '%');
        var speed = p.querySelector('[data-m-player-cmd="speed"]');
        if (speed) speed.textContent = m.playbackRate + '×';
        syncCaption(p, m);
    }

    function syncCaption(p, m) {
        var box = p.querySelector('[data-m-player-caption]');
        if (!box) return;
        var on = p.classList.contains('m-media-captions-on');
        var cues = p._mCues || (p._mCues = json(p, 'data-captions', []));
        var t = m.currentTime, text = '';
        if (on) for (var i = 0; i < cues.length; i++) if (t >= cues[i].start && t < cues[i].end) { text = cues[i].text; break; }
        if (box.textContent !== text) box.textContent = text;
        box.hidden = !text;
    }

    ['play', 'pause', 'ended', 'timeupdate', 'durationchange', 'loadedmetadata', 'volumechange', 'ratechange', 'progress', 'seeked'].forEach(function (type) {
        document.addEventListener(type, function (e) { var p = playerOf(e.target); if (p) syncPlayer(p); }, true);
    });
    document.addEventListener('waiting', function (e) { var p = playerOf(e.target); if (p) p.classList.add('m-media-waiting'); }, true);
    ['playing', 'canplay', 'pause'].forEach(function (type) {
        document.addEventListener(type, function (e) { var p = playerOf(e.target); if (p) p.classList.remove('m-media-waiting'); }, true);
    });
    document.addEventListener('error', function (e) {
        var p = playerOf(e.target);
        if (p && (e.target.tagName === 'VIDEO' || e.target.tagName === 'AUDIO' || e.target.tagName === 'SOURCE')) {
            p.classList.add('m-media-error');
            p.classList.remove('m-media-waiting');
        }
    }, true);

    var SPEEDS = [0.5, 0.75, 1, 1.25, 1.5, 2];

    function playerCommand(p, cmd) {
        var m = mediaOf(p);
        if (!m) return;
        if (cmd === 'play') {
            if (m.paused || m.ended) {
                // One player at a time on the page.
                document.querySelectorAll('[data-m-player] video, [data-m-player] audio').forEach(function (o) { if (o !== m && !o.paused) o.pause(); });
                var r = m.play();
                if (r && r.catch) r.catch(function () { /* blocked or no source */ });
            } else m.pause();
        } else if (cmd === 'mute') {
            m.muted = !m.muted;
            if (!m.muted && m.volume === 0) m.volume = 0.5;
        } else if (cmd === 'speed') {
            m.playbackRate = SPEEDS[(SPEEDS.indexOf(m.playbackRate) + 1) % SPEEDS.length];
        } else if (cmd === 'captions') {
            var on = p.classList.toggle('m-media-captions-on');
            var b = p.querySelector('[data-m-player-cmd="captions"]');
            if (b) b.setAttribute('aria-pressed', on ? 'true' : 'false');
        } else if (cmd === 'fullscreen') {
            if (document.fullscreenElement) document.exitFullscreen();
            else if (p.requestFullscreen) p.requestFullscreen().catch(function () { });
            else if (m.webkitEnterFullscreen) m.webkitEnterFullscreen();
        } else if (cmd === 'back') m.currentTime = Math.max(0, m.currentTime - 10);
        else if (cmd === 'forward') m.currentTime = Math.min(m.duration || 0, m.currentTime + 10);
        syncPlayer(p);
    }

    document.addEventListener('click', function (e) {
        var btn = e.target instanceof Element && e.target.closest('[data-m-player-cmd]');
        var p = btn && playerOf(btn);
        if (p) { e.preventDefault(); playerCommand(p, btn.getAttribute('data-m-player-cmd')); return; }
        // A click on the picture plays or pauses, as in the browser's own player.
        if (e.target instanceof HTMLVideoElement && playerOf(e.target)) playerCommand(playerOf(e.target), 'play');
    });
    document.addEventListener('dblclick', function (e) {
        if (e.target instanceof HTMLVideoElement && playerOf(e.target)) playerCommand(playerOf(e.target), 'fullscreen');
    });
    document.addEventListener('input', function (e) {
        var p = playerOf(e.target);
        if (!p) return;
        var m = mediaOf(p);
        if (e.target.hasAttribute('data-m-player-seek') && m.duration) {
            m.currentTime = e.target.value / 1000 * m.duration;
            e.target.style.setProperty('--m-progress', (e.target.value / 10) + '%');
        } else if (e.target.hasAttribute('data-m-player-volume')) {
            m.volume = Number(e.target.value);
            m.muted = m.volume === 0;
        }
    });
    document.addEventListener('fullscreenchange', function () {
        document.querySelectorAll('[data-m-player]').forEach(function (p) {
            p.classList.toggle('m-media-fullscreen', document.fullscreenElement === p);
        });
    });
    // k / Space play, j / l or arrows seek, m mutes, c captions, f fullscreen: while focus is inside the player.
    document.addEventListener('keydown', function (e) {
        var p = playerOf(e.target);
        if (!p || e.ctrlKey || e.metaKey || e.altKey) return;
        var onRange = e.target instanceof HTMLInputElement && e.target.type === 'range';
        var m = mediaOf(p);
        var key = e.key.toLowerCase();
        var cmd = { k: 'play', m: 'mute', c: 'captions', f: 'fullscreen', j: 'back', l: 'forward' }[key];
        if (key === ' ' && !(e.target instanceof HTMLButtonElement)) cmd = 'play';
        if (!onRange && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) { e.preventDefault(); m.currentTime = Math.max(0, m.currentTime + (e.key === 'ArrowLeft' ? -5 : 5)); return; }
        if (cmd) { e.preventDefault(); playerCommand(p, cmd); }
    });

    // ---- Editor (Tiptap) ----------------------------------------------------------------------------------

    // One pre-bundled IIFE (see lib/tiptap/LICENSE.md): Editor, StarterKit (formatting, lists, link,
    // undo/redo), Image from a URL and Placeholder. No CSS of its own; modulus.css styles .tiptap.
    var TIPTAP = [LIB + 'tiptap/tiptap.js'];

    function editorHtml(ed) {
        return ed.isEmpty() ? '' : ed.getHTML();
    }

    function initEditor(root) {
        var host = root.querySelector('.m-editor-host');
        if (!host) return;
        root.setAttribute('data-m-ready', '');
        libs([], TIPTAP).then(function () {
            if (!document.contains(root) || root._mTip) return;
            var t = window.ModulusTiptap;
            var attrs = { role: 'textbox', 'aria-multiline': 'true', class: 'tiptap' };
            var label = root.getAttribute('data-label');
            if (label) attrs['aria-label'] = label;
            var labelledBy = root.getAttribute('data-labelledby');
            if (labelledBy) attrs['aria-labelledby'] = labelledBy;
            var ed = new t.Editor({
                element: host,
                content: host.innerHTML,   // the starting HTML, rendered by the server
                editable: !root.hasAttribute('data-readonly'),
                extensions: [t.StarterKit, t.Image, t.Placeholder.configure({ placeholder: root.getAttribute('data-placeholder') || '' })],
                editorProps: { attributes: attrs }
            });
            root._mTip = ed;
            var value = root.querySelector('[data-m-editor-value]');
            var timer = 0;
            ed.on('update', function () {
                clearTimeout(timer);
                // Blazor gets one update per pause in typing, not one per key.
                timer = setTimeout(function () { raiseChange(value, editorHtml(ed)); }, root.hasAttribute('data-m-debounce') ? 300 : 0);
                syncEditor(root, ed);
            });
            ed.on('selectionUpdate', function () { syncEditor(root, ed); });
            ed.on('focus', function () { root.classList.add('m-editor-focused'); });
            ed.on('blur', function () {
                root.classList.remove('m-editor-focused');
                // Leaving the editor (for a Save button, say) sends the text at once, ahead of the click.
                clearTimeout(timer);
                if (value && value.value !== editorHtml(ed)) raiseChange(value, editorHtml(ed));
            });
            root.classList.add('m-editor-ready');
            keepAlive(root, function () { root._mTip.destroy(); });
            syncEditor(root, ed);
        }).catch(function () { root.classList.add('m-editor-failed'); });
    }

    // A form posts what the editors hold now, even inside the debounce pause.
    document.addEventListener('submit', function (e) {
        if (!(e.target instanceof HTMLFormElement)) return;
        e.target.querySelectorAll('[data-m-editor]').forEach(function (root) {
            var ed = root._mTip, value = root.querySelector('[data-m-editor-value]');
            if (ed && value) value.value = editorHtml(ed);
        });
    }, true);

    function syncEditor(root, ed) {
        var marks = {
            bold: ed.isActive('bold'), italic: ed.isActive('italic'), underline: ed.isActive('underline'), strike: ed.isActive('strike'),
            blockquote: ed.isActive('blockquote'), 'code-block': ed.isActive('codeBlock'), link: ed.isActive('link')
        };
        root.querySelectorAll('[data-m-ed]').forEach(function (b) {
            var name = b.getAttribute('data-m-ed'), arg = b.getAttribute('data-arg'), on;
            if (name === 'list') on = arg === 'ordered' ? ed.isActive('orderedList') : ed.isActive('bulletList');
            else on = !!marks[name];
            b.classList.toggle('m-editor-btn-active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        var heading = root.querySelector('[data-m-ed-heading]');
        if (heading) {
            var level = [1, 2, 3].find(function (n) { return ed.isActive('heading', { level: n }); });
            heading.value = level ? String(level) : '';
        }
        var count = root.querySelector('[data-m-editor-count]');
        if (count) {
            var words = ed.getText().trim().split(/\s+/).filter(Boolean).length;
            count.textContent = words + (words === 1 ? ' word' : ' words');
        }
        var undo = root.querySelector('[data-m-ed="undo"]'), redo = root.querySelector('[data-m-ed="redo"]');
        if (undo) undo.disabled = !ed.can().undo();
        if (redo) redo.disabled = !ed.can().redo();
    }

    function editorOf(el) { var root = el instanceof Element && el.closest('[data-m-editor]'); return root && root._mTip ? root : null; }

    function openBar(root, mode) {
        var ed = root._mTip;
        var bar = root.querySelector('[data-m-editor-bar]');
        if (!bar) return;
        bar.setAttribute('data-mode', mode);
        var input = bar.querySelector('input');
        var current = mode === 'link' ? (ed.getAttributes('link').href || '') : '';
        input.value = current || '';
        input.placeholder = mode === 'link' ? 'https://example.com' : 'https://example.com/image.png';
        input.setAttribute('aria-label', mode === 'link' ? 'Link address' : 'Image address');
        bar.querySelector('[data-m-editor-bar-remove]').hidden = mode !== 'link' || !current;
        bar.querySelector('[data-m-editor-bar-title]').textContent = mode === 'link' ? 'Link' : 'Image';
        bar.hidden = false;
        input.focus();
        input.select();
    }

    function closeBar(root, focus) {
        var bar = root.querySelector('[data-m-editor-bar]');
        if (!bar || bar.hidden) return;
        bar.hidden = true;
        if (focus) root._mTip.commands.focus();
    }

    function safeUrl(url, image) {
        url = (url || '').trim();
        if (!url) return '';
        if (/^(https?:)?\/\//i.test(url) || /^\/(?!\/)/.test(url)) return url;
        if (!image && /^(mailto|tel):/i.test(url)) return url;
        if (/^[a-z][a-z0-9+.-]*:/i.test(url)) return ''; // javascript:, data: …
        return 'https://' + url;
    }

    function applyBar(root) {
        var ed = root._mTip;
        var bar = root.querySelector('[data-m-editor-bar]');
        var mode = bar.getAttribute('data-mode');
        var url = safeUrl(bar.querySelector('input').value, mode === 'image');
        closeBar(root, true);
        if (!url) return;
        if (mode === 'image') {
            ed.chain().focus().setImage({ src: url }).run();
        } else {
            var sel = ed.state.selection, empty = sel.empty;
            // An empty selection on an existing link: extend to the whole link. Anywhere else, type the URL in as
            // the text, linked to itself.
            if (empty && !ed.isActive('link'))
                ed.chain().focus().insertContent({ type: 'text', marks: [{ type: 'link', attrs: { href: url } }], text: url }).run();
            else
                ed.chain().focus().extendMarkRange('link').setLink({ href: url }).run();
        }
    }

    function editorCommand(root, name, arg) {
        var ed = root._mTip, c = ed.chain().focus();
        if (name === 'undo') { c.undo().run(); return; }
        if (name === 'redo') { c.redo().run(); return; }
        if (name === 'link' || name === 'image') { openBar(root, name); return; }
        if (name === 'clean') { c.unsetAllMarks().clearNodes().run(); return; }
        if (name === 'list') { (arg === 'ordered' ? c.toggleOrderedList() : c.toggleBulletList()).run(); return; }
        var toggles = {
            bold: c.toggleBold(), italic: c.toggleItalic(), underline: c.toggleUnderline(), strike: c.toggleStrike(),
            blockquote: c.toggleBlockquote(), 'code-block': c.toggleCodeBlock()
        };
        if (toggles[name]) toggles[name].run();
    }

    document.addEventListener('mousedown', function (e) {
        // Toolbar buttons must not take the selection away from the text.
        if (e.target instanceof Element && e.target.closest('[data-m-ed]') && editorOf(e.target)) e.preventDefault();
    });
    document.addEventListener('click', function (e) {
        if (!(e.target instanceof Element)) return;
        var btn = e.target.closest('[data-m-ed]');
        var root = btn && editorOf(btn);
        if (root) { e.preventDefault(); editorCommand(root, btn.getAttribute('data-m-ed'), btn.getAttribute('data-arg')); return; }
        var apply = e.target.closest('[data-m-editor-bar-apply]');
        if (apply && editorOf(apply)) { e.preventDefault(); applyBar(editorOf(apply)); return; }
        var cancel = e.target.closest('[data-m-editor-bar-cancel]');
        if (cancel && editorOf(cancel)) { e.preventDefault(); closeBar(editorOf(cancel), true); return; }
        var remove = e.target.closest('[data-m-editor-bar-remove]');
        if (remove && editorOf(remove)) {
            e.preventDefault();
            var r = editorOf(remove);
            closeBar(r, true);
            r._mTip.chain().focus().extendMarkRange('link').unsetLink().run();
        }
    });
    document.addEventListener('change', function (e) {
        var sel = e.target instanceof Element && e.target.closest('[data-m-ed-heading]');
        var root = sel && editorOf(sel);
        if (!root) return;
        var ed = root._mTip;
        if (sel.value) ed.chain().focus().setHeading({ level: Number(sel.value) }).run();
        else ed.chain().focus().setParagraph().run();
    });
    document.addEventListener('keydown', function (e) {
        var bar = e.target instanceof Element && e.target.closest('[data-m-editor-bar]');
        var root = bar && editorOf(bar);
        if (root) {
            if (e.key === 'Enter') { e.preventDefault(); applyBar(root); }
            else if (e.key === 'Escape') { e.preventDefault(); closeBar(root, true); }
            return;
        }
        // Ctrl+K opens the link bar, as in most editors.
        root = e.target instanceof Element && e.target.closest('.tiptap') && editorOf(e.target);
        if (root && (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); openBar(root, 'link'); }
    });

    // ---- Tile maps (MapLibre GL JS) ------------------------------------------------------------------------

    var MAPLIBRE = [LIB + 'maplibre/maplibre-gl.js'];
    var MAPLIBRE_CSS = [LIB + 'maplibre/maplibre-gl.css'];

    function pinElement(tone, title) {
        var el = document.createElement('div');
        el.className = 'm-map-pin m-map-pin-' + (tone || 'primary');
        el.innerHTML = '<span class="m-map-pin-head"></span>';
        if (title) { el.title = title; el.setAttribute('aria-label', title); }
        el.tabIndex = 0;
        return el;
    }

    function popupContent(mk) {
        var box = document.createElement('div');
        box.className = 'm-map-popup';
        var t = document.createElement('div');
        t.className = 'm-map-popup-title';
        t.textContent = mk.title || '';
        box.appendChild(t);
        if (mk.text) {
            var p = document.createElement('div');
            p.className = 'm-map-popup-text';
            p.textContent = mk.text;
            box.appendChild(p);
        }
        return box;
    }

    function osmStyle() {
        return {
            version: 8,
            sources: {
                osm: {
                    type: 'raster',
                    tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
                    tileSize: 256,
                    maxzoom: 19,
                    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a> contributors'
                }
            },
            layers: [{ id: 'osm', type: 'raster', source: 'osm' }]
        };
    }

    function initMap(el) {
        el.setAttribute('data-m-ready', '');
        libs(MAPLIBRE_CSS, MAPLIBRE).then(function () {
            if (!document.contains(el) || el._mMap) return;
            var gl = window.maplibregl;
            var markers = json(el, 'data-markers', []);
            var center = (el.getAttribute('data-center') || '').split(',').map(Number);
            var zoom = Number(el.getAttribute('data-zoom') || 4);
            var map = new gl.Map({
                container: el,
                style: osmStyle(),
                // Off by default, so the page scrolls past the map (data-scroll-zoom turns it on).
                scrollZoom: el.hasAttribute('data-scroll-zoom'),
                attributionControl: { compact: true },
                // trackResize (the default) keeps the canvas at the container's size.
                maxZoom: 19
            });
            map.addControl(new gl.NavigationControl({ showCompass: false }));
            var layers = markers.map(function (mk) {
                var pin = pinElement(mk.tone, mk.title);
                pin.addEventListener('keydown', function (e) {
                    if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); marker.togglePopup(); }
                });
                var marker = new gl.Marker({ element: pin, anchor: 'bottom' })
                    .setLngLat([mk.lng, mk.lat])
                    .setPopup(new gl.Popup({ closeButton: true, offset: 32, maxWidth: '260px' }).setDOMContent(popupContent(mk)))
                    .addTo(map);
                return marker;
            });
            if (center.length === 2 && center.every(isFinite)) { map.setCenter([center[1], center[0]]); map.setZoom(zoom); }
            else if (layers.length) {
                var bounds = layers.reduce(function (b, m) { return b.extend(m.getLngLat()); }, new gl.LngLatBounds());
                map.fitBounds(bounds, { padding: Math.max(24, Math.round(el.clientWidth * 0.12)), maxZoom: zoom || 12, animate: false });
            }
            else { map.setCenter([0, 20]); map.setZoom(2); }
            el._mMap = { map: map, markers: layers };
            keepAlive(el, function () { el._mMap.map.remove(); });
        }).catch(function () { el.classList.add('m-map-failed'); });
    }

    document.addEventListener('click', function (e) {
        var go = e.target instanceof Element && e.target.closest('[data-m-map-go]');
        if (!go) return;
        var el = document.getElementById(go.getAttribute('data-m-map-target') || '');
        var state = el && el._mMap;
        if (!state) return;
        var mk = state.markers[Number(go.getAttribute('data-m-map-go'))];
        if (!mk) return;
        e.preventDefault();
        state.map.flyTo({ center: mk.getLngLat(), zoom: Math.max(state.map.getZoom(), Number(go.getAttribute('data-zoom') || 12)), duration: 800 });
        state.map.once('moveend', function () { mk.togglePopup(); });
        var list = go.closest('[data-m-map-list]');
        if (list) list.querySelectorAll('[data-m-map-go]').forEach(function (b) { b.classList.toggle('active', b === go); b.setAttribute('aria-current', b === go ? 'true' : 'false'); });
    });

    // ---- Vector maps (jsVectorMap) -------------------------------------------------------------------------

    var VECTOR = [LIB + 'jsvectormap/jsvectormap.min.js', LIB + 'jsvectormap/world.js'];
    var VECTOR_CSS = [LIB + 'jsvectormap/jsvectormap.min.css'];
    var vectorId = 0;

    function initVectorMap(el) {
        el.setAttribute('data-m-ready', '');
        libs(VECTOR_CSS, VECTOR).then(function () {
            if (!document.contains(el)) return;
            drawVectorMap(el);
        }).catch(function () { el.classList.add('m-map-failed'); });
    }

    function drawVectorMap(el) {
        if (el._mVector) { try { el._mVector.destroy(); } catch (e) { /* already gone */ } el._mVector = null; }
        el.textContent = '';
        if (!el.id) el.id = 'm-vector-map-' + (++vectorId);
        var fills = json(el, 'data-fills', {});
        var labels = json(el, 'data-labels', {});
        var classes = {}, scale = {};
        Object.keys(fills).forEach(function (code) { var c = fills[code]; scale[c] = c; classes[code] = c; });
        var land = cssVar(el, '--m-map-land', '#e5e7eb');
        var selected = el.getAttribute('data-selected');
        el._mVector = new window.jsVectorMap({
            selector: '#' + CSS.escape(el.id),
            map: el.getAttribute('data-map') || 'world',
            backgroundColor: 'transparent',
            zoomButtons: true,
            zoomOnScroll: false,
            regionsSelectable: true,
            regionsSelectableOne: true,
            selectedRegions: selected ? [selected] : [],
            regionStyle: {
                initial: { fill: land, stroke: cssVar(el, '--m-map-border', '#ffffff'), strokeWidth: 0.5, fillOpacity: 1 },
                hover: { fillOpacity: 0.8, cursor: 'pointer' },
                selected: { fill: cssVar(el, '--m-map-selected', '#faad14') },
                selectedHover: {}
            },
            series: { regions: [{ attribute: 'fill', scale: scale, values: classes }] },
            onRegionTooltipShow: function (event, tooltip, code) {
                var name = tooltip.text();
                tooltip.text(labels[code] ? name + ' — ' + labels[code] : name + ' — no data');
            },
            onRegionSelected: function (code, isSelected) {
                var out = el.parentElement && el.parentElement.querySelector('[data-m-vector-selected]');
                if (!out) return;
                out.textContent = isSelected ? (el._mVector.mapData.paths[code] ? el._mVector.mapData.paths[code].name : code) + ': ' + (labels[code] || 'no data') : out.getAttribute('data-empty') || '';
            }
        });
        keepAlive(el, function () { el._mVector.destroy(); });
    }

    // Colours are read when a vector map is drawn, so it is drawn again in the other theme.
    window.addEventListener('modulus:theme', function () {
        document.querySelectorAll('[data-m-vector-map]').forEach(function (el) { if (el._mVector) drawVectorMap(el); });
    });

    // ---- Spreadsheet (Univer) ------------------------------------------------------------------------------

    // One pre-bundled IIFE (see lib/univer/LICENSE.md): createUniver, the sheets core preset plus filter, sort,
    // conditional formatting, data validation, find & replace, hyperlinks, notes and tables, the themes and the
    // en-US locale. It is about 11.7 MB (3 MB gzipped) and loads only on pages that show a spreadsheet. The sheets
    // travel as JSON in data-sheets ([{ name, rows, frozenRows, frozenColumns, columnWidths }]); a cell is a
    // number, text, a bool or a "=SUM(A1:B2)" string. Edits land in the [data-m-sheet-value] input as the same
    // JSON, so the sheet posts with its form and Blazor binds it. Formatting made in the grid (fonts, colours,
    // rules) lives as long as the grid; values, formulas, frozen panes and column widths travel.
    var UNIVER = [LIB + 'univer/univer.js'];
    var UNIVER_CSS = [LIB + 'univer/univer.css'];
    var SHEET_COL_WIDTH = 88;
    var sheetId = 0;

    function sheetCellData(sheet) {
        var cells = {}, head = sheet.frozenRows || 0;
        (sheet.rows || []).forEach(function (row, r) {
            (row || []).forEach(function (cell, c) {
                if (cell === '' || cell == null) return;
                // Univer keeps a formula with its "=": { f: "=SUM(A1:B2)" }.
                var out = typeof cell === 'string' && cell.charAt(0) === '=' && cell.length > 1 ? { f: cell } : { v: cell };
                if (r < head) out.s = { bl: 1 }; // frozen rows are header rows
                (cells[r] = cells[r] || {})[c] = out;
            });
        });
        return cells;
    }

    function sheetRows(cellData) {
        var maxR = -1, maxC = -1;
        Object.keys(cellData || {}).forEach(function (r) {
            Object.keys(cellData[r] || {}).forEach(function (c) {
                var cell = cellData[r][c];
                if (!cell || (!cell.f && (cell.v == null || cell.v === ''))) return;
                maxR = Math.max(maxR, +r);
                maxC = Math.max(maxC, +c);
            });
        });
        var rows = [];
        for (var r = 0; r <= maxR; r++) {
            var row = [];
            for (var c = 0; c <= maxC; c++) {
                var cell = cellData[r] && cellData[r][c];
                row.push(cell && cell.f ? (cell.f.charAt(0) === '=' ? cell.f : '=' + cell.f) : cell && cell.v != null ? cell.v : '');
            }
            while (row.length && row[row.length - 1] === '') row.pop();
            rows.push(row);
        }
        while (rows.length && !rows[rows.length - 1].length) rows.pop();
        return rows;
    }

    // Column widths that differ from the default, as a list where 0 is the default width.
    function sheetWidths(s) {
        var cols = s.columnData || {}, def = s.defaultColumnWidth || SHEET_COL_WIDTH, out = [];
        Object.keys(cols).forEach(function (c) {
            var w = cols[c] && cols[c].w;
            if (w && Math.round(w) !== def) out[+c] = Math.round(w);
        });
        for (var i = 0; i < out.length; i++) if (!out[i]) out[i] = 0;
        return out;
    }

    function sheetSnapshot(root) {
        var wb = root._mUniver && root._mUniver.univerAPI.getActiveWorkbook();
        if (!wb) return [];
        var snap = typeof wb.save === 'function' ? wb.save() : wb.getSnapshot(); // save() in Univer 1.x
        return (snap.sheetOrder || Object.keys(snap.sheets || {})).map(function (id) {
            var s = (snap.sheets || {})[id] || {}, f = s.freeze || {};
            var out = { name: s.name || 'Sheet1', rows: sheetRows(s.cellData) };
            if (f.ySplit > 0) out.frozenRows = f.ySplit;
            if (f.xSplit > 0) out.frozenColumns = f.xSplit;
            var widths = sheetWidths(s);
            if (widths.length) out.columnWidths = widths;
            return out;
        });
    }

    function sheetWorksheet(s, i) {
        var rows = s.rows || [], cols = 0, columnData = {};
        rows.forEach(function (row) { cols = Math.max(cols, (row || []).length); });
        (s.columnWidths || []).forEach(function (w, c) { if (w > 0) columnData[c] = { w: w }; });
        var ws = {
            id: 's' + i,
            name: s.name || 'Sheet' + (i + 1),
            rowCount: Math.max(100, rows.length + 50),
            columnCount: Math.max(26, cols + 6),
            defaultColumnWidth: SHEET_COL_WIDTH,
            columnData: columnData,
            cellData: sheetCellData(s)
        };
        var fr = s.frozenRows || 0, fc = s.frozenColumns || 0;
        if (fr > 0 || fc > 0) ws.freeze = { xSplit: fc, ySplit: fr, startRow: fr, startColumn: fc };
        return ws;
    }

    // The brand colour as Univer's primary scale (600 is the base): tints towards white, shades towards black.
    function sheetTheme(root) {
        var u = window.ModulusUniver, probe = document.createElement('span');
        probe.style.color = cssVar(root, '--m-primary', '#066fd1');
        probe.style.display = 'none';
        root.appendChild(probe);
        var m = /(\d+),\s*(\d+),\s*(\d+)/.exec(getComputedStyle(probe).color);
        probe.remove();
        if (!m) return u.defaultTheme;
        var rgb = [+m[1], +m[2], +m[3]];
        var mix = function (to, t) {
            return '#' + rgb.map(function (v) { return ('0' + Math.round(v + (to - v) * t).toString(16)).slice(-2); }).join('');
        };
        var primary = { 50: mix(255, 0.95), 100: mix(255, 0.9), 200: mix(255, 0.78), 300: mix(255, 0.62), 400: mix(255, 0.4),
            500: mix(255, 0.2), 600: mix(255, 0), 700: mix(0, 0.12), 800: mix(0, 0.24), 900: mix(0, 0.36) };
        return Object.assign({}, u.defaultTheme, { primary: primary });
    }

    function sheetDark() {
        return (document.documentElement.getAttribute('data-theme') || 'light') === 'dark';
    }

    // The sheet's half of univerEditor below: its presets, its workbook and its JSON.
    var SHEET = {
        value: '[data-m-sheet-value]',
        // Only these change what the input carries: not selection, scrolling, the cell editor's own text or formula results.
        mutation: /^sheet\./,
        presets: function (u, app, readonly) {
            // Read-only keeps the sheet tabs and zoom (footer) but drops the ribbon, formula bar and menus. Auto focus stays on
            // for editing: without it a key typed on a selected cell reaches only the formula bar, not the cell.
            if (readonly) return [u.UniverSheetsCorePreset({ container: app, header: false, toolbar: false, formulaBar: false, menu: false, contextMenu: false, disableAutoFocus: true })];
            return [u.UniverSheetsCorePreset({ container: app }), u.UniverSheetsFilterPreset(), u.UniverSheetsSortPreset(),
                u.UniverSheetsConditionalFormattingPreset(), u.UniverSheetsDataValidationPreset(), u.UniverSheetsFindReplacePreset(),
                u.UniverSheetsHyperLinkPreset(), u.UniverSheetsNotePreset(), u.UniverSheetsTablePreset()];
        },
        load: function (api, root, readonly) {
            var defs = json(root, 'data-sheets', []);
            if (!defs.length) defs = [{ name: 'Sheet1', rows: [] }];
            var data = {
                id: 'm-sheet-' + (++sheetId),
                name: defs[0].name || 'Sheet1',
                editable: !readonly,
                sheetOrder: defs.map(function (s, i) { return 's' + i; }),
                sheets: {}
            };
            defs.forEach(function (s, i) { data.sheets['s' + i] = sheetWorksheet(s, i); });
            api.createWorkbook(data);
            return data.id;
        },
        ready: function (api, app, readonly) { if (!readonly) univerPrintButton(api, app); },
        snapshot: function (root) { return root._mUniver ? JSON.stringify(sheetSnapshot(root)) : null; }
    };

    // ---- Univer editors (the spreadsheet and the document) ---------------------------------------------------

    // Starting Univer is the slow part (its plugins, renderer and ribbon), so an editor keeps its instance for as long as
    // the page shows it. When the root is replaced by an identical one (a Reset or Save that renders the page again, an
    // htmx or SPA swap, a Blazor @key change), the new root adopts the running instance and only the workbook or document
    // is swapped: no "Loading…" and no blank frame. Edits are snapshotted only after a mutation of the content (not on
    // selection or scrolling), in idle time, and the pause before a snapshot grows with what a snapshot costs, so a large
    // file stays responsive.
    function univerKey(root, kind) {
        var value = root.querySelector(kind.value);
        return (root.id || (value && value.id) || '') + (root.hasAttribute('data-readonly') ? '|ro' : '|rw');
    }

    // A running editor of the same kind, id and mode whose root just left the page.
    function univerOrphan(root, kind) {
        var key = univerKey(root, kind);
        for (var i = 0; i < liveRoots.length; i++) {
            var old = liveRoots[i], ed = old._mEd;
            if (ed && ed.kind === kind && !document.contains(old) && univerKey(old, kind) === key) return old;
        }
        return null;
    }

    // Puts the editor on root (a new one, or the one it starts on) and loads root's content into it.
    function univerAttach(ed, root) {
        var api = ed.created.univerAPI, previous = ed.unit;
        ed.root = root;
        root._mEd = ed;
        root._mUniver = ed.created;
        if (ed.app.parentNode !== root) root.appendChild(ed.app);
        ed.value = root.querySelector(ed.kind.value);
        ed.last = ed.value ? ed.value.value : null;
        clearTimeout(ed.timer);
        ed.loading = true;
        try {
            ed.unit = ed.kind.load(api, root, ed.readonly);
            if (previous) api.disposeUnit(previous); // after the new one shows, so there is never an empty frame
        } finally { ed.loading = false; }
        root.classList.add('m-sheet-ready');
        keepAlive(root, function () { univerDestroy(ed); });
    }

    // The editor usually sits in a form (it posts its JSON), but Univer's own controls are not the form's: its buttons
    // carry no type (so the sheet list next to "+" would submit, and download, the page's form) and Enter in one of its
    // boxes would submit it too. Pointing them at a form id that does not exist takes them out of it.
    var NO_FORM = 'm-univer-no-form';

    function univerOwnControls(app) {
        var queued = false;
        var detach = function () {
            queued = false;
            app.querySelectorAll('button:not([form]), input:not([form]), select:not([form]), textarea:not([form])')
                .forEach(function (el) { el.setAttribute('form', NO_FORM); });
        };
        detach();
        var observer = new MutationObserver(function () {
            if (queued) return;
            queued = true;
            queueMicrotask(detach); // before the click or key that could reach the form
        });
        observer.observe(app, { childList: true, subtree: true });
        return observer;
    }

    function univerDestroy(ed) {
        clearTimeout(ed.timer);
        ed.controls.disconnect();
        try { ed.created.univerAPI.dispose(); } catch (e) { /* already gone */ }
        try { ed.created.univer.dispose(); } catch (e) { /* already gone */ }
        if (ed.root) { ed.root._mUniver = null; ed.root._mEd = null; }
    }

    var idle = window.requestIdleCallback || function (fn) { return setTimeout(fn, 0); };

    function univerSchedule(ed) {
        clearTimeout(ed.timer);
        ed.timer = setTimeout(function () {
            idle(function () {
                if (!ed.root || ed.root._mEd !== ed || !ed.value) return;
                var t = performance.now(), out = ed.kind.snapshot(ed.root);
                // A snapshot that takes 50 ms waits 400 ms of quiet; one that takes 300 ms waits 2.4 s (at most 3 s).
                ed.delay = Math.min(3000, Math.max(400, (performance.now() - t) * 8));
                if (out && out !== ed.last) { ed.last = out; raiseChange(ed.value, out); }
            }, { timeout: 1000 });
        }, ed.delay);
    }

    function univerEditor(root, kind) {
        root.setAttribute('data-m-ready', '');
        var orphan = univerOrphan(root, kind);
        if (orphan) {
            var adopted = orphan._mEd;
            orphan._mUniver = null;
            orphan._mEd = null;
            liveRoots.splice(liveRoots.indexOf(orphan), 1);
            univerAttach(adopted, root);
            return;
        }
        libs(UNIVER_CSS, UNIVER).then(function () {
            if (!document.contains(root) || root._mUniver) return;
            var u = window.ModulusUniver;
            var readonly = root.hasAttribute('data-readonly');
            var app = document.createElement('div');
            app.className = 'm-sheet-app';
            root.appendChild(app);
            var created = u.createUniver({
                locale: u.LocaleType.EN_US,
                // The map is keyed by the LocaleType value itself ("enUS"), not the locale file name.
                locales: { [u.LocaleType.EN_US]: u.enUS },
                theme: sheetTheme(root),
                darkMode: sheetDark(),
                presets: kind.presets(u, app, readonly)
            });
            var ed = { kind: kind, readonly: readonly, app: app, created: created, controls: univerOwnControls(app), unit: null, root: null, value: null, last: null, timer: 0, delay: 400, loading: false };
            if (kind.ready) kind.ready(created.univerAPI, app, readonly);
            univerAttach(ed, root);
            // Mutations (type 2) of the content only; loading a workbook or document is not an edit.
            created.univerAPI.addEvent(created.univerAPI.Event.CommandExecuted, function (e) {
                if (ed.readonly || ed.loading || e.type !== 2 || !kind.mutation.test(e.id)) return;
                univerSchedule(ed);
            });
        }).catch(function (e) {
            root.classList.add('m-sheet-failed');
            if (window.console) console.error('Modulus: the ' + (kind === SHEET ? 'spreadsheet' : 'document editor') + ' failed to start', e);
        });
    }

    function initSheet(root) { univerEditor(root, SHEET); }

    // The theme switch (light/dark, or a new brand colour) restyles Univer in place, without losing the edits.
    window.addEventListener('modulus:theme', function () {
        document.querySelectorAll('[data-m-sheet], [data-m-doc]').forEach(function (root) {
            if (!root._mUniver) return;
            root._mUniver.univerAPI.setTheme(sheetTheme(root));
            root._mUniver.univerAPI.toggleDarkMode(sheetDark());
        });
    });

    // A form posts what the editors hold now, even inside the pause after an edit.
    document.addEventListener('submit', function (e) {
        if (!(e.target instanceof HTMLFormElement)) return;
        // A control of Univer's that was not taken out of the form yet: not a submit.
        if (e.submitter && e.submitter.closest('.m-sheet-app')) { e.preventDefault(); e.stopImmediatePropagation(); return; }
        e.target.querySelectorAll('[data-m-sheet], [data-m-doc]').forEach(function (root) {
            var ed = root._mEd;
            if (!ed || ed.readonly || !ed.value) return;
            clearTimeout(ed.timer);
            var out = ed.kind.snapshot(root);
            if (out) { ed.value.value = out; ed.last = out; }
        });
    }, true);

    // ---- Document editor (Univer Docs) ---------------------------------------------------------------------

    // The same bundle as the spreadsheet (its docs engine is already in it for cell editing): a Word-style ribbon over A4
    // pages. The document travels as Univer's JSON snapshot in data-doc (built and read on the server by
    // Modulus.Documents.UniverDocument); edits land in [data-m-doc-value] as that JSON, with a change event.
    var docId = 0;

    // A blank A4 page, laid out as pages, for an editor without a document.
    function docBlank() {
        return {
            body: { dataStream: '\r\n', textRuns: [], paragraphs: [{ startIndex: 0 }], sectionBreaks: [{ startIndex: 1 }] },
            documentStyle: { pageSize: { width: 793.7, height: 1122.5 }, documentFlavor: 1, marginTop: 72, marginBottom: 72, marginLeft: 96, marginRight: 96,
                defaultParagraphStyle: { spaceAbove: { v: 0 }, lineSpacing: 1.15, spaceBelow: { v: 8 } } }
        };
    }

    function docSnapshot(root) {
        var doc = root._mUniver && root._mUniver.univerAPI.getActiveDocument();
        return doc ? JSON.stringify(doc.save()) : null;
    }

    // Univer's sheets ribbon has a full screen button; the docs ribbon gets the same one (same icon and tooltip), and
    // like the sheet it takes Univer's own container full screen, so its menus and dialogs still show.
    function docFullscreenButton(api, app) {
        if (!app.requestFullscreen || !api.createMenu) return;
        try {
            api.createMenu({
                id: 'modulus.doc.fullscreen',
                title: '',
                tooltip: 'sheets-ui.toolbar.fullscreen',
                icon: 'ExpandIcon',
                action: function () {
                    if (document.fullscreenElement) document.exitFullscreen().catch(function () { });
                    else app.requestFullscreen().catch(function () { });
                }
            }).appendTo('ribbon.start.others');
        } catch (e) { if (window.console) console.error(e); }
    }

    // The document's half of univerEditor: its presets, its document and its JSON.
    var DOC = {
        value: '[data-m-doc-value]',
        // Text, paragraph and image edits; not selection or the formula engine.
        mutation: /^(doc|docs|drawing|docs-drawing)[.-]/,
        presets: function (u, app, readonly) {
            // Read-only drops the ribbon and menus and locks the text; the pages and the footer (pages, words, zoom) stay.
            if (readonly) return [u.UniverDocsCorePreset({ container: app, header: false, toolbar: false, menu: false, contextMenu: false, disableAutoFocus: true })];
            return [u.UniverDocsCorePreset({ container: app, disableAutoFocus: true }), u.UniverDocsDrawingPreset(), u.UniverDocsHyperLinkPreset()];
        },
        ready: function (api, app, readonly) { if (!readonly) { docFullscreenButton(api, app); univerPrintButton(api, app); } },
        load: function (api, root, readonly) {
            var data = json(root, 'data-doc', null);
            if (!data || !data.body) data = docBlank();
            data.id = 'm-doc-' + (++docId);
            if (readonly) data.disabled = true;
            api.createDocument(data);
            return data.id;
        },
        snapshot: docSnapshot
    };

    function initDoc(root) { univerEditor(root, DOC); }

    // ---- Print (the spreadsheet and the document) ----------------------------------------------------------
    // Univer's own printing is a Pro plugin, so printing is ours: the sheets (every visible sheet, a page break between
    // them, the frozen rows repeated on each page) or the document (its page size and margins, headings, lists, tables,
    // links and images) become plain HTML in a hidden frame, and the browser prints that. The editors get a Print button
    // on the ribbon and take Ctrl+P while focused; any [data-m-print="{editor id}"] button prints too, also read-only
    // ones without a ribbon, and before Univer has started (from the JSON in the markup).

    var PRINT_ICON = 'ModulusPrintIcon';

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; });
    }

    // The frame and the print dialog are modulusShell.print (modulus-shell.js).
    function printFrame(title, css, body) {
        window.modulusShell.print(title, css, body);
    }

    function printTitle(root, fallback) {
        return root.getAttribute('aria-label') || document.title || fallback;
    }

    // A Univer style (cell or text run) as CSS.
    function univerCss(st) {
        if (!st) return '';
        var css = '';
        if (st.ff) css += 'font-family:' + esc(st.ff) + ';';
        if (st.fs) css += 'font-size:' + (+st.fs) + 'pt;';
        if (st.bl === 1) css += 'font-weight:700;';
        if (st.it === 1) css += 'font-style:italic;';
        var deco = (st.ul && st.ul.s === 1 ? ' underline' : '') + (st.st && st.st.s === 1 ? ' line-through' : '');
        if (deco) css += 'text-decoration:' + deco.trim() + ';';
        if (st.cl && st.cl.rgb) css += 'color:' + esc(st.cl.rgb) + ';';
        if (st.bg && st.bg.rgb) css += 'background:' + esc(st.bg.rgb) + ';';
        return css;
    }

    // -- The sheets --

    var PRINT_SHEET_CSS = '@page{margin:12mm}body{margin:0;font:10pt/1.3 Arial,Helvetica,sans-serif;color:#000}' +
        'h1{font-size:12pt;margin:0 0 6px}section+section{break-before:page}' +
        'table{border-collapse:collapse;table-layout:fixed}td{padding:1px 4px;border:1px solid #d4d4d4;white-space:nowrap;overflow:hidden;vertical-align:bottom}' +
        'table.m-print-nogrid td{border-color:transparent}thead{display:table-header-group}tr{break-inside:avoid}' +
        '.n{text-align:right}.b{text-align:center}.w{white-space:pre-wrap;word-break:break-word}.m-print-empty{color:#666}';

    var BORDER_SIDES = { t: 'top', b: 'bottom', l: 'left', r: 'right' };

    function sheetCellCss(st) {
        if (!st) return '';
        var css = univerCss(st);
        if (st.ht === 1) css += 'text-align:left;'; else if (st.ht === 2) css += 'text-align:center;'; else if (st.ht === 3) css += 'text-align:right;';
        if (st.vt === 1) css += 'vertical-align:top;'; else if (st.vt === 2) css += 'vertical-align:middle;';
        Object.keys(st.bd || {}).forEach(function (k) {
            var b = st.bd[k];
            if (!BORDER_SIDES[k] || !b || !b.s) return;
            // Univer's border styles: 1 thin, 2 hair, 3 dotted, 4 dashed, … 7 double, 8 medium, 13 thick.
            var width = b.s === 13 ? 3 : b.s === 8 || b.s === 9 || b.s === 10 || b.s === 11 || b.s === 12 ? 2 : 1;
            var line = b.s === 3 ? 'dotted' : b.s === 4 || b.s === 9 ? 'dashed' : b.s === 7 ? 'double' : 'solid';
            css += 'border-' + BORDER_SIDES[k] + ':' + (line === 'double' ? 3 : width) + 'px ' + line + ' ' + esc((b.cl && b.cl.rgb) || '#000') + ';';
        });
        return css;
    }

    // The sheets as the workbook holds them now: every value as the grid shows it (formula results, number formats).
    function sheetPrintModel(root) {
        var wb = root._mUniver && root._mUniver.univerAPI.getActiveWorkbook();
        if (!wb) {
            // Not started yet: the sheets from the markup, formulas as written.
            return json(root, 'data-sheets', []).map(function (s) {
                return { name: s.name, cellData: sheetCellData(s), styles: {}, columnData: sheetWorksheet(s, 0).columnData, frozenRows: s.frozenRows || 0 };
            });
        }
        var snap = typeof wb.save === 'function' ? wb.save() : wb.getSnapshot();
        return (snap.sheetOrder || Object.keys(snap.sheets || {})).map(function (id) {
            var s = (snap.sheets || {})[id] || {};
            if (s.hidden === 1) return null;
            var display = null;
            try { display = { sheet: wb.getSheetBySheetId(id) }; } catch (e) { /* values from the snapshot */ }
            return { name: s.name, cellData: s.cellData || {}, styles: snap.styles || {}, columnData: s.columnData || {}, rowData: s.rowData || {},
                defaultColumnWidth: s.defaultColumnWidth, mergeData: s.mergeData || [], frozenRows: (s.freeze && s.freeze.ySplit) || 0,
                gridlines: s.showGridlines !== 0, display: display };
        }).filter(Boolean);
    }

    function sheetPrintSection(s) {
        var cells = s.cellData || {}, maxR = -1, maxC = -1;
        Object.keys(cells).forEach(function (r) {
            Object.keys(cells[r] || {}).forEach(function (c) {
                var cell = cells[r][c];
                if (!cell || (cell.f == null && (cell.v == null || cell.v === '') && !cell.p)) return;
                maxR = Math.max(maxR, +r);
                maxC = Math.max(maxC, +c);
            });
        });
        var title = '<h1>' + esc(s.name || 'Sheet') + '</h1>';
        if (maxR < 0) return '<section>' + title + '<p class="m-print-empty">This sheet is empty.</p></section>';
        (s.mergeData || []).forEach(function (m) { maxR = Math.max(maxR, m.endRow); maxC = Math.max(maxC, m.endColumn); });

        var values = null;
        if (s.display && s.display.sheet) {
            try { values = s.display.sheet.getRange(0, 0, maxR + 1, maxC + 1).getDisplayValues(); } catch (e) { values = null; }
        }
        var spans = {}, covered = {};
        (s.mergeData || []).forEach(function (m) {
            spans[m.startRow + ':' + m.startColumn] = { rows: m.endRow - m.startRow + 1, cols: m.endColumn - m.startColumn + 1 };
            for (var r = m.startRow; r <= m.endRow; r++)
                for (var c = m.startColumn; c <= m.endColumn; c++)
                    if (r !== m.startRow || c !== m.startColumn) covered[r + ':' + c] = true;
        });
        var colHidden = function (c) { return s.columnData[c] && s.columnData[c].hd === 1; };
        var rowHidden = function (r) { return s.rowData && s.rowData[r] && s.rowData[r].hd === 1; };

        var cols = '', width = 0;
        for (var c = 0; c <= maxC; c++) {
            if (colHidden(c)) continue;
            var w = Math.round((s.columnData[c] && s.columnData[c].w) || s.defaultColumnWidth || SHEET_COL_WIDTH);
            width += w;
            cols += '<col style="width:' + w + 'px">';
        }
        var head = '', body = '';
        for (var r = 0; r <= maxR; r++) {
            if (rowHidden(r)) continue;
            var h = s.rowData && s.rowData[r] && s.rowData[r].h;
            var tr = '<tr' + (h ? ' style="height:' + Math.round(h) + 'px"' : '') + '>';
            for (c = 0; c <= maxC; c++) {
                if (colHidden(c) || covered[r + ':' + c]) continue;
                var cell = (cells[r] || {})[c] || null;
                var st = cell && cell.s ? (typeof cell.s === 'string' ? s.styles[cell.s] : cell.s) : null;
                if (r < (s.frozenRows || 0) && !st) st = { bl: 1 };
                var raw = cell ? (cell.v != null ? cell.v : cell.f || '') : '';
                var text = values && values[r] && values[r][c] != null ? values[r][c] : raw;
                if (cell && cell.p && cell.p.body && text === '') text = (cell.p.body.dataStream || '').replace(/[\r\n]+$/, '');
                var kind = typeof (cell && cell.v) === 'number' || (cell && cell.f && typeof text === 'string' && /^-?[\d.,\s%$€£¥()-]+$/.test(text)) ? ' n'
                    : typeof (cell && cell.v) === 'boolean' ? ' b' : '';
                var span = spans[r + ':' + c], attrs = '';
                if (span) attrs += (span.rows > 1 ? ' rowspan="' + span.rows + '"' : '') + (span.cols > 1 ? ' colspan="' + span.cols + '"' : '');
                var cls = (kind + (st && st.tb === 3 ? ' w' : '')).trim();
                tr += '<td' + attrs + (cls ? ' class="' + cls + '"' : '') + (st ? ' style="' + sheetCellCss(st) + '"' : '') + '>' + esc(text) + '</td>';
            }
            tr += '</tr>';
            if (r < (s.frozenRows || 0)) head += tr; else body += tr;
        }
        // A wide sheet shrinks to the page's width (about 700 px of A4 portrait at 12 mm margins).
        var zoom = width > 700 ? ' style="zoom:' + Math.max(0.4, 700 / width).toFixed(3) + '"' : '';
        return '<section' + zoom + '>' + title + '<table' + (s.gridlines === false ? ' class="m-print-nogrid"' : '') + ' style="width:' + width + 'px"><colgroup>' + cols + '</colgroup>' +
            (head ? '<thead>' + head + '</thead>' : '') + '<tbody>' + body + '</tbody></table></section>';
    }

    function printSheet(root) {
        printFrame(printTitle(root, 'Spreadsheet'), PRINT_SHEET_CSS, sheetPrintModel(root).map(sheetPrintSection).join(''));
    }

    // -- The document --

    // Univer Docs' stream markers (see Modulus.Documents.UniverDocument).
    var D_PARA = '\r', D_SECTION = '\n', D_TABLE = '\u001A', D_ROW = '\u001B', D_CELL = '\u001C', D_CELL_END = '\u001D',
        D_ROW_END = '\u000E', D_TABLE_END = '\u000F', D_BLOCK = '\b', D_PAGE = '\f', D_END = '\0';

    function docPrintHtml(data) {
        var body = data.body || {}, stream = body.dataStream || '', ds = data.documentStyle || {};
        var paragraphs = {};
        (body.paragraphs || []).forEach(function (p) { paragraphs[p.startIndex] = p; });
        var runs = (body.textRuns || []).filter(function (r) { return r.ts; }).sort(function (a, b) { return a.st - b.st; });
        var links = (body.customRanges || []).filter(function (r) { return r.rangeType === 0 && r.properties && r.properties.url; });
        var blocks = {};
        (body.customBlocks || []).forEach(function (b) { blocks[b.startIndex] = b.blockId; });
        var tables = {};
        (body.tables || []).forEach(function (t) { tables[t.startIndex] = t.tableId; });
        var drawings = data.drawings || {}, tableSource = data.tableSource || {};
        var lists = {}, i = 0;

        var runAt = function (k) {
            for (var n = 0; n < runs.length && runs[n].st <= k; n++) if (k < runs[n].ed) return runs[n].ts;
            return null;
        };
        var linkAt = function (k) {
            for (var n = 0; n < links.length; n++) if (k >= links[n].startIndex && k <= links[n].endIndex) return links[n].properties.url;
            return null;
        };
        var image = function (k) {
            var d = drawings[blocks[k]];
            if (!d || !d.source || d.imageSourceType === 'UUID') return '';
            var size = (d.docTransform && d.docTransform.size) || d.transform || {};
            var dims = (size.width ? 'width:' + Math.round(size.width) + 'px;' : '') + (size.height ? 'height:' + Math.round(size.height) + 'px;' : '');
            return '<img src="' + esc(d.source) + '" alt="' + esc(d.description || d.title || '') + '" style="max-width:100%;' + dims + '">';
        };

        // The text between two paragraph marks, a span wherever the style or the link changes.
        var text = function (from, to) {
            var out = '', key = null, open = '', buf = '';
            var flush = function () { if (buf) out += open ? open + buf + (open.charAt(1) === 'a' ? '</span></a>' : '</span>') : buf; buf = ''; };
            for (var k = from; k < to; k++) {
                var ch = stream.charAt(k);
                if (ch === D_BLOCK) { flush(); key = null; out += image(k); continue; }
                if (ch === '\t') { ch = ' '; }
                else if (ch < ' ') continue;
                var ts = runAt(k), url = linkAt(k), next = JSON.stringify([ts, url]);
                if (next !== key) {
                    flush();
                    key = next;
                    var css = univerCss(ts);
                    if (ts && ts.va === 3) css += 'vertical-align:super;font-size:smaller;';
                    else if (ts && ts.va === 2) css += 'vertical-align:sub;font-size:smaller;';
                    open = url ? '<a href="' + esc(url) + '"><span style="' + css + '">' : css ? '<span style="' + css + '">' : '';
                }
                buf += esc(ch);
            }
            flush();
            return out || '&#8203;';
        };

        var marker = function (bullet) {
            var level = Math.max(0, Math.min(8, bullet.nestingLevel || 0)), type = bullet.listType || '';
            if (/^CHECK_LIST/.test(type)) return /CHECKED/.test(type) ? '☑' : '☐';
            if (!/ORDER/.test(type)) return ['•', '◦', '▪'][level % 3];
            var counts = lists[bullet.listId] = lists[bullet.listId] || [];
            counts[level] = (counts[level] || 0) + 1;
            counts.length = level + 1; // a deeper level starts again
            var n = counts[level];
            return (level % 3 === 1 ? String.fromCharCode(96 + ((n - 1) % 26) + 1) : level % 3 === 2 ? roman(n) : n) + '.';
        };
        var roman = function (n) {
            var out = '', map = [[10, 'x'], [9, 'ix'], [5, 'v'], [4, 'iv'], [1, 'i']];
            map.forEach(function (m) { while (n >= m[0] && n < 40) { out += m[1]; n -= m[0]; } });
            return out || String(n);
        };

        var paragraph = function (from, end) {
            var p = paragraphs[end] || {}, ps = p.paragraphStyle || {}, css = '';
            // Title, Subtitle, then Heading 1 to 5.
            var tag = { 2: 'h1', 3: 'p', 4: 'h1', 5: 'h2', 6: 'h3', 7: 'h4', 8: 'h5' }[ps.namedStyleType] || 'p';
            var cls = ps.namedStyleType === 2 ? ' class="title"' : ps.namedStyleType === 3 ? ' class="subtitle"' : '';
            var align = { 2: 'center', 3: 'right', 4: 'justify', 5: 'justify' }[ps.horizontalAlign];
            if (align) css += 'text-align:' + align + ';';
            if (ps.spaceAbove && ps.spaceAbove.v != null) css += 'margin-top:' + ps.spaceAbove.v + 'px;';
            if (ps.spaceBelow && ps.spaceBelow.v != null) css += 'margin-bottom:' + ps.spaceBelow.v + 'px;';
            if (ps.lineSpacing) css += 'line-height:' + ps.lineSpacing + ';';
            if (ps.indentFirstLine && ps.indentFirstLine.v) css += 'text-indent:' + ps.indentFirstLine.v + 'px;';
            var indent = (ps.indentStart && ps.indentStart.v) || 0, mark = '';
            if (p.bullet) {
                indent += 24 * ((p.bullet.nestingLevel || 0) + 1);
                css += 'text-indent:-20px;';
                mark = '<span class="mark">' + esc(marker(p.bullet)) + '</span>';
            }
            if (indent) css += 'margin-left:' + indent + 'px;';
            return '<' + tag + cls + (css ? ' style="' + css + '"' : '') + '>' + mark + text(from, end) + '</' + tag + '>';
        };

        var table = function () {
            var source = tableSource[tables[i]] || {}, rows = source.tableRows || [];
            var cols = (source.tableColumns || []).map(function (c) { return '<col style="width:' + Math.round((c.size && c.size.width && c.size.width.v) || 0) + 'px">'; }).join('');
            var out = '<table class="doc-table"><colgroup>' + cols + '</colgroup>', r = -1, c = -1;
            i++;
            while (i < stream.length) {
                var ch = stream.charAt(i);
                if (ch === D_TABLE_END) { i++; break; }
                if (ch === D_ROW) { r++; c = -1; out += '<tr>'; i++; }
                else if (ch === D_ROW_END) { out += '</tr>'; i++; }
                else if (ch === D_CELL) {
                    c++;
                    i++;
                    var cell = rows[r] && rows[r].tableCells && rows[r].tableCells[c] || {};
                    var attrs = (cell.rowSpan > 1 ? ' rowspan="' + cell.rowSpan + '"' : '') + (cell.columnSpan > 1 ? ' colspan="' + cell.columnSpan + '"' : '');
                    var bg = cell.backgroundColor && cell.backgroundColor.rgb ? ' style="background:' + esc(cell.backgroundColor.rgb) + '"' : '';
                    out += '<td' + attrs + bg + '>' + content(true) + '</td>';
                    if (stream.charAt(i) === D_CELL_END) i++;
                }
                else i++;
            }
            return out + '</table>';
        };

        var content = function (inCell) {
            var out = '', start = i;
            while (i < stream.length) {
                var ch = stream.charAt(i);
                if (ch === D_CELL_END && inCell) break;
                if (ch === D_END) { i = stream.length; break; }
                if (ch === D_TABLE) { out += table(); start = i; continue; }
                if (ch === D_PARA) { out += paragraph(start, i); start = ++i; continue; }
                if (ch === D_PAGE) { out += '<div class="page-break"></div>'; start = i + 1; }
                else if (ch === D_SECTION) start = i + 1;
                i++;
            }
            return out;
        };

        var html = content(false);
        var size = ds.pageSize || {}, px = function (v, d) { return (v != null ? v : d) + 'px'; };
        var page = '@page{size:' + px(size.width, 793.7) + ' ' + px(size.height, 1122.5) + ';margin:' + px(ds.marginTop, 72) + ' ' + px(ds.marginRight, 96) + ' ' + px(ds.marginBottom, 72) + ' ' + px(ds.marginLeft, 96) + '}';
        return { css: page + PRINT_DOC_CSS, body: html };
    }

    var PRINT_DOC_CSS = 'body{margin:0;font:11pt/1.15 Arial,Helvetica,sans-serif;color:#000}' +
        'p,h1,h2,h3,h4,h5,h6{margin:0 0 8px;break-inside:avoid-page}h1,h2,h3,h4,h5,h6{break-after:avoid-page;font-weight:700}' +
        'h1{font-size:20pt}h1.title{font-size:26pt}p.subtitle{font-size:15pt;color:#666}h2{font-size:16pt}h3{font-size:14pt}h4{font-size:12pt}h5{font-size:11pt}' +
        '.mark{display:inline-block;width:20px;text-indent:0}a{color:#1155cc}img{vertical-align:bottom}' +
        '.doc-table{border-collapse:collapse;table-layout:fixed;margin:0 0 8px}.doc-table td{border:1px solid #000;padding:5px 10px;vertical-align:top}.doc-table td p{margin:3px 0}' +
        '.page-break{break-after:page}';

    function printDoc(root) {
        var doc = root._mUniver && root._mUniver.univerAPI.getActiveDocument();
        var data = doc ? doc.save() : json(root, 'data-doc', null);
        var out = docPrintHtml(data && data.body ? data : docBlank());
        printFrame(printTitle(root, 'Document'), out.css, out.body);
    }

    function printEditor(root) {
        if (root.hasAttribute('data-m-sheet')) printSheet(root);
        else if (root.hasAttribute('data-m-doc')) printDoc(root);
    }

    // The Print button on an editor's ribbon, with Univer's own icon style (a printer, registered once per editor).
    function univerPrintButton(api, app) {
        if (!api.createMenu) return;
        try {
            var cm = api.getComponentManager && api.getComponentManager(), h = cm && cm.reactUtils && cm.reactUtils.createElement;
            var icons = h && univerIconManager(api);
            if (icons && !icons.has(PRINT_ICON)) {
                icons.register(PRINT_ICON, function (props) {
                    return h('svg', { className: props && props.className, style: props && props.style, width: '1em', height: '1em', viewBox: '0 0 24 24', fill: 'none',
                        stroke: 'currentColor', strokeWidth: 1.8, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': true },
                        h('path', { d: 'M17 17h2a2 2 0 0 0 2-2v-4a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v4a2 2 0 0 0 2 2h2' }),
                        h('path', { d: 'M17 9V5a2 2 0 0 0-2-2H9a2 2 0 0 0-2 2v4' }),
                        h('rect', { x: 7, y: 13, width: 10, height: 8, rx: 2 }));
                });
            }
            api.createMenu({
                id: 'modulus.print',
                title: '',
                tooltip: 'Print',
                icon: icons ? PRINT_ICON : 'TextIcon',
                action: function () { var root = app.closest('[data-m-sheet], [data-m-doc]'); if (root) printEditor(root); }
            }).appendTo('ribbon.start.others');
        } catch (e) { if (window.console) console.error(e); }
    }

    // Univer's icon registry is not on its facade; it is the service in the injector that keeps a map of icons.
    function univerIconManager(api) {
        for (var inj = api._injector; inj; inj = inj.parent) {
            var map = inj.resolvedDependencyCollection && inj.resolvedDependencyCollection.resolvedDependencies;
            if (!map) continue;
            var found = null;
            map.forEach(function (list) {
                (list || []).forEach(function (s) { if (!found && s && s._icons instanceof Map && typeof s.register === 'function' && typeof s.has === 'function') found = s; });
            });
            if (found) return found;
        }
        return null;
    }

    document.addEventListener('click', function (e) {
        var button = e.target instanceof Element ? e.target.closest('[data-m-print]') : null;
        if (!button) return;
        var root = document.getElementById(button.getAttribute('data-m-print'));
        if (!root || !(root.hasAttribute('data-m-sheet') || root.hasAttribute('data-m-doc'))) return;
        e.preventDefault();
        printEditor(root);
    });

    // Ctrl+P (Cmd+P) inside an editor prints its content, not the page with the editor's canvas on it.
    document.addEventListener('keydown', function (e) {
        if (!(e.ctrlKey || e.metaKey) || e.altKey || e.shiftKey || (e.key !== 'p' && e.key !== 'P')) return;
        var root = e.target instanceof Element ? e.target.closest('[data-m-sheet], [data-m-doc]') : null;
        if (!root) return;
        e.preventDefault();
        printEditor(root);
    }, true);

    // ---- Submit on file pick -------------------------------------------------------------------------------
    // <input type="file" data-m-file-submit> (usually hidden inside a button-styled label) posts its form, which may be
    // another one named by its form attribute, as soon as a file is picked.

    document.addEventListener('change', function (e) {
        var input = e.target;
        if (!(input instanceof HTMLInputElement) || input.type !== 'file' || !input.hasAttribute('data-m-file-submit')) return;
        if (!input.files || !input.files.length || !input.form) return;
        if (input.form.requestSubmit) input.form.requestSubmit(); else input.form.submit();
    });

    // ---- Copy to clipboard ---------------------------------------------------------------------------------
    // [data-m-copy="text"] copies its text on click, marks itself data-copied for a moment and announces it in the
    // [data-m-copy-live] of its [data-m-copy-scope].

    document.addEventListener('click', function (e) {
        var btn = e.target instanceof Element && e.target.closest('[data-m-copy]');
        if (!btn) return;
        e.preventDefault();
        var text = btn.getAttribute('data-m-copy');
        var done = function () {
            btn.setAttribute('data-copied', '');
            clearTimeout(btn._mCopied);
            btn._mCopied = setTimeout(function () { btn.removeAttribute('data-copied'); }, 1500);
            var live = btn.closest('[data-m-copy-scope]');
            live = live && live.querySelector('[data-m-copy-live]');
            if (live) live.textContent = 'Copied ' + text;
        };
        if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text).then(done, function () { });
        else {
            var area = document.createElement('textarea');
            area.value = text;
            area.setAttribute('readonly', '');
            area.style.cssText = 'position:fixed;opacity:0';
            document.body.appendChild(area);
            area.select();
            try { if (document.execCommand('copy')) done(); } catch (err) { /* not allowed */ }
            area.remove();
        }
    });

    // ---- PDF viewer (pdf.js 6.3.289, Apache-2.0; lib/pdfjs holds its ESM builds, imported on first use) ------

    var PDFJS = LIB + 'pdfjs/pdf.min.mjs';
    var PDFJS_WORKER = LIB + 'pdfjs/pdf.worker.min.mjs';
    var pdfLib = null;

    function loadPdf() {
        if (!pdfLib) pdfLib = import(PDFJS).then(function (m) { m.GlobalWorkerOptions.workerSrc = PDFJS_WORKER; return m; });
        return pdfLib;
    }

    // One page at a time: prev / next, a page box, zoom (1 is fit to the width) and a download link.
    function pdfSync(el) {
        var s = el._mPdf;
        var count = s.doc ? s.doc.numPages : 0;
        el.querySelector('[data-m-pdf-page]').value = count ? s.page : '';
        el.querySelector('[data-m-pdf-page]').max = count || 1;
        el.querySelector('[data-m-pdf-count]').textContent = count ? String(count) : '\u2013';
        el.querySelector('[data-m-pdf-prev]').disabled = s.page <= 1;
        el.querySelector('[data-m-pdf-next]').disabled = !count || s.page >= count;
        el.querySelector('[data-m-pdf-zoom-label]').textContent = Math.round(s.zoom * 100) + '%';
        el.querySelector('[data-m-pdf-zoom-out]').disabled = s.zoom <= 0.5;
        el.querySelector('[data-m-pdf-zoom-in]').disabled = s.zoom >= 3;
    }

    function pdfRender(el) {
        var s = el._mPdf;
        if (!s.doc) return;
        var token = ++s.token;
        s.doc.getPage(s.page).then(function (page) {
            if (token !== s.token || !document.contains(el)) return null;
            var scroll = el.querySelector('[data-m-pdf-scroll]');
            var base = page.getViewport({ scale: 1 });
            var fit = Math.max(160, scroll.clientWidth - 24) / base.width;
            var css = fit * s.zoom;
            var dpr = Math.min(window.devicePixelRatio || 1, 2);
            var viewport = page.getViewport({ scale: css * dpr });
            var canvas = document.createElement('canvas');
            canvas.width = Math.floor(viewport.width);
            canvas.height = Math.floor(viewport.height);
            canvas.style.width = Math.floor(viewport.width / dpr) + 'px';
            canvas.style.height = Math.floor(viewport.height / dpr) + 'px';
            canvas.setAttribute('role', 'img');
            canvas.setAttribute('aria-label', 'Page ' + s.page + ' of ' + s.doc.numPages);
            return page.render({ canvasContext: canvas.getContext('2d'), viewport: viewport }).promise.then(function () {
                if (token !== s.token || !document.contains(el)) return;
                el.querySelector('[data-m-pdf-canvas]').replaceChildren(canvas);
                el.classList.remove('m-pdf-busy');
                pdfSync(el);
            });
        }).catch(function () { el.classList.add('m-pdf-failed'); });
        el.classList.add('m-pdf-busy');
    }

    function pdfGo(el, page) {
        var s = el._mPdf;
        if (!s.doc) return;
        s.page = Math.min(Math.max(1, page), s.doc.numPages);
        pdfSync(el);
        pdfRender(el);
    }

    function initPdf(el) {
        el.setAttribute('data-m-ready', '');
        var s = el._mPdf = { page: Math.max(1, Number(el.getAttribute('data-page')) || 1), zoom: 1, doc: null, token: 0 };
        el.addEventListener('click', function (e) {
            var t = e.target instanceof Element ? e.target : null;
            if (!t) return;
            if (t.closest('[data-m-pdf-prev]')) pdfGo(el, s.page - 1);
            else if (t.closest('[data-m-pdf-next]')) pdfGo(el, s.page + 1);
            else if (t.closest('[data-m-pdf-zoom-in]') && s.zoom < 3) { s.zoom = Math.min(3, Math.round((s.zoom + 0.25) * 100) / 100); pdfRender(el); pdfSync(el); }
            else if (t.closest('[data-m-pdf-zoom-out]') && s.zoom > 0.5) { s.zoom = Math.max(0.5, Math.round((s.zoom - 0.25) * 100) / 100); pdfRender(el); pdfSync(el); }
        });
        el.querySelector('[data-m-pdf-page]').addEventListener('change', function (e) {
            var n = parseInt(e.target.value, 10);
            if (isFinite(n)) pdfGo(el, n); else pdfSync(el);
        });
        pdfSync(el);
        loadPdf().then(function (pdfjs) {
            return pdfjs.getDocument({ url: el.getAttribute('data-src'), withCredentials: false }).promise;
        }).then(function (doc) {
            if (!document.contains(el)) return;
            s.doc = doc;
            s.page = Math.min(s.page, doc.numPages);
            el.querySelector('[data-m-pdf-loading]').hidden = true;
            pdfSync(el);
            pdfRender(el);
        }).catch(function () { el.classList.add('m-pdf-failed'); el.querySelector('[data-m-pdf-loading]').hidden = true; });
        keepAlive(el, function () { if (s.doc) s.doc.destroy(); s.token++; });
    }

    // ---- Uppy drop area (Uppy 6, MIT; lib/uppy holds one IIFE with the engine, the Dashboard and XHR upload) ----

    var UPPY = [LIB + 'uppy/uppy.js'];
    var UPPY_CSS = LIB + 'uppy/uppy.min.css';

    function splitList(text) {
        return text ? text.split(',').map(function (s) { return s.trim(); }).filter(Boolean) : null;
    }

    // Each file uploads as soon as it is dropped or picked; the limits are checked here first and again on the server.
    function initUploader(root) {
        var host = root.querySelector('[data-m-uploader-host]');
        if (!host) return;
        root.setAttribute('data-m-ready', '');
        libs([UPPY_CSS], UPPY).then(function () {
            if (!document.contains(root) || root._mUppy) return;
            var U = window.ModulusUppy;
            var maxSize = Number(root.getAttribute('data-max-size')) || 0;
            var maxCount = Number(root.getAttribute('data-max-count')) || 0;
            var uppy = new U.Uppy({
                autoProceed: true,
                restrictions: {
                    maxFileSize: maxSize || null,
                    maxNumberOfFiles: maxCount || null,
                    allowedFileTypes: splitList(root.getAttribute('data-accept'))
                }
            });
            uppy.use(U.Dashboard, {
                target: host, inline: true, height: 300, width: '100%', theme: sheetDark() ? 'dark' : 'light',
                proudlyDisplayPoweredByUppy: false, showProgressDetails: true, hideUploadButton: true,
                note: root.getAttribute('data-note') || undefined
            });
            // The antiforgery token rides along as the page's htmx header does (MVC checks it); an empty reply is a success.
            var csrf = {};
            try { csrf = JSON.parse(document.body.getAttribute('hx-headers') || '{}'); } catch (err) { /* no token */ }
            uppy.use(U.XHRUpload, {
                endpoint: root.getAttribute('data-action') || '',
                fieldName: root.getAttribute('data-name') || 'file',
                formData: true,
                headers: csrf,
                getResponseData: function () { return {}; }
            });
            var result = root.querySelector('[data-m-uploader-result]');
            uppy.on('complete', function (outcome) {
                var ok = outcome.successful.length;
                var bad = outcome.failed.length;
                if (result) result.textContent = ok + (ok === 1 ? ' file uploaded' : ' files uploaded') + (bad ? ', ' + bad + ' failed' : '');
            });
            var fallback = root.querySelector('[data-m-uploader-fallback]');
            if (fallback) fallback.remove();
            root._mUppy = uppy;
            keepAlive(root, function () { uppy.destroy(); });
        }).catch(function () { root.classList.add('m-uploader-failed'); });
    }

    window.addEventListener('modulus:theme', function () {
        document.querySelectorAll('[data-m-uploader]').forEach(function (root) {
            if (root._mUppy) root._mUppy.getPlugin('Dashboard').setOptions({ theme: sheetDark() ? 'dark' : 'light' });
        });
    });

    // ---- Image cropper (Cropper.js 2.2.0, MIT; lib/cropper holds one IIFE) ---------------------------------

    var CROPPER = [LIB + 'cropper/cropper.js'];
    var CROP_RATIOS = { free: NaN, '1': 1, '4/3': 4 / 3, '16/9': 16 / 9 };

    function initCropper(root) {
        var img = root.querySelector('[data-m-cropper-image]');
        if (!img) return;
        root.setAttribute('data-m-ready', '');
        libs([], CROPPER).then(function () {
            if (!document.contains(root) || root._mCrop) return;
            var cropper = new window.ModulusCropper(img, { container: root.querySelector('[data-m-cropper-stage]') });
            var selection = cropper.getCropperSelection();
            var picture = cropper.getCropperImage();
            if (!selection || !picture) { root.classList.add('m-cropper-failed'); return; }
            var output = root.querySelector('[data-m-cropper-box]');
            var preview = root.querySelector('[data-m-cropper-preview]');
            var value = root.querySelector('[data-m-cropper-value]');
            var buttons = root.querySelectorAll('[data-m-cropper-ratio]');
            var timer = 0;

            // The crop in the picture's natural pixels: the selection's box on screen, scaled by the picture's size.
            function crop() {
                var natural = picture.$image;
                if (!natural || !natural.naturalWidth) return null;
                var box = picture.getBoundingClientRect();
                var edge = selection.getBoundingClientRect();
                var k = natural.naturalWidth / box.width;
                return {
                    x: Math.round((edge.left - box.left) * k),
                    y: Math.round((edge.top - box.top) * k),
                    width: Math.round(edge.width * k),
                    height: Math.round(edge.height * k)
                };
            }

            function report() {
                var box = crop();
                if (!box) return;
                output.textContent = box.width + ' × ' + box.height + ' px, from ' + box.x + ', ' + box.y;
                clearTimeout(timer);
                timer = setTimeout(function () { raiseChange(value, JSON.stringify(box)); }, 200);
                selection.$toCanvas().then(function (canvas) {
                    if (!document.contains(root)) return;
                    canvas.setAttribute('role', 'img');
                    canvas.setAttribute('aria-label', 'Preview of the crop');
                    preview.replaceChildren(canvas);
                }).catch(function () { /* the preview is optional */ });
            }

            function pick(key) {
                selection.aspectRatio = CROP_RATIOS[key];
                buttons.forEach(function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-m-cropper-ratio') === key ? 'true' : 'false'); });
            }

            var start = root.getAttribute('data-ratio') || 'free';
            pick(start in CROP_RATIOS ? start : 'free');
            buttons.forEach(function (b) {
                // The shape applies on the next microtask, so the readout is read after it.
                b.addEventListener('click', function () { pick(b.getAttribute('data-m-cropper-ratio')); setTimeout(report, 0); });
            });
            selection.addEventListener('change', report);
            picture.$image.addEventListener('load', report);
            report();
            root._mCrop = cropper;
            keepAlive(root, function () { clearTimeout(timer); cropper.destroy(); });
        }).catch(function () { root.classList.add('m-cropper-failed'); });
    }

    // ---- Code editor (CodeMirror 6; lib/codemirror holds one IIFE with the language packs) ---------------

    var CODEMIRROR = [LIB + 'codemirror/codemirror.js'];

    // The text starts from the hidden input (the server put it there, escaped); edits go back to it, debounced.
    function initCode(root) {
        var host = root.querySelector('[data-m-code-host]');
        if (!host) return;
        root.setAttribute('data-m-ready', '');
        libs([], CODEMIRROR).then(function () {
            if (!document.contains(root) || root._mCode) return;
            var value = root.querySelector('[data-m-code-value]');
            var timer = 0;
            host.replaceChildren();
            var editor = window.ModulusCodeMirror.mount(host, {
                doc: value.value,
                language: root.getAttribute('data-language') || '',
                readOnly: root.hasAttribute('data-readonly'),
                dark: sheetDark(),
                label: root.getAttribute('data-label') || 'Code',
                onChange: function (text) {
                    clearTimeout(timer);
                    timer = setTimeout(function () { raiseChange(value, text); }, 300);
                }
            });
            root._mCode = editor;
            keepAlive(root, function () { clearTimeout(timer); editor.destroy(); });
        }).catch(function () { root.classList.add('m-code-failed'); });
    }

    window.addEventListener('modulus:theme', function () {
        document.querySelectorAll('[data-m-code]').forEach(function (root) {
            if (root._mCode) root._mCode.setDark(sheetDark());
        });
    });

    // ---- Start widgets as they appear ----------------------------------------------------------------------

    widget('[data-m-map]', initMap);
    widget('[data-m-vector-map]', initVectorMap);
    widget('[data-m-pdf]', initPdf);
    widget('[data-m-uploader]', initUploader);
    widget('[data-m-cropper]', initCropper);
    widget('[data-m-code]', initCode);
    widget('[data-m-editor]', initEditor);
    widget('[data-m-sheet]', initSheet);
    widget('[data-m-doc]', initDoc);
    // The signature pad draws in modulus-shell.js; Blazor's AppSignaturePad and MVC's <app-signature-pad> both start here.
    widget('[data-m-signature]', function (root) {
        root.setAttribute('data-m-ready', '');
        if (window.modulusShell && window.modulusShell.signature) window.modulusShell.signature(root);
    });

    function scan() {
        // The registry: every widget whose root appeared (and has not marked itself ready yet).
        registry.forEach(function (w) {
            document.querySelectorAll(w.selector + ':not([data-m-ready])').forEach(function (el) { w.init(el); });
        });
        // Stateless widgets: the player syncs once, the sortable lists their empty state every pass.
        document.querySelectorAll('[data-m-player]').forEach(function (p) { if (!p._mSynced) { p._mSynced = true; syncPlayer(p); } });
        document.querySelectorAll('[data-m-sortable]').forEach(syncEmpty);
        // Roots whose element left the page (Blazor navigation) release what their widget built on them.
        liveRoots = liveRoots.filter(function (el) {
            if (document.contains(el)) return true;
            try { el._mDestroy(); } catch (e) { /* already gone */ }
            return false;
        });
    }

    var queued = false;
    function queueScan() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(function () { queued = false; scan(); });
    }

    function start() {
        scan();
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) if (records[i].addedNodes.length || records[i].removedNodes.length) { queueScan(); return; }
        }).observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();
