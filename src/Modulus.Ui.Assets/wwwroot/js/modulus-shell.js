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
 *   [data-m-customizer]    theme settings panel (colour mode, scheme, layout, width, radius)
 *   [data-m-mark-read]     marks header notifications as read
 *   [data-m-tenant]        tenant switcher: writes the modulus-tenant cookie and reloads (cancel modulus:tenant-change to
 *                          handle it yourself); [data-m-tenant-filter] filters the tenant list
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
        if (d.open && d.matches && d.matches('details.m-dropdown, details[data-m-dropdown]')) closeDropdowns(d);
    }, true);

    document.addEventListener('click', function (e) {
        var t = e.target;
        if (!(t instanceof Element)) return;
        if (!t.closest('details.m-dropdown, details[data-m-dropdown]')) closeDropdowns(null);
        var link = t.closest('a[href]');
        if (link) {
            var open = link.closest('details.m-dropdown[open], details[data-m-dropdown][open]');
            if (open) open.removeAttribute('open');
            if (link.closest('.m-sidebar')) closeNav();
            if (link.closest('.m-search-results')) closeSearch(true);
        }

        if (t.closest('[data-m-nav-open]')) { root.classList.add('m-nav-open'); return; }
        if (t.closest('[data-m-nav-close]')) { closeNav(); return; }
        if (t.closest('[data-m-theme-toggle]')) { window.modulusTheme && window.modulusTheme.toggle(); syncCustomizer(); return; }
        if (t.closest('[data-m-customizer-open]')) { openCustomizer(); return; }
        if (t.closest('[data-m-customizer-close]')) { closeCustomizer(); return; }
        if (t.closest('[data-m-customizer-reset]')) { window.modulusTheme && window.modulusTheme.reset(); return; }
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
            closeCustomizer();
            if (document.activeElement && document.activeElement.classList.contains('m-search-input')) closeSearch(true);
        }
        var typing = /^(INPUT|TEXTAREA|SELECT)$/.test((e.target && e.target.tagName) || '') || (e.target && e.target.isContentEditable);
        if ((e.key === 'k' || e.key === 'K') && (e.ctrlKey || e.metaKey)) { e.preventDefault(); openSearch(); }
        else if (e.key === '/' && !typing) { e.preventDefault(); openSearch(); }
    });

    // Tenant switcher -------------------------------------------------------------------

    function chooseTenant(id) {
        var ev = new CustomEvent('modulus:tenant-change', { detail: { id: id }, bubbles: true, cancelable: true });
        if (!document.dispatchEvent(ev)) return;
        document.cookie = 'modulus-tenant=' + encodeURIComponent(id) + '; path=/; max-age=31536000; samesite=lax';
        location.reload();
    }

    function filterTenants(input) {
        var menu = input.closest('.m-tenant-menu');
        var q = input.value.trim().toLowerCase();
        var shown = 0;
        menu.querySelectorAll('.m-tenant-item').forEach(function (item) {
            var match = !q || (item.getAttribute('data-m-tenant-name') || '').toLowerCase().indexOf(q) >= 0;
            item.hidden = !match;
            if (match) shown++;
        });
        var empty = menu.querySelector('.m-tenant-empty');
        if (empty) empty.hidden = shown > 0;
    }

    document.addEventListener('click', function (e) {
        var item = e.target instanceof Element && e.target.closest('[data-m-tenant]');
        if (!item) return;
        var d = item.closest('details');
        if (d) d.removeAttribute('open');
        if (item.getAttribute('aria-selected') !== 'true') chooseTenant(item.getAttribute('data-m-tenant'));
    });

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
            if (!href || seen[href]) return;
            seen[href] = true;
            var group = a.closest('.m-snav-sub');
            var section = '';
            if (group) {
                var summary = group.parentElement && group.parentElement.querySelector(':scope > summary .m-snav-title');
                section = summary ? summary.textContent.trim() : '';
            }
            var icon = a.querySelector('svg use');
            items.push({ href: href, label: a.textContent.trim(), section: section, icon: icon ? icon.getAttribute('href') : null });
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
            var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('class', 'm-ti');
            svg.setAttribute('aria-hidden', 'true');
            var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
            use.setAttribute('href', m.icon || '/_content/Modulus.Ui.Assets/icons/tabler.svg#ti-file');
            svg.appendChild(use);
            a.appendChild(svg);
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

    // Theme settings panel ------------------------------------------------------------------------

    var lastFocus = null;

    function customizer() { return document.querySelector('[data-m-customizer]'); }

    function openCustomizer() {
        var panel = customizer();
        if (!panel) return;
        lastFocus = document.activeElement;
        syncCustomizer();
        panel.hidden = false;
        var first = panel.querySelector('input:checked, input');
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
        ['layout', 'width', 'nav'].forEach(function (n) { check(panel, n, theme.getPref(n)); });
        var primary = theme.getPref('primary');
        if (primary) check(panel, 'primary', primary);
        var radius = theme.getPref('radius');
        if (radius) check(panel, 'radius', radius);
    }

    function check(panel, name, value) {
        var input = panel.querySelector('input[name="' + name + '"][value="' + value + '"]');
        if (input) input.checked = true;
    }

    document.addEventListener('change', function (e) {
        var input = e.target;
        if (!input.closest || !input.closest('[data-m-customizer-form]')) return;
        var theme = window.modulusTheme;
        if (!theme) return;
        if (input.name === 'mode') {
            if (input.value === 'system') theme.useSystem(); else theme.set(input.value);
        } else if (input.name === 'primary') {
            theme.setPref('primary', input.value, input.getAttribute('data-css'));
        } else {
            theme.setPref(input.name, input.value);
        }
    });

    // Close the drawer when the viewport grows past the breakpoint.
    var wide = window.matchMedia('(min-width: 992px)');
    if (wide.addEventListener) wide.addEventListener('change', function (e) { if (e.matches) closeNav(); });
    window.modulusShell = window.modulusShell || {};
    window.modulusShell.openPopup = function (gridId) {
        // The component wires up its overlay in its own first after-render, and the table may render a new row a
        // cycle later, so look for the grid's editing cell for a few frames.
        var tries = 0;
        (function attempt() {
            var cell = document.querySelector('#' + gridId + ' .m-grid-cell-editing');
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

    // Tour: window.modulusShell.tour(root, focus) spotlights the step's data-target and places the .ant-tour card
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
})();
