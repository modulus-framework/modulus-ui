/*
 * Modulus workflow designer (an n8n / Zapier style canvas) — shared by Blazor, MVC and Razor Pages.
 *
 * Loaded on demand by modulus-plugins.js when a [data-m-flow] editor appears (so pages without one never download it). The
 * server (Modulus.Ui.Abstractions.Flow.FlowHtml) renders the nodes and connections; this script makes them interactive and keeps
 * the same DOM. Every change writes the graph as JSON to [data-m-flow-value] and raises a bubbling `change` there (Blazor
 * @onchange, htmx hx-trigger="change", a plain form post), plus `m-flow-change` on the editor with { json, graph }.
 *
 * Events on the editor (all bubble):
 *   m-flow-change   the graph changed          detail { json, graph }
 *   m-flow-select   the selection changed      detail { nodes: [ids], edge: id | null }
 *   m-flow-open     a node was opened (double click, Enter)      detail { id }
 *   m-flow-pending  a connection was let go over empty canvas    detail { from, port }   (the next node added is wired to it)
 *
 * Gestures: drag the background to pan, Ctrl/Cmd + wheel (or pinch) to zoom, the wheel pans once the editor has focus; drag a
 * node to move it (it snaps to data-grid px); drag from an output dot to an input to connect; click a connection to select it.
 * Keys: Delete removes, arrows nudge, Ctrl+Z / Ctrl+Shift+Z undo and redo, Ctrl+D duplicates, Ctrl+A selects all, D disables,
 * C then Enter on another node connects, U disconnects, + - 0 zoom and fit, Enter opens, Escape cancels.
 *
 * API: modulusFlow.setStatus(editor, nodeId, 'running'|'success'|'error'|'waiting'|'none', note) shows a run on the canvas;
 * modulusFlow.updateNode(editor, nodeId, { title, config, note, disabled }) edits a node from a settings panel (one undo step),
 * modulusFlow.getGraph(editor), modulusFlow.fit(editor).
 */
(function () {
    'use strict';

    var NODE_W = 220;
    var MIN_ZOOM = 0.1, MAX_ZOOM = 2;
    var HISTORY = 100;
    var SVG_NS = 'http://www.w3.org/2000/svg';
    var DRAG_START = 3;

    // Geometry: the same numbers as FlowGeometry in C#, so the server's first paint and this script agree.
    function nodeH(outputs) { return Math.max(64, 28 + 24 * outputs); }
    function outY(i, outputs) { return outputs <= 1 ? nodeH(outputs) / 2 : 26 + 24 * i; }
    function inY(outputs) { return nodeH(outputs) / 2; }
    function r1(v) { return Math.round(v * 10) / 10; }
    function edgePath(x1, y1, x2, y2) {
        var dx = Math.max(40, Math.abs(x2 - x1) * 0.5);
        return 'M' + r1(x1) + ',' + r1(y1) + ' C' + r1(x1 + dx) + ',' + r1(y1) + ' ' + r1(x2 - dx) + ',' + r1(y2) + ' ' + r1(x2) + ',' + r1(y2);
    }

    function svgEl(cls) {
        var el = document.createElementNS(SVG_NS, 'path');
        el.setAttribute('class', cls);
        return el;
    }

    function clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }

    function init(root) {
        if (root._mFlow) return root._mFlow;

        // The hidden inputs sit inside the editor, or beside it (data-m-flow-outside: the Blazor component renders them itself).
        var scope = root.hasAttribute('data-m-flow-outside') && root.parentElement ? root.parentElement : root;
        var input = scope.querySelector('[data-m-flow-value]');
        var viewport = root.querySelector('[data-m-flow-viewport]');
        var stage = root.querySelector('[data-m-flow-stage]');
        var svg = stage && stage.querySelector('.m-flow-edges');
        if (!viewport || !stage || !svg) return null;
        var live = root.querySelector('[data-m-flow-live]');
        var zoomLabel = root.querySelector('[data-m-flow-zoom]');
        var search = root.querySelector('[data-m-flow-search]');
        var readonly = root.hasAttribute('data-readonly');
        var grid = parseInt(root.getAttribute('data-grid'), 10) || 0;

        var catalog = {};
        try { JSON.parse(root.getAttribute('data-catalog') || '[]').forEach(function (t) { catalog[t.key] = t; }); } catch (e) { /* no catalog: nodes keep one output */ }

        // The icon markup of each type, from the palette and the nodes already drawn, so new nodes look like the server's.
        var icons = {};
        root.querySelectorAll('.m-flow-pitem, .m-flow-node').forEach(function (el) {
            var t = el.getAttribute('data-type'), ico = el.querySelector('.m-flow-ico');
            if (t && ico && !icons[t]) icons[t] = ico.innerHTML;
        });

        var state = parse(input ? input.value : '');
        var view = { x: 0, y: 0, z: 1 };
        var selected = [];       // node ids
        var selectedEdge = null; // edge id
        var undo = [], redo = [];
        var pending = null;      // { from, port }: a connection let go over empty canvas
        var kbFrom = null;       // keyboard connect source
        var gesture = null;
        var pointers = {};
        var pinch = null;
        var nodeEls = {}, edgeEls = {};
        var delBtn = null;
        var addCount = 0;
        var edgeFrame = 0;

        // ---- State -----------------------------------------------------------------------------------------------

        function parse(text) {
            try {
                var g = JSON.parse(text || '');
                if (g && typeof g === 'object') return { nodes: Array.isArray(g.nodes) ? g.nodes : [], edges: Array.isArray(g.edges) ? g.edges : [] };
            } catch (e) { /* an empty or broken value is an empty graph */ }
            return { nodes: [], edges: [] };
        }

        function ser() { return JSON.stringify({ nodes: state.nodes, edges: state.edges }); }

        function node(id) { for (var i = 0; i < state.nodes.length; i++) if (state.nodes[i].id === id) return state.nodes[i]; return null; }

        function outsOf(n) {
            var t = catalog[n.type];
            return t && t.outputs && t.outputs.length ? t.outputs : [{ id: 'out', label: '' }];
        }

        function hasInput(n) { var t = catalog[n.type]; return !t || t.hasInput !== false; }

        function portIndex(n, port) {
            var outs = outsOf(n);
            for (var i = 0; i < outs.length; i++) if (outs[i].id === port) return i;
            return 0;
        }

        function edgeD(e) {
            var a = node(e.from), b = node(e.to);
            if (!a || !b) return null;
            var ao = outsOf(a);
            return edgePath(a.x + NODE_W, a.y + outY(portIndex(a, e.fromPort || 'out'), ao.length), b.x, b.y + inY(outsOf(b).length));
        }

        function uid(prefix, taken) {
            var n = 1;
            while (taken(prefix + '-' + n)) n++;
            return prefix + '-' + n;
        }

        function nodeIdTaken(id) { return !!node(id); }
        function edgeIdTaken(id) { return state.edges.some(function (e) { return e.id === id; }); }

        // Reports an event as a DOM event on the editor and, for Blazor and htmx, as JSON in [data-m-flow-event] followed by change.
        // Selection only goes to the input when the page asked for it (data-m-flow-select): there it is a round trip per click.
        var evInput = scope.querySelector('[data-m-flow-event]');
        function emit(type, detail) {
            root.dispatchEvent(new CustomEvent('m-flow-' + type, { bubbles: true, detail: detail }));
            if (!evInput || (type === 'select' && !root.hasAttribute('data-m-flow-select'))) return;
            var payload = { type: type };
            Object.keys(detail).forEach(function (k) { payload[k] = detail[k]; });
            evInput.value = JSON.stringify(payload);
            evInput.dispatchEvent(new Event('change', { bubbles: true }));
        }

        function announce(text) { if (live) { live.textContent = ''; live.textContent = text; } }

        function titleOf(n) { return n.title || (catalog[n.type] && catalog[n.type].title) || n.type; }

        // ---- History and output ----------------------------------------------------------------------------------

        function write() {
            var json = ser();
            if (input) {
                input.value = json;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
            root.classList.toggle('m-flow-empty', state.nodes.length === 0);
            root.dispatchEvent(new CustomEvent('m-flow-change', { bubbles: true, detail: { json: json, graph: JSON.parse(json) } }));
        }

        // Runs a change as one undo step: nothing is recorded when it changed nothing.
        function change(fn) {
            var before = ser();
            fn();
            commit(before);
        }

        function commit(before) {
            if (ser() === before) return;
            undo.push(before);
            if (undo.length > HISTORY) undo.shift();
            redo = [];
            write();
            syncButtons();
        }

        function restore(json) {
            state = parse(json);
            selected = selected.filter(function (id) { return !!node(id); });
            if (selectedEdge && !edgeIdTaken(selectedEdge)) selectedEdge = null;
            renderKeepFocus();
            write();
            syncButtons();
        }

        function doUndo() {
            if (readonly || !undo.length) return;
            redo.push(ser());
            restore(undo.pop());
            announce('Undone');
        }

        function doRedo() {
            if (readonly || !redo.length) return;
            undo.push(ser());
            restore(redo.pop());
            announce('Redone');
        }

        function syncButtons() {
            var u = root.querySelector('[data-m-flow-act="undo"]'), r = root.querySelector('[data-m-flow-act="redo"]');
            if (u) u.disabled = !undo.length;
            if (r) r.disabled = !redo.length;
        }

        // ---- Drawing ---------------------------------------------------------------------------------------------

        function buildNode(n) {
            var t = catalog[n.type];
            var outs = outsOf(n);
            var el = document.createElement('div');
            el.className = 'm-flow-node' + (n.disabled ? ' m-flow-disabled' : '') + (t && t.hasInput === false ? ' m-flow-trigger' : '');
            el.setAttribute('role', 'group');
            el.tabIndex = 0;
            el.setAttribute('data-id', n.id);
            el.setAttribute('data-type', n.type);
            el.style.setProperty('--c', 'var(--m-series-' + ((t && t.slot) || 8) + ')');
            el.style.height = nodeH(outs.length) + 'px';

            var ico = document.createElement('span');
            ico.className = 'm-flow-ico';
            ico.setAttribute('aria-hidden', 'true');
            ico.innerHTML = icons[n.type] || '';
            var text = document.createElement('span');
            text.className = 'm-flow-text';
            var title = document.createElement('span');
            title.className = 'm-flow-title';
            var sub = document.createElement('span');
            sub.className = 'm-flow-sub';
            text.appendChild(title);
            text.appendChild(sub);
            var stateEl = document.createElement('span');
            stateEl.className = 'm-flow-state';
            stateEl.setAttribute('aria-hidden', 'true');
            el.appendChild(ico);
            el.appendChild(text);
            el.appendChild(stateEl);
            if (hasInput(n)) {
                var pin = document.createElement('span');
                pin.className = 'm-flow-port m-flow-in';
                pin.setAttribute('data-port', 'in');
                pin.style.top = inY(outs.length) + 'px';
                el.appendChild(pin);
            }
            outs.forEach(function (o, i) {
                var p = document.createElement('span');
                p.className = 'm-flow-port m-flow-out';
                p.setAttribute('data-port', o.id);
                p.style.top = outY(i, outs.length) + 'px';
                if (outs.length > 1) {
                    var l = document.createElement('span');
                    l.className = 'm-flow-plabel';
                    l.textContent = o.label || o.id;
                    p.appendChild(l);
                }
                el.appendChild(p);
            });
            paintNode(n, el);
            return el;
        }

        // Text, position and run status of a node's element from its state.
        function paintNode(n, el) {
            el.style.left = n.x + 'px';
            el.style.top = n.y + 'px';
            el.querySelector('.m-flow-title').textContent = titleOf(n);
            el.querySelector('.m-flow-sub').textContent = n.note || (catalog[n.type] && catalog[n.type].title) || n.type;
            el.classList.toggle('m-flow-disabled', !!n.disabled);
            var s = n.status && n.status !== 'none' ? n.status : '';
            if (s) el.setAttribute('data-status', s); else el.removeAttribute('data-status');
            el.setAttribute('aria-label', titleOf(n) + ', ' + ((catalog[n.type] && catalog[n.type].title) || n.type) + (n.disabled ? ', disabled' : '') + (s ? ', ' + s : ''));
        }

        function render() {
            Object.keys(nodeEls).forEach(function (id) { nodeEls[id].remove(); });
            nodeEls = {};
            // Not only the tracked ones: the server's own nodes are in the DOM at the first render.
            stage.querySelectorAll('.m-flow-node').forEach(function (el) { el.remove(); });
            svg.textContent = '';
            edgeEls = {};
            state.nodes.forEach(function (n) {
                var el = buildNode(n);
                nodeEls[n.id] = el;
                stage.appendChild(el);
            });
            state.edges.forEach(function (e) {
                var hit = svgEl('m-flow-edge-hit'), path = svgEl('m-flow-edge');
                hit.setAttribute('data-edge', e.id);
                path.setAttribute('data-edge', e.id);
                svg.appendChild(hit);
                svg.appendChild(path);
                edgeEls[e.id] = { hit: hit, path: path };
            });
            drawEdges();
            paintSelection();
            root.classList.toggle('m-flow-empty', state.nodes.length === 0);
        }

        // render() for edits made while the user is working: the nodes are rebuilt, so keyboard focus goes back to the same node
        // (or the canvas), or the next shortcut would never reach the editor.
        function renderKeepFocus() {
            var active = document.activeElement && document.activeElement.closest ? document.activeElement.closest('.m-flow-node') : null;
            var id = active && active.getAttribute('data-id');
            var had = root.contains(document.activeElement);
            render();
            if (!had) return;
            (id && nodeEls[id] ? nodeEls[id] : viewport).focus({ preventScroll: true });
        }

        function drawEdges() {
            state.edges.forEach(function (e) {
                var els = edgeEls[e.id];
                if (!els) return;
                var d = edgeD(e);
                if (d) { els.hit.setAttribute('d', d); els.path.setAttribute('d', d); } else { els.hit.removeAttribute('d'); els.path.removeAttribute('d'); }
            });
            placeDelete();
        }

        function drawEdgesSoon() {
            if (edgeFrame) return;
            edgeFrame = requestAnimationFrame(function () { edgeFrame = 0; drawEdges(); });
        }

        function paintSelection() {
            Object.keys(nodeEls).forEach(function (id) { nodeEls[id].classList.toggle('m-flow-selected', selected.indexOf(id) >= 0); });
            Object.keys(edgeEls).forEach(function (id) { edgeEls[id].path.classList.toggle('m-flow-edge-selected', id === selectedEdge); });
            placeDelete();
        }

        // The small remove button on the selected connection, at the middle of its curve.
        function placeDelete() {
            var e = selectedEdge && state.edges.filter(function (x) { return x.id === selectedEdge; })[0];
            var a = e && node(e.from), b = e && node(e.to);
            if (!e || !a || !b || readonly) { if (delBtn) delBtn.hidden = true; return; }
            if (!delBtn) {
                delBtn = document.createElement('button');
                delBtn.type = 'button';
                delBtn.className = 'm-flow-edge-del';
                delBtn.setAttribute('aria-label', 'Remove connection');
                delBtn.title = 'Remove connection';
                delBtn.textContent = '×';
                delBtn.addEventListener('click', function () { removeEdge(selectedEdge); });
                stage.appendChild(delBtn);
            }
            var ao = outsOf(a);
            var x1 = a.x + NODE_W, y1 = a.y + outY(portIndex(a, e.fromPort || 'out'), ao.length), x2 = b.x, y2 = b.y + inY(outsOf(b).length);
            var dx = Math.max(40, Math.abs(x2 - x1) * 0.5);
            // A cubic curve at t = 0.5 is (P0 + 3 P1 + 3 P2 + P3) / 8.
            delBtn.style.left = (x1 + 3 * (x1 + dx) + 3 * (x2 - dx) + x2) / 8 + 'px';
            delBtn.style.top = (y1 + 3 * y1 + 3 * y2 + y2) / 8 + 'px';
            delBtn.hidden = false;
        }

        function select(ids, edge, quiet) {
            selected = ids;
            selectedEdge = edge || null;
            paintSelection();
            if (!quiet) emit('select', { nodes: selected.slice(), edge: selectedEdge });
        }

        // ---- View ------------------------------------------------------------------------------------------------

        function applyView() {
            stage.style.transform = 'translate(' + view.x + 'px,' + view.y + 'px) scale(' + view.z + ')';
            var size = (grid || 20) * view.z;
            viewport.style.setProperty('--gs', size + 'px');
            viewport.classList.toggle('m-flow-nogrid', size < 9); // dots closer than this are noise
            viewport.style.setProperty('--gx', view.x + 'px');
            viewport.style.setProperty('--gy', view.y + 'px');
            if (zoomLabel) zoomLabel.textContent = Math.round(view.z * 100) + '%';
        }

        function toWorld(cx, cy) {
            var r = viewport.getBoundingClientRect();
            return { x: (cx - r.left - view.x) / view.z, y: (cy - r.top - view.y) / view.z };
        }

        function zoomAt(cx, cy, factor) {
            var r = viewport.getBoundingClientRect();
            var px = cx - r.left, py = cy - r.top;
            var z = clamp(view.z * factor, MIN_ZOOM, MAX_ZOOM);
            var k = z / view.z;
            view.x = px - (px - view.x) * k;
            view.y = py - (py - view.y) * k;
            view.z = z;
            applyView();
        }

        function zoomCenter(factor) {
            var r = viewport.getBoundingClientRect();
            zoomAt(r.left + r.width / 2, r.top + r.height / 2, factor);
        }

        var fitted = false;
        function fit() {
            var r = viewport.getBoundingClientRect();
            fitted = r.width > 0;
            if (!state.nodes.length || !r.width) { view = { x: 40, y: 40, z: 1 }; applyView(); return; }
            var x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
            state.nodes.forEach(function (n) {
                x0 = Math.min(x0, n.x); y0 = Math.min(y0, n.y);
                x1 = Math.max(x1, n.x + NODE_W); y1 = Math.max(y1, n.y + nodeH(outsOf(n).length));
            });
            var pad = 48;
            // On a phone a whole wide flow would be too small to read: stay at a readable size, start at the left, and pan.
            var floor = r.width < 600 ? 0.5 : MIN_ZOOM;
            var z = clamp(Math.min(1, (r.width - pad * 2) / (x1 - x0), (r.height - pad * 2) / (y1 - y0)), floor, 1);
            view.z = z;
            var wide = (x1 - x0) * z > r.width - pad;
            view.x = wide ? 24 - x0 * z : (r.width - (x1 - x0) * z) / 2 - x0 * z;
            view.y = (r.height - (y1 - y0) * z) / 2 - y0 * z;
            applyView();
        }

        // ---- Editing ---------------------------------------------------------------------------------------------

        function snap(v) { return grid ? Math.round(v / grid) * grid : Math.round(v); }

        function addEdge(from, port, to) {
            var a = node(from), b = node(to);
            if (!a || !b || from === to || !hasInput(b)) return false;
            if (state.edges.some(function (e) { return e.from === from && (e.fromPort || 'out') === port && e.to === to; })) return false;
            change(function () { state.edges.push({ id: uid('edge', edgeIdTaken), from: from, to: to, fromPort: port }); });
            drawNewEdges();
            return true;
        }

        function drawNewEdges() {
            state.edges.forEach(function (e) {
                if (edgeEls[e.id]) return;
                var hit = svgEl('m-flow-edge-hit'), path = svgEl('m-flow-edge');
                hit.setAttribute('data-edge', e.id);
                path.setAttribute('data-edge', e.id);
                svg.appendChild(hit);
                svg.appendChild(path);
                edgeEls[e.id] = { hit: hit, path: path };
            });
            drawEdges();
        }

        function removeEdge(id) {
            if (readonly || !id) return;
            change(function () { state.edges = state.edges.filter(function (e) { return e.id !== id; }); });
            if (edgeEls[id]) { edgeEls[id].hit.remove(); edgeEls[id].path.remove(); delete edgeEls[id]; }
            if (selectedEdge === id) select(selected, null);
            announce('Connection removed');
        }

        function addNode(type, x, y) {
            var t = catalog[type];
            if (readonly || !t) return null;
            var n = { id: uid(type.replace(/\./g, '-'), nodeIdTaken), type: type, x: snap(x), y: snap(y), config: JSON.parse(JSON.stringify(t.defaults || {})), disabled: false, status: 'none' };
            var from = pending && node(pending.from) ? pending : null;
            change(function () {
                state.nodes.push(n);
                if (from && hasInput(n)) state.edges.push({ id: uid('edge', edgeIdTaken), from: from.from, to: n.id, fromPort: from.port });
            });
            pending = null;
            root.classList.remove('m-flow-pending');
            var el = buildNode(n);
            nodeEls[n.id] = el;
            stage.appendChild(el);
            drawNewEdges();
            select([n.id], null);
            el.focus({ preventScroll: true });
            announce(titleOf(n) + ' added' + (from ? ' and connected' : ''));
            return n;
        }

        function addFromPalette(type) {
            var r = viewport.getBoundingClientRect();
            var c = toWorld(r.left + r.width / 2, r.top + r.height / 2);
            var x = c.x - NODE_W / 2 + (addCount % 6) * 24, y = c.y - 32 + (addCount % 6) * 24;
            var src = pending && node(pending.from);
            if (src) { x = src.x + 300; y = src.y + portIndex(src, pending.port) * 100; }
            addCount++;
            root.classList.remove('m-flow-palette-open'); // on a phone the palette folds away again
            addNode(type, x, y);
        }

        function removeSelected() {
            if (readonly) return;
            if (selectedEdge) { removeEdge(selectedEdge); return; }
            if (!selected.length) return;
            var ids = selected.slice(), names = ids.map(function (id) { return titleOf(node(id) || { type: id }); });
            change(function () {
                state.nodes = state.nodes.filter(function (n) { return ids.indexOf(n.id) < 0; });
                state.edges = state.edges.filter(function (e) { return ids.indexOf(e.from) < 0 && ids.indexOf(e.to) < 0; });
            });
            selected = [];
            render();
            emit('select', { nodes: [], edge: null });
            announce(names.join(', ') + ' removed');
            viewport.focus({ preventScroll: true });
        }

        function duplicateSelected() {
            if (readonly || !selected.length) return;
            var map = {}, made = [];
            change(function () {
                selected.forEach(function (id) {
                    var n = node(id);
                    if (!n) return;
                    var copy = JSON.parse(JSON.stringify(n));
                    copy.id = uid(n.type.replace(/\./g, '-'), nodeIdTaken);
                    copy.x = snap(n.x + 40);
                    copy.y = snap(n.y + 40);
                    copy.status = 'none';
                    state.nodes.push(copy);
                    map[id] = copy.id;
                    made.push(copy.id);
                });
                state.edges.slice().forEach(function (e) {
                    if (map[e.from] && map[e.to]) state.edges.push({ id: uid('edge', edgeIdTaken), from: map[e.from], to: map[e.to], fromPort: e.fromPort });
                });
            });
            renderKeepFocus();
            select(made, null);
            announce(made.length + (made.length === 1 ? ' node' : ' nodes') + ' duplicated');
        }

        function toggleDisabled() {
            if (readonly || !selected.length) return;
            change(function () {
                var on = !selected.every(function (id) { return node(id).disabled; });
                selected.forEach(function (id) { node(id).disabled = on; });
            });
            selected.forEach(function (id) { paintNode(node(id), nodeEls[id]); });
        }

        function disconnectSelected() {
            if (readonly || !selected.length) return;
            change(function () { state.edges = state.edges.filter(function (e) { return selected.indexOf(e.from) < 0 && selected.indexOf(e.to) < 0; }); });
            renderKeepFocus();
            announce('Connections removed');
        }

        function nudge(dx, dy) {
            if (readonly || !selected.length) return;
            change(function () { selected.forEach(function (id) { var n = node(id); n.x += dx; n.y += dy; }); });
            selected.forEach(function (id) { paintNode(node(id), nodeEls[id]); });
            drawEdges();
        }

        // Columns by the longest path from the start (loops ignored), like FlowGraph.AutoLayout in C#.
        function tidy() {
            if (readonly || !state.nodes.length) return;
            var ids = state.nodes.map(function (n) { return n.id; });
            var col = {}, visiting = {}, done = {}, back = {};
            ids.forEach(function (id) { col[id] = 0; });
            function walk(id) {
                visiting[id] = true;
                state.edges.filter(function (e) { return e.from === id && col[e.to] !== undefined; }).forEach(function (e) {
                    if (visiting[e.to]) back[e.id] = true; else if (!done[e.to] && !visiting[e.to]) walk(e.to);
                });
                visiting[id] = false;
                done[id] = true;
            }
            var hasIn = {};
            state.edges.forEach(function (e) { hasIn[e.to] = true; });
            ids.filter(function (id) { return !hasIn[id]; }).concat(ids).forEach(function (id) { if (!done[id]) walk(id); });
            var fwd = state.edges.filter(function (e) { return !back[e.id] && e.from !== e.to && col[e.from] !== undefined && col[e.to] !== undefined; });
            var indeg = {}, queue = [];
            ids.forEach(function (id) { indeg[id] = 0; });
            fwd.forEach(function (e) { indeg[e.to]++; });
            ids.forEach(function (id) { if (!indeg[id]) queue.push(id); });
            while (queue.length) {
                var n = queue.shift();
                fwd.filter(function (e) { return e.from === n; }).forEach(function (e) {
                    col[e.to] = Math.max(col[e.to], col[n] + 1);
                    if (--indeg[e.to] === 0) queue.push(e.to);
                });
            }
            var rows = {};
            change(function () {
                state.nodes.forEach(function (n) {
                    var c = col[n.id], r = rows[c] || 0;
                    rows[c] = r + 1;
                    n.x = 40 + c * 300;
                    n.y = 40 + r * 120;
                });
            });
            state.nodes.forEach(function (n) { paintNode(n, nodeEls[n.id]); });
            drawEdges();
            fit();
            announce('Layout tidied');
        }

        // ---- Pointer gestures ------------------------------------------------------------------------------------

        function onDown(e) {
            if (e.target.closest('.m-flow-edge-del')) return;
            pointers[e.pointerId] = { x: e.clientX, y: e.clientY };
            if (Object.keys(pointers).length === 2) { startPinch(); return; }
            if (e.pointerType === 'mouse' && e.button !== 0 && e.button !== 1) return;
            try { viewport.setPointerCapture(e.pointerId); } catch (err) { /* synthetic pointer */ }

            var port = e.target.closest('.m-flow-port');
            var nodeEl = e.target.closest('.m-flow-node');
            var hit = e.target.closest('.m-flow-edge-hit');
            var base = { pid: e.pointerId, sx: e.clientX, sy: e.clientY, moved: false };

            if (e.button === 1) { e.preventDefault(); gesture = Object.assign(base, { type: 'pan', ox: view.x, oy: view.y }); return; }
            if (port && nodeEl && port.classList.contains('m-flow-out') && !readonly) {
                e.preventDefault();
                gesture = Object.assign(base, { type: 'connect', from: nodeEl.getAttribute('data-id'), port: port.getAttribute('data-port'), temp: null });
                return;
            }
            if (nodeEl) {
                var id = nodeEl.getAttribute('data-id');
                var additive = e.shiftKey || e.ctrlKey || e.metaKey;
                if (additive) select(selected.indexOf(id) >= 0 ? selected.filter(function (x) { return x !== id; }) : selected.concat(id), null);
                else if (selected.indexOf(id) < 0) select([id], null);
                nodeEl.focus({ preventScroll: true });
                if (!readonly && selected.indexOf(id) >= 0) {
                    gesture = Object.assign(base, {
                        type: 'node', id: id, additive: additive, before: ser(),
                        origins: selected.map(function (sid) { var n = node(sid); return { id: sid, x: n.x, y: n.y }; })
                    });
                }
                return;
            }
            if (hit) {
                select([], hit.getAttribute('data-edge'));
                viewport.focus({ preventScroll: true });
                return;
            }
            // The background: pan, and clear the selection if the pointer does not move.
            e.preventDefault();
            viewport.focus({ preventScroll: true });
            gesture = Object.assign(base, { type: 'pan', ox: view.x, oy: view.y, clear: true });
        }

        function onMove(e) {
            if (pointers[e.pointerId]) { pointers[e.pointerId].x = e.clientX; pointers[e.pointerId].y = e.clientY; }
            if (pinch) { movePinch(); return; }
            if (!gesture || gesture.pid !== e.pointerId) return;
            var dx = e.clientX - gesture.sx, dy = e.clientY - gesture.sy;
            if (!gesture.moved && Math.abs(dx) < DRAG_START && Math.abs(dy) < DRAG_START) return;
            gesture.moved = true;

            if (gesture.type === 'pan') {
                view.x = gesture.ox + dx;
                view.y = gesture.oy + dy;
                applyView();
            } else if (gesture.type === 'node') {
                var primary = gesture.origins.filter(function (o) { return o.id === gesture.id; })[0];
                var px = snap(primary.x + dx / view.z), py = snap(primary.y + dy / view.z);
                var mx = px - primary.x, my = py - primary.y;
                gesture.origins.forEach(function (o) {
                    var n = node(o.id);
                    n.x = o.x + mx;
                    n.y = o.y + my;
                    paintNode(n, nodeEls[o.id]);
                });
                drawEdgesSoon();
            } else if (gesture.type === 'connect') {
                moveConnect(e);
            }
        }

        function moveConnect(e) {
            var from = node(gesture.from);
            if (!from) return;
            if (!gesture.temp) {
                gesture.temp = svgEl('m-flow-edge m-flow-edge-temp');
                svg.appendChild(gesture.temp);
                root.classList.add('m-flow-connecting');
            }
            var w = toWorld(e.clientX, e.clientY);
            var ao = outsOf(from);
            var x1 = from.x + NODE_W, y1 = from.y + outY(portIndex(from, gesture.port), ao.length);
            var under = document.elementFromPoint(e.clientX, e.clientY);
            var targetEl = under && under.closest ? under.closest('.m-flow-node') : null;
            var tid = targetEl ? targetEl.getAttribute('data-id') : null;
            var ok = tid && tid !== gesture.from && hasInput(node(tid));
            Object.keys(nodeEls).forEach(function (id) {
                nodeEls[id].classList.toggle('m-flow-target', !!ok && id === tid);
                nodeEls[id].classList.toggle('m-flow-target-bad', !!tid && !ok && id === tid);
            });
            gesture.target = ok ? tid : null;
            // Snap the end to the input dot when over a valid node.
            var end = ok ? { x: node(tid).x, y: node(tid).y + inY(outsOf(node(tid)).length) } : w;
            gesture.temp.setAttribute('d', edgePath(x1, y1, end.x, end.y));
        }

        function onUp(e) {
            delete pointers[e.pointerId];
            if (pinch) { if (Object.keys(pointers).length < 2) pinch = null; gesture = null; return; }
            if (!gesture || gesture.pid !== e.pointerId) return;
            var g = gesture;
            gesture = null;
            try { viewport.releasePointerCapture(e.pointerId); } catch (err) { /* already released */ }

            if (g.type === 'node') {
                if (g.moved) commit(g.before);
                else if (!g.additive && selected.length > 1) select([g.id], null);
            } else if (g.type === 'connect') {
                if (g.temp) g.temp.remove();
                root.classList.remove('m-flow-connecting');
                Object.keys(nodeEls).forEach(function (id) { nodeEls[id].classList.remove('m-flow-target', 'm-flow-target-bad'); });
                if (g.target) {
                    if (addEdge(g.from, g.port, g.target)) announce('Connected ' + titleOf(node(g.from)) + ' to ' + titleOf(node(g.target)));
                } else {
                    // Let go over empty canvas (or a click on the dot): the next node picked is wired to this output.
                    pending = { from: g.from, port: g.port };
                    root.classList.add('m-flow-pending');
                    emit('pending', { from: g.from, port: g.port });
                    announce('Pick a node to connect it to ' + titleOf(node(g.from)));
                    if (search) search.focus({ preventScroll: true });
                }
            } else if (g.type === 'pan' && !g.moved && g.clear) {
                select([], null);
                if (pending) { pending = null; root.classList.remove('m-flow-pending'); }
            }
        }

        function startPinch() {
            var ids = Object.keys(pointers);
            // A node being dragged when the second finger lands keeps the move, as one undo step.
            if (gesture && gesture.type === 'node' && gesture.moved) commit(gesture.before);
            if (gesture && gesture.temp) { gesture.temp.remove(); root.classList.remove('m-flow-connecting'); }
            gesture = null;
            pinch = { d: dist(pointers[ids[0]], pointers[ids[1]]) };
        }

        function dist(a, b) { return Math.hypot(a.x - b.x, a.y - b.y) || 1; }

        function movePinch() {
            var ids = Object.keys(pointers);
            if (ids.length < 2) return;
            var a = pointers[ids[0]], b = pointers[ids[1]], d = dist(a, b);
            zoomAt((a.x + b.x) / 2, (a.y + b.y) / 2, d / pinch.d);
            pinch.d = d;
        }

        // ---- Events ----------------------------------------------------------------------------------------------

        viewport.addEventListener('pointerdown', onDown);
        viewport.addEventListener('pointermove', onMove);
        viewport.addEventListener('pointerup', onUp);
        viewport.addEventListener('pointercancel', onUp);

        viewport.addEventListener('wheel', function (e) {
            if (e.ctrlKey || e.metaKey) {
                e.preventDefault();
                zoomAt(e.clientX, e.clientY, Math.exp(-e.deltaY * 0.0015));
            } else if (root.contains(document.activeElement)) {
                // Scrolling pans once the editor has focus; before that the wheel still scrolls the page.
                e.preventDefault();
                view.x -= e.shiftKey ? e.deltaY : e.deltaX;
                view.y -= e.shiftKey ? 0 : e.deltaY;
                applyView();
            }
        }, { passive: false });

        viewport.addEventListener('dblclick', function (e) {
            // Pointer capture makes the canvas the event target, so the node is found from the pointer position.
            var under = document.elementFromPoint(e.clientX, e.clientY);
            var el = under && under.closest ? under.closest('.m-flow-node') : null;
            if (el && root.contains(el)) emit('open', { id: el.getAttribute('data-id') });
        });

        viewport.addEventListener('dragover', function (e) {
            if (readonly || !e.dataTransfer || Array.prototype.indexOf.call(e.dataTransfer.types, 'text/plain') < 0) return;
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        });
        viewport.addEventListener('drop', function (e) {
            if (readonly || !e.dataTransfer) return;
            var text = e.dataTransfer.getData('text/plain') || '';
            if (text.indexOf('m-flow:') !== 0) return;
            e.preventDefault();
            var w = toWorld(e.clientX, e.clientY);
            addNode(text.slice(7), w.x - NODE_W / 2, w.y - 32);
        });

        root.addEventListener('dragstart', function (e) {
            var item = e.target.closest && e.target.closest('.m-flow-pitem');
            if (!item || !e.dataTransfer) return;
            e.dataTransfer.setData('text/plain', 'm-flow:' + item.getAttribute('data-type'));
            e.dataTransfer.effectAllowed = 'copy';
        });

        root.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-m-flow-act]');
            if (btn) {
                switch (btn.getAttribute('data-m-flow-act')) {
                    case 'zoom-in': zoomCenter(1.2); break;
                    case 'zoom-out': zoomCenter(1 / 1.2); break;
                    case 'fit': fit(); break;
                    case 'undo': doUndo(); break;
                    case 'redo': doRedo(); break;
                    case 'layout': tidy(); break;
                    case 'palette':
                        root.classList.toggle('m-flow-palette-open');
                        if (root.classList.contains('m-flow-palette-open') && search) search.focus({ preventScroll: true });
                        break;
                }
                return;
            }
            var item = e.target.closest('.m-flow-pitem');
            if (item) addFromPalette(item.getAttribute('data-type'));
        });

        if (search) {
            search.addEventListener('input', function () {
                var q = search.value.trim().toLowerCase(), any = false;
                root.querySelectorAll('.m-flow-pitem').forEach(function (it) {
                    var show = !q || it.getAttribute('data-search').indexOf(q) >= 0;
                    it.hidden = !show;
                    any = any || show;
                });
                root.querySelectorAll('.m-flow-pgroup').forEach(function (g) { g.hidden = !g.querySelector('.m-flow-pitem:not([hidden])'); });
                var none = root.querySelector('.m-flow-none');
                if (none) none.hidden = any;
            });
            // The search box lives inside a form: its own change must not look like a change of the graph, and Enter adds the first match.
            search.addEventListener('change', function (e) { e.stopPropagation(); });
            search.addEventListener('keydown', function (e) {
                if (e.key !== 'Enter') return;
                e.preventDefault();
                var first = root.querySelector('.m-flow-pitem:not([hidden])');
                if (first) addFromPalette(first.getAttribute('data-type'));
            });
        }

        root.addEventListener('keydown', function (e) {
            var t = e.target;
            if (t === search || (t.closest && t.closest('input, textarea, select, [contenteditable]'))) return;
            var mod = e.ctrlKey || e.metaKey;
            var key = e.key;
            var onNode = t.closest && t.closest('.m-flow-node');
            var step = grid || 10;

            if (mod && (key === 'z' || key === 'Z')) { e.preventDefault(); if (e.shiftKey) doRedo(); else doUndo(); return; }
            if (mod && (key === 'y' || key === 'Y')) { e.preventDefault(); doRedo(); return; }
            if (mod && (key === 'a' || key === 'A')) { e.preventDefault(); select(state.nodes.map(function (n) { return n.id; }), null); return; }
            if (mod && (key === 'd' || key === 'D')) { e.preventDefault(); duplicateSelected(); return; }
            if (mod) return;

            if (key === 'Delete' || key === 'Backspace') { if (selected.length || selectedEdge) { e.preventDefault(); removeSelected(); } return; }
            if (key === 'Escape') {
                kbFrom = null;
                if (pending) { pending = null; root.classList.remove('m-flow-pending'); }
                select([], null);
                announce('Cancelled');
                return;
            }
            if (key === 'Enter' && onNode) {
                var id = onNode.getAttribute('data-id');
                e.preventDefault();
                if (kbFrom && kbFrom !== id) {
                    var src = node(kbFrom);
                    if (src && addEdge(kbFrom, outsOf(src)[0].id, id)) announce('Connected ' + titleOf(src) + ' to ' + titleOf(node(id)));
                    else announce('Cannot connect these nodes');
                    kbFrom = null;
                } else {
                    emit('open', { id: id });
                }
                return;
            }
            if ((key === 'c' || key === 'C') && onNode && !readonly) {
                kbFrom = onNode.getAttribute('data-id');
                announce('Connecting from ' + titleOf(node(kbFrom)) + '. Move to another node and press Enter. Escape cancels.');
                return;
            }
            if ((key === 'd' || key === 'D') && !readonly) { toggleDisabled(); return; }
            if ((key === 'u' || key === 'U') && !readonly) { disconnectSelected(); return; }
            if (key.indexOf('Arrow') === 0 && selected.length && !readonly) {
                e.preventDefault();
                var m = e.shiftKey ? step * 5 : step;
                nudge(key === 'ArrowLeft' ? -m : key === 'ArrowRight' ? m : 0, key === 'ArrowUp' ? -m : key === 'ArrowDown' ? m : 0);
                return;
            }
            if (key === '+' || key === '=') { zoomCenter(1.2); return; }
            if (key === '-') { zoomCenter(1 / 1.2); return; }
            if (key === '0') { fit(); }
        });

        // ---- API -------------------------------------------------------------------------------------------------

        var api = {
            getGraph: function () { return JSON.parse(ser()); },
            fit: fit,
            // Changes a node's title, settings, note or disabled flag from the page (a settings panel), as one undo step.
            updateNode: function (id, patch) {
                var n = node(id);
                if (!n || readonly || !patch) return false;
                change(function () {
                    ['title', 'note', 'disabled'].forEach(function (k) { if (patch[k] != null) n[k] = patch[k] === '' ? undefined : patch[k]; });
                    if (patch.config) n.config = JSON.parse(JSON.stringify(patch.config));
                });
                paintNode(n, nodeEls[id]);
                return true;
            },
            setStatus: function (id, status, note) {
                var n = node(id);
                if (!n) return;
                n.status = status || 'none';
                if (note !== undefined) n.note = note || undefined;
                if (nodeEls[id]) paintNode(n, nodeEls[id]);
            }
        };
        root._mFlow = api;
        // An editor that starts hidden (a closed tab) has no size yet: it fits itself the first time it has one.
        if (window.ResizeObserver) new ResizeObserver(function () { if (!fitted && viewport.clientWidth) fit(); }).observe(viewport);

        render();
        applyView();
        fit();
        syncButtons();
        root.classList.add('m-flow-ready');
        root.style.opacity = '';
        return api;
    }

    // The editor itself, or an element around it (the Blazor component's wrapper).
    function of(el) {
        var root = el && (el.hasAttribute && el.hasAttribute('data-m-flow') ? el : el && el.querySelector && el.querySelector('[data-m-flow]'));
        return root && (root._mFlow || init(root));
    }

    window.modulusFlow = {
        init: init,
        getGraph: function (root) { var f = of(root); return f ? f.getGraph() : null; },
        fit: function (root) { var f = of(root); if (f) f.fit(); },
        setStatus: function (root, id, status, note) { var f = of(root); if (f) f.setStatus(id, status, note); },
        updateNode: function (root, id, patch) { var f = of(root); return f ? f.updateNode(id, patch) : false; }
    };
})();
