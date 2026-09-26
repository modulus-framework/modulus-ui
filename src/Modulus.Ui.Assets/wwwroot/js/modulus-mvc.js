/*
 * Modulus runtime for MVC / Razor Pages (htmx + Alpine.js).
 * Loaded by <modulus-scripts />, before Alpine, so the components below register in time.
 *
 * Server → client events (send with Response.HxMessage / HxCloseDrawer / HxTrigger):
 *   modulus:message        { type: 'success'|'info'|'warning'|'error', content }
 *   modulus:drawer-close
 *   modulus:notification   { type, message, description }
 *   modulus:modal-close    closes every open <app-modal>
 *
 * Client helpers:
 *   Alpine.store('drawer').show(title, width)   — opens the layout drawer; load its body with
 *                                                  hx-get + hx-target="#modulus-drawer-body"
 *   Alpine.store('drawer').load(url, title)     — opens the drawer and loads url into it
 *   modulusModal.open(name) / $dispatch('modulus:modal-open', name)
 *                                              — opens <app-modal name="…">
 *   modulusNotify(type, message, description)  — Ant Design notification (top right)
 *   hx-confirm="..."                             — Ant Design confirm dialog instead of window.confirm
 *                                                  (add data-confirm-danger / data-confirm-ok="Delete")
 */
(function () {
    'use strict';

    // Alpine components -----------------------------------------------------

    var openModals = 0;

    document.addEventListener('alpine:init', function () {
        var Alpine = window.Alpine;

        // Sider collapse. On wide screens the choice is persisted in a cookie so the
        // server renders the same state. Below the lg breakpoint (as ProLayout does) the
        // sider folds to its rail and expanding it overlays the content instead.
        var narrowQuery = window.matchMedia('(max-width: 991.98px)');
        Alpine.data('modulusShell', function (collapsed, width, foldedWidth) {
            return {
                pref: collapsed,
                narrow: narrowQuery.matches,
                open: false,
                get collapsed() { return this.narrow ? !this.open : this.pref; },
                get siderWidth() { return this.collapsed ? foldedWidth : width; },
                get contentOffset() { return this.narrow ? foldedWidth : this.siderWidth; },
                init: function () {
                    var self = this;
                    narrowQuery.addEventListener('change', function (e) { self.narrow = e.matches; self.open = false; });
                },
                toggle: function () {
                    if (this.narrow) { this.open = !this.open; return; }
                    this.pref = !this.pref;
                    window.modulusTheme.setCollapsed(this.pref);
                }
            };
        });

        Alpine.data('dropdown', function () {
            return {
                open: false,
                toggle: function () { this.open = !this.open; },
                close: function () { this.open = false; }
            };
        });

        Alpine.data('themeToggle', function () {
            return {
                dark: window.modulusTheme.current() === 'dark',
                toggle: function () { this.dark = window.modulusTheme.toggle() === 'dark'; }
            };
        });

        // Row selection for server-rendered tables: <div x-data="tableSelection"> … name="ids" checkboxes.
        Alpine.data('tableSelection', function () {
            var densityKey = 'modulus-table-density';
            var stored = null;
            try { stored = localStorage.getItem(densityKey); } catch (e) { /* storage blocked */ }
            return {
                count: 0,
                // Table density (Ant Design ProTable's density menu): '' | 'ant-table-middle' | 'ant-table-small'.
                density: stored === null ? 'ant-table-middle' : stored,
                setDensity: function (value) {
                    this.density = value;
                    try { localStorage.setItem(densityKey, value); } catch (e) { /* storage blocked */ }
                },
                sync: function () {
                    this.count = this.$root.querySelectorAll('input[name="ids"]:checked').length;
                    var all = this.$root.querySelector('input[data-select-all]');
                    if (all) {
                        var total = this.$root.querySelectorAll('input[name="ids"]:not(:disabled)').length;
                        all.checked = total > 0 && this.count === total;
                        syncCheckbox(all);
                    }
                },
                toggleAll: function (checked) {
                    // Disabled rows (e.g. the signed-in user) stay unselected, as in Ant Design's Selection.
                    this.$root.querySelectorAll('input[name="ids"]:not(:disabled)').forEach(function (box) {
                        box.checked = checked;
                        syncCheckbox(box);
                    });
                    this.sync();
                },
                clear: function () { this.toggleAll(false); }
            };
        });

        // <app-tabs>: active key, ink bar under the active tab, arrow-key navigation.
        Alpine.data('modulusTabs', function (initial) {
            return {
                active: initial,
                init: function () {
                    var self = this;
                    this.$nextTick(function () { self.ink(); });
                    this._resize = function () { self.ink(); };
                    window.addEventListener('resize', this._resize);
                },
                destroy: function () { window.removeEventListener('resize', this._resize); },
                tabs: function () {
                    return Array.prototype.slice.call(this.$refs.list.querySelectorAll(':scope > .ant-tabs-tab:not(.ant-tabs-tab-disabled)'));
                },
                select: function (key) {
                    var self = this;
                    this.active = key;
                    this.$nextTick(function () { self.ink(); });
                    this.$dispatch('modulus:tab-change', key);
                },
                move: function (step) {
                    var tabs = this.tabs(), self = this;
                    var i = tabs.findIndex(function (t) { return t.dataset.key === self.active; });
                    var next = tabs[(i + step + tabs.length) % tabs.length];
                    if (!next) return;
                    this.select(next.dataset.key);
                    next.querySelector('[role="tab"]').focus();
                },
                ink: function () {
                    var bar = this.$refs.ink, self = this;
                    var tab = this.tabs().find(function (t) { return t.dataset.key === self.active; });
                    if (!bar || !tab) return;
                    bar.style.left = tab.offsetLeft + 'px';
                    bar.style.width = tab.offsetWidth + 'px';
                }
            };
        });

        // <app-collapse>: open panel keys; accordion keeps at most one open.
        Alpine.data('modulusCollapse', function (initial, accordion) {
            return {
                open: initial,
                isOpen: function (key) { return this.open.indexOf(key) >= 0; },
                toggle: function (key) {
                    if (this.isOpen(key)) this.open = this.open.filter(function (k) { return k !== key; });
                    else this.open = accordion ? [key] : this.open.concat([key]);
                }
            };
        });

        // <app-upload>: file list with remove, and drag-and-drop into the real <input type="file">.
        Alpine.data('modulusUpload', function () {
            return {
                files: [],
                over: false,
                sync: function () { this.files = Array.prototype.slice.call(this.$refs.input.files || []); },
                set: function (list) {
                    var dt = new DataTransfer();
                    list.forEach(function (f) { dt.items.add(f); });
                    this.$refs.input.files = dt.files;
                    this.sync();
                    this.$refs.input.dispatchEvent(new Event('change', { bubbles: true }));
                },
                drop: function (e) {
                    this.over = false;
                    var dropped = Array.prototype.slice.call(e.dataTransfer.files || []);
                    this.set(this.$refs.input.multiple ? this.files.concat(dropped) : dropped.slice(0, 1));
                },
                remove: function (i) { this.set(this.files.filter(function (_, j) { return j !== i; })); }
            };
        });

        // <app-modal name="…">: opened by name, returns focus to the opener when closed.
        Alpine.data('modulusModal', function (name) {
            return {
                name: name,
                open: false,
                _opener: null,
                init: function () {
                    var self = this;
                    window.addEventListener('modulus:modal-open', function (e) { if (e.detail === self.name) self.show(); });
                    window.addEventListener('modulus:modal-close', function (e) { if (!e.detail || e.detail === self.name) self.hide(); });
                    document.body.addEventListener('modulus:modal-close', function () { self.hide(); });
                },
                show: function () {
                    var self = this;
                    this._opener = document.activeElement;
                    this.open = true;
                    openModals++;
                    document.documentElement.classList.add('m-scroll-lock');
                    setTimeout(function () {
                        // Also opens <app-drawer> (same component, drawer markup).
                        var target = self.$root.querySelector(':is(.ant-modal-body, .ant-drawer-body) :is([autofocus], input:not([type=hidden]), select, textarea), .ant-modal-footer .ant-btn-primary');
                        var fallback = target || self.$root.querySelector('.ant-modal-close, .ant-drawer-close');
                        if (fallback) fallback.focus();
                    }, 50);
                },
                hide: function () {
                    if (!this.open) return;
                    this.open = false;
                    openModals = Math.max(0, openModals - 1);
                    if (openModals === 0) document.documentElement.classList.remove('m-scroll-lock');
                    if (this._opener && this._opener.focus) this._opener.focus();
                },
                ok: function () {
                    this.$dispatch('modulus:modal-ok', this.name);
                    this.hide();
                }
            };
        });

        Alpine.store('drawer', {
            open: false,
            title: '',
            width: 480,
            show: function (title, width) {
                this.title = title || '';
                this.width = width || 480;
                this.open = true;
            },
            // Opens the drawer and loads its body from url (htmx.ajax, so nothing is inherited
            // from the clicked element's hx-* context).
            load: function (url, title, width) {
                var body = document.getElementById('modulus-drawer-body');
                if (body) body.innerHTML = '';
                this.show(title, width);
                return window.htmx.ajax('GET', url, { target: '#modulus-drawer-body', swap: 'innerHTML' });
            },
            hide: function () { this.open = false; }
        });

        Alpine.store('confirm', {
            open: false,
            title: '',
            okText: 'OK',
            danger: false,
            _resolve: null,
            ask: function (title, okText, danger) {
                var self = this;
                this.title = title;
                this.okText = okText || 'OK';
                this.danger = !!danger;
                this.open = true;
                return new Promise(function (resolve) { self._resolve = resolve; });
            },
            answer: function (ok) {
                this.open = false;
                if (this._resolve) this._resolve(ok);
                this._resolve = null;
            }
        });
    });

    // htmx integration --------------------------------------------------------

    document.addEventListener('htmx:confirm', function (e) {
        if (!e.detail.question) return;
        e.preventDefault();
        var el = e.detail.elt;
        window.Alpine.store('confirm')
            .ask(e.detail.question, el.getAttribute('data-confirm-ok'), el.hasAttribute('data-confirm-danger'))
            .then(function (ok) { if (ok) e.detail.issueRequest(true); });
    });

    // Filter forms submit with GET and push the URL: leave out empty fields so the pushed,
    // shareable URL carries only the active filters (?status=Active, not ?keyword=&status=Active&tier=).
    document.addEventListener('htmx:configRequest', function (e) {
        if (e.detail.verb !== 'get' || e.detail.elt.tagName !== 'FORM') return;
        var data = e.detail.formData;
        if (!data) return;
        Array.from(data.keys()).forEach(function (key) {
            if (data.getAll(key).every(function (v) { return v === ''; })) data.delete(key);
        });
    });

    document.body.addEventListener('modulus:drawer-close', function () {
        window.Alpine.store('drawer').hide();
    });

    document.body.addEventListener('modulus:notification', function (e) {
        notify(e.detail.type, e.detail.message, e.detail.description);
    });

    window.modulusModal = {
        open: function (name) { window.dispatchEvent(new CustomEvent('modulus:modal-open', { detail: name })); },
        close: function (name) { window.dispatchEvent(new CustomEvent('modulus:modal-close', { detail: name })); }
    };

    document.body.addEventListener('modulus:message', function (e) {
        message(e.detail.type, e.detail.content);
    });

    // Surface failed requests instead of failing silently.
    document.body.addEventListener('htmx:responseError', function (e) {
        var status = e.detail.xhr.status;
        message('error', status === 403 ? "You don't have permission to do that." : 'Something went wrong (' + status + '). Please try again.');
    });

    // Ant Design message toast -------------------------------------------------

    var ICONS = { success: 'check-circle', info: 'info-circle', warning: 'exclamation-circle', error: 'close-circle' };

    function message(type, content) {
        type = ICONS[type] ? type : 'info';
        var host = document.getElementById('modulus-messages');
        var template = document.getElementById('modulus-icon-' + ICONS[type]);
        if (!host) return;
        var notice = document.createElement('div');
        notice.className = 'ant-message-notice m-message-enter';
        notice.setAttribute('role', type === 'error' ? 'alert' : 'status');
        notice.innerHTML =
            '<div class="ant-message-notice-content"><div class="ant-message-custom-content ant-message-' + type + '">' +
            (template ? template.innerHTML : '') + '<span></span></div></div>';
        notice.querySelector('.ant-message-custom-content > span:last-child').textContent = content;
        host.appendChild(notice);
        setTimeout(function () {
            notice.classList.add('m-message-leave');
            setTimeout(function () { notice.remove(); }, 250);
        }, 3000);
    }

    window.modulusMessage = message;

    // Ant Design notification (top right) -----------------------------------------

    function notify(type, title, description) {
        type = ICONS[type] ? type : 'info';
        var host = document.getElementById('modulus-notifications');
        var icon = document.getElementById('modulus-icon-o-' + ICONS[type]);
        var close = document.getElementById('modulus-icon-close');
        if (!host) return;
        var notice = document.createElement('div');
        notice.className = 'ant-notification-notice ant-notification-notice-closable m-notice-enter';
        notice.setAttribute('role', type === 'error' ? 'alert' : 'status');
        notice.innerHTML =
            '<div class="ant-notification-notice-content"><div class="ant-notification-notice-with-icon">' +
            '<span class="ant-notification-notice-icon ant-notification-notice-icon-' + type + '">' + (icon ? icon.innerHTML : '') + '</span>' +
            '<div class="ant-notification-notice-message"></div><div class="ant-notification-notice-description"></div></div></div>' +
            '<button type="button" class="ant-notification-notice-close" aria-label="Close"><span class="ant-notification-notice-close-x">' + (close ? close.innerHTML : '×') + '</span></button>';
        notice.querySelector('.ant-notification-notice-message').textContent = title || '';
        notice.querySelector('.ant-notification-notice-description').textContent = description || '';
        var dismiss = function () {
            notice.classList.add('m-notice-leave');
            setTimeout(function () { notice.remove(); }, 250);
        };
        notice.querySelector('.ant-notification-notice-close').addEventListener('click', dismiss);
        host.appendChild(notice);
        var timer = setTimeout(dismiss, 4500);
        notice.addEventListener('mouseenter', function () { clearTimeout(timer); });
        notice.addEventListener('mouseleave', function () { timer = setTimeout(dismiss, 2000); });
    }

    window.modulusNotify = notify;

    // <app-date-picker>: open the native picker on click, not only from the browser's own icon.
    window.modulusShowPicker = function (input) {
        try { input.showPicker(); } catch (e) { /* unsupported or not user-activated */ }
    };

    // Native inputs styled as Ant Design controls -------------------------------
    // antd's CSS keys off wrapper classes (ant-checkbox-checked, ant-radio-button-wrapper-checked …);
    // keep them in sync with the real <input> so forms stay plain HTML and post normally.

    function syncCheckbox(input) {
        var box = input.closest('.ant-checkbox');
        if (box) box.classList.toggle('ant-checkbox-checked', input.checked);
        var wrapper = input.closest('.ant-checkbox-wrapper');
        if (wrapper) wrapper.classList.toggle('ant-checkbox-wrapper-checked', input.checked);
    }

    function syncRadioGroup(input) {
        document.querySelectorAll('input[type="radio"][name="' + CSS.escape(input.name) + '"]').forEach(function (r) {
            var button = r.closest('.ant-radio-button-wrapper');
            if (button) {
                button.classList.toggle('ant-radio-button-wrapper-checked', r.checked);
                r.closest('.ant-radio-button').classList.toggle('ant-radio-button-checked', r.checked);
            }
            var wrapper = r.closest('.ant-radio-wrapper');
            if (wrapper) {
                wrapper.classList.toggle('ant-radio-wrapper-checked', r.checked);
                r.closest('.ant-radio').classList.toggle('ant-radio-checked', r.checked);
            }
        });
    }

    function syncSegmented(input) {
        var group = input.closest('.ant-segmented-group');
        if (!group) return;
        group.querySelectorAll('.ant-segmented-item').forEach(function (item) {
            var r = item.querySelector('input');
            item.classList.toggle('ant-segmented-item-selected', !!(r && r.checked));
        });
    }

    document.addEventListener('change', function (e) {
        var t = e.target;
        if (t.matches('.ant-segmented-item-input')) syncSegmented(t);
        else if (t.matches('.ant-checkbox-input')) syncCheckbox(t);
        else if (t.matches('.ant-radio-input, .ant-radio-button-input')) syncRadioGroup(t);
    });
})();
