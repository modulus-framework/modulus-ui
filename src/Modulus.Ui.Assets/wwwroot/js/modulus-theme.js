/*
 * Modulus theme runtime — shared by Blazor, MVC and Razor Pages.
 *
 * Load synchronously in <head>, AFTER the stylesheet links, so the correct
 * theme is applied before first paint (no light-to-dark flash).
 *
 * Stylesheets that belong to one theme carry data-theme-css="light|dark".
 * Switching flips their `media` between "all" and "not all": both files are
 * already downloaded, so the swap is instant and never shows unstyled content.
 *
 * The choice is persisted in a cookie (not only localStorage) so the server
 * can render the right theme on the very first byte.
 *
 * <script src=".../modulus-theme.js" data-default="light|dark"
 *         data-layout="horizontal" data-width="boxed" data-nav="light"
 *         data-menu="all" data-font="sans" data-base="gray" data-density="compact" data-side="left"
 *         data-sidebar="default" data-fold="full" data-header="fixed" data-topbar="light"
 *         data-dir="ltr"></script>
 * Omit data-default to follow the OS until the user picks a theme.
 *
 * Theme settings (layout, content width, navigation theme, menu scope, font,
 * theme base, sidebar side / style / fold, header mode / colour, text direction,
 * corner radius, colour scheme) are cookies too; all but the last two become
 * data-* attributes on <html> before first paint (and dir="rtl" for the direction),
 * radius and colour are also rendered by the server (ThemeCss).
 */
(function () {
    'use strict';

    // The full-screen button shows only where the browser allows it. It is decided here, in <head>, so the
    // header already has the button's space at first paint; decided later (in modulus-shell.js), the button
    // would appear on a hard refresh and push the search box and the tools along.
    document.documentElement.classList.toggle('m-can-fullscreen', !!(document.fullscreenEnabled || document.webkitFullscreenEnabled));

    var THEME_COOKIE = 'modulus-theme';
    var COLLAPSED_COOKIE = 'modulus-collapsed';
    var root = document.documentElement;
    var script = document.currentScript;
    var configuredDefault = script ? script.getAttribute('data-default') : null;
    var PREFS = {
        layout: { cookie: 'modulus-layout', values: ['horizontal', 'vertical', 'condensed'] },
        width: { cookie: 'modulus-width', values: ['boxed', 'fluid', 'framed'] },
        nav: { cookie: 'modulus-nav', values: ['light', 'dark'] },
        menu: { cookie: 'modulus-menu', values: ['all', 'app'] },
        font: { cookie: 'modulus-font', values: ['sans', 'system', 'serif', 'mono'] },
        base: { cookie: 'modulus-base', values: ['gray', 'slate', 'zinc', 'neutral', 'stone'] },
        density: { cookie: 'modulus-density', values: ['compact', 'comfortable'] },
        side: { cookie: 'modulus-side', values: ['left', 'right'] },
        sidebar: { cookie: 'modulus-sidebar', values: ['default', 'transparent', 'floating'] },
        fold: { cookie: 'modulus-fold', values: ['full', 'folded', 'hover'] },
        header: { cookie: 'modulus-header', values: ['fixed', 'sticky', 'static', 'floating'] },
        topbar: { cookie: 'modulus-topbar', values: ['light', 'dark'] },
        dir: { cookie: 'modulus-dir', values: ['ltr', 'rtl'] }
    };
    var RADIUS_COOKIE = 'modulus-radius';
    var PRIMARY_COOKIE = 'modulus-primary';
    var darkQuery = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

    function readCookie(name) {
        var match = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return match ? decodeURIComponent(match[1]) : null;
    }

    function writeCookie(name, value) {
        document.cookie = name + '=' + encodeURIComponent(value) + ';path=/;max-age=31536000;samesite=lax';
    }

    function deleteCookie(name) {
        document.cookie = name + '=;path=/;max-age=0;samesite=lax';
    }

    function pref(name) {
        var p = PREFS[name];
        var saved = readCookie(p.cookie);
        if (p.values.indexOf(saved) >= 0) return saved;
        var configured = script ? script.getAttribute('data-' + name) : null;
        return p.values.indexOf(configured) >= 0 ? configured : p.values[0];
    }

    function setAttr(name, value) {
        root.setAttribute('data-' + name, value);
        if (name === 'dir') root.setAttribute('dir', value);
    }

    function applyRadius(px) {
        var r = parseInt(px, 10);
        if (isNaN(r)) return;
        root.style.setProperty('--m-radius-sm', Math.max(r - 2, 0) + 'px');
        root.style.setProperty('--m-radius', r + 'px');
        root.style.setProperty('--m-radius-lg', (r + 2) + 'px');
    }

    function preferred() {
        var saved = readCookie(THEME_COOKIE);
        if (saved === 'light' || saved === 'dark') return saved;
        if (configuredDefault === 'light' || configuredDefault === 'dark') return configuredDefault;
        return darkQuery && darkQuery.matches ? 'dark' : 'light';
    }

    function apply(mode) {
        root.setAttribute('data-theme', mode);
        root.style.colorScheme = mode;
        var links = document.querySelectorAll('link[data-theme-css]');
        for (var i = 0; i < links.length; i++) {
            links[i].media = links[i].getAttribute('data-theme-css') === mode ? 'all' : 'not all';
        }
        window.dispatchEvent(new CustomEvent('modulus:theme', { detail: mode }));
        return mode;
    }

    var api = {
        current: function () { return root.getAttribute('data-theme') || preferred(); },
        set: function (mode) {
            if (mode !== 'light' && mode !== 'dark') throw new Error('modulusTheme.set expects "light" or "dark"');
            writeCookie(THEME_COOKIE, mode);
            return apply(mode);
        },
        toggle: function () { return api.set(api.current() === 'dark' ? 'light' : 'dark'); },
        /** Forget the explicit choice and follow the operating system (or the configured default). */
        useSystem: function () { deleteCookie(THEME_COOKIE); return apply(preferred()); },
        /** Theme settings: layout | width | nav | menu | font | base | density | side | sidebar | fold | header | topbar | dir | radius | primary. */
        setPref: function (name, value, css) {
            if (PREFS[name]) {
                if (PREFS[name].values.indexOf(value) < 0) return;
                writeCookie(PREFS[name].cookie, value);
                setAttr(name, value);
            } else if (name === 'radius') {
                writeCookie(RADIUS_COOKIE, value);
                applyRadius(value);
            } else if (name === 'primary') {
                writeCookie(PRIMARY_COOKIE, value);
                var style = document.getElementById('modulus-brand');
                if (style && css) style.textContent = css;
            }
            window.dispatchEvent(new CustomEvent('modulus:pref', { detail: { name: name, value: value } }));
        },
        getPref: function (name) { return PREFS[name] ? pref(name) : readCookie(name === 'radius' ? RADIUS_COOKIE : PRIMARY_COOKIE); },
        /** Clears every theme setting and reloads so the server renders the configured defaults. */
        reset: function () {
            [THEME_COOKIE, RADIUS_COOKIE, PRIMARY_COOKIE, COLLAPSED_COOKIE].forEach(deleteCookie);
            for (var p in PREFS) deleteCookie(PREFS[p].cookie);
            window.location.reload();
        },
        isCollapsed: function () { return readCookie(COLLAPSED_COOKIE) === 'true'; },
        setCollapsed: function (collapsed) { writeCookie(COLLAPSED_COOKIE, collapsed ? 'true' : 'false'); }
    };

    // Follow the OS setting live until the user makes an explicit choice.
    if (darkQuery && darkQuery.addEventListener) {
        darkQuery.addEventListener('change', function () {
            if (!readCookie(THEME_COOKIE) && !configuredDefault) apply(preferred());
        });
    }

    window.modulusTheme = api;
    apply(preferred());
    for (var name in PREFS) setAttr(name, pref(name));
})();
