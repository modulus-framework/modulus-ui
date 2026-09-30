/*
 * Modulus plugins runtime — shared by Blazor, MVC and Razor Pages.
 *
 * Like modulus-shell.js it works from the markup alone: event delegation on document, plus a MutationObserver
 * that starts the widgets which need a library (maps, editor) when their element appears, so Blazor re-renders,
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
 *   [data-m-editor]        WYSIWYG editor on Quill; the HTML goes to [data-m-editor-value] with a change event.
 *   [data-m-map]           Leaflet tile map with markers (data-markers JSON) and popups; [data-m-map-go] buttons fly
 *                          to a marker.
 *   [data-m-vector-map]    jsVectorMap choropleth: region fills from data-fills, values in the tooltip.
 *   [data-m-copy]          copies its value to the clipboard on click.
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

    // ---- Editor (Quill) ------------------------------------------------------------------------------------

    var QUILL = [LIB + 'quill/quill.js'];
    var QUILL_CSS = [LIB + 'quill/quill.snow.css'];

    function editorHtml(q) {
        var html = q.getLength() <= 1 ? '' : q.getSemanticHTML();
        // Quill writes every space as &nbsp; in its semantic HTML; keep them only where they are needed.
        return html.replace(/&nbsp;/g, ' ').replace(/ {2}/g, '  ');
    }

    function initEditor(root) {
        var host = root.querySelector('.m-editor-host');
        if (!host) return;
        root.setAttribute('data-m-ready', '');
        libs(QUILL_CSS, QUILL).then(function () {
            if (!document.contains(root) || root._mQuill) return;
            var q = new window.Quill(host, {
                theme: 'snow',
                placeholder: root.getAttribute('data-placeholder') || '',
                readOnly: root.hasAttribute('data-readonly'),
                // The toolbar is our own markup (Ant Design icons); pasted or dropped image files are refused, images
                // come from a URL only, so the stored HTML stays small and passes RichText.Sanitize.
                modules: { toolbar: false, uploader: { mimetypes: [] }, history: { delay: 800, maxStack: 200 } }
            });
            root._mQuill = q;
            var value = root.querySelector('[data-m-editor-value]');
            var editable = q.root;
            editable.setAttribute('role', 'textbox');
            editable.setAttribute('aria-multiline', 'true');
            var label = root.getAttribute('data-label');
            if (label) editable.setAttribute('aria-label', label);
            var labelledBy = root.getAttribute('data-labelledby');
            if (labelledBy) editable.setAttribute('aria-labelledby', labelledBy);
            var timer = 0;
            q.on('text-change', function () {
                clearTimeout(timer);
                // Blazor gets one update per pause in typing, not one per key.
                timer = setTimeout(function () { raiseChange(value, editorHtml(q)); }, root.hasAttribute('data-m-debounce') ? 300 : 0);
                syncEditor(root, q);
            });
            q.on('selection-change', function (range) {
                root.classList.toggle('m-editor-focused', !!range);
                syncEditor(root, q);
                // Leaving the editor (for a Save button, say) sends the text at once, ahead of the click.
                if (!range) { clearTimeout(timer); if (value && value.value !== editorHtml(q)) raiseChange(value, editorHtml(q)); }
            });
            root.classList.add('m-editor-ready');
            syncEditor(root, q);
        }).catch(function () { root.classList.add('m-editor-failed'); });
    }

    // A form posts what the editors hold now, even inside the debounce pause.
    document.addEventListener('submit', function (e) {
        if (!(e.target instanceof HTMLFormElement)) return;
        e.target.querySelectorAll('[data-m-editor]').forEach(function (root) {
            var q = root._mQuill, value = root.querySelector('[data-m-editor-value]');
            if (q && value) value.value = editorHtml(q);
        });
    }, true);

    function syncEditor(root, q) {
        var range = q.getSelection();
        var f = range ? q.getFormat(range) : {};
        root.querySelectorAll('[data-m-ed]').forEach(function (b) {
            var name = b.getAttribute('data-m-ed'), arg = b.getAttribute('data-arg');
            var on = arg ? String(f[name]) === arg : !!f[name];
            b.classList.toggle('m-editor-btn-active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        var heading = root.querySelector('[data-m-ed-heading]');
        if (heading) heading.value = f.header ? String(f.header) : '';
        var count = root.querySelector('[data-m-editor-count]');
        if (count) {
            var words = q.getText().trim().split(/\s+/).filter(Boolean).length;
            count.textContent = words + (words === 1 ? ' word' : ' words');
        }
        var undo = root.querySelector('[data-m-ed="undo"]'), redo = root.querySelector('[data-m-ed="redo"]');
        if (undo) undo.disabled = !q.history.stack.undo.length;
        if (redo) redo.disabled = !q.history.stack.redo.length;
    }

    function editorOf(el) { var root = el instanceof Element && el.closest('[data-m-editor]'); return root && root._mQuill ? root : null; }

    function openBar(root, mode) {
        var q = root._mQuill;
        var bar = root.querySelector('[data-m-editor-bar]');
        if (!bar) return;
        var range = q.getSelection(true);
        bar._mRange = range;
        bar.setAttribute('data-mode', mode);
        var input = bar.querySelector('input');
        var current = mode === 'link' ? q.getFormat(range).link : '';
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
        if (focus) root._mQuill.focus();
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
        var q = root._mQuill;
        var bar = root.querySelector('[data-m-editor-bar]');
        var mode = bar.getAttribute('data-mode');
        var url = safeUrl(bar.querySelector('input').value, mode === 'image');
        var range = bar._mRange || q.getSelection(true) || { index: q.getLength() - 1, length: 0 };
        closeBar(root, true);
        if (!url) return;
        if (mode === 'image') {
            q.insertEmbed(range.index, 'image', url, 'user');
            q.setSelection(range.index + 1, 0, 'silent');
        } else if (range.length) {
            q.formatText(range.index, range.length, 'link', url, 'user');
        } else {
            q.insertText(range.index, url, 'link', url, 'user');
            q.setSelection(range.index + url.length, 0, 'silent');
        }
    }

    function editorCommand(root, name, arg) {
        var q = root._mQuill;
        if (name === 'undo') { q.history.undo(); return; }
        if (name === 'redo') { q.history.redo(); return; }
        if (name === 'link' || name === 'image') { openBar(root, name); return; }
        var range = q.getSelection(true);
        if (name === 'clean') {
            if (range.length) q.removeFormat(range.index, range.length, 'user');
            else q.formatLine(range.index, 1, { header: false, list: false, blockquote: false, 'code-block': false }, 'user');
            return;
        }
        var f = q.getFormat(range);
        if (arg) q.format(name, String(f[name]) === arg ? false : arg, 'user');
        else q.format(name, !f[name], 'user');
        syncEditor(root, q);
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
            var r = editorOf(remove), bar = r.querySelector('[data-m-editor-bar]'), range = bar._mRange;
            closeBar(r, true);
            if (range) {
                var q = r._mQuill;
                if (range.length) q.formatText(range.index, range.length, 'link', false, 'user');
                else {
                    // A caret inside a link: unlink the whole link.
                    var leaf = q.getLeaf(range.index + 1)[0], blot = leaf && leaf.parent;
                    if (blot && blot.statics && blot.statics.blotName === 'link') q.formatText(q.getIndex(blot), blot.length(), 'link', false, 'user');
                }
            }
        }
    });
    document.addEventListener('change', function (e) {
        var sel = e.target instanceof Element && e.target.closest('[data-m-ed-heading]');
        var root = sel && editorOf(sel);
        if (!root) return;
        var q = root._mQuill;
        q.focus();
        q.format('header', sel.value ? Number(sel.value) : false, 'user');
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
        root = e.target instanceof Element && e.target.closest('.ql-editor') && editorOf(e.target);
        if (root && (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); openBar(root, 'link'); }
    });

    // ---- Tile maps (Leaflet) -------------------------------------------------------------------------------

    var LEAFLET = [LIB + 'leaflet/leaflet.js'];
    var LEAFLET_CSS = [LIB + 'leaflet/leaflet.css'];
    var maps = [];

    function pin(L, tone) {
        return L.divIcon({
            className: 'm-map-pin m-map-pin-' + (tone || 'primary'),
            html: '<span class="m-map-pin-head"></span>',
            iconSize: [26, 34], iconAnchor: [13, 32], popupAnchor: [0, -28]
        });
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

    function initMap(el) {
        el.setAttribute('data-m-ready', '');
        libs(LEAFLET_CSS, LEAFLET).then(function () {
            if (!document.contains(el) || el._mMap) return;
            var L = window.L;
            var markers = json(el, 'data-markers', []);
            var center = (el.getAttribute('data-center') || '').split(',').map(Number);
            var zoom = Number(el.getAttribute('data-zoom') || 4);
            var map = L.map(el, {
                scrollWheelZoom: el.hasAttribute('data-scroll-zoom'),
                zoomControl: true,
                attributionControl: true
            });
            L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            }).addTo(map);
            var layers = markers.map(function (mk) {
                var m = L.marker([mk.lat, mk.lng], { icon: pin(L, mk.tone), title: mk.title || '', alt: mk.title || '', keyboard: true });
                m.bindPopup(popupContent(mk), { closeButton: true, autoPanPadding: [24, 24] });
                return m.addTo(map);
            });
            if (center.length === 2 && center.every(isFinite)) map.setView(center, zoom);
            else if (layers.length) map.fitBounds(L.featureGroup(layers).getBounds().pad(0.2), { maxZoom: zoom || 12 });
            else map.setView([20, 0], 2);
            el._mMap = { map: map, markers: layers };
            maps.push(el);
            if (window.ResizeObserver) new ResizeObserver(function () { map.invalidateSize(); }).observe(el);
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
        state.map.flyTo(mk.getLatLng(), Math.max(state.map.getZoom(), Number(go.getAttribute('data-zoom') || 12)), { duration: 0.8 });
        state.map.once('moveend', function () { mk.openPopup(); });
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
    }

    // Colours are read when a vector map is drawn, so it is drawn again in the other theme.
    window.addEventListener('modulus:theme', function () {
        document.querySelectorAll('[data-m-vector-map]').forEach(function (el) { if (el._mVector) drawVectorMap(el); });
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

    // ---- Start widgets as they appear ----------------------------------------------------------------------

    function scan() {
        document.querySelectorAll('[data-m-map]:not([data-m-ready])').forEach(initMap);
        document.querySelectorAll('[data-m-vector-map]:not([data-m-ready])').forEach(initVectorMap);
        document.querySelectorAll('[data-m-editor]:not([data-m-ready])').forEach(initEditor);
        document.querySelectorAll('[data-m-player]').forEach(function (p) { if (!p._mSynced) { p._mSynced = true; syncPlayer(p); } });
        document.querySelectorAll('[data-m-sortable]').forEach(syncEmpty);
        // Maps whose element left the page (Blazor navigation) release their listeners.
        maps = maps.filter(function (el) {
            if (document.contains(el)) return true;
            try { el._mMap.map.remove(); } catch (e) { /* already removed */ }
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
