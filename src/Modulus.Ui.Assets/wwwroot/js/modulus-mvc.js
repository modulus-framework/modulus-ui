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
                toggle: function () {
                    this.open = !this.open;
                    // Inside a scrolling or clipping box (a Kanban board, a table) the panel floats above the page,
                    // as Ant Design's portal-rendered dropdown does in Blazor.
                    if (this.open && window.modulusShell && window.modulusShell.placeDropdown) {
                        var root = this.$root;
                        this.$nextTick(function () { window.modulusShell.placeDropdown(root); });
                    }
                },
                close: function () { clearTimeout(this.hoverTimer); this.open = false; },
                // trigger="hover" (the dropdown button, as in Ant Design): opens on pointer enter, closes shortly after leave.
                hoverOpen: function () {
                    var self = this;
                    clearTimeout(this.hoverTimer);
                    if (!this.open) this.hoverTimer = setTimeout(function () { if (!self.open) self.toggle(); }, 150);
                },
                hoverClose: function () {
                    var self = this;
                    clearTimeout(this.hoverTimer);
                    if (this.open) this.hoverTimer = setTimeout(function () { self.open = false; }, 150);
                }
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
        // <app-upload>: the list mirrors the input's files. Options: maxSize (bytes), maxCount, accept, picture
        // (thumbnails) and action (post each file there at once, with progress; the input is then emptied).
        Alpine.data('modulusUpload', function (options) {
            var o = options || {};
            var seq = 0;
            var xhrs = {}; // by entry key; kept out of Alpine's proxies
            function size(bytes) {
                return bytes < 1024 ? bytes + ' B' : bytes < 1048576 ? Math.round(bytes / 1024) + ' KB' : (bytes / 1048576).toFixed(1) + ' MB';
            }
            function accepts(file) {
                if (!o.accept) return true;
                var name = file.name.toLowerCase(), type = (file.type || '').toLowerCase();
                return o.accept.split(',').some(function (a) {
                    a = a.trim().toLowerCase();
                    if (!a) return false;
                    if (a.charAt(0) === '.') return name.slice(-a.length) === a;
                    if (a.slice(-2) === '/*') return type.indexOf(a.slice(0, -1)) === 0;
                    return type === a;
                });
            }
            return {
                files: [],
                over: false,
                entry: function (file) {
                    var e = { key: ++seq, file: file, name: file.name, size: size(file.size), url: null, percent: 0, status: 'done', error: null };
                    if (!accepts(file)) { e.status = 'error'; e.error = 'This file type is not allowed.'; }
                    else if (o.maxSize && file.size > o.maxSize) { e.status = 'error'; e.error = 'Larger than ' + size(o.maxSize) + '.'; }
                    if (o.picture && /^image\//.test(file.type) && !e.error) e.url = URL.createObjectURL(file);
                    return e;
                },
                add: function (list) {
                    var self = this;
                    var added = list.map(function (f) { return self.entry(f); });
                    var keep = this.$refs.input.multiple ? this.files.filter(function (f) { return o.action || f.status !== 'error'; }) : [];
                    if (!this.$refs.input.multiple) { this.files.forEach(function (f) { if (f.url) URL.revokeObjectURL(f.url); }); added = added.slice(0, 1); }
                    var room = o.maxCount ? Math.max(0, o.maxCount - keep.filter(function (f) { return f.status !== 'error'; }).length) : Infinity;
                    added.forEach(function (e) {
                        if (e.status === 'error') return;
                        if (room <= 0) { e.status = 'error'; e.error = 'At most ' + o.maxCount + ' files.'; } else room--;
                    });
                    this.files = keep.concat(added);
                    if (o.action) {
                        added.forEach(function (e) { if (e.status !== 'error') self.upload(e); });
                        this.$refs.input.value = '';
                    } else {
                        this.write();
                    }
                },
                // Only accepted files stay in the input, so the form never posts a rejected one.
                write: function () {
                    var dt = new DataTransfer();
                    this.files.forEach(function (f) { if (f.status !== 'error') dt.items.add(f.file); });
                    this.$refs.input.files = dt.files;
                    this.$refs.input.dispatchEvent(new Event('change', { bubbles: true }));
                },
                upload: function (e) {
                    var self = this;
                    var item = function () { return self.files.find(function (f) { return f.key === e.key; }); };
                    var body = new FormData();
                    body.append(this.$refs.input.name, e.file);
                    var xhr = new XMLHttpRequest();
                    xhr.open('POST', o.action);
                    var headers = {};
                    try { headers = JSON.parse(document.body.getAttribute('hx-headers') || '{}'); } catch (err) { /* no token */ }
                    Object.keys(headers).forEach(function (k) { xhr.setRequestHeader(k, headers[k]); });
                    item().status = 'uploading';
                    xhr.upload.onprogress = function (p) { var f = item(); if (f && p.lengthComputable) f.percent = Math.round(p.loaded / p.total * 100); };
                    xhr.onload = function () {
                        var f = item(); if (!f) return;
                        f.percent = 100;
                        if (xhr.status >= 200 && xhr.status < 300) { f.status = 'done'; return; }
                        f.status = 'error';
                        f.error = xhr.responseText && xhr.responseText.length < 200 ? xhr.responseText : 'Upload failed (' + xhr.status + ').';
                    };
                    xhr.onerror = function () { var f = item(); if (f) { f.status = 'error'; f.error = 'Upload failed: no connection.'; } };
                    xhrs[e.key] = xhr;
                    xhr.send(body);
                },
                sync: function () {
                    if (this._writing) return;
                    this._writing = true;
                    this.add(Array.prototype.slice.call(this.$refs.input.files || []).filter(function (f) { return !this.files.some(function (e) { return e.file === f; }); }, this));
                    this._writing = false;
                },
                drop: function (e) {
                    this.over = false;
                    this._writing = true;
                    this.add(Array.prototype.slice.call(e.dataTransfer.files || []));
                    this._writing = false;
                },
                remove: function (i) {
                    var f = this.files[i];
                    if (xhrs[f.key] && f.status === 'uploading') xhrs[f.key].abort();
                    delete xhrs[f.key];
                    if (f.url) URL.revokeObjectURL(f.url);
                    this.files = this.files.filter(function (_, j) { return j !== i; });
                    if (!o.action) { this._writing = true; this.write(); this._writing = false; }
                }
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
        // SPA navigation (modulus-spa.js) shows a failed link's error page instead.
        if (e.detail.boosted && e.detail.requestConfig && e.detail.requestConfig.verb === 'get') return;
        var status = e.detail.xhr.status;
        message('error', status === 403 ? "You don't have permission to do that." : 'Something went wrong (' + status + '). Please try again.');
    });

    // No response at all (offline, server down): htmx raises sendError, not responseError.
    document.body.addEventListener('htmx:sendError', function () {
        message('error', "Couldn't reach the server. Check your connection and try again.");
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

    // Expandable rows of server-rendered tables: a [data-toggle-detail] button shows or hides the next
    // <tr data-detail-for="{row key}"> (the Ant Design expand icon; the editable grid has its own toggleRow).
    document.addEventListener('click', function (e) {
        var button = e.target instanceof Element ? e.target.closest('[data-toggle-detail]') : null;
        if (!button) return;
        var row = button.closest('tr');
        var detail = row && row.nextElementSibling;
        if (!detail || detail.getAttribute('data-detail-for') !== row.getAttribute('data-row-key')) return;
        var open = detail.hidden;
        detail.hidden = !open;
        button.classList.toggle('ant-table-row-expand-icon-expanded', open);
        button.classList.toggle('ant-table-row-expand-icon-collapsed', !open);
        button.setAttribute('aria-expanded', open ? 'true' : 'false');
        button.setAttribute('aria-label', open ? 'Collapse row' : 'Expand row');
    });

    // Grouped rows of server-rendered tables (TableGroups.HeaderRow): a [data-m-group-toggle] button hides or shows
    // the rows of its tbody marked data-m-group="{key}" (a class, so a row detail keeps its own hidden state).
    document.addEventListener('click', function (e) {
        var button = e.target instanceof Element ? e.target.closest('[data-m-group-toggle]') : null;
        var body = button && button.closest('tbody');
        if (!body) return;
        var key = button.getAttribute('data-m-group-toggle');
        var open = button.getAttribute('aria-expanded') === 'false';
        body.querySelectorAll('tr[data-m-group]').forEach(function (tr) {
            if (tr.getAttribute('data-m-group') === key) tr.classList.toggle('m-group-collapsed', !open);
        });
        button.setAttribute('aria-expanded', open ? 'true' : 'false');
        button.setAttribute('aria-label', (button.getAttribute('aria-label') || '').replace(/^(Expand|Collapse) /, open ? 'Collapse ' : 'Expand '));
        var icon = button.querySelector('.ant-table-row-expand-icon');
        if (icon) {
            icon.classList.toggle('ant-table-row-expand-icon-expanded', open);
            icon.classList.toggle('ant-table-row-expand-icon-collapsed', !open);
        }
    });
})();
