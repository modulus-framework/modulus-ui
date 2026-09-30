/*
 * Virtual tables (<app-virtual-table>, [data-m-virtual]): only the rows in view, and a few either side, are in the DOM.
 *
 *   data-src         endpoint returning a block of rows as <tr> elements (?current={block + 1}&pageSize={block size})
 *   data-total       rows in all
 *   data-row-height  fixed row height in px
 *   data-block       rows per request
 *   template[data-m-virtual-seed]   the first block, rendered with the page, so it shows without a request
 *
 * Blocks are cached (the last 24), a block scrolled past before it arrives is cancelled, and drawing happens once per
 * animation frame. Missing rows show as skeletons until their block arrives. Works for tables swapped in by htmx
 * (htmx:load) as well as those in the first page.
 */
(function () {
    'use strict';

    var CACHE_BLOCKS = 24;
    var OVERSCAN = 8;

    function init(root) {
        if (root._mVirtual) return;
        var scroller = root.querySelector('.m-virtual-scroll');
        var body = root.querySelector('[data-m-virtual-body]');
        if (!scroller || !body) return;
        root._mVirtual = true;

        var src = root.getAttribute('data-src') || '';
        var total = parseInt(root.getAttribute('data-total'), 10) || 0;
        var rowH = parseInt(root.getAttribute('data-row-height'), 10) || 40;
        var block = parseInt(root.getAttribute('data-block'), 10) || 100;
        var head = scroller.querySelector('thead');
        var cols = Math.max(1, head ? head.querySelectorAll('tr:first-child > th').length : 1);
        var cache = new Map();    // block → [row html]
        var pending = new Map();  // block → AbortController
        var version = 0, drawn = '', frame = 0;

        var seed = root.querySelector('template[data-m-virtual-seed]');
        if (seed) keep(0, rowsOf(seed.content));

        function rowsOf(fragment) {
            return Array.prototype.map.call(fragment.querySelectorAll('tr'), function (tr) { return tr.outerHTML; });
        }

        function keep(b, rows) {
            cache.delete(b);
            cache.set(b, rows);
            while (cache.size > CACHE_BLOCKS) cache.delete(cache.keys().next().value);
            version++;
        }

        function url(b) {
            var u = new URL(src, location.href);
            u.searchParams.set('current', String(b + 1));
            u.searchParams.set('pageSize', String(block));
            u.searchParams.delete('cursor');
            return u.pathname + u.search;
        }

        function load(b) {
            if (cache.has(b) || pending.has(b)) return;
            var controller = new AbortController();
            pending.set(b, controller);
            fetch(url(b), { credentials: 'same-origin', signal: controller.signal, headers: { 'HX-Request': 'true' } })
                .then(function (res) {
                    if (!res.ok) throw new Error('HTTP ' + res.status);
                    return res.text();
                })
                .then(function (html) {
                    var t = document.createElement('template');
                    t.innerHTML = '<table><tbody>' + html + '</tbody></table>';
                    keep(b, rowsOf(t.content));
                    root.removeAttribute('data-m-error');
                    schedule();
                })
                .catch(function (e) { if (e.name !== 'AbortError') root.setAttribute('data-m-error', ''); })
                .finally(function () { if (pending.get(b) === controller) pending.delete(b); });
        }

        // aria-rowindex counts the header row as 1.
        function indexed(html, i) { return html.replace(/^<tr\b/, '<tr aria-rowindex="' + (i + 2) + '"'); }

        function skeleton(i) {
            return '<tr class="ant-table-row m-virtual-skeleton" aria-rowindex="' + (i + 2) + '" aria-busy="true"><td class="ant-table-cell" colspan="' + cols + '"><span class="m-virtual-bar"></span></td></tr>';
        }

        function pad(height) {
            return height > 0 ? '<tr class="m-virtual-pad" aria-hidden="true"><td colspan="' + cols + '" style="height:' + height + 'px"></td></tr>' : '';
        }

        function draw() {
            frame = 0;
            if (!root.isConnected) { pending.forEach(function (c) { c.abort(); }); return; }
            var headH = head ? head.offsetHeight : 0;
            var top = Math.max(0, scroller.scrollTop);
            var view = Math.max(rowH, scroller.clientHeight - headH);
            var first = Math.max(0, Math.floor(top / rowH) - OVERSCAN);
            var last = Math.min(total, Math.ceil((top + view) / rowH) + OVERSCAN);

            var needed = {};
            if (last > first) {
                for (var b = Math.floor(first / block); b <= Math.floor((last - 1) / block); b++) { needed[b] = true; load(b); }
            }
            pending.forEach(function (controller, b) { if (!needed[b]) controller.abort(); });

            var key = first + ':' + last + ':' + version;
            if (key === drawn) return;
            drawn = key;
            var html = pad(first * rowH);
            for (var i = first; i < last; i++) {
                var rows = cache.get(Math.floor(i / block));
                var row = rows && rows[i % block];
                html += row ? indexed(row, i) : skeleton(i);
            }
            html += pad((total - last) * rowH);
            if (total === 0) html = '<tr class="ant-table-placeholder"><td class="ant-table-cell" colspan="' + cols + '"><div class="m-table-empty">No data</div></td></tr>';
            body.innerHTML = html;
        }

        function schedule() { if (!frame) frame = requestAnimationFrame(draw); }

        scroller.addEventListener('scroll', schedule, { passive: true });
        if ('ResizeObserver' in window) new ResizeObserver(schedule).observe(scroller);
        draw();
    }

    function scan(root) {
        if (!root || !root.querySelectorAll) return;
        if (root.matches && root.matches('[data-m-virtual]')) init(root);
        root.querySelectorAll('[data-m-virtual]').forEach(init);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { scan(document); });
    else scan(document);
    document.addEventListener('htmx:load', function (e) { scan(e.detail && e.detail.elt); });
})();
