/*
 * Modulus SPA navigation for MVC / Razor Pages (htmx). Loaded by <modulus-scripts /> after htmx and modulus-mvc.js.
 *
 * Page hooks (always available, SPA navigation on or off):
 *   Modulus.onPage(fn)   every page view: at load and after each navigation. fn(main) may return a cleanup function,
 *                        which runs when the user leaves the page. For scripts every page loads.
 *   Modulus.page(fn)     this page view only: runs once the DOM is ready, its cleanup when the user leaves. For a
 *                        page's own @section Scripts.
 *
 * With ModulusThemeOptions.SpaNavigation (<body hx-boost data-m-spa>), links and forms in the shell load through htmx:
 *   - the server answers with the page body and the navigation only (_ModulusLayout, X-Modulus-Fragment), swapped
 *     into #main with a view transition; anything else (a page outside the shell, a file, an error page) loads as a
 *     whole page instead
 *   - a newer navigation aborts the one in flight; a progress bar shows when a page takes longer than 120 ms
 *   - data-m-prefetch: a link the pointer rests on (65 ms) or touches is fetched ahead and used once if clicked
 *     within 30 s; any POST, PUT, PATCH or DELETE, or a modulus:invalidate event, clears what was fetched
 *   - back and forward reload the page body from the server (.m-page is the history element) and restore the scroll
 *     position; htmx keeps no snapshots, since Alpine-rendered DOM can't be restored from one
 *   - after each page: the drawer, modals, the navigation drawer and the launcher close, the sidebar keeps its scroll
 *     position and open groups, focus moves to #main and the title is announced to screen readers
 *   - a[download] and [data-m-reload] links always load normally
 */
(function () {
    'use strict';

    // Page hooks ------------------------------------------------------------------------------------------------

    var everyPage = [];   // Modulus.onPage
    var thisPage = [];    // Modulus.page, cleared when the user leaves the page
    var cleanups = [];
    var started = false;

    function run(fn) {
        try {
            var cleanup = fn(document.getElementById('main') || document.body);
            if (typeof cleanup === 'function') cleanups.push(cleanup);
        } catch (e) {
            if (window.console) console.error(e);
        }
    }

    function enterPage() {
        started = true;
        everyPage.forEach(run);
        thisPage.forEach(run);
    }

    function leavePage() {
        var pending = cleanups;
        cleanups = [];
        thisPage = [];
        pending.forEach(function (fn) { try { fn(); } catch (e) { if (window.console) console.error(e); } });
    }

    var Modulus = window.Modulus = window.Modulus || {};
    Modulus.onPage = function (fn) {
        everyPage.push(fn);
        if (started) run(fn);
    };
    Modulus.page = function (fn) {
        // Registered after the page is in (a script in a swapped page body): run it now, once.
        if (started) { thisPage.push(fn); run(fn); return; }
        thisPage.push(fn);
    };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', enterPage); else enterPage();

    var body = document.body;
    var htmx = window.htmx;
    if (!body || !htmx || !body.hasAttribute('data-m-spa')) return;

    // Configuration ---------------------------------------------------------------------------------------------

    htmx.config.historyCacheSize = 0;
    htmx.config.refreshOnHistoryMiss = false;
    htmx.config.scrollIntoViewOnBoost = false;
    if ('scrollRestoration' in history) history.scrollRestoration = 'manual';

    var FRAGMENT = 'X-Modulus-Fragment';
    var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

    function main() { return document.getElementById('main'); }
    function currentApp() { var m = main(); return m ? m.getAttribute('data-m-app') || '' : ''; }

    function boosted(d) {
        return !!(d && (d.boosted || (d.requestConfig && d.requestConfig.boosted) || (d.headers && d.headers['HX-Boosted'] === 'true')));
    }

    // A page navigation: the link or form's nearest hx-boost is <body>. A boost area of its own (a list's filters and pager,
    // with hx-target and hx-select) is an update within the page and keeps its own targeting.
    function pageLevel(elt) {
        var boost = elt && elt.closest ? elt.closest('[hx-boost]') : null;
        return boost === body;
    }

    var MAIN_SWAP = 'outerHTML show:window:top';

    function hardNavigate(url, replace) {
        url = url || window.location.href;
        if (replace) window.location.replace(url); else window.location.assign(url);
    }

    // Progress bar ----------------------------------------------------------------------------------------------

    var bar = document.createElement('div');
    bar.className = 'm-nav-progress';
    bar.setAttribute('aria-hidden', 'true');
    bar.appendChild(document.createElement('div'));
    body.appendChild(bar);
    var barTimer = 0, barHide = 0;

    function progressStart() {
        clearTimeout(barTimer);
        clearTimeout(barHide);
        bar.classList.remove('m-done');
        barTimer = setTimeout(function () {
            bar.classList.remove('m-active');
            void bar.offsetWidth; // restart the animation
            bar.classList.add('m-active');
        }, 120);
    }

    function progressDone() {
        clearTimeout(barTimer);
        if (!bar.classList.contains('m-active')) return;
        bar.classList.add('m-done');
        barHide = setTimeout(function () { bar.classList.remove('m-active', 'm-done'); }, 300);
    }

    // Screen reader announcement of the new page ----------------------------------------------------------------

    var announcer = document.createElement('div');
    announcer.className = 'm-sr-only';
    announcer.setAttribute('aria-live', 'assertive');
    announcer.setAttribute('aria-atomic', 'true');
    body.appendChild(announcer);

    // Scroll positions, for back and forward ----------------------------------------------------------------------

    var SCROLL_KEY = 'modulus-spa-scroll';

    function scrollStore() {
        try { return JSON.parse(sessionStorage.getItem(SCROLL_KEY) || '{}'); } catch (e) { return {}; }
    }

    // Where the page on screen is scrolled to. Back and forward change the URL before htmx tells us, so the position
    // is tracked as the user scrolls, not read when leaving.
    var here = { href: location.href, y: window.scrollY };
    window.addEventListener('scroll', function () { here.href = location.href; here.y = window.scrollY; }, { passive: true });

    function saveScroll(href, y) {
        var store = scrollStore();
        delete store[href];
        store[href] = y;
        var keys = Object.keys(store);
        for (var i = 0; i < keys.length - 50; i++) delete store[keys[i]];
        try { sessionStorage.setItem(SCROLL_KEY, JSON.stringify(store)); } catch (e) { /* storage full or blocked */ }
    }

    function restoreScroll() {
        var y = scrollStore()[location.href];
        window.scrollTo(0, typeof y === 'number' ? y : 0);
    }

    // Sidebar state across a swap of the navigation ---------------------------------------------------------------

    function sidebarState() {
        var sidebar = document.getElementById('m-sidebar');
        if (!sidebar) return null;
        return {
            app: currentApp(),
            scroll: Array.prototype.map.call(sidebar.querySelectorAll('.m-sidebar-nav'), function (n) { return n.scrollTop; }),
            open: Array.prototype.map.call(sidebar.querySelectorAll('details.m-snav-group'), function (d) { return d.open; })
        };
    }

    function restoreSidebar(state) {
        var sidebar = document.getElementById('m-sidebar');
        // Another app has another menu: its state doesn't carry over.
        if (!state || !sidebar || state.app !== currentApp()) return;
        sidebar.querySelectorAll('details.m-snav-group').forEach(function (d, i) { if (state.open[i]) d.open = true; });
        sidebar.querySelectorAll('.m-sidebar-nav').forEach(function (n, i) { if (state.scroll[i]) n.scrollTop = state.scroll[i]; });
    }

    // Leaving and entering a page ---------------------------------------------------------------------------------

    var pendingSidebar = null;

    // Overlays that belong to the old page: modals and the drawer (closing them also releases the scroll lock).
    function closeOverlays() {
        window.dispatchEvent(new CustomEvent('modulus:modal-close'));
        if (window.Alpine && window.Alpine.store('drawer')) window.Alpine.store('drawer').hide();
    }

    function beforeLeave() {
        saveScroll(location.href, window.scrollY);
        pendingSidebar = sidebarState();
        closeOverlays();
        leavePage();
    }

    function afterEnter() {
        var shell = window.modulusShell || {};
        if (shell.closeNav) shell.closeNav();
        if (shell.closeLauncher) shell.closeLauncher();
        restoreSidebar(pendingSidebar);
        pendingSidebar = null;
        var m = main();
        if (m) m.focus({ preventScroll: true });
        announcer.textContent = document.title;
        here = { href: location.href, y: window.scrollY };
        enterPage();
        progressDone();
        setTimeout(function () { document.documentElement.classList.remove('m-vt'); }, 400);
    }

    // The page body is named for the view transition only while a navigation swaps it (see modulus-shell.css).
    document.addEventListener('htmx:beforeTransition', function () { document.documentElement.classList.add('m-vt'); });

    // Files the page already has are not loaded again (a page's @section Scripts and Head come with every fragment).
    var ASSET = /<script\b[^>]*\bsrc="([^"]+)"[^>]*>\s*<\/script>|<link\b[^>]*\brel="stylesheet"[^>]*>/gi;

    function withoutLoadedAssets(html) {
        return html.replace(ASSET, function (tag, src) {
            if (src) return document.querySelector('script[src="' + CSS.escape(src) + '"]') ? '' : tag;
            var href = /\bhref="([^"]+)"/i.exec(tag);
            return href && document.querySelector('link[rel="stylesheet"][href="' + CSS.escape(href[1]) + '"]') ? '' : tag;
        });
    }

    // Prefetch --------------------------------------------------------------------------------------------------

    var prefetchOn = body.hasAttribute('data-m-prefetch') && !(navigator.connection && navigator.connection.saveData);
    var cache = new Map();   // url → { html, app, time }
    var CACHE_TTL = 30000, CACHE_SIZE = 10;
    var hoverTimer = 0, inflight = null;

    function sameOriginUrl(a) {
        try {
            var url = new URL(a.href, location.href);
            return url.origin === location.origin ? url : null;
        } catch (e) { return null; }
    }

    function boostable(a) {
        if (!(a instanceof HTMLAnchorElement) || !a.hasAttribute('href')) return null;
        if (!pageLevel(a) || body.getAttribute('hx-boost') !== 'true') return null;
        if (a.hasAttribute('download') || a.closest('[data-m-reload]') || (a.target && a.target !== '_self')) return null;
        if (/^(#|javascript:|mailto:|tel:)/i.test(a.getAttribute('href'))) return null;
        if (['hx-get', 'hx-post', 'hx-put', 'hx-patch', 'hx-delete'].some(function (n) { return a.hasAttribute(n); })) return null;
        var url = sameOriginUrl(a);
        if (!url || (url.pathname === location.pathname && url.search === location.search)) return null;
        return url;
    }

    function cached(path) {
        var url;
        try { url = new URL(path, location.href); } catch (e) { return null; }
        url = url.pathname + url.search;
        var entry = cache.get(url);
        if (!entry) return null;
        cache.delete(url); // used once
        return Date.now() - entry.time < CACHE_TTL && entry.app === currentApp() ? entry : null;
    }

    function prefetch(url) {
        var key = url.pathname + url.search;
        if (cache.has(key) || (inflight && inflight.key === key)) return;
        if (inflight) inflight.controller.abort();
        var controller = new AbortController();
        inflight = { key: key, controller: controller };
        var app = currentApp();
        fetch(key, {
            credentials: 'same-origin',
            signal: controller.signal,
            headers: { 'HX-Request': 'true', 'HX-Boosted': 'true', 'HX-Current-URL': location.href, 'X-Modulus-App': app, 'X-Modulus-Prefetch': '1' }
        }).then(function (res) {
            // Only a plain page fragment is kept: not a redirect, and nothing the server wanted to happen (HX-Trigger).
            if (!res.ok || res.redirected || !res.headers.get(FRAGMENT) || res.headers.get('HX-Trigger')) return null;
            return res.text();
        }).then(function (html) {
            if (html == null) return;
            cache.set(key, { html: html, app: app, time: Date.now() });
            while (cache.size > CACHE_SIZE) cache.delete(cache.keys().next().value);
        }).catch(function () { /* aborted or offline: the click loads it normally */ })
          .finally(function () { if (inflight && inflight.controller === controller) inflight = null; });
    }

    if (prefetchOn) {
        document.addEventListener('mouseover', function (e) {
            var a = e.target instanceof Element ? e.target.closest('a') : null;
            var url = a && boostable(a);
            clearTimeout(hoverTimer);
            if (url) hoverTimer = setTimeout(function () { prefetch(url); }, 65);
        }, { passive: true });
        document.addEventListener('touchstart', function (e) {
            var a = e.target instanceof Element ? e.target.closest('a') : null;
            var url = a && boostable(a);
            if (url) prefetch(url);
        }, { passive: true });
    }

    function invalidate() { cache.clear(); }
    document.addEventListener('modulus:invalidate', invalidate);

    // Shows a prefetched fragment the way htmx would have shown the response.
    function showCached(entry, url) {
        beforeLeave();
        history.pushState({ htmx: true }, '', url);
        var title = /<title>([^<]*)<\/title>/i.exec(entry.html);
        htmx.swap('#main', withoutLoadedAssets(entry.html),
            { swapStyle: 'outerHTML', swapDelay: 0, settleDelay: 20, transition: !reduceMotion.matches, scroll: 'top', scrollTarget: 'window' },
            {
                select: '#main',
                afterSettleCallback: function () {
                    if (title) { var t = document.createElement('textarea'); t.innerHTML = title[1]; document.title = t.value; }
                    window.scrollTo(0, 0);
                    afterEnter();
                }
            });
    }

    // htmx lifecycle ----------------------------------------------------------------------------------------------

    var navigation = null; // the boosted request in flight

    document.addEventListener('htmx:configRequest', function (e) {
        var d = e.detail;
        if (d.verb !== 'get') invalidate();
        if (!boosted(d)) return;
        var elt = d.elt;
        if (elt && elt.matches && elt.matches('a[download], [data-m-reload] a, a[data-m-reload]')) {
            e.preventDefault();
            hardNavigate(elt.href);
            return;
        }
        d.headers['X-Modulus-App'] = currentApp();
        if (d.verb === 'get' && pageLevel(elt)) {
            var entry = cached(d.path);
            if (entry) {
                e.preventDefault();
                if (navigation) { navigation.abort(); navigation = null; }
                showCached(entry, d.path);
            }
        }
    });

    document.addEventListener('htmx:beforeRequest', function (e) {
        if (!boosted(e.detail) || !pageLevel(e.detail.elt)) return;
        if (navigation && navigation !== e.detail.xhr) navigation.abort();
        navigation = e.detail.xhr;
        progressStart();
    });

    document.addEventListener('htmx:beforeSwap', function (e) {
        var d = e.detail;
        if (!boosted(d)) return;
        var xhr = d.xhr, get = (d.requestConfig ? d.requestConfig.verb : 'get') === 'get';
        if (xhr.status >= 400) {
            // An error page for a page link is a page of its own; a failed form post stays and shows the error toast.
            if (get && pageLevel(d.elt)) { d.shouldSwap = false; hardNavigate(xhr.responseURL); }
            return;
        }
        if (!xhr.getResponseHeader(FRAGMENT)) {
            // Not a shell page: sign-in, a status page, a file.
            d.shouldSwap = false;
            hardNavigate(xhr.responseURL);
            return;
        }
        d.serverResponse = withoutLoadedAssets(d.serverResponse);
        if (!pageLevel(d.elt)) return; // a boost area's own update: its hx-target and hx-select apply
        d.target = main();
        d.selectOverride = '#main';
        d.swapOverride = MAIN_SWAP + (reduceMotion.matches ? '' : ' transition:true');
        beforeLeave();
    });

    document.addEventListener('htmx:afterSettle', function (e) {
        if (!boosted(e.detail) || e.detail.xhr !== navigation) return;
        navigation = null;
        afterEnter();
    });

    document.addEventListener('htmx:afterRequest', function (e) {
        if (boosted(e.detail) && !e.detail.successful) { progressDone(); if (e.detail.xhr === navigation) navigation = null; }
    });
    document.addEventListener('htmx:sendAbort', progressDone);
    document.addEventListener('htmx:sendError', progressDone);

    // Back and forward: the history element (.m-page) reloads from the server.
    document.addEventListener('htmx:historyCacheMiss', function () {
        if (here.href !== location.href) saveScroll(here.href, here.y);
        closeOverlays();
        leavePage();
        progressStart();
    });
    document.addEventListener('htmx:historyCacheMissLoad', function (e) {
        var d = e.detail;
        if (typeof d.response !== 'string' || d.response.indexOf('hx-history-elt') < 0) { hardNavigate(d.path, true); return; }
        d.response = withoutLoadedAssets(d.response);
    });
    document.addEventListener('htmx:historyCacheMissLoadError', function (e) { hardNavigate(e.detail.path, true); });
    document.addEventListener('htmx:historyRestore', function () {
        pendingSidebar = null;
        afterEnter();
        restoreScroll();
        here = { href: location.href, y: window.scrollY };
    });
})();
