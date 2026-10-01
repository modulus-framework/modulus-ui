/*
 * Modulus shell runtime — shared by Blazor, MVC and Razor Pages.
 *
 * The shell markup comes from ShellRenderer (server side); this script only adds
 * behaviour, through event delegation on document, so it keeps working when Blazor
 * re-renders the shell or htmx swaps content:
 *
 *   details.m-dropdown     one open at a time; closes on outside click, Escape or link click
 *   [data-m-nav-open]      opens the sidebar drawer (small screens); [data-m-nav-close] closes it
 *   [data-m-search]        filters every navigation link; Ctrl+K or "/" focuses it
 *   [data-m-theme-toggle]  light / dark
 *   [data-m-fullscreen]    the whole app full screen and back (html.m-fullscreen while it is; Escape also leaves).
 *                          A whole page load ends full screen in every browser, and only a click may start it again, so
 *                          after one the next click or key press goes back to full screen
 *   select[data-m-nav-select]  options are URLs (a pager's page size): the chosen one loads as a link in its place, so
 *                          the list's boost area (or SPA navigation) swaps it in instead of loading a whole page
 *   [data-m-customizer]    theme settings panel (colour mode, scheme, base, font, radius, layout presets, layout, width,
 *                          header, sidebar, menu, direction)
 *   [data-m-fold-toggle]   folds or unfolds the vertical sidebar; a folded sidebar shows groups and names as flyouts
 *   [data-m-launcher]      full-screen app launcher (Odoo home menu, Oracle navigator): [data-m-launcher-open] opens it,
 *                          typing shows the search box, filters the apps and lists matching menu entries, arrow keys
 *                          move between the icons
 *   [data-m-app-menu]      header app grid and module switcher: [data-m-app-filter] filters the apps (name, description,
 *                          group), Enter opens the first match, arrow keys move between them
 *   [data-m-mark-read]     marks header notifications as read
 *   [data-m-tenant]        tenant switcher: writes the modulus-tenant cookie and reloads (cancel modulus:tenant-change to
 *                          handle it yourself); [data-m-tenant-filter] filters the tenant list. Multi-company:
 *                          [data-m-tenant-toggle] checkboxes edit the active set, which is written to the
 *                          modulus-tenants cookie when the switcher closes, followed by a reload (cancel
 *                          modulus:tenants-change { ids } to handle it yourself)
 *   .m-resizable table     drag a header cell's right edge to resize the column (raises m-col-resize { key, width })
 *   .m-demo-nav            gets m-demo-nav-stuck while it sticks under the header
 *   input[data-m-mask]     fits typed text to its mask (AppMaskedInput / app-masked-input) before Blazor or Alpine read it
 *   .ant-otp               one-time code boxes: typing moves on, Backspace moves back, a paste fills every box
 *   .m-richtext            rich text toolbar commands, plain-text paste, and the value kept in step
 *   [data-m-lightbox]      image lightbox: [data-lightbox-index] opens the preview (zoom, rotate, pan, swipe, captions,
 *                          thumbnails, download); AppLightbox, app-lightbox, app-image and app-image-group
 *
 * window.modulusShell.fields holds the business-field rules of Modulus.Ui.Abstractions.Forms (NumberText,
 * DurationText, InputMask) for both flavours; signature(root, dotnet) and richText(root, dotnet) attach a
 * signature pad or rich text editor to Blazor (MVC needs no call).
 *
 * window.modulusShell.download(name, text) saves text as a file (the grids' CSV export); watchResize(id, dotnet)
 * reports resizes to a Blazor grid.
 *
 * window.modulusShell.openPopup(gridId) opens the Ant Design Select / DatePicker in the grid's editing cell, or
 * focuses its input (Blazor EditableGrid calls it when a cell switches to its editor; the components ignore Open and
 * AutoFocus on first render).
 */
(function () {
    'use strict';

    var root = document.documentElement;

    function closeDropdowns(except) {
        document.querySelectorAll('details.m-dropdown[open], details[data-m-dropdown][open]').forEach(function (d) {
            if (d !== except && !d.contains(except)) d.removeAttribute('open');
        });
    }

    function closeNav() { root.classList.remove('m-nav-open'); }

    // Dropdowns -----------------------------------------------------------------------

    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (d.open && d.matches && d.matches('details.m-dropdown, details[data-m-dropdown]')) {
            closeDropdowns(d);
            // A panel inside a scrolling box (a pivot header in its table) floats out of it.
            if (d.querySelector(':scope > .m-dropdown-panel')) placeDropdown(d);
        }
        // A pivot header's cell rises above the other sticky cells while its menu is open (CSS :has() does the same).
        if (d.matches && d.matches('details.m-pivot-groupby')) {
            var cell = d.closest('th');
            if (cell) cell.classList.toggle('m-pivot-menu-open', d.open);
        }
        // A submenu inside a floating panel (the pivot's date intervals) changes its height: place it again.
        if (d.matches && d.matches('details.m-pivot-sub')) {
            var menu = d.closest('details.m-dropdown-float');
            if (menu) placeDropdown(menu);
        }
    }, true);

    // A pivot link (header, menu, toolbar, sort) loads the next layout: mark the pivot busy until the new markup
    // replaces it (htmx swap or Blazor render), so a slow query shows progress. The timer clears a pivot that stayed.
    document.addEventListener('click', function (e) {
        // Not e.defaultPrevented: htmx and the Blazor router cancel the click to follow the link themselves.
        if (e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        var t = e.target;
        var link = t instanceof Element ? t.closest('.m-pivot a[href]') : null;
        if (!link || link.hasAttribute('download') || link.target === '_blank') return;
        var pivot = link.closest('.m-pivot');
        if (link.closest('.m-pivot-value')) return; // a drill-down leaves the page
        pivot.classList.add('m-pivot-busy');
        pivot.setAttribute('aria-busy', 'true');
        keepPivotScroll(pivot);
        setTimeout(function () {
            pivot.classList.remove('m-pivot-busy');
            pivot.removeAttribute('aria-busy');
        }, 15000);
    });

    // The new pivot replaces the old one whole: give its table the old scroll position, and keep the page where it was
    // (the Blazor router scrolls to the top after a navigation).
    var pivotWatch = null;
    function keepPivotScroll(pivot) {
        var box = pivot.querySelector('.m-pivot-table .ant-table-content');
        var saved = { id: pivot.id, top: box ? box.scrollTop : 0, left: box ? box.scrollLeft : 0, y: window.scrollY };
        if (pivotWatch) pivotWatch.disconnect();
        var watch = pivotWatch = new MutationObserver(function () {
            var next = saved.id ? document.getElementById(saved.id) : document.querySelector('.m-pivot');
            if (!next || next === pivot) return;
            watch.disconnect();
            var nextBox = next.querySelector('.m-pivot-table .ant-table-content');
            if (nextBox) { nextBox.scrollTop = saved.top; nextBox.scrollLeft = saved.left; }
            window.scrollTo(window.scrollX, saved.y);
            requestAnimationFrame(function () { window.scrollTo(window.scrollX, saved.y); });
        });
        watch.observe(document.body, { childList: true, subtree: true });
        setTimeout(function () { watch.disconnect(); }, 15000);
    }

    document.addEventListener('click', function (e) {
        var t = e.target;
        if (!(t instanceof Element)) return;
        if (!t.closest('details.m-dropdown, details[data-m-dropdown]')) closeDropdowns(null);
        var link = t.closest('a[href]');
        if (link) {
            var open = link.closest('details.m-dropdown[open], details[data-m-dropdown][open]');
            if (open) open.removeAttribute('open');
            if (link.closest('.m-sidebar')) { closeNav(); closeFly(); }
            if (link.closest('.m-search-results')) closeSearch(true);
        }

        if (t.closest('[data-m-nav-open]')) { root.classList.add('m-nav-open'); return; }
        if (t.closest('[data-m-nav-close]')) { closeNav(); return; }
        if (t.closest('[data-m-fold-toggle]')) { toggleFold(); return; }
        // A folded sidebar's groups open as flyouts; branches inside a flyout still fold open on click.
        if (folded() && t.closest('.m-sidebar .m-snav > .m-snav-item > .m-snav-group > summary')) { e.preventDefault(); return; }
        if (t.closest('[data-m-fullscreen]')) { toggleFullscreen(); return; }
        if (t.closest('[data-m-theme-toggle]')) { window.modulusTheme && window.modulusTheme.toggle(); syncCustomizer(); return; }
        if (t.closest('[data-m-customizer-open]')) { openCustomizer(); return; }
        if (t.closest('[data-m-customizer-close]')) { closeCustomizer(); return; }
        if (t.closest('[data-m-customizer-reset]')) { window.modulusTheme && window.modulusTheme.reset(); return; }
        var settingTab = t.closest('[data-m-customizer-tab]');
        if (settingTab) { showSettingPane(settingTab.getAttribute('data-m-customizer-tab')); return; }
        if (t.closest('[data-m-launcher-open]')) { openLauncher(); return; }
        if (t.closest('[data-m-launcher-close]') || t.matches('.m-launcher-panel') || (link && link.closest('[data-m-launcher]'))) { closeLauncher(); return; }
        if (t.closest('[data-m-search-open]')) { openSearch(); return; }
        if (t.closest('[data-m-mark-read]')) {
            var panel = t.closest('[data-m-notices]');
            if (panel) {
                panel.querySelectorAll('.m-notice.unread').forEach(function (n) { n.classList.remove('unread'); });
                panel.querySelectorAll('.m-notice .m-status-dot, .m-notice-dot').forEach(function (n) { n.remove(); });
                t.closest('[data-m-mark-read]').remove();
            }
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            closeDropdowns(null);
            closeNav();
            closeFly();
            closeCustomizer();
            closeLauncher();
            if (document.activeElement && document.activeElement.classList.contains('m-search-input')) closeSearch(true);
        }
        var typing = /^(INPUT|TEXTAREA|SELECT)$/.test((e.target && e.target.tagName) || '') || (e.target && e.target.isContentEditable);
        if ((e.key === 'k' || e.key === 'K') && (e.ctrlKey || e.metaKey)) { e.preventDefault(); openSearch(); }
        else if (e.key === '/' && !typing) { e.preventDefault(); openSearch(); }
    });

    // Full screen: the button shows only where the browser allows it (not on iPhone Safari), and html.m-fullscreen
    // swaps its icon and label, also after Escape or F11's own exit and after Blazor or htmx re-render the header.
    function fullscreenElement() { return document.fullscreenElement || document.webkitFullscreenElement || null; }
    function toggleFullscreen() {
        if (fullscreenElement()) {
            var exit = document.exitFullscreen || document.webkitExitFullscreen;
            if (exit) exit.call(document);
            return;
        }
        var enter = root.requestFullscreen || root.webkitRequestFullscreen;
        if (!enter) return;
        var done = enter.call(root, { navigationUI: 'hide' });
        if (done && done.catch) done.catch(function () { });
    }
    var FULLSCREEN_KEY = 'modulus-fullscreen', leaving = false;
    function rememberFullscreen(on) {
        try { if (on) sessionStorage.setItem(FULLSCREEN_KEY, '1'); else sessionStorage.removeItem(FULLSCREEN_KEY); } catch (e) { }
    }
    function rememberedFullscreen() {
        try { return sessionStorage.getItem(FULLSCREEN_KEY) === '1'; } catch (e) { return false; }
    }
    // The page is unloading (a whole page load): the browser ends full screen, but the app should come back to it.
    window.addEventListener('beforeunload', function () { leaving = true; setTimeout(function () { leaving = false; }, 3000); });
    window.addEventListener('pagehide', function () { leaving = true; });
    function fullscreenChanged() {
        var on = !!fullscreenElement();
        if (on) rememberFullscreen(true);
        // Left by Escape or the button: forget it. Not when the page unloads (the timer never runs then).
        else setTimeout(function () { if (!leaving) rememberFullscreen(false); }, 0);
        syncFullscreen();
    }
    // After a whole page load in full screen: the next click or key press (a user gesture) enters it again.
    var restoreArmed = false;
    function armFullscreenRestore() {
        if (restoreArmed || !rememberedFullscreen() || fullscreenElement() || !(document.fullscreenEnabled || document.webkitFullscreenEnabled)) return;
        restoreArmed = true;
        function again(e) {
            if (e.type === 'keydown' && (e.key === 'Escape' || e.ctrlKey || e.metaKey || e.altKey)) return;
            document.removeEventListener('click', again, true);
            document.removeEventListener('keydown', again, true);
            restoreArmed = false;
            if (e.target && e.target.closest && e.target.closest('[data-m-fullscreen]')) return; // the button decides
            if (!fullscreenElement() && rememberedFullscreen()) toggleFullscreen();
        }
        document.addEventListener('click', again, true);
        document.addEventListener('keydown', again, true);
    }
    window.addEventListener('pageshow', function () { leaving = false; armFullscreenRestore(); });
    function syncFullscreen() {
        var on = !!fullscreenElement();
        root.classList.toggle('m-fullscreen', on);
        root.classList.toggle('m-can-fullscreen', !!(document.fullscreenEnabled || document.webkitFullscreenEnabled));
        document.querySelectorAll('[data-m-fullscreen]').forEach(function (b) {
            var label = on ? 'Exit full screen' : 'Full screen';
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
            b.setAttribute('aria-label', label);
            b.title = label;
        });
    }
    document.addEventListener('fullscreenchange', fullscreenChanged);
    document.addEventListener('webkitfullscreenchange', fullscreenChanged);
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', syncFullscreen); else syncFullscreen();
    document.addEventListener('htmx:afterSettle', syncFullscreen);
    window.addEventListener('load', function () {
        if (window.Blazor && window.Blazor.addEventListener) window.Blazor.addEventListener('enhancedload', syncFullscreen);
    });

    // A select whose options are URLs (a pager's page size): the chosen URL loads as a link next to it, which htmx
    // boosts like the list's other links (its boost area, or SPA navigation), so the page doesn't load whole.
    document.addEventListener('change', function (e) {
        var select = e.target;
        if (!(select instanceof HTMLSelectElement) || !select.hasAttribute('data-m-nav-select') || !select.value) return;
        var link = select.nextElementSibling;
        if (!link || !link.hasAttribute('data-m-nav-link')) {
            link = document.createElement('a');
            link.hidden = true;
            link.tabIndex = -1;
            link.setAttribute('aria-hidden', 'true');
            link.setAttribute('data-m-nav-link', '');
            select.insertAdjacentElement('afterend', link);
        }
        link.setAttribute('href', select.value);
        if (window.htmx) window.htmx.process(link); // boost it with its new href
        link.click();
    });

    // The search keycap reads ⌘ K on Apple devices; again after Blazor's enhanced navigation re-renders the header.
    var apple = /Mac|iPhone|iPad|iPod/.test((navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || navigator.userAgent);
    function modKeys() {
        if (apple) document.querySelectorAll('[data-m-mod-key]').forEach(function (k) { k.textContent = '⌘'; });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', modKeys); else modKeys();
    window.addEventListener('load', function () {
        if (window.Blazor && window.Blazor.addEventListener) window.Blazor.addEventListener('enhancedload', modKeys);
    });

    // Tenant switcher -------------------------------------------------------------------

    function setCookie(name, value) {
        document.cookie = name + '=' + encodeURIComponent(value) + '; path=/; max-age=31536000; samesite=lax';
    }

    // Checked ids of the multi-company checkboxes in a switcher.
    function activeTenantIds(switcher) {
        return Array.prototype.map.call(switcher.querySelectorAll('[data-m-tenant-toggle][aria-checked="true"]'),
            function (b) { return b.getAttribute('data-m-tenant-toggle'); });
    }

    function chooseTenant(id, switcher) {
        var ev = new CustomEvent('modulus:tenant-change', { detail: { id: id }, bubbles: true, cancelable: true });
        if (!document.dispatchEvent(ev)) return;
        if (switcher && switcher.querySelector('[data-m-tenant-toggle]')) {
            // The new current tenant joins the active set.
            var ids = activeTenantIds(switcher);
            if (ids.indexOf(id) < 0) ids.push(id);
            setCookie('modulus-tenants', ids.join(','));
        }
        setCookie('modulus-tenant', id);
        location.reload();
    }

    function filterTenants(input) {
        var menu = input.closest('.m-tenant-menu');
        var q = input.value.trim().toLowerCase();
        var shown = 0;
        menu.querySelectorAll('.m-tenant-item').forEach(function (item) {
            var match = !q || (item.getAttribute('data-m-tenant-name') || '').toLowerCase().indexOf(q) >= 0;
            (item.closest('.m-tenant-row') || item).hidden = !match;
            if (match) shown++;
        });
        var empty = menu.querySelector('.m-tenant-empty');
        if (empty) empty.hidden = shown > 0;
    }

    document.addEventListener('click', function (e) {
        var toggle = e.target instanceof Element && e.target.closest('[data-m-tenant-toggle]');
        if (toggle) {
            if (toggle.disabled) return;
            toggle.setAttribute('aria-checked', toggle.getAttribute('aria-checked') === 'true' ? 'false' : 'true');
            toggle.closest('[data-m-tenants]').setAttribute('data-m-tenants-changed', '');
            return;
        }
        var item = e.target instanceof Element && e.target.closest('[data-m-tenant]');
        if (!item) return;
        var d = item.closest('details');
        if (d) { d.removeAttribute('data-m-tenants-changed'); d.removeAttribute('open'); }
        var current = item.getAttribute('aria-selected') === 'true' || item.getAttribute('aria-current') === 'true';
        if (!current) chooseTenant(item.getAttribute('data-m-tenant'), d);
    });

    // Multi-company: apply the edited active set when the switcher closes.
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (d.open || !d.matches || !d.matches('[data-m-tenants][data-m-tenants-changed]')) return;
        d.removeAttribute('data-m-tenants-changed');
        var ids = activeTenantIds(d);
        var ev = new CustomEvent('modulus:tenants-change', { detail: { ids: ids }, bubbles: true, cancelable: true });
        if (!document.dispatchEvent(ev)) return;
        setCookie('modulus-tenants', ids.join(','));
        location.reload();
    }, true);

    document.addEventListener('input', function (e) {
        if (e.target.matches && e.target.matches('[data-m-tenant-filter]')) filterTenants(e.target);
    });

    document.addEventListener('keydown', function (e) {
        if (!(e.target.matches && e.target.matches('[data-m-tenant-filter]')) || e.key !== 'Enter') return;
        e.preventDefault();
        var first = e.target.closest('.m-tenant-menu').querySelector('.m-tenant-item:not([hidden])');
        if (first) first.click();
    });

    // Opening the switcher resets the filter and focuses it (or the current tenant).
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (!d.open || !d.matches || !d.matches('[data-m-tenants]')) return;
        var filter = d.querySelector('[data-m-tenant-filter]');
        if (filter) { filter.value = ''; filterTenants(filter); filter.focus(); }
        else { var current = d.querySelector('.m-tenant-item.active'); if (current) current.focus(); }
    }, true);

    // App switchers (header grid, module switcher) ---------------------------------------

    var appItems = '.m-app-tile, .m-module-item';

    function filterApps(input) {
        var menu = input.closest('[data-m-app-menu]');
        var q = input.value.trim().toLowerCase();
        var any = false;
        menu.querySelectorAll('[data-m-app-group]').forEach(function (group) {
            var inGroup = q && (group.getAttribute('data-m-app-group') || '').toLowerCase().indexOf(q) >= 0;
            var shown = 0;
            group.querySelectorAll(appItems).forEach(function (a) {
                var match = !q || inGroup || (a.textContent + ' ' + (a.title || '')).toLowerCase().indexOf(q) >= 0;
                a.hidden = !match;
                if (match) shown++;
            });
            group.hidden = shown === 0;
            if (shown) any = true;
        });
        var empty = menu.querySelector('.m-app-menu-empty');
        if (empty) empty.hidden = any;
    }

    function visibleApps(menu) {
        return Array.prototype.filter.call(menu.querySelectorAll(appItems), function (a) { return !a.hidden && !a.closest('[hidden]'); });
    }

    document.addEventListener('input', function (e) {
        if (e.target.matches && e.target.matches('[data-m-app-filter]')) filterApps(e.target);
    });

    // Enter opens the first match; the arrow keys move from the filter through the apps, in reading order.
    document.addEventListener('keydown', function (e) {
        var t = e.target;
        var menu = t instanceof Element && t.closest('[data-m-app-menu]');
        if (!menu) return;
        var filter = menu.querySelector('[data-m-app-filter]');
        var apps = visibleApps(menu);
        if (t === filter && e.key === 'Enter') {
            e.preventDefault();
            if (apps[0]) apps[0].click();
            return;
        }
        var step = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[e.key];
        if (!step || (t === filter && step < 0) || (t === filter && e.key === 'ArrowRight')) return;
        if (document.documentElement.dir === 'rtl' && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) step = -step;
        var i = apps.indexOf(t);
        var next = t === filter ? apps[0] : apps[i + step];
        if (!next && step < 0 && filter) next = filter;
        if (next) { e.preventDefault(); next.focus(); }
    });

    // Opening a switcher clears its filter and focuses it (or the current app).
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (!d.open || !d.querySelector) return;
        var menu = d.querySelector(':scope > [data-m-app-menu]');
        if (!menu) return;
        var filter = menu.querySelector('[data-m-app-filter]');
        if (filter) { filter.value = ''; filterApps(filter); }
        var body = menu.querySelector('.m-app-menu-body');
        if (body) body.scrollTop = 0;
        var current = menu.querySelector('.m-module-item.active');
        if (current && body) current.scrollIntoView({ block: 'nearest' });
        if (filter && window.matchMedia('(hover: hover)').matches) filter.focus({ preventScroll: true });
        else if (current) current.focus({ preventScroll: true });
    }, true);

    // Search: every navigation link, deduplicated by URL ---------------------------------------

    var activeIndex = -1;

    function searchInput() { return document.querySelector('[data-m-search] .m-search-input'); }

    function openSearch() {
        var input = searchInput();
        if (!input) return;
        root.classList.add('m-search-active');
        input.focus();
        input.select();
    }

    function closeSearch(clear) {
        var input = searchInput();
        var results = document.getElementById('m-search-results');
        if (results) { results.hidden = true; results.textContent = ''; }
        if (input) {
            input.setAttribute('aria-expanded', 'false');
            if (clear) { input.value = ''; input.blur(); }
        }
        root.classList.remove('m-search-active');
        activeIndex = -1;
    }

    function index() {
        var seen = {}, items = [];
        document.querySelectorAll('.m-sidebar a[href], .m-navbar a[href]').forEach(function (a) {
            var href = a.getAttribute('href');
            if (!href) return;
            var icon = iconRef(a);
            // Sidebar links under a group have no icon; the navbar's copy of the same entry usually does.
            if (seen[href]) { if (!seen[href].icon && icon) seen[href].icon = icon; return; }
            var group = a.closest('.m-snav-sub');
            var section = a.getAttribute('data-m-trail') || '';
            if (!section && group) {
                var summary = group.parentElement && group.parentElement.querySelector(':scope > summary .m-snav-title');
                section = summary ? summary.textContent.trim() : '';
            }
            var item = { href: href, label: a.textContent.trim(), section: section, icon: icon };
            seen[href] = item;
            items.push(item);
        });
        return items;
    }

    function renderResults(query) {
        var results = document.getElementById('m-search-results');
        var input = searchInput();
        if (!results || !input) return;
        query = query.trim().toLowerCase();
        if (!query) { closeSearch(false); root.classList.add('m-search-active'); return; }
        var matches = index().filter(function (i) {
            return (i.label + ' ' + i.section).toLowerCase().indexOf(query) >= 0;
        }).slice(0, 12);
        results.textContent = '';
        if (matches.length === 0) {
            var empty = document.createElement('div');
            empty.className = 'm-search-empty';
            empty.textContent = 'No pages match “' + query + '”';
            results.appendChild(empty);
        }
        matches.forEach(function (m, i) {
            var a = document.createElement('a');
            a.className = 'm-search-item' + (i === 0 ? ' active' : '');
            a.href = m.href;
            a.setAttribute('role', 'option');
            a.appendChild(iconSvg(m.icon));
            var label = document.createElement('span');
            label.textContent = m.label;
            a.appendChild(label);
            if (m.section) {
                var section = document.createElement('span');
                section.className = 'm-search-item-section';
                section.textContent = m.section;
                a.appendChild(section);
            }
            results.appendChild(a);
        });
        activeIndex = matches.length ? 0 : -1;
        results.hidden = false;
        input.setAttribute('aria-expanded', 'true');
    }

    // A link's icon as a string that survives localStorage: its name (ShellRenderer marks every icon with data-m-icon).
    function iconRef(a) {
        var inline = a.querySelector('svg.m-ai[data-m-icon]');
        return inline ? inline.getAttribute('data-m-icon') : null;
    }

    // A copy of the page's own drawing of that icon; an empty icon of the same size when none is on the page
    // (an entry saved before an upgrade, or a menu item whose icon is no longer rendered).
    function iconSvg(ref) {
        var source = ref ? document.querySelector('svg.m-ai[data-m-icon="' + CSS.escape(ref) + '"]') : null;
        if (source) {
            var copy = source.cloneNode(true);
            copy.setAttribute('class', 'm-ti m-ai');
            return copy;
        }
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('class', 'm-ti m-ai');
        svg.setAttribute('aria-hidden', 'true');
        return svg;
    }

    function moveActive(delta) {
        var items = document.querySelectorAll('#m-search-results .m-search-item');
        if (!items.length) return;
        activeIndex = (activeIndex + delta + items.length) % items.length;
        items.forEach(function (el, i) { el.classList.toggle('active', i === activeIndex); });
        items[activeIndex].scrollIntoView({ block: 'nearest' });
    }

    document.addEventListener('input', function (e) {
        if (e.target.classList && e.target.classList.contains('m-search-input')) renderResults(e.target.value);
    });

    document.addEventListener('keydown', function (e) {
        if (!(e.target.classList && e.target.classList.contains('m-search-input'))) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); moveActive(1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); moveActive(-1); }
        else if (e.key === 'Enter') {
            var active = document.querySelectorAll('#m-search-results .m-search-item')[activeIndex];
            if (active) { e.preventDefault(); active.click(); }
        }
    });

    document.addEventListener('focusout', function (e) {
        if (!(e.target.classList && e.target.classList.contains('m-search-input'))) return;
        // Let a click on a result land before the list closes.
        setTimeout(function () {
            var search = document.querySelector('[data-m-search]');
            if (search && !search.contains(document.activeElement)) closeSearch(false);
        }, 150);
    });

    // App launcher (Odoo home menu) ---------------------------------------------------------------

    var launcherFocus = null;

    function launcher() { return document.querySelector('[data-m-launcher]'); }

    function launcherOpen() { var l = launcher(); return !!l && !l.hidden; }

    function openLauncher() {
        var l = launcher();
        if (!l) return;
        closeDropdowns(null);
        closeNav();
        closeSearch(true);
        launcherFocus = document.activeElement;
        var input = l.querySelector('[data-m-launcher-search]');
        input.value = '';
        filterLauncher(l, '');
        l.hidden = false;
        root.classList.add('m-launcher-open');
        // A keyboard on screen would cover the icons on phones, so touch devices start on the current app.
        var fine = window.matchMedia && window.matchMedia('(pointer: fine)').matches;
        var target = fine ? input : (l.querySelector('.m-launcher-app.active') || l.querySelector('.m-launcher-app'));
        if (target) target.focus();
    }

    function closeLauncher() {
        var l = launcher();
        if (!l || l.hidden) return;
        l.hidden = true;
        root.classList.remove('m-launcher-open');
        if (launcherFocus && launcherFocus.focus && document.contains(launcherFocus)) launcherFocus.focus();
        launcherFocus = null;
    }

    function filterLauncher(l, query) {
        var q = query.trim().toLowerCase();
        var shown = 0;
        l.querySelectorAll('.m-launcher-app').forEach(function (a) {
            var match = !q || (a.getAttribute('data-m-launcher-name') || '').toLowerCase().indexOf(q) >= 0;
            a.hidden = !match;
            if (match) shown++;
        });
        l.classList.toggle('m-launcher-searching', !!q);
        var results = l.querySelector('.m-launcher-results');
        results.textContent = '';
        var found = !q ? [] : index().filter(function (i) {
            return (i.label + ' ' + i.section).toLowerCase().indexOf(q) >= 0;
        }).slice(0, 8);
        found.forEach(function (m) {
            var a = document.createElement('a');
            a.className = 'm-launcher-result';
            a.href = m.href;
            a.appendChild(iconSvg(m.icon));
            var text = document.createElement('span');
            text.className = 'm-launcher-result-text';
            if (m.section) {
                var trail = document.createElement('span');
                trail.className = 'm-launcher-result-trail';
                trail.textContent = m.section + ' › ';
                text.appendChild(trail);
            }
            text.appendChild(document.createTextNode(m.label));
            a.appendChild(text);
            results.appendChild(a);
        });
        l.querySelector('.m-launcher-menus').hidden = found.length === 0;
        l.querySelector('.m-launcher-empty').hidden = shown > 0 || found.length > 0;
    }

    function launcherItems(l) {
        return Array.prototype.filter.call(l.querySelectorAll('.m-launcher-app, .m-launcher-result'),
            function (el) { return el.offsetParent !== null; });
    }

    // The nearest item on screen in the arrow's direction, so keys follow the icon grid and the menu results.
    function launcherNeighbour(items, from, key) {
        var a = from.getBoundingClientRect(), ax = a.left + a.width / 2, ay = a.top + a.height / 2;
        var best = null, bestScore = Infinity;
        items.forEach(function (el) {
            if (el === from) return;
            var b = el.getBoundingClientRect(), dx = b.left + b.width / 2 - ax, dy = b.top + b.height / 2 - ay;
            var ahead = { ArrowDown: dy > a.height / 2, ArrowUp: dy < -a.height / 2, ArrowRight: dx > a.width / 2, ArrowLeft: dx < -a.width / 2 }[key];
            if (!ahead) return;
            var vertical = key === 'ArrowDown' || key === 'ArrowUp';
            var score = vertical ? Math.abs(dy) + Math.abs(dx) * 2 : Math.abs(dx) + Math.abs(dy) * 4;
            if (score < bestScore) { bestScore = score; best = el; }
        });
        return best;
    }

    document.addEventListener('input', function (e) {
        if (e.target.matches && e.target.matches('[data-m-launcher-search]')) filterLauncher(launcher(), e.target.value);
    });

    document.addEventListener('keydown', function (e) {
        if (!launcherOpen()) return;
        var l = launcher();
        var input = l.querySelector('[data-m-launcher-search]');
        var t = e.target;
        if (t === input) {
            if (e.key === 'ArrowDown') { e.preventDefault(); var first = launcherItems(l)[0]; if (first) first.focus(); }
            else if (e.key === 'Enter') { e.preventDefault(); var go = launcherItems(l)[0]; if (go) go.click(); }
            return;
        }
        if (!(t instanceof Element) || !l.contains(t)) return;
        var items = launcherItems(l);
        if (items.indexOf(t) >= 0 && /^Arrow/.test(e.key)) {
            e.preventDefault();
            var next = launcherNeighbour(items, t, e.key);
            if (next) next.focus();
            else if (e.key === 'ArrowUp') input.focus();
            return;
        }
        // Typing anywhere in the launcher searches.
        if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) input.focus();
    });

    // Keep focus inside the open launcher.
    document.addEventListener('focusin', function (e) {
        var l = launcher();
        if (l && !l.hidden && !l.contains(e.target)) l.querySelector('[data-m-launcher-search]').focus();
    });

    // Theme settings panel ------------------------------------------------------------------------

    var lastFocus = null;

    function customizer() { return document.querySelector('[data-m-customizer]'); }

    function openCustomizer() {
        var panel = customizer();
        if (!panel) return;
        lastFocus = document.activeElement;
        syncCustomizer();
        panel.hidden = false;
        var first = panel.querySelector('.m-setting-pane:not([hidden]) input:checked, .m-setting-pane:not([hidden]) input');
        if (first) first.focus();
    }

    function closeCustomizer() {
        var panel = customizer();
        if (!panel || panel.hidden) return;
        panel.hidden = true;
        if (lastFocus && lastFocus.focus) lastFocus.focus();
    }

    /** Reflects the live theme state (it can change outside the panel, e.g. the header toggle). */
    function syncCustomizer() {
        var panel = customizer();
        var theme = window.modulusTheme;
        if (!panel || !theme) return;
        var mode = document.cookie.match(/(?:^|; )modulus-theme=(light|dark)/) ? theme.current() : 'system';
        check(panel, 'mode', mode);
        LAYOUT_PREFS.concat(['nav', 'menu', 'font', 'base', 'density']).forEach(function (n) { check(panel, n, theme.getPref(n)); });
        syncPresets(panel);
        var primary = theme.getPref('primary');
        if (primary) check(panel, 'primary', primary);
        var radius = theme.getPref('radius');
        if (radius) check(panel, 'radius', radius);
    }

    var LAYOUT_PREFS = ['layout', 'width', 'side', 'sidebar', 'fold', 'header', 'topbar', 'dir'];

    /** Checks the preset whose settings all match the current ones (none when the mix is custom). */
    function syncPresets(panel) {
        var theme = window.modulusTheme;
        panel.querySelectorAll('input[name="preset"]').forEach(function (input) {
            var preset = JSON.parse(input.getAttribute('data-preset'));
            input.checked = Object.keys(preset).every(function (k) { return theme.getPref(k) === preset[k]; });
        });
    }

    /** Shows one pane of the theme settings (Appearance or Layout); arrow keys move between the tabs. */
    function showSettingPane(name, focus) {
        var panel = customizer();
        if (!panel) return;
        panel.querySelectorAll('[data-m-customizer-tab]').forEach(function (tab) {
            var on = tab.getAttribute('data-m-customizer-tab') === name;
            tab.setAttribute('aria-selected', on ? 'true' : 'false');
            tab.tabIndex = on ? 0 : -1;
            var pane = document.getElementById(tab.getAttribute('aria-controls'));
            if (pane) pane.hidden = !on;
            if (on && focus) tab.focus();
        });
        var body = panel.querySelector('.m-offcanvas-body');
        if (body) body.scrollTop = 0;
    }

    document.addEventListener('keydown', function (e) {
        var tab = e.target instanceof Element && e.target.closest('[data-m-customizer-tab]');
        if (!tab || (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight')) return;
        var tabs = Array.prototype.slice.call(tab.parentNode.querySelectorAll('[data-m-customizer-tab]'));
        var next = tabs[(tabs.indexOf(tab) + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
        e.preventDefault();
        showSettingPane(next.getAttribute('data-m-customizer-tab'), true);
    });

    function check(panel, name, value) {
        var input = panel.querySelector('input[name="' + name + '"][value="' + value + '"]');
        if (input) input.checked = true;
    }

    document.addEventListener('change', function (e) {
        var input = e.target;
        if (!input.closest || !input.closest('[data-m-customizer-form]')) return;
        if (input.hasAttribute('data-m-culture')) {
            // ASP.NET Core's culture cookie (CookieRequestCultureProvider), then reload in the new request culture.
            var c = input.value;
            document.cookie = input.getAttribute('data-cookie') + '=' + encodeURIComponent('c=' + c + '|uic=' + c) +
                '; path=/; max-age=31536000; samesite=lax' + (location.protocol === 'https:' ? '; secure' : '');
            location.reload();
            return;
        }
        var theme = window.modulusTheme;
        if (!theme) return;
        if (input.name === 'mode') {
            if (input.value === 'system') theme.useSystem(); else theme.set(input.value);
        } else if (input.name === 'primary') {
            theme.setPref('primary', input.value, input.getAttribute('data-css'));
        } else if (input.name === 'preset') {
            var preset = JSON.parse(input.getAttribute('data-preset'));
            Object.keys(preset).forEach(function (k) { theme.setPref(k, preset[k]); });
            syncCustomizer();
        } else {
            theme.setPref(input.name, input.value);
            if (LAYOUT_PREFS.indexOf(input.name) >= 0) syncPresets(customizer());
        }
    });

    // Folded sidebar ------------------------------------------------------------------------------

    function folded() {
        return root.getAttribute('data-layout') === 'vertical' && root.getAttribute('data-fold') === 'folded' && wide.matches;
    }

    function toggleFold() {
        var theme = window.modulusTheme;
        if (!theme) return;
        theme.setPref('fold', theme.getPref('fold') === 'full' ? 'folded' : 'full');
        syncCustomizer();
    }

    // The flyout is the item's own submenu (or name), shown fixed next to the icon: the sidebar scrolls, so it
    // can't be positioned in CSS alone. A closed <details> renders nothing, so the group opens while it flies out.
    var fly = null;

    function openFly(item) {
        if (fly && fly.item === item) return;
        closeFly();
        var details = item.querySelector(':scope > details');
        var rect = item.getBoundingClientRect();
        var bar = item.closest('.m-sidebar').getBoundingClientRect();
        var toLeft = bar.left + bar.width / 2 > window.innerWidth / 2;
        item.style.setProperty('--m-fly-top', Math.max(8, rect.top) + 'px');
        item.style.setProperty(toLeft ? '--m-fly-right' : '--m-fly-left', (toLeft ? window.innerWidth - bar.left + 8 : bar.right + 8) + 'px');
        item.style.removeProperty(toLeft ? '--m-fly-left' : '--m-fly-right');
        item.classList.toggle('m-fly-left', toLeft);
        item.classList.add('m-fly');
        fly = { item: item, details: details, wasOpen: !!(details && details.open) };
        if (details) details.open = true;
    }

    function closeFly() {
        if (!fly) return;
        fly.item.classList.remove('m-fly', 'm-fly-left');
        if (fly.details) fly.details.open = fly.wasOpen;
        fly = null;
    }

    function flyTarget(t) {
        return t instanceof Element && folded() ? t.closest('.m-sidebar .m-snav > .m-snav-item') : null;
    }

    document.addEventListener('mouseover', function (e) {
        var item = flyTarget(e.target);
        if (item) openFly(item);
        else if (fly && !(e.target instanceof Node && fly.item.contains(e.target))) closeFly();
    });
    document.addEventListener('focusin', function (e) {
        var item = flyTarget(e.target);
        if (item) openFly(item); else closeFly();
    });
    document.documentElement.addEventListener('mouseleave', closeFly);
    document.addEventListener('scroll', function (e) {
        if (fly && !(e.target instanceof Node && fly.item.contains(e.target))) closeFly();
    }, { capture: true, passive: true });
    window.addEventListener('modulus:pref', function () { if (!folded()) closeFly(); });

    // Close the drawer when the viewport grows past the breakpoint.
    var wide = window.matchMedia('(min-width: 992px)');
    if (wide.addEventListener) wide.addEventListener('change', function (e) { if (e.matches) closeNav(); });
    window.modulusShell = window.modulusShell || {};
    // SPA navigation (modulus-spa.js) closes the navigation drawer and the app launcher once the next page is in.
    window.modulusShell.closeNav = closeNav;
    window.modulusShell.closeLauncher = closeLauncher;
    window.modulusShell.openPopup = function (gridId) {
        // The component wires up its overlay in its own first after-render, and the table may render a new row a
        // cycle later, so look for the grid's editing cell for a few frames.
        var tries = 0;
        (function attempt() {
            var cell = document.querySelector('#' + gridId + ' .m-grid-cell-editing');
            if (cell && cell.querySelector('.m-cselect')) return; // AppCreatableSelect opens itself (DefaultOpen)
            var popup = cell && cell.querySelector('.ant-select-selector, .ant-picker-input input');
            var input = cell && cell.querySelector('input, textarea');
            if (!popup && !input) {
                if (++tries < 20) setTimeout(attempt, 30);
                return;
            }
            if (popup) {
                setTimeout(function () { popup.click(); if (popup.focus) popup.focus(); }, 30);
                return;
            }
            // Plain inputs (InputNumber ignores AutoFocus): focus and select the text.
            if (document.activeElement !== input) {
                input.focus();
                if (input.select) input.select();
            }
        })();
    };

    // Pointer capture for drag controls (the Blazor colour picker palette and sliders keep receiving pointermove
    // after the pointer leaves them).
    window.modulusShell.capture = function (el, pointerId) {
        try { if (el && el.setPointerCapture) el.setPointerCapture(pointerId); } catch (e) { /* pointer already up */ }
    };

    // Arrow keys on colour picker sliders move the handle, not the page.
    document.addEventListener('keydown', function (e) {
        if (/^Arrow/.test(e.key) && e.target instanceof Element && e.target.matches('.ant-color-picker [role="slider"]')) e.preventDefault();
    });

    // Back-to-top float buttons: [data-m-back-top="<visibility height>"], optional data-m-target selector ------------

    function backTopScroller(btn) {
        var sel = btn.getAttribute('data-m-target');
        return (sel && document.querySelector(sel)) || null;
    }

    function syncBackTops() {
        document.querySelectorAll('[data-m-back-top]').forEach(function (btn) {
            var scroller = backTopScroller(btn);
            var top = scroller ? scroller.scrollTop : (window.scrollY || document.documentElement.scrollTop);
            var show = top >= (parseInt(btn.getAttribute('data-m-back-top'), 10) || 0);
            if (show) btn.setAttribute('data-m-visible', ''); else btn.removeAttribute('data-m-visible');
        });
    }

    document.addEventListener('scroll', syncBackTops, { capture: true, passive: true });
    document.addEventListener('click', function (e) {
        var btn = e.target instanceof Element && e.target.closest('[data-m-back-top]');
        if (!btn) return;
        var scroller = backTopScroller(btn);
        (scroller || window).scrollTo({ top: 0, behavior: 'smooth' });
    });
    window.modulusShell.syncBackTops = syncBackTops;

    // Chart legend toggle (ChartOptions.LegendToggle): [data-m-legend] buttons hide and show the chart's
    // [data-s="<series>"] marks and tooltip rows. The last visible series can't be hidden.
    document.addEventListener('click', function (e) {
        var btn = e.target instanceof Element && e.target.closest('[data-m-legend] [data-series]');
        if (!btn) return;
        var legend = btn.closest('[data-m-legend]');
        var chart = btn.closest('.m-chart');
        var on = btn.getAttribute('aria-pressed') !== 'false';
        if (on && legend.querySelectorAll('[aria-pressed="true"]').length < 2) return;
        btn.setAttribute('aria-pressed', on ? 'false' : 'true');
        chart.querySelectorAll('[data-s="' + btn.getAttribute('data-series') + '"]').forEach(function (el) {
            el.classList.toggle('m-series-hidden', on);
        });
    });

    // Tour:window.modulusShell.tour(root, focus) spotlights the step's data-target and places the .ant-tour card
    // beside it (data-placement top | bottom | left | right), clamped to the viewport. Re-runs on resize and scroll.
    var activeTour = null;

    function placeTour() {
        var root = activeTour;
        if (!root || !root.isConnected) { activeTour = null; return; }
        var card = root.querySelector('.ant-tour');
        if (!card) return;
        var sel = card.getAttribute('data-target');
        var target = sel ? document.querySelector(sel) : null;
        var hole = root.querySelector('.m-tour-hole');
        var vw = window.innerWidth, vh = window.innerHeight, gap = 12, pad = 6, edge = 8;
        var cw = card.offsetWidth, ch = card.offsetHeight;
        var left, top, placement = card.getAttribute('data-placement') || 'bottom';
        root.classList.toggle('m-tour-centered', !target);
        if (!target) {
            if (hole) ['x', 'y', 'width', 'height'].forEach(function (a) { hole.setAttribute(a, 0); });
            left = (vw - cw) / 2;
            top = (vh - ch) / 2;
        } else {
            var r = target.getBoundingClientRect();
            if (hole) {
                hole.setAttribute('x', r.left - pad);
                hole.setAttribute('y', r.top - pad);
                hole.setAttribute('width', r.width + pad * 2);
                hole.setAttribute('height', r.height + pad * 2);
            }
            // Flip to the other side when the preferred one has no room.
            if (placement === 'bottom' && r.bottom + pad + gap + ch > vh && r.top - pad - gap - ch > 0) placement = 'top';
            else if (placement === 'top' && r.top - pad - gap - ch < 0) placement = 'bottom';
            else if (placement === 'right' && r.right + pad + gap + cw > vw) placement = 'left';
            else if (placement === 'left' && r.left - pad - gap - cw < 0) placement = 'right';
            if (placement === 'top' || placement === 'bottom') {
                left = r.left + r.width / 2 - cw / 2;
                top = placement === 'top' ? r.top - pad - gap - ch : r.bottom + pad + gap;
            } else {
                top = r.top + r.height / 2 - ch / 2;
                left = placement === 'left' ? r.left - pad - gap - cw : r.right + pad + gap;
            }
            var arrow = card.querySelector('.ant-tour-arrow');
            left = Math.max(edge, Math.min(left, vw - cw - edge));
            top = Math.max(edge, Math.min(top, vh - ch - edge));
            if (arrow) {
                if (placement === 'top' || placement === 'bottom') { arrow.style.left = Math.max(12, Math.min(r.left + r.width / 2 - left, cw - 12)) + 'px'; arrow.style.top = ''; }
                else { arrow.style.top = Math.max(12, Math.min(r.top + r.height / 2 - top, ch - 12)) + 'px'; arrow.style.left = ''; }
            }
        }
        ['top', 'bottom', 'left', 'right'].forEach(function (p) { card.classList.toggle('ant-tour-placement-' + p, target != null && p === placement); });
        card.style.left = Math.round(left) + 'px';
        card.style.top = Math.round(top) + 'px';
    }

    window.modulusShell.tour = function (root, focus) {
        if (!root) return;
        activeTour = root;
        var card = root.querySelector('.ant-tour');
        var sel = card && card.getAttribute('data-target');
        var target = sel ? document.querySelector(sel) : null;
        if (target) {
            var r = target.getBoundingClientRect();
            if (r.top < 0 || r.bottom > window.innerHeight) target.scrollIntoView({ block: 'center' });
        }
        placeTour();
        if (focus && card) card.focus({ preventScroll: true });
    };

    window.addEventListener('resize', placeTour);
    document.addEventListener('scroll', placeTour, { capture: true, passive: true });

    // Creatable select (AppCreatableSelect / app-creatable-select): the dropdown is position: fixed under (or above)
    // its trigger, so table cells and scrolling cards don't clip it. Re-placed on scroll and resize.
    function placeSelect(root) {
        var popup = root && root.querySelector(':scope > .m-cselect-dropdown');
        if (!popup) return;
        var r = root.getBoundingClientRect(), gap = 4, edge = 8;
        var vw = document.documentElement.clientWidth, vh = window.innerHeight;
        popup.style.minWidth = Math.max(r.width, 200) + 'px';
        var h = popup.offsetHeight, w = popup.offsetWidth;
        var below = vh - r.bottom - gap - edge, above = r.top - gap - edge;
        var up = h > below && above > below;
        popup.classList.toggle('ant-select-dropdown-placement-topLeft', up);
        popup.classList.toggle('ant-select-dropdown-placement-bottomLeft', !up);
        popup.style.left = Math.round(Math.max(edge, Math.min(r.left, vw - w - edge))) + 'px';
        popup.style.top = Math.round(up ? Math.max(edge, r.top - gap - h) : r.bottom + gap) + 'px';
    }
    // <app-dropdown> panels are absolutely positioned inside the dropdown. When an ancestor scrolls or clips
    // (overflow other than visible), the panel would be cut off, so it switches to position: fixed next to its
    // trigger (m-dropdown-float), opening upwards when there is no room below.
    function clipsOverflow(el) {
        for (var p = el.parentElement; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
            var s = getComputedStyle(p);
            if (s.overflowX !== 'visible' || s.overflowY !== 'visible') return true;
        }
        return false;
    }
    function placeDropdown(root) {
        var panel = root && root.querySelector(':scope > .m-dropdown-panel');
        if (!panel || getComputedStyle(panel).display === 'none') return;
        if (!root.classList.contains('m-dropdown-float')) {
            if (!clipsOverflow(root)) return;
            root.classList.add('m-dropdown-float');
        }
        var trigger = root.querySelector(':scope > .m-dropdown-trigger') || root;
        var r = trigger.getBoundingClientRect(), gap = 4, edge = 8;
        var vw = document.documentElement.clientWidth, vh = window.innerHeight;
        var h = panel.offsetHeight, w = panel.offsetWidth;
        var up = h > vh - r.bottom - gap - edge && r.top > vh - r.bottom;
        var left = panel.classList.contains('m-dropdown-left') ? r.left : r.right - w;
        panel.style.left = Math.round(Math.max(edge, Math.min(left, vw - w - edge))) + 'px';
        panel.style.top = Math.round(up ? Math.max(edge, r.top - gap - h) : r.bottom + gap) + 'px';
    }
    window.modulusShell.placeDropdown = placeDropdown;

    function placeSelects() {
        document.querySelectorAll('.m-cselect.ant-select-open, .m-lookup.m-lookup-open').forEach(placeSelect);
        document.querySelectorAll('.m-dropdown.m-dropdown-float').forEach(placeDropdown);
    }

    window.modulusShell.placeSelect = function (root) {
        placeSelect(root);
        var popup = root && root.querySelector(':scope > .m-cselect-dropdown');
        var active = popup && popup.querySelector('.ant-select-item-option-active');
        if (active) active.scrollIntoView({ block: 'nearest' });
        var selector = root && root.querySelector('.ant-select-selector');
        if (selector && !root.contains(document.activeElement)) selector.focus({ preventScroll: true });
    };


    // ---- Data tables and grids (Blazor and MVC share the DOM) ------------------------------------------------
    //  [data-m-filter-row]        MVC: shows / hides the table's filter row (the header searches as boxes under
    //                             the titles). Kept per table for the tab, and open while a column is searched.
    //  [data-m-search-reveal]     MVC: an empty toolbar search folds into its icon; a click opens it.
    //  Keys inside a grid:        Ctrl+S saves ([data-m-grid-save]), Insert adds a row ([data-m-grid-add]), Ctrl+F
    //                             searches, Delete removes the selected rows ([data-m-grid-delete-selected]), and the
    //                             arrow keys move between cells.

    function tableCard(el) { return el && el.closest ? el.closest('.m-table-card') : null; }
    // A table's own header row group (a nested grid's headers sit further down, inside a detail row).
    function ownHead(card) {
        var heads = card.querySelectorAll('thead');
        for (var i = 0; i < heads.length; i++) if (tableCard(heads[i]) === card) return heads[i];
        return null;
    }
    function filterRowKey(card) { return card.id ? 'modulus-filter-row:' + location.pathname + ':' + card.id : null; }
    function setFilterRow(card, on, remember) {
        var head = ownHead(card);
        if (head) head.classList.toggle('m-filter-row-on', on);
        card.querySelectorAll('[data-m-filter-row]').forEach(function (b) {
            if (tableCard(b) !== card) return;
            b.classList.toggle('active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
            b.title = on ? 'Hide the filter row' : 'Show the filter row';
        });
        var key = filterRowKey(card);
        if (remember && key) { try { sessionStorage.setItem(key, on ? '1' : '0'); } catch (e) { /* storage blocked */ } }
    }
    // On when the user turned it on (this session), when a column search is set, or by default for a table marked
    // data-m-filter-row-open (the nested grid's levels) unless the user turned it off.
    function syncFilterRow(card) {
        if (!card || !card.querySelector('[data-m-filter-row]')) return;
        var head = ownHead(card), key = filterRowKey(card), stored = null;
        try { stored = key ? sessionStorage.getItem(key) : null; } catch (e) { /* storage blocked */ }
        var searched = !!head && Array.prototype.some.call(head.querySelectorAll('.m-th-filter'), function (i) { return i.value.trim() !== ''; });
        var fallback = card.hasAttribute('data-m-filter-row-open');
        setFilterRow(card, searched || (stored === null ? fallback : stored === '1'), false);
    }
    window.modulusShell.syncFilterRow = function (el) { syncFilterRow(tableCard(el) || el); };
    function syncFilterRows(root) {
        (root && root.querySelectorAll ? root : document).querySelectorAll('.m-table-card').forEach(syncFilterRow);
        if (root && root.classList && root.classList.contains('m-table-card')) syncFilterRow(root);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { syncFilterRows(document); }); else syncFilterRows(document);
    document.addEventListener('htmx:load', function (e) { syncFilterRows(e.detail && e.detail.elt); });

    document.addEventListener('click', function (e) {
        var t = e.target instanceof Element ? e.target : null;
        var toggle = t && t.closest('[data-m-filter-row]');
        if (toggle) {
            var card = tableCard(toggle), head = card && ownHead(card);
            if (!head) return;
            var on = !head.classList.contains('m-filter-row-on');
            setFilterRow(card, on, true);
            if (on) {
                var first = head.querySelector('.m-th-filter');
                if (first) first.focus();
            }
            return;
        }
        var reveal = t && t.closest('[data-m-search-reveal]');
        if (reveal) {
            var box = reveal.nextElementSibling;
            if (!box) return;
            reveal.hidden = true;
            box.hidden = false;
            var input = box.querySelector('input');
            if (input) input.focus();
        }
    });
    // Leaving an empty revealed search folds it back into its icon.
    document.addEventListener('focusout', function (e) {
        var input = e.target;
        if (!(input instanceof HTMLInputElement) || input.value) return;
        var box = input.closest('.m-table-toolbar-search'), reveal = box && box.previousElementSibling;
        if (!reveal || !reveal.hasAttribute('data-m-search-reveal')) return;
        setTimeout(function () {
            if (box.contains(document.activeElement) || input.value) return;
            box.hidden = true;
            reveal.hidden = false;
        }, 150);
    });

    // ---- Data tables: a Columns picker ([data-m-table-columns]) and Export CSV ([data-m-table-export]) --------------
    // Both work from the rendered table, so the same toolbar buttons serve Blazor's ProTable and the MVC tables.
    // Hidden columns are remembered per page and table (localStorage), by header text, and hidden with a style rule
    // scoped to the card, so a Blazor re-render or an htmx swap keeps them hidden. The export writes the rows shown
    // (this page, after filters) in the visible columns.
    var tableSeq = 0;
    function tableKey(card) { if (!card.id) card.id = 'm-table-' + (++tableSeq); return card.id; }
    function colsStore(card) { return 'modulus-table-cols:' + location.pathname + ':' + tableKey(card); }
    function utilityCell(th) {
        return th.classList.contains('ant-table-selection-column') || th.classList.contains('ant-table-row-expand-icon-cell') ||
            th.classList.contains('m-grid-actions') || th.classList.contains('ant-table-cell-scrollbar');
    }
    // The card's own header cells: { index (1-based, as nth-child), label, th }, without the checkbox, expand and actions cells.
    function tableColumns(card) {
        var head = ownHead(card), row = head && head.querySelector('tr');
        if (!row) return [];
        var cols = [];
        Array.prototype.forEach.call(row.children, function (th, i) {
            if (utilityCell(th)) return;
            var title = th.querySelector('.ant-table-column-title') || th;
            var label = title.textContent.replace(/\s+/g, ' ').replace(/\s*\*$/, '').trim();
            if (label) cols.push({ index: i + 1, label: label, th: th });
        });
        return cols;
    }
    function readHiddenCols(card) {
        try { var v = JSON.parse(localStorage.getItem(colsStore(card)) || '[]'); return Array.isArray(v) ? v : []; } catch (e) { return []; }
    }
    function writeHiddenCols(card, list) {
        try { if (list.length) localStorage.setItem(colsStore(card), JSON.stringify(list)); else localStorage.removeItem(colsStore(card)); } catch (e) { /* storage blocked */ }
    }
    function applyHiddenCols(card) {
        var key = tableKey(card), hidden = readHiddenCols(card), id = '#' + CSS.escape(key);
        var rules = tableColumns(card).filter(function (c) { return hidden.indexOf(c.label) >= 0; }).map(function (c) {
            var n = ':nth-child(' + c.index + ')';
            return id + ' :is(thead, tbody, tfoot) > tr:not(.ant-table-expanded-row):not(.ant-table-placeholder) > ' + n + ':not(.ant-table-expanded-row *), ' +
                id + ' colgroup > col' + n + ':not(.ant-table-expanded-row *) { display: none; }';
        }).join('\n');
        var style = document.getElementById(key + '-cols');
        if (!rules) { if (style) style.remove(); return; }
        if (!style) { style = document.createElement('style'); style.id = key + '-cols'; document.head.appendChild(style); }
        if (style.textContent !== rules) style.textContent = rules;
    }
    function applyAllHiddenCols() {
        document.querySelectorAll('[data-m-table-columns]').forEach(function (b) { var card = tableCard(b); if (card) applyHiddenCols(card); });
    }
    window.modulusShell.applyHiddenColumns = applyAllHiddenCols;

    function escHtml(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
    var colsPanel = null;
    function closeColsPanel() {
        if (!colsPanel) return;
        if (colsPanel._button) { colsPanel._button.setAttribute('aria-expanded', 'false'); colsPanel._button.focus({ preventScroll: true }); }
        colsPanel.remove();
        colsPanel = null;
    }
    function renderColsPanel(panel, card) {
        var hidden = readHiddenCols(card), cols = tableColumns(card);
        var shown = cols.filter(function (c) { return hidden.indexOf(c.label) < 0; }).length;
        var items = cols.map(function (c) {
            var on = hidden.indexOf(c.label) < 0, last = on && shown === 1;
            return '<li class="m-grid-columns-item"><label class="ant-checkbox-wrapper' + (on ? ' ant-checkbox-wrapper-checked' : '') + (last ? ' ant-checkbox-wrapper-disabled' : '') + '">' +
                '<span class="ant-checkbox' + (on ? ' ant-checkbox-checked' : '') + (last ? ' ant-checkbox-disabled' : '') + '"><input type="checkbox" class="ant-checkbox-input" data-col="' + escHtml(c.label) + '"' + (on ? ' checked' : '') + (last ? ' disabled title="At least one column stays visible"' : '') + ' /><span class="ant-checkbox-inner"></span></span>' +
                '<span>' + escHtml(c.label) + '</span></label></li>';
        }).join('');
        panel.innerHTML = '<div class="ant-popover-content"><div class="ant-popover-inner" role="dialog" aria-label="Columns">' +
            '<div class="ant-popover-title"><div class="m-grid-columns-head"><span>Columns</span><span class="m-muted">' + shown + ' of ' + cols.length + ' shown</span></div></div>' +
            '<div class="ant-popover-inner-content"><ul class="m-grid-columns" role="list">' + items + '</ul>' +
            '<div class="m-table-cols-foot"><button type="button" class="ant-btn ant-btn-link ant-btn-sm" data-cols-reset' + (hidden.length ? '' : ' disabled') + '><span>Show all</span></button></div></div></div></div>';
    }
    function openColsPanel(button) {
        var card = tableCard(button);
        if (!card) return;
        closeColsPanel();
        var panel = document.createElement('div');
        panel.className = 'ant-popover ant-popover-placement-bottomRight m-grid-columns-popover m-table-cols-pop';
        panel._card = card;
        panel._button = button;
        renderColsPanel(panel, card);
        document.body.appendChild(panel);
        var r = button.getBoundingClientRect(), vw = document.documentElement.clientWidth;
        panel.style.position = 'fixed';
        panel.style.top = Math.round(r.bottom + 4) + 'px';
        panel.style.left = Math.round(Math.max(8, Math.min(r.right - panel.offsetWidth, vw - panel.offsetWidth - 8))) + 'px';
        button.setAttribute('aria-expanded', 'true');
        colsPanel = panel;
        var first = panel.querySelector('input:not(:disabled)');
        if (first) first.focus({ preventScroll: true });
    }
    document.addEventListener('change', function (e) {
        var box = e.target;
        if (!colsPanel || !(box instanceof HTMLInputElement) || !colsPanel.contains(box) || !box.hasAttribute('data-col')) return;
        var card = colsPanel._card, label = box.getAttribute('data-col');
        var hidden = readHiddenCols(card).filter(function (l) { return l !== label; });
        if (!box.checked) hidden.push(label);
        writeHiddenCols(card, hidden);
        applyHiddenCols(card);
        renderColsPanel(colsPanel, card);
        var again = colsPanel.querySelector('input[data-col="' + CSS.escape(label) + '"]');
        if (again) again.focus({ preventScroll: true });
    });

    function csvCell(v) {
        var s = String(v == null ? '' : v);
        if (/^[=+@\t\r]/.test(s)) s = "'" + s; // no formulas when opened in a spreadsheet
        return /[",\r\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s;
    }
    function cellText(td) {
        var field = td.querySelector('input:not([type=hidden]):not([type=checkbox]), select, textarea');
        if (field && field.offsetParent !== null) return field.value;
        return (td.innerText || td.textContent || '').replace(/\s+/g, ' ').trim();
    }
    function exportTable(card, name) {
        var hidden = readHiddenCols(card);
        var cols = tableColumns(card).filter(function (c) { return hidden.indexOf(c.label) < 0; });
        var rows = Array.prototype.filter.call(card.querySelectorAll('tbody > tr.ant-table-row'), function (tr) {
            return tableCard(tr) === card && !tr.parentElement.closest('.ant-table-expanded-row');
        });
        var lines = [cols.map(function (c) { return csvCell(c.label); }).join(',')];
        rows.forEach(function (tr) {
            lines.push(cols.map(function (c) { var td = tr.children[c.index - 1]; return csvCell(td ? cellText(td) : ''); }).join(','));
        });
        var blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = (name || 'export').replace(/[\\/:*?"<>|]+/g, '-') + '.csv';
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
    }

    document.addEventListener('click', function (e) {
        var t = e.target instanceof Element ? e.target : null;
        // A checkbox click re-renders the panel, so its label is detached by the time the click reaches here.
        if (!t || !t.isConnected) return;
        var colsButton = t.closest('[data-m-table-columns]');
        if (colsButton) {
            e.preventDefault();
            if (colsPanel && colsPanel._button === colsButton) closeColsPanel(); else openColsPanel(colsButton);
            return;
        }
        if (colsPanel && t.closest('[data-cols-reset]') && colsPanel.contains(t)) {
            writeHiddenCols(colsPanel._card, []);
            applyHiddenCols(colsPanel._card);
            renderColsPanel(colsPanel, colsPanel._card);
            return;
        }
        if (colsPanel && !colsPanel.contains(t)) closeColsPanel();
        var exportButton = t.closest('[data-m-table-export]');
        if (exportButton) {
            var card = tableCard(exportButton);
            if (!card) return;
            e.preventDefault();
            var title = card.querySelector('.m-table-toolbar-title');
            exportTable(card, exportButton.getAttribute('data-m-table-export') || (title && title.firstChild && title.firstChild.textContent.trim()) || 'export');
        }
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && colsPanel) { e.preventDefault(); closeColsPanel(); } });
    window.addEventListener('resize', closeColsPanel);
    document.addEventListener('scroll', function (e) { if (colsPanel && !(e.target instanceof Node && colsPanel.contains(e.target))) closeColsPanel(); }, { capture: true, passive: true });
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', applyAllHiddenCols); else applyAllHiddenCols();
    document.addEventListener('htmx:load', applyAllHiddenCols);

    function visible(el) { return !!el && !el.disabled && el.offsetParent !== null && getComputedStyle(el).visibility !== 'hidden'; }
    // The grid a key press belongs to: the one holding the focus, or the only grid on the page.
    function keyGrid(target) {
        var card = tableCard(target);
        if (card) return card;
        var grids = document.querySelectorAll('.m-grid');
        return grids.length === 1 ? grids[0] : null;
    }
    function own(card, selector) {
        var found = card.querySelectorAll(selector);
        for (var i = 0; i < found.length; i++) if (tableCard(found[i]) === card && visible(found[i])) return found[i];
        return null;
    }
    function editing(target) {
        return target instanceof Element && !!target.closest('input, textarea, select, [contenteditable="true"], .m-grid-cell-editing, .ant-select, .ant-picker');
    }
    // The cell in the same column of the next shown row (dy), or the next cell in the row (dx).
    function neighbourCell(td, dx, dy) {
        var tr = td.parentElement;
        if (dx) {
            var sib = dx > 0 ? td.nextElementSibling : td.previousElementSibling;
            while (sib && !sib.querySelector('.m-grid-cell')) sib = dx > 0 ? sib.nextElementSibling : sib.previousElementSibling;
            return sib;
        }
        var key = td.getAttribute('data-key'), index = Array.prototype.indexOf.call(tr.children, td);
        var row = dy > 0 ? tr.nextElementSibling : tr.previousElementSibling;
        while (row && (row.hidden || !row.classList.contains('ant-table-row'))) row = dy > 0 ? row.nextElementSibling : row.previousElementSibling;
        if (!row) return null;
        return (key && row.querySelector(':scope > td[data-key="' + CSS.escape(key) + '"]')) || row.children[index] || null;
    }
    function focusCell(td) {
        var cell = td && td.querySelector('.m-grid-cell:not([hidden])');
        if (!cell) return false;
        if (!cell.hasAttribute('tabindex')) cell.setAttribute('tabindex', '-1');
        cell.focus();
        return true;
    }

    document.addEventListener('keydown', function (e) {
        var t = e.target instanceof Element ? e.target : null;
        var mod = e.ctrlKey || e.metaKey;
        if (mod && !e.altKey && !e.shiftKey && (e.key === 's' || e.key === 'S')) {
            var grid = keyGrid(t), save = grid && own(grid, '[data-m-grid-save]');
            if (!save) return;
            e.preventDefault();
            // Leave the cell first, so its value is committed before the save.
            if (document.activeElement && grid.contains(document.activeElement)) document.activeElement.blur();
            setTimeout(function () { save.click(); }, 0);
            return;
        }
        if (mod && !e.altKey && !e.shiftKey && (e.key === 'f' || e.key === 'F')) {
            var card = tableCard(t);
            if (!card) return;
            var box = own(card, '.m-table-toolbar-search input');
            var open = box || own(card, '[data-m-search-toggle]');
            if (!open) return;
            e.preventDefault();
            if (box) { box.focus(); box.select(); } else open.click();
            return;
        }
        if (!t || mod || e.altKey || editing(t)) return;
        var cardHere = tableCard(t);
        if (!cardHere || !cardHere.classList.contains('m-grid')) return;
        if (e.key === 'Insert') {
            var add = own(cardHere, '[data-m-grid-add]');
            if (add) { e.preventDefault(); add.click(); }
            return;
        }
        if (e.key === 'Delete') {
            var del = own(cardHere, '[data-m-grid-delete-selected]');
            if (del) { e.preventDefault(); del.click(); }
            return;
        }
        var move = { ArrowUp: [0, -1], ArrowDown: [0, 1], ArrowLeft: [-1, 0], ArrowRight: [1, 0] }[e.key];
        var td = move && t.closest('td');
        if (!td || !td.querySelector('.m-grid-cell') || tableCard(td) !== cardHere) return;
        var next = neighbourCell(td, move[0], move[1]);
        if (next && focusCell(next)) e.preventDefault();
    });

    // Grid scrollers publish their visible width as --m-grid-view, so a detail row's content stays in view while
    // the table scrolls sideways (the CSS can't use a container query there; see modulus.css).
    var gridViews = 'ResizeObserver' in window ? new ResizeObserver(function (entries) {
        entries.forEach(function (e) { e.target.style.setProperty('--m-grid-view', e.target.clientWidth + 'px'); });
    }) : null;
    var watchedViews = typeof WeakSet === 'function' ? new WeakSet() : null;
    function watchGridViews() {
        if (!gridViews || !watchedViews) return;
        document.querySelectorAll('.m-grid .ant-table-content').forEach(function (el) {
            if (watchedViews.has(el)) return;
            watchedViews.add(el);
            gridViews.observe(el);
        });
    }
    var gridViewsQueued = false;
    if ('MutationObserver' in window) new MutationObserver(function () {
        if (gridViewsQueued) return;
        gridViewsQueued = true;
        // Also re-applies hidden data-table columns after a Blazor render or navigation (a no-op when nothing changed).
        requestAnimationFrame(function () { gridViewsQueued = false; watchGridViews(); applyAllHiddenCols(); });
    }).observe(document.documentElement, { childList: true, subtree: true });
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watchGridViews); else watchGridViews();

    window.addEventListener('resize', placeSelects);
    document.addEventListener('scroll', function (e) {
        // Scrolling the option list itself must not move the dropdown.
        if (e.target instanceof Element && e.target.closest('.m-cselect-dropdown')) return;
        placeSelects();
    }, { capture: true, passive: true });

    // Arrow keys, Enter and Space on the trigger open and move through the list instead of scrolling the page.
    document.addEventListener('keydown', function (e) {
        var t = e.target;
        if (!(t instanceof Element) || !t.matches('.m-cselect > .ant-select-selector')) return;
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === ' ' || e.key === 'Enter') e.preventDefault();
    });

    // Sticky section navs (the component gallery pill) get a raised shadow once they stick under the header.
    function syncStuck() {
        document.querySelectorAll('.m-demo-nav').forEach(function (nav) {
            var top = parseFloat(getComputedStyle(nav).top) || 0;
            nav.classList.toggle('m-demo-nav-stuck', window.scrollY > 0 && nav.getBoundingClientRect().top <= top + 0.5);
        });
    }
    document.addEventListener('scroll', syncStuck, { capture: true, passive: true });
    window.addEventListener('resize', syncStuck);

    // ---- Resizable columns: drag the right edge of a header cell in any .m-resizable table ----
    // The grip is one bar on <body>, placed over the edge under the pointer, so nothing is injected into markup
    // that Blazor or htmx own. Dragging sets the <col> width (and grows the table), re-pins sticky columns, and
    // raises "m-col-resize" { key, width } on the header cell (the editable grids keep the width in their schema).
    var grip = null, gripTh = null, drag = null;
    function resizableTh(th) {
        return !!th && th.colSpan === 1 && !!th.closest('.m-resizable .ant-table-thead') &&
            !th.matches('.ant-table-selection-column, .ant-table-row-expand-icon-cell, .m-grid-actions, .m-no-resize');
    }
    function columnIndex(cell) {
        var i = 0;
        for (var c = cell.previousElementSibling; c; c = c.previousElementSibling) i += c.colSpan || 1;
        return i;
    }
    function thKey(th) {
        var el = th.hasAttribute('data-key') ? th : th.querySelector('[data-key]');
        return el ? el.getAttribute('data-key') : null;
    }
    // Sticky offsets follow the header widths: a left-pinned cell sits after every column before it, a right-pinned one before every column after it.
    function syncSticky(table) {
        var head = table.querySelector('.ant-table-thead > tr');
        if (!head) return;
        var widths = [];
        Array.prototype.forEach.call(head.children, function (th) { widths.push(th.getBoundingClientRect().width); });
        var total = widths.reduce(function (a, b) { return a + b; }, 0);
        table.querySelectorAll('tr').forEach(function (tr) {
            if (tr.closest('table') !== table || tr.classList.contains('ant-table-expanded-row') || tr.classList.contains('ant-table-placeholder')) return;
            Array.prototype.forEach.call(tr.children, function (cell) {
                var left = cell.classList.contains('ant-table-cell-fix-left'), right = cell.classList.contains('ant-table-cell-fix-right');
                if (!left && !right) return;
                var i = columnIndex(cell), before = 0;
                for (var k = 0; k < i; k++) before += widths[k] || 0;
                if (left) cell.style.left = Math.round(before) + 'px';
                else cell.style.right = Math.round(total - before - (widths[i] || 0)) + 'px';
            });
        });
    }
    function hideGrip() { if (grip && !drag) { grip.style.display = 'none'; gripTh = null; } }
    function ensureGrip() {
        if (grip && grip.isConnected) return grip;
        grip = document.createElement('div');
        grip.className = 'm-col-resize-grip';
        grip.setAttribute('aria-hidden', 'true');
        grip.style.display = 'none';
        grip.addEventListener('pointerdown', function (e) {
            if (!gripTh || e.button !== 0) return;
            var th = gripTh, table = th.closest('table'), col = table && table.querySelector('colgroup') && table.querySelector('colgroup').children[columnIndex(th)];
            if (!col) return;
            e.preventDefault();
            grip.setPointerCapture(e.pointerId);
            drag = { th: th, table: table, col: col, x: e.clientX, w: th.getBoundingClientRect().width, tw: table.getBoundingClientRect().width, width: 0 };
            document.documentElement.classList.add('m-col-resizing');
        });
        grip.addEventListener('pointermove', function (e) {
            if (!drag) return;
            var w = Math.max(60, Math.round(drag.w + e.clientX - drag.x));
            drag.width = w;
            drag.col.style.width = drag.col.style.minWidth = w + 'px';
            drag.table.style.width = Math.round(drag.tw + w - drag.w) + 'px';
            var r = drag.th.getBoundingClientRect();
            grip.style.left = Math.round(r.right - 4) + 'px';
            syncSticky(drag.table);
        });
        var end = function () {
            if (!drag) return;
            var d = drag;
            drag = null;
            document.documentElement.classList.remove('m-col-resizing');
            hideGrip();
            if (d.width) d.th.dispatchEvent(new CustomEvent('m-col-resize', { bubbles: true, detail: { key: thKey(d.th), width: d.width } }));
        };
        grip.addEventListener('pointerup', end);
        grip.addEventListener('pointercancel', end);
        document.body.appendChild(grip);
        return grip;
    }
    document.addEventListener('pointermove', function (e) {
        if (drag || e.pointerType === 'touch' || e.target === grip) return;
        var th = e.target instanceof Element ? e.target.closest('th') : null;
        if (!resizableTh(th)) { hideGrip(); return; }
        var r = th.getBoundingClientRect();
        if (r.right - e.clientX > 6) { hideGrip(); return; }
        var g = ensureGrip();
        gripTh = th;
        g.style.cssText = 'display: block; left: ' + Math.round(r.right - 4) + 'px; top: ' + Math.round(r.top) + 'px; height: ' + Math.round(r.height) + 'px;';
    }, { passive: true });
    document.addEventListener('scroll', hideGrip, { capture: true, passive: true });
    window.modulusShell.syncSticky = syncSticky;

    // Blazor grids hear about a resize through a .NET reference (custom DOM events don't reach Blazor handlers).
    window.modulusShell.watchResize = function (root, dotnet) {
        if (typeof root === 'string') root = document.getElementById(root);
        if (!root || root._mResize) return;
        root._mResize = function (e) { if (e.detail.key) dotnet.invokeMethodAsync('OnColumnResized', e.detail.key, e.detail.width); };
        root.addEventListener('m-col-resize', root._mResize);
    };

    // Saves text as a file (the grids' CSV export). A BOM keeps Excel reading UTF-8.
    window.modulusShell.download = function (name, text, type) {
        var blob = new Blob(['﻿', text], { type: type || 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = name;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
    };

    // Posts a native form as a full page request. Blazor Server calls it once its own form has validated, for work a
    // circuit can't do, such as setting the sign-in cookie.
    window.modulusShell.submit = function (form) {
        if (form instanceof HTMLFormElement) form.submit();
    };
    // ---- Business fields (Modulus.Ui.Abstractions.Forms, mirrored for the browser) ----------------------------
    // The same rules as NumberText, DurationText and InputMask, so a value formatted in the browser matches one
    // rendered on the server. The MVC Alpine components (modulus-widgets.js) and the delegated handlers below use them.
    var fields = window.modulusShell.fields = {};

    // NumberText.TryParse: "1,234.50", "(12.00)", "12-" → number; null for anything else.
    fields.parseNumber = function (text) {
        if (text == null) return null;
        var s = String(text).trim(), neg = false;
        if (!s) return null;
        if (s.length > 1 && s[0] === '(' && s[s.length - 1] === ')') { neg = true; s = s.slice(1, -1).trim(); }
        if (s[0] === '-') { neg = !neg; s = s.slice(1).trim(); }
        else if (s[s.length - 1] === '-') { neg = !neg; s = s.slice(0, -1).trim(); }
        else if (s[0] === '+') s = s.slice(1).trim();
        var out = '', dot = false;
        for (var i = 0; i < s.length; i++) {
            var c = s[i];
            if (c >= '0' && c <= '9') out += c;
            else if (c === '.' && !dot) { dot = true; out += c; }
            else if (c === ',' || c === ' ' || c.charCodeAt(0) === 160 || c === '\'' || c === '_') continue;
            else return null;
        }
        if (!out || out === '.') return null;
        var v = parseFloat(out);
        return isNaN(v) ? null : neg ? -v : v;
    };

    // NumberText.Round: half away from zero (toPrecision drops binary noise such as 1.005 * 100 = 100.4999…).
    fields.round = function (v, decimals) {
        var f = Math.pow(10, Math.max(0, Math.min(10, decimals)));
        var r = Math.round(Number((Math.abs(v) * f).toPrecision(15))) / f;
        return v < 0 ? -r : r;
    };

    // NumberText.Format: "1,234.50"; "" for null.
    fields.formatNumber = function (v, decimals, group) {
        if (v == null || isNaN(v)) return '';
        var d = Math.max(0, Math.min(10, decimals)), r = fields.round(v, d);
        var parts = Math.abs(r).toFixed(d).split('.');
        if (group !== false) parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        return (r < 0 ? '-' : '') + parts.join('.');
    };

    // NumberText.Invariant: the posted value, "1234.5".
    fields.invariant = function (v) { return v == null || isNaN(v) ? '' : String(v); };

    // DurationText.TryParse → whole minutes, or null.
    fields.parseDuration = function (text) {
        var s = String(text == null ? '' : text).trim().toLowerCase(), m, total;
        if (!s) return null;
        var num = function (x) { return parseFloat(x.replace(',', '.')); };
        if ((m = /^(\d{1,4}):([0-5]?\d)$/.exec(s))) total = parseInt(m[1], 10) * 60 + parseInt(m[2], 10);
        else if ((m = /^(\d{1,4}(?:[.,]\d+)?)\s*(?:h|hr|hrs|hours?)?$/.exec(s))) total = num(m[1]) * 60;
        else if ((m = /^(?:(\d{1,4}(?:[.,]\d+)?)\s*(?:h|hr|hrs|hours?))?\s*(?:(\d{1,6})\s*(?:m|min|mins|minutes?)?)?$/.exec(s)) && (m[1] || m[2]))
            total = (m[1] ? num(m[1]) * 60 : 0) + (m[2] ? parseInt(m[2], 10) : 0);
        else return null;
        if (total > 9999 * 60) return null;
        return Math.round(total);
    };

    // DurationText.Format: "1:30"; "" for null.
    fields.formatDuration = function (minutes) {
        if (minutes == null || isNaN(minutes)) return '';
        var m = Math.round(minutes);
        return Math.floor(m / 60) + ':' + ('0' + (m % 60)).slice(-2);
    };

    // InputMask: 9 digit, A letter, * letter or digit, \ escapes, anything else is a literal.
    function maskSlots(mask) {
        var slots = [];
        for (var i = 0; i < mask.length; i++) {
            var c = mask[i];
            if (c === '\\' && i + 1 < mask.length) slots.push({ lit: mask[++i] });
            else if (c === '9' || c === 'A' || c === '*') slots.push({ kind: c, lit: null });
            else slots.push({ lit: c });
        }
        return slots;
    }
    function maskAccepts(kind, c) {
        return kind === '9' ? /[0-9]/.test(c) : kind === 'A' ? /[A-Za-z]/.test(c) : /[A-Za-z0-9]/.test(c);
    }
    fields.applyMask = function (mask, text) {
        text = String(text == null ? '' : text);
        if (!text || !mask) return '';
        var slots = maskSlots(mask), out = '', t = 0;
        for (var i = 0; i < text.length && t < slots.length; i++) {
            var c = text[i], k = t, pending = '', typed = false;
            while (k < slots.length && slots[k].lit !== null) {
                if (c === slots[k].lit) { out += pending + c; t = k + 1; typed = true; break; }
                pending += slots[k].lit;
                k++;
            }
            if (typed || k >= slots.length || !maskAccepts(slots[k].kind, c)) continue;
            out += pending + (slots[k].kind === '9' ? c : c.toUpperCase());
            t = k + 1;
        }
        return out;
    };

    // Masked inputs: fit the text before any other handler reads it (capture phase on document runs first),
    // keeping the caret after the same number of letters and digits.
    document.addEventListener('input', function (e) {
        var el = e.target;
        if (!(el instanceof HTMLInputElement) || !el.hasAttribute('data-m-mask')) return;
        var before = el.value, after = fields.applyMask(el.getAttribute('data-m-mask'), before);
        if (after === before) return;
        var caret = el.selectionStart == null ? before.length : el.selectionStart;
        var atEnd = caret >= before.length;
        var wanted = before.slice(0, caret).replace(/[^A-Za-z0-9]/g, '').length;
        el.value = after;
        var pos = after.length;
        if (!atEnd) for (var n = 0, i = 0; i < after.length; i++) { if (n >= wanted) { pos = i; break; } if (/[A-Za-z0-9]/.test(after[i])) n++; }
        try { el.setSelectionRange(pos, pos); } catch (err) { /* not focusable */ }
    }, true);

    // ---- One-time code boxes (.ant-otp: AppOtpInput / app-otp-input) ----
    // Each box holds one character. Typing moves to the next box, Backspace in an empty box clears the one before,
    // arrows move, and a paste (or an SMS autofill into one box) spreads over the boxes. Every change raises "input"
    // on its box, so Blazor handlers see it; MVC keeps the joined code in the group's hidden input.
    var otpFilling = false;
    function otpBoxes(root) { return Array.prototype.slice.call(root.querySelectorAll('.ant-otp-input')); }
    function otpAllowed(root, c) { return root.getAttribute('data-m-otp') === 'numeric' ? /[0-9]/.test(c) : /[A-Za-z0-9]/.test(c); }
    function otpSync(root) {
        var hidden = root.querySelector('input[type=hidden]');
        if (!hidden) return;
        hidden.value = otpBoxes(root).map(function (b) { return b.value; }).join('');
        hidden.dispatchEvent(new Event('change', { bubbles: true }));
        if (otpBoxes(root).every(function (b) { return b.value; })) root.dispatchEvent(new CustomEvent('m-otp-complete', { bubbles: true, detail: hidden.value }));
    }
    function otpSet(box, value) {
        otpFilling = true;
        box.value = value;
        box.dispatchEvent(new Event('input', { bubbles: true }));
        otpFilling = false;
    }
    function otpFill(root, start, text) {
        var boxes = otpBoxes(root), i = start;
        String(text).split('').filter(function (c) { return otpAllowed(root, c); }).forEach(function (c) {
            if (i < boxes.length) otpSet(boxes[i++], root.getAttribute('data-m-otp') === 'numeric' ? c : c.toUpperCase());
        });
        boxes[Math.min(i, boxes.length - 1)].focus();
        otpSync(root);
    }
    document.addEventListener('input', function (e) {
        var el = e.target;
        if (otpFilling || !(el instanceof HTMLInputElement) || !el.classList.contains('ant-otp-input')) return;
        var root = el.closest('.ant-otp');
        if (!root) return;
        var boxes = otpBoxes(root), i = boxes.indexOf(el), v = el.value;
        // A key typed into a box that already has a character replaces it (the caret was not over the old one).
        if (v.length > 1 && e.inputType === 'insertText' && e.data && e.data.length === 1) v = el.value = e.data;
        if (v.length > 1) {
            // Autofill into one box: keep the first character here and spread the rest now, so the focus has moved
            // on before the next key arrives.
            el.value = otpAllowed(root, v[0]) ? (root.getAttribute('data-m-otp') === 'numeric' ? v[0] : v[0].toUpperCase()) : '';
            otpFill(root, i + 1, v.slice(1));
            return;
        }
        if (v && !otpAllowed(root, v)) el.value = '';
        else if (v && root.getAttribute('data-m-otp') !== 'numeric') el.value = v.toUpperCase();
        if (el.value && i < boxes.length - 1) boxes[i + 1].focus();
        setTimeout(function () { otpSync(root); });
    }, true);
    // A typed key replaces the box's character and moves the focus on before the browser inserts anything, so fast
    // typing never depends on where the caret was or on the focus select having run yet.
    document.addEventListener('beforeinput', function (e) {
        var el = e.target;
        if (e.inputType !== 'insertText' || !e.data || e.data.length !== 1) return;
        if (!(el instanceof HTMLInputElement) || !el.classList.contains('ant-otp-input')) return;
        var root = el.closest('.ant-otp');
        if (!root) return;
        e.preventDefault();
        if (!otpAllowed(root, e.data)) return;
        var boxes = otpBoxes(root), i = boxes.indexOf(el);
        otpSet(el, root.getAttribute('data-m-otp') === 'numeric' ? e.data : e.data.toUpperCase());
        if (i < boxes.length - 1) boxes[i + 1].focus();
        otpSync(root);
    }, true);
    document.addEventListener('keydown', function (e) {
        var el = e.target;
        if (!(el instanceof HTMLInputElement) || !el.classList.contains('ant-otp-input')) return;
        var root = el.closest('.ant-otp'), boxes = otpBoxes(root), i = boxes.indexOf(el);
        if (e.key === 'Backspace' && !el.value && i > 0) { e.preventDefault(); otpSet(boxes[i - 1], ''); boxes[i - 1].focus(); otpSync(root); }
        else if (e.key === 'ArrowLeft' && i > 0) { e.preventDefault(); boxes[i - 1].focus(); }
        else if (e.key === 'ArrowRight' && i < boxes.length - 1) { e.preventDefault(); boxes[i + 1].focus(); }
    }, true);
    document.addEventListener('paste', function (e) {
        var el = e.target;
        if (!(el instanceof HTMLInputElement) || !el.classList.contains('ant-otp-input')) return;
        e.preventDefault();
        var root = el.closest('.ant-otp');
        otpFill(root, otpBoxes(root).indexOf(el), (e.clipboardData || window.clipboardData).getData('text'));
    }, true);
    document.addEventListener('focusin', function (e) {
        var el = e.target;
        // select() would also focus the box, so skip it once the focus has moved on (fast typing).
        if (el instanceof HTMLInputElement && el.classList.contains('ant-otp-input')) setTimeout(function () { if (document.activeElement === el) el.select(); });
    });

    // ---- Lookup fields (.m-lookup): keys that must not scroll the page or submit the form while the list is open ----
    document.addEventListener('keydown', function (e) {
        var el = e.target;
        if (!(el instanceof HTMLInputElement) || !el.classList.contains('m-lookup-input')) return;
        var open = !!el.closest('.m-lookup-open');
        if (e.key === 'F4' || e.key === 'ArrowDown' || e.key === 'ArrowUp' || (e.key === 'Enter' && open)) e.preventDefault();
    });
    document.addEventListener('keydown', function (e) {
        var el = e.target;
        if (el instanceof HTMLInputElement && el.classList.contains('m-lookup-search-input') && (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Enter')) e.preventDefault();
    });
    window.modulusShell.scrollLookupRow = function (root) {
        var row = root && root.querySelector('.m-lookup-row-active, .m-lookup-dropdown .ant-select-item-option-active');
        if (row) row.scrollIntoView({ block: 'nearest' });
    };
    window.modulusShell.focus = function (el) { if (el && el.focus) el.focus({ preventScroll: true }); };

    // ---- Signature pad (.m-signature: AppSignaturePad / app-signature-pad) ----
    // Draws with the pointer on a white "paper" canvas and publishes a PNG data URL: into the hidden input (MVC) and
    // to dotnet.OnSignature (Blazor). A typed name is drawn in a script face instead. The drawing is redrawn from the
    // value when the pad resizes. Call again with a value to show it (Blazor, when the bound value changes).
    window.modulusShell.signature = function (root, dotnet, value) {
        if (!root) return;
        if (root._mSig) { if (value !== undefined) root._mSig.load(value || ''); return; }
        var canvas = root.querySelector('.m-signature-canvas'), hidden = root.querySelector('input[type=hidden]');
        var typed = root.querySelector('.m-signature-type'), ctx = canvas.getContext('2d');
        var data = value != null ? value : hidden ? hidden.value : '', drawing = false, last = null, timer = 0;
        var ink = root.getAttribute('data-ink') || '#111827';
        var disabled = root.classList.contains('m-signature-disabled');
        function size() {
            var r = canvas.getBoundingClientRect(), dpr = window.devicePixelRatio || 1;
            var w = Math.max(1, Math.round(r.width * dpr)), h = Math.max(1, Math.round(r.height * dpr));
            if (canvas.width !== w || canvas.height !== h) { canvas.width = w; canvas.height = h; }
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            return r;
        }
        function draw(url) {
            var r = size();
            ctx.clearRect(0, 0, r.width, r.height);
            root.classList.toggle('m-signature-signed', !!url);
            if (!url) return;
            var img = new Image();
            img.onload = function () { var rr = size(); ctx.drawImage(img, 0, 0, rr.width, rr.height); };
            img.src = url;
        }
        function publish(url) {
            data = url;
            root.classList.toggle('m-signature-signed', !!url);
            if (hidden) { hidden.value = url; hidden.dispatchEvent(new Event('change', { bubbles: true })); }
            if (dotnet) dotnet.invokeMethodAsync('OnSignature', url);
        }
        function point(e) { var r = canvas.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; }
        function pen() { ctx.strokeStyle = ink; ctx.lineWidth = 2.2; ctx.lineCap = 'round'; ctx.lineJoin = 'round'; }
        canvas.addEventListener('pointerdown', function (e) {
            if (disabled || e.button !== 0) return;
            e.preventDefault();
            canvas.setPointerCapture(e.pointerId);
            if (typed && typed.value) { typed.value = ''; draw(''); }
            drawing = true;
            last = point(e);
            pen();
            ctx.beginPath();
            ctx.moveTo(last.x, last.y);
            ctx.lineTo(last.x + 0.01, last.y);
            ctx.stroke();
            root.classList.add('m-signature-signed');
        });
        canvas.addEventListener('pointermove', function (e) {
            if (!drawing) return;
            var p = point(e);
            ctx.beginPath();
            ctx.moveTo(last.x, last.y);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
            last = p;
        });
        function end() { if (!drawing) return; drawing = false; publish(canvas.toDataURL('image/png')); }
        canvas.addEventListener('pointerup', end);
        canvas.addEventListener('pointercancel', end);
        root.addEventListener('click', function (e) {
            if (disabled || !(e.target instanceof Element) || !e.target.closest('[data-m-signature-clear]')) return;
            if (typed) typed.value = '';
            draw('');
            publish('');
        });
        if (typed) typed.addEventListener('input', function () {
            var name = typed.value.trim(), r = size();
            ctx.clearRect(0, 0, r.width, r.height);
            root.classList.toggle('m-signature-signed', !!name);
            clearTimeout(timer);
            if (!name) { timer = setTimeout(function () { publish(''); }, 250); return; }
            var px = Math.round(Math.min(r.height * 0.42, r.width * 1.6 / Math.max(4, name.length)));
            ctx.fillStyle = ink;
            ctx.font = 'italic ' + px + 'px "Segoe Script", "Brush Script MT", "Snell Roundhand", "Apple Chancery", cursive';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(name, r.width / 2, r.height * 0.5);
            timer = setTimeout(function () { publish(canvas.toDataURL('image/png')); }, 250);
        });
        if ('ResizeObserver' in window) new ResizeObserver(function () { if (!drawing) draw(data); }).observe(canvas);
        root._mSig = { load: function (url) { if (url !== data) { data = url; draw(url); } } };
        draw(data);
    };

    // ---- Rich text (.m-richtext: AppRichText / app-rich-text) ----
    // Toolbar buttons run editing commands on the editor (mousedown keeps the selection), pastes arrive as plain
    // text, and every change is written to the hidden input (MVC) or sent to dotnet.OnRichTextInput (Blazor,
    // debounced). The server cleans what it receives with RichText.Sanitize.
    function richEditor(root) { return root.querySelector('.m-richtext-editor'); }
    function richChanged(root) {
        var editor = richEditor(root), html = editor.innerHTML;
        if (!editor.textContent.trim() && !editor.querySelector('li, hr')) html = '';
        root.classList.toggle('m-richtext-empty', !html);
        var hidden = root.querySelector('input[type=hidden]');
        if (hidden) { hidden.value = html; hidden.dispatchEvent(new Event('change', { bubbles: true })); }
        var state = root._mRich;
        if (state && state.dotnet) {
            clearTimeout(state.timer);
            state.timer = setTimeout(function () { state.html = html; state.dotnet.invokeMethodAsync('OnRichTextInput', html); }, 300);
        }
    }
    function richToolbar(root) {
        root.querySelectorAll('.m-richtext-btn[data-state]').forEach(function (b) {
            var on = false;
            try { on = document.queryCommandState(b.getAttribute('data-cmd')); } catch (err) { /* unsupported */ }
            b.classList.toggle('m-richtext-btn-active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }
    function richLink() {
        var url = window.prompt('Link address', 'https://');
        if (!url || url === 'https://') return null;
        url = url.trim();
        return /^(https?:|mailto:|tel:|\/|#)/i.test(url) ? url : 'https://' + url;
    }
    function richCommand(root, cmd, arg) {
        var editor = richEditor(root);
        if (!editor || editor.getAttribute('contenteditable') === 'false' || !editor.hasAttribute('contenteditable')) return;
        editor.focus();
        if (cmd === 'createLink') { arg = richLink(); if (!arg) return; }
        document.execCommand(cmd, false, arg || null);
        richChanged(root);
        richToolbar(root);
    }
    document.addEventListener('mousedown', function (e) {
        if (e.target instanceof Element && e.target.closest('.m-richtext-btn')) e.preventDefault();
    });
    document.addEventListener('click', function (e) {
        var btn = e.target instanceof Element && e.target.closest('.m-richtext-btn');
        if (!btn || btn.disabled) return;
        richCommand(btn.closest('.m-richtext'), btn.getAttribute('data-cmd'), btn.getAttribute('data-arg'));
    });
    document.addEventListener('input', function (e) {
        var editor = e.target instanceof Element && e.target.closest('.m-richtext-editor');
        if (editor) richChanged(editor.closest('.m-richtext'));
    });
    document.addEventListener('paste', function (e) {
        var editor = e.target instanceof Element && e.target.closest('.m-richtext-editor');
        if (!editor) return;
        e.preventDefault();
        document.execCommand('insertText', false, (e.clipboardData || window.clipboardData).getData('text/plain'));
    });
    document.addEventListener('keydown', function (e) {
        var editor = e.target instanceof Element && e.target.closest('.m-richtext-editor');
        if (editor && (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); richCommand(editor.closest('.m-richtext'), 'createLink'); }
    });
    document.addEventListener('selectionchange', function () {
        var sel = document.getSelection(), node = sel && sel.anchorNode;
        var el = node && (node.nodeType === 1 ? node : node.parentElement);
        var root = el && el.closest && el.closest('.m-richtext');
        if (root) richToolbar(root);
    });
    window.modulusShell.richText = function (root, dotnet, html) {
        if (!root) return;
        if (!root._mRich) root._mRich = { dotnet: dotnet, html: null, timer: 0 };
        else if (dotnet) root._mRich.dotnet = dotnet;
        if (html != null && html !== root._mRich.html) {
            root._mRich.html = html;
            var editor = richEditor(root);
            if (editor && editor.innerHTML !== html) editor.innerHTML = html;
            root.classList.toggle('m-richtext-empty', !html);
        }
    };

    // ---- Lightbox: AppLightbox, <app-lightbox>, <app-image> and <app-image-group> ----
    // A [data-m-lightbox] root carries its images as data-items JSON (LightboxItem.Json) and a hidden
    // .m-lightbox-source holding the overlay: Ant Design's image preview DOM plus a caption and a thumbnail strip.
    // Clicking any [data-lightbox-index] inside the root clones that overlay into <body> (clear of the cards'
    // stacking and of the DOM Blazor or htmx own) and drives it from here, so both flavours behave the same:
    // zoom (toolbar, wheel, + - 0, double click), rotate, drag to pan, swipe or arrow keys to page, download.
    // The page does not scroll and Tab stays inside while it is open; closing returns focus to the thumbnail.
    var LB_ZOOM = 1.5, LB_WHEEL = 1.2, LB_SWIPE = 80, LB_MAX = 8;
    var lightbox = null;

    document.addEventListener('click', function (e) {
        var trigger = e.target instanceof Element && e.target.closest('[data-lightbox-index]');
        var root = trigger && trigger.closest('[data-m-lightbox]');
        if (!root) return;
        e.preventDefault();
        openLightbox(root, parseInt(trigger.getAttribute('data-lightbox-index'), 10) || 0, trigger);
    });
    window.addEventListener('popstate', function () { if (lightbox) lightbox.close(); });

    function lightboxLock(on) {
        var body = document.body;
        var gap = window.innerWidth - document.documentElement.clientWidth;
        body.style.overflow = on ? 'hidden' : '';
        body.style.paddingRight = on && gap > 0 ? gap + 'px' : '';
    }

    function lightboxFileName(item) {
        var ext = /^data:image\/(svg|png|jpe?g|gif|webp)/i.exec(item.src) || /\.(\w{3,4})(?:[?#]|$)/.exec(item.src);
        var name = (item.title || 'image').replace(/[\\/:*?"<>|]+/g, '').trim() || 'image';
        return ext ? name + '.' + ext[1].toLowerCase().replace('jpeg', 'jpg') : name;
    }

    function openLightbox(root, index, opener) {
        var items, source = root.querySelector('.m-lightbox-source');
        try { items = JSON.parse(root.getAttribute('data-items') || '[]'); } catch (err) { items = []; }
        if (!items.length || !source || !source.firstElementChild) return;
        if (lightbox) lightbox.close(true);

        var el = source.firstElementChild.cloneNode(true);
        var part = function (name) { return el.querySelector('[data-lb="' + name + '"]'); };
        var img = part('img'), pan = part('pan'), wrap = el.querySelector('.ant-image-preview-wrap');
        var footer = part('footer'), thumbs = part('thumbs'), spin = part('spin');
        var many = items.length > 1;
        var captioned = items.some(function (item) { return item.title || item.caption; });
        var s = { index: -1, scale: 1, rotate: 0, x: 0, y: 0, dragged: false };

        ['progress', 'prev', 'next'].forEach(function (n) { var p = part(n); if (p) p.hidden = !many; });
        // The caption keeps its place on every image when any image has one, so the image does not jump.
        var text = part('text');
        if (text) text.hidden = !captioned;
        if (footer) footer.hidden = !many && !captioned;
        if (thumbs) {
            thumbs.hidden = !many;
            items.forEach(function (item, i) {
                var b = document.createElement('button');
                b.type = 'button';
                b.className = 'm-lightbox-strip-thumb';
                b.setAttribute('data-lb-go', i);
                b.setAttribute('aria-label', 'Image ' + (i + 1) + (item.title ? ': ' + item.title : ''));
                var t = document.createElement('img');
                t.src = item.thumb || item.src;
                t.alt = '';
                t.loading = 'lazy';
                b.appendChild(t);
                thumbs.appendChild(b);
            });
        }

        function apply() {
            pan.style.transform = 'translate3d(' + s.x + 'px, ' + s.y + 'px, 0)';
            img.style.transform = 'scale3d(' + s.scale + ', ' + s.scale + ', 1) rotate(' + s.rotate + 'deg)';
            var zin = part('zoom-in'), zout = part('zoom-out');
            if (zin) { zin.disabled = s.scale >= LB_MAX; zin.classList.toggle('ant-image-preview-operations-operation-disabled', zin.disabled); }
            if (zout) { zout.disabled = s.scale <= 1; zout.classList.toggle('ant-image-preview-operations-operation-disabled', zout.disabled); }
        }
        // Keep a zoomed image from being dragged off screen; at 100% it sits centred.
        function clamp() {
            if (s.scale <= 1) { s.x = s.y = 0; return; }
            var sideways = Math.abs(s.rotate / 90) % 2 === 1;
            var w = (sideways ? img.offsetHeight : img.offsetWidth) * s.scale;
            var h = (sideways ? img.offsetWidth : img.offsetHeight) * s.scale;
            var mx = Math.max(0, (w - window.innerWidth) / 2), my = Math.max(0, (h - window.innerHeight) / 2);
            s.x = Math.min(mx, Math.max(-mx, s.x));
            s.y = Math.min(my, Math.max(-my, s.y));
        }
        function zoomTo(v) {
            s.scale = Math.round(Math.min(LB_MAX, Math.max(1, v)) * 100) / 100;
            clamp();
            apply();
        }
        function show(i) {
            if (i < 0 || i >= items.length || i === s.index) return;
            var item = items[i];
            s.index = i;
            s.scale = 1; s.rotate = 0; s.x = s.y = 0;
            apply();
            if (spin) spin.hidden = false;
            img.src = item.src;
            img.alt = item.title || '';
            if (img.complete && img.naturalWidth && spin) spin.hidden = true;
            wrap.setAttribute('aria-label', item.title || 'Image preview');
            var progress = part('progress');
            if (progress) progress.textContent = (i + 1) + ' / ' + items.length;
            var prev = part('prev'), next = part('next');
            if (prev) { prev.disabled = i === 0; prev.classList.toggle('ant-image-preview-switch-left-disabled', i === 0); }
            if (next) { next.disabled = i === items.length - 1; next.classList.toggle('ant-image-preview-switch-right-disabled', i === items.length - 1); }
            var title = part('title'), caption = part('caption');
            if (title) title.textContent = item.title || '';
            if (caption) caption.textContent = item.caption || '';
            var download = part('download');
            if (download) { download.href = item.src; download.setAttribute('download', lightboxFileName(item)); }
            if (thumbs) {
                Array.prototype.forEach.call(thumbs.children, function (b, n) {
                    if (n === i) b.setAttribute('aria-current', 'true');
                    else b.removeAttribute('aria-current');
                });
                var current = thumbs.children[i];
                if (current) thumbs.scrollTo({ left: current.offsetLeft - (thumbs.clientWidth - current.offsetWidth) / 2, behavior: 'smooth' });
            }
            // Room for the toolbar and the footer, so the image never sits under them at 100%.
            var bar = el.querySelector('.ant-image-preview-operations');
            pan.style.top = (bar ? bar.offsetHeight : 0) + 'px';
            pan.style.bottom = (footer && !footer.hidden ? footer.offsetHeight : 0) + 'px';
            [i - 1, i + 1].forEach(function (n) { if (items[n]) new Image().src = items[n].src; });
        }
        function go(d) { show(s.index + d); }
        function close(now) {
            if (lightbox !== api) return;
            lightbox = null;
            document.removeEventListener('keydown', key, true);
            lightboxLock(false);
            el.classList.add('m-image-preview-enter', 'm-image-preview-hidden');
            setTimeout(function () { el.remove(); }, now ? 0 : 200);
            if (opener && opener.isConnected && opener.focus) opener.focus();
        }
        function key(e) {
            switch (e.key) {
                case 'Escape': close(); break;
                case 'ArrowLeft': go(-1); break;
                case 'ArrowRight': go(1); break;
                case 'Home': show(0); break;
                case 'End': show(items.length - 1); break;
                case '+': case '=': zoomTo(s.scale * LB_ZOOM); break;
                case '-': case '_': zoomTo(s.scale / LB_ZOOM); break;
                case '0': zoomTo(1); break;
                case 'Tab': trap(e); return;
                default: return;
            }
            e.preventDefault();
            e.stopPropagation();
        }
        // Tab and Shift+Tab cycle through the overlay's visible, enabled controls.
        function trap(e) {
            var controls = Array.prototype.filter.call(el.querySelectorAll('button, a[href]'), function (c) {
                return !c.disabled && c.offsetParent !== null;
            });
            if (!controls.length) return;
            var first = controls[0], last = controls[controls.length - 1], active = document.activeElement;
            if (controls.indexOf(active) < 0 || (e.shiftKey && active === first) || (!e.shiftKey && active === last)) {
                e.preventDefault();
                (e.shiftKey ? last : first).focus();
            }
        }

        el.addEventListener('click', function (e) {
            var t = e.target instanceof Element ? e.target : null;
            var op = t && t.closest('[data-lb]');
            var name = op && op.getAttribute('data-lb');
            var thumb = t && t.closest('[data-lb-go]');
            if (thumb) show(parseInt(thumb.getAttribute('data-lb-go'), 10));
            else if (name === 'close') close();
            else if (name === 'zoom-in') zoomTo(s.scale * LB_ZOOM);
            else if (name === 'zoom-out') zoomTo(s.scale / LB_ZOOM);
            else if (name === 'rotate-right') { s.rotate += 90; clamp(); apply(); }
            else if (name === 'rotate-left') { s.rotate -= 90; clamp(); apply(); }
            else if (name === 'prev') go(-1);
            else if (name === 'next') go(1);
            // A click on the dark area closes, unless it ends a drag that started on the image.
            else if (t === wrap || t === pan || t === el.querySelector('.ant-image-preview-body')) {
                if (s.dragged) s.dragged = false;
                else close();
            }
        });
        img.addEventListener('load', function () { if (spin) spin.hidden = true; });
        img.addEventListener('error', function () { if (spin) spin.hidden = true; });
        img.addEventListener('dblclick', function () { zoomTo(s.scale > 1 ? 1 : 2); });
        wrap.addEventListener('wheel', function (e) {
            if (e.target instanceof Element && e.target.closest('[data-lb="footer"]')) return;
            e.preventDefault();
            if (e.deltaY) zoomTo(e.deltaY < 0 ? s.scale * LB_WHEEL : s.scale / LB_WHEEL);
        }, { passive: false });
        // Drag pans a zoomed image; at 100% a horizontal swipe pages and anything shorter springs back.
        img.addEventListener('pointerdown', function (e) {
            if (e.button !== 0) return;
            e.preventDefault();
            var sx = e.clientX, sy = e.clientY, ox = s.x, oy = s.y;
            s.dragged = false;
            el.classList.add('ant-image-preview-moving');
            function move(ev) {
                if (Math.abs(ev.clientX - sx) + Math.abs(ev.clientY - sy) > 4) s.dragged = true;
                s.x = ox + ev.clientX - sx;
                s.y = oy + ev.clientY - sy;
                apply();
            }
            function up(ev) {
                window.removeEventListener('pointermove', move);
                window.removeEventListener('pointerup', up);
                window.removeEventListener('pointercancel', up);
                el.classList.remove('ant-image-preview-moving');
                var dx = ev.clientX - sx, dy = ev.clientY - sy;
                if (s.scale > 1) clamp();
                else {
                    if (Math.abs(dx) > LB_SWIPE && Math.abs(dx) > Math.abs(dy)) go(dx < 0 ? 1 : -1);
                    s.x = s.y = 0;
                }
                apply();
            }
            window.addEventListener('pointermove', move);
            window.addEventListener('pointerup', up);
            window.addEventListener('pointercancel', up);
        });

        var api = { close: close };
        lightbox = api;
        el.classList.add('m-image-preview-hidden');
        document.body.appendChild(el);
        lightboxLock(true);
        document.addEventListener('keydown', key, true);
        show(Math.min(Math.max(index, 0), items.length - 1));
        requestAnimationFrame(function () {
            el.classList.add('m-image-preview-enter');
            el.classList.remove('m-image-preview-hidden');
        });
        var closeButton = part('close');
        if (closeButton) closeButton.focus();
    }
})();
