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
})();
