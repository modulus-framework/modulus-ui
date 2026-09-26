/*
 * Alpine components behind the MVC widgets in AdvancedEntryTagHelpers.cs:
 * app-tree, app-tree-select, app-cascader, app-transfer, app-calendar, app-carousel, app-anchor,
 * app-editable-grid (EditableGridTagHelper.cs), app-menu (MenuTagHelpers.cs), app-splitter, app-affix,
 * app-back-top (FrameTagHelpers.cs), app-auto-complete, app-mentions, app-image-group and app-countdown
 * (ExtraTagHelpers.cs).
 * The server renders the initial DOM; these components keep the Ant Design classes in step with the state.
 * Loaded by <modulus-scripts /> before Alpine.
 */
(function () {
    'use strict';

    // Tree state shared by app-tree and app-tree-select. Nodes are addressed by index:
    // cfg.nodes = [{ v: value, t: title, p: parent index (-1 for roots), l: leaf, d: disabled }].
    function treeState(cfg) {
        var nodes = cfg.nodes;
        var kids = nodes.map(function () { return []; });
        nodes.forEach(function (n, i) { if (n.p >= 0) kids[n.p].push(i); });
        var leaves = nodes.map(function () { return null; });
        function enabledLeaves(i) {
            if (leaves[i]) return leaves[i];
            var n = nodes[i];
            leaves[i] = n.d ? [] : n.l ? [i] : kids[i].reduce(function (all, k) { return all.concat(enabledLeaves(k)); }, []);
            return leaves[i];
        }
        var expanded = {}, checked = {};
        cfg.expanded.forEach(function (i) { expanded[i] = true; });
        cfg.checked.forEach(function (i) { checked[i] = true; });

        return {
            expanded: expanded,
            checkedLeaves: checked,
            selected: cfg.selected,
            shown: function (i) {
                for (var p = nodes[i].p; p >= 0; p = nodes[p].p) if (!this.expanded[p]) return false;
                return true;
            },
            isOpen: function (i) { return !!this.expanded[i]; },
            toggle: function (i) { this.expanded[i] = !this.expanded[i]; },
            isChecked: function (i) { return !!this.checkedLeaves[i]; },
            state: function (i) {
                if (nodes[i].l) return this.checkedLeaves[i] ? 'checked' : '';
                var all = enabledLeaves(i), self = this;
                var on = all.filter(function (k) { return self.checkedLeaves[k]; }).length;
                return all.length > 0 && on === all.length ? 'checked' : on > 0 ? 'indeterminate' : '';
            },
            check: function (i) {
                if (nodes[i].d || cfg.disabled) return;
                var on = this.state(i) !== 'checked', self = this;
                (nodes[i].l ? [i] : enabledLeaves(i)).forEach(function (k) { self.checkedLeaves[k] = on; });
            },
            clickNode: function (i) {
                if (nodes[i].d || cfg.disabled) return;
                if (cfg.checkable) { this.check(i); return; }
                if (cfg.selectable) this.select(i);
            },
            select: function (i) { this.selected = i; },
            selectedValue: function () { return this.selected >= 0 ? nodes[this.selected].v : ''; },
            selectedTitle: function () { return this.selected >= 0 ? nodes[this.selected].t : ''; },
            hasValue: function () {
                if (!cfg.checkable) return this.selected >= 0;
                for (var k in this.checkedLeaves) if (this.checkedLeaves[k]) return true;
                return false;
            },
            ariaChecked: function (i) { var s = this.state(i); return s === 'checked' ? 'true' : s === 'indeterminate' ? 'mixed' : 'false'; },
            nodeClass: function (i) {
                var s = cfg.checkable ? this.state(i) : '', open = this.isOpen(i), leaf = nodes[i].l;
                return {
                    'ant-tree-treenode-switcher-open': !leaf && open,
                    'ant-tree-treenode-switcher-close': !leaf && !open,
                    'ant-tree-treenode-checkbox-checked': s === 'checked',
                    'ant-tree-treenode-checkbox-indeterminate': s === 'indeterminate',
                    'ant-tree-treenode-selected': this.selected === i
                };
            },
            switcherClass: function (i) {
                var open = this.isOpen(i);
                return { 'ant-tree-switcher_open': open, 'ant-tree-switcher_close': !open };
            },
            checkboxClass: function (i) {
                var s = this.state(i);
                return { 'ant-tree-checkbox-checked': s === 'checked', 'ant-tree-checkbox-indeterminate': s === 'indeterminate' };
            },
            contentClass: function (i) {
                var open = this.isOpen(i), leaf = nodes[i].l;
                return {
                    'ant-tree-node-content-wrapper-open': !leaf && open,
                    'ant-tree-node-content-wrapper-close': !leaf && !open,
                    'ant-tree-node-selected': this.selected === i
                };
            }
        };
    }

    document.addEventListener('alpine:init', function () {
        var Alpine = window.Alpine;

        // <app-tree>
        Alpine.data('modulusTree', function (cfg) { return treeState(cfg); });

        // <app-tree-select>: the tree in a dropdown; picking a node closes it (single mode).
        Alpine.data('modulusTreeSelect', function (cfg) {
            var tree = treeState(cfg);
            tree.open = false;
            tree.toggleOpen = function () { if (!cfg.disabled) this.open = !this.open; };
            tree.select = function (i) { this.selected = i; this.open = false; };
            return tree;
        });

        // <app-cascader>: cfg.options = [{ v, t, d, c: children | null }]; cfg.path = values from root to the selection.
        Alpine.data('modulusCascader', function (cfg) {
            function find(options, path) {
                var out = [], level = options;
                for (var i = 0; i < path.length && level; i++) {
                    var node = level.filter(function (o) { return o.v === path[i]; })[0];
                    if (!node) break;
                    out.push(node);
                    level = node.c;
                }
                return out;
            }
            return {
                open: false,
                value: cfg.path.length ? cfg.path[cfg.path.length - 1] : '',
                chosen: cfg.path.slice(),
                active: cfg.path.slice(),
                toggleOpen: function () {
                    if (cfg.disabled) return;
                    this.open = !this.open;
                    if (this.open) this.active = this.chosen.slice();
                },
                columns: function () {
                    var cols = [cfg.options];
                    find(cfg.options, this.active).forEach(function (node) { if (node.c) cols.push(node.c); });
                    return cols;
                },
                pick: function (column, option) {
                    if (option.d) return;
                    this.active = this.active.slice(0, column).concat(option.v);
                    if (!option.c || cfg.changeOnSelect) {
                        this.chosen = this.active.slice();
                        this.value = option.v;
                    }
                    if (!option.c) this.open = false;
                },
                label: function () {
                    return find(cfg.options, this.chosen).map(function (n) { return n.t; }).join(cfg.separator);
                }
            };
        });

        // <app-transfer>: cfg.items = [{ t: title, d: disabled }]; cfg.right = [bool] (item is in the target list);
        // cfg.unit / cfg.units label the header count.
        Alpine.data('modulusTransfer', function (cfg) {
            var items = cfg.items;
            return {
                right: cfg.right.slice(),
                checked: items.map(function () { return false; }),
                search: ['', ''],
                onSide: function (side, i) { return this.right[i] === (side === 1); },
                visible: function (side, i) {
                    var q = this.search[side].trim().toLowerCase();
                    return this.onSide(side, i) && (!q || items[i].t.toLowerCase().indexOf(q) >= 0);
                },
                visibleCount: function (side) {
                    var n = 0;
                    for (var i = 0; i < items.length; i++) if (this.visible(side, i)) n++;
                    return n;
                },
                selectable: function (side) {
                    var out = [];
                    for (var i = 0; i < items.length; i++) if (this.visible(side, i) && !items[i].d) out.push(i);
                    return out;
                },
                selectedCount: function (side) {
                    var n = 0;
                    for (var i = 0; i < items.length; i++) if (this.onSide(side, i) && this.checked[i]) n++;
                    return n;
                },
                allChecked: function (side) {
                    var all = this.selectable(side), self = this;
                    return all.length > 0 && all.every(function (i) { return self.checked[i]; });
                },
                someChecked: function (side) { return !this.allChecked(side) && this.selectedCount(side) > 0; },
                checkAll: function (side, on) {
                    var self = this;
                    this.selectable(side).forEach(function (i) { self.checked[i] = on; });
                },
                header: function (side) {
                    var total = 0;
                    for (var i = 0; i < items.length; i++) if (this.onSide(side, i)) total++;
                    var sel = this.selectedCount(side);
                    return (sel > 0 ? sel + '/' : '') + total + ' ' + (total === 1 ? cfg.unit : cfg.units);
                },
                canMove: function (from) { return this.selectedCount(from) > 0; },
                move: function (from) {
                    for (var i = 0; i < items.length; i++) {
                        if (this.onSide(from, i) && this.checked[i]) { this.right[i] = from === 0; this.checked[i] = false; }
                    }
                    this.$dispatch('change', { targetKeys: this.right });
                }
            };
        });

        // <app-calendar>: re-renders the panel (same markup as CalendarTagHelper) when the month, year or mode changes.
        Alpine.data('modulusCalendar', function (cfg) {
            function pad(n) { return (n < 10 ? '0' : '') + n; }
            function iso(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
            function parse(s) { var p = s.split('-'); return new Date(+p[0], +p[1] - 1, +(p[2] || 1)); }
            function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
            function cell(title, cls, text) {
                return '<td title="' + title + '" class="' + cls + '"><div class="ant-picker-cell-inner ant-picker-calendar-date">' +
                    '<div class="ant-picker-calendar-date-value">' + esc(text) + '</div><div class="ant-picker-calendar-date-content"></div></div></td>';
            }
            var start = parse(cfg.selected);
            return {
                selected: cfg.selected,
                mode: cfg.mode,
                year: start.getFullYear(),
                month: start.getMonth(),
                panel: function () {
                    var html, sel = this.selected;
                    if (this.mode === 'year') {
                        var year = this.year, selMonth = parse(sel).getFullYear() === year ? parse(sel).getMonth() : -1, today = parse(cfg.today);
                        html = '<div class="ant-picker-month-panel"><div class="ant-picker-body"><table class="ant-picker-content"><tbody>';
                        for (var r = 0; r < 4; r++) {
                            html += '<tr>';
                            for (var c = 0; c < 3; c++) {
                                var m = r * 3 + c;
                                var cls = 'ant-picker-cell ant-picker-cell-in-view' +
                                    (today.getFullYear() === year && today.getMonth() === m ? ' ant-picker-cell-today' : '') + (m === selMonth ? ' ant-picker-cell-selected' : '');
                                html += cell(year + '-' + pad(m + 1), cls, cfg.months[m]);
                            }
                            html += '</tr>';
                        }
                        return html + '</tbody></table></div></div>';
                    }
                    var first = new Date(this.year, this.month, 1);
                    var day = new Date(first);
                    day.setDate(1 - ((first.getDay() - cfg.firstDay + 7) % 7));
                    html = '<div class="ant-picker-date-panel"><div class="ant-picker-body"><table class="ant-picker-content"><thead><tr>';
                    cfg.days.forEach(function (d) { html += '<th>' + esc(d) + '</th>'; });
                    html += '</tr></thead><tbody>';
                    for (var w = 0; w < 6; w++) {
                        html += '<tr>';
                        for (var i = 0; i < 7; i++) {
                            var key = iso(day);
                            var cls2 = 'ant-picker-cell' + (day.getMonth() === this.month ? ' ant-picker-cell-in-view' : '') +
                                (key === cfg.today ? ' ant-picker-cell-today' : '') + (key === sel ? ' ant-picker-cell-selected' : '');
                            html += cell(key, cls2, day.getDate());
                            day.setDate(day.getDate() + 1);
                        }
                        html += '</tr>';
                    }
                    return html + '</tbody></table></div></div>';
                },
                pick: function (e) {
                    var td = e.target.closest('td[title]');
                    if (!td) return;
                    var d = parse(td.title);
                    if (this.mode === 'year') {
                        this.month = d.getMonth();
                        this.mode = 'month';
                        return;
                    }
                    this.selected = td.title;
                    this.year = d.getFullYear();
                    this.month = d.getMonth();
                    this.$dispatch('change', { value: td.title });
                }
            };
        });

        // <app-editable-grid>: click-to-edit cells, client-side add/delete of rows, expandable rows, fixed-column
        // shadows, density, and one floating column menu. Column operations and Save post the enclosing form
        // (the server re-renders the grid). view() and the formatters mirror EditableGridTagHelper.View / GridSchema.Format.
        Alpine.data('modulusGrid', function (cfg) {
            function esc(s) { return String(s == null ? '' : s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
            function num(v) { if (v === null || v === undefined || String(v).trim() === '') return null; var n = Number(v); return isFinite(n) ? n : null; }
            function nf(opts) { try { return new Intl.NumberFormat(cfg.lang, opts); } catch (e) { return new Intl.NumberFormat('en-US', opts); } }
            function fmtDate(v) {
                var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v || '');
                if (!m) return v || '';
                var y = m[1], mo = +m[2], d = +m[3];
                return cfg.datePattern.replace(/yyyy|yy|MM|M|dd|d/g, function (t) {
                    return { yyyy: y, yy: y.slice(2), MM: (mo < 10 ? '0' : '') + mo, M: String(mo), dd: (d < 10 ? '0' : '') + d, d: String(d) }[t];
                });
            }
            function many(v) { return v ? String(v).split(',').map(function (x) { return x.trim(); }).filter(Boolean) : []; }
            function tag(o, v) {
                var color = o && o[1] && o[1] !== 'default' ? ' ant-tag-' + o[1] : '';
                return '<span class="ant-tag' + color + '">' + esc(o ? o[0] : v) + '</span>';
            }
            function format(col, v) {
                if (v === null || v === undefined || v === '') return col.type === 'Switch' ? 'No' : '';
                var n = num(v);
                switch (col.type) {
                    case 'Number': return n === null ? v : nf({ maximumFractionDigits: 2 }).format(n);
                    case 'Currency':
                        if (n === null) return v;
                        var whole = n % 1 === 0;
                        return nf({ style: 'currency', currency: cfg.currency, minimumFractionDigits: whole ? 0 : 2, maximumFractionDigits: whole ? 0 : 2 }).format(n);
                    case 'Percent': return n === null ? v : nf({ style: 'percent', maximumFractionDigits: 0 }).format(n / 100);
                    case 'Progress': return n === null ? v : Math.round(n) + '%';
                    case 'Date': return fmtDate(v);
                    case 'Select': case 'Tag': return col.o[v] ? col.o[v][0] : v;
                    case 'MultiSelect': return many(v).map(function (x) { return col.o[x] ? col.o[x][0] : x; }).join(', ');
                    default: return v;
                }
            }
            function view(col, v) {
                if (col.type === 'Tag' && col.o[v]) return tag(col.o[v], v);
                if (col.type === 'MultiSelect' && many(v).length) {
                    return '<span class="m-grid-tags">' + many(v).map(function (x) { return tag(col.o[x], x); }).join('') + '</span>';
                }
                if (col.type === 'Progress' && num(v) !== null) {
                    var p = Math.max(0, Math.min(100, num(v))), done = p >= 100;
                    return '<div class="ant-progress ant-progress-status-' + (done ? 'success' : 'normal') + ' ant-progress-small ant-progress-line ant-progress-show-info">' +
                        '<div class="ant-progress-outer"><div class="ant-progress-inner"><div class="ant-progress-bg" style="width: ' + p + '%; height: 6px;"></div></div></div>' +
                        '<span title="' + Math.round(p) + '%" class="ant-progress-text">' + (done ? cfg.check : Math.round(p) + '%') + '</span></div>';
                }
                var text = format(col, v);
                return text === '' ? '<span class="m-grid-empty">Add</span>' : '<span' + (col.right ? ' class="m-num"' : '') + '>' + esc(text) + '</span>';
            }
            function validate(col, v) {
                if (v === null || v === undefined || v === '') return col.req && col.type !== 'Switch' ? col.t + ' is required.' : null;
                switch (col.type) {
                    case 'Number': case 'Currency': case 'Percent': case 'Progress': case 'Rating':
                        var n = num(v);
                        if (n === null) return col.t + ' must be a number.';
                        if (col.min !== null && n < col.min) return col.t + ' must be at least ' + nf({}).format(col.min) + '.';
                        if (col.max !== null && n > col.max) return col.t + ' must be at most ' + nf({}).format(col.max) + '.';
                        return null;
                    case 'Date': return /^\d{4}-\d{2}-\d{2}$/.test(v) ? null : col.t + ' must be a date.';
                    case 'Select': case 'Tag': return col.o[v] ? null : 'Choose a valid ' + col.t.toLowerCase() + '.';
                    default: return null;
                }
            }
            function read(editor, col) {
                if (col.type === 'MultiSelect') {
                    return Array.prototype.filter.call(editor.querySelectorAll('input[type=checkbox]'), function (i) { return i.checked; })
                        .map(function (i) { return i.value; }).join(',');
                }
                var control = editor.querySelector('input, select');
                return control ? control.value.trim() : '';
            }
            function snapshot(editor) {
                return Array.prototype.map.call(editor.querySelectorAll('input, select'), function (i) { return i.type === 'checkbox' ? i.checked : i.value; });
            }
            function syncCheckbox(input) {
                var wrapper = input.closest('.ant-checkbox-wrapper');
                if (!wrapper) return;
                wrapper.classList.toggle('ant-checkbox-wrapper-checked', input.checked);
                wrapper.querySelector('.ant-checkbox').classList.toggle('ant-checkbox-checked', input.checked);
            }
            function restore(editor, values) {
                Array.prototype.forEach.call(editor.querySelectorAll('input, select'), function (i, k) {
                    if (i.type === 'checkbox') { i.checked = values[k]; syncCheckbox(i); } else i.value = values[k];
                });
            }

            return {
                dirty: !!cfg.dirty,
                density: 'ant-table-middle',
                pingLeft: false,
                pingRight: false,
                menu: { key: '', fixed: 'none', first: false, last: false, style: '' },
                columnOpen: false,
                newTitle: '',
                newType: 'Text',
                newFixed: 'None',
                newRequired: false,
                _editor: null,
                _original: null,

                init: function () {
                    var self = this;
                    // The server renders the default density so the first paint is right; Alpine owns it from here.
                    this.$refs.table.classList.remove('ant-table-middle');
                    this._resize = function () { self.onScroll(); };
                    this._close = function () { self.closeMenu(); };
                    window.addEventListener('resize', this._resize);
                    window.addEventListener('scroll', this._close, { passive: true });
                    this.$nextTick(function () { self.onScroll(); });
                },
                destroy: function () {
                    window.removeEventListener('resize', this._resize);
                    window.removeEventListener('scroll', this._close);
                },
                tableClass: function () {
                    return [this.density, this.pingLeft ? 'ant-table-ping-left' : '', this.pingRight ? 'ant-table-ping-right' : ''].join(' ').trim();
                },
                onScroll: function () {
                    var el = this.$refs.scroller;
                    if (!el) return;
                    this.pingLeft = el.scrollLeft > 0;
                    this.pingRight = el.scrollLeft + el.clientWidth < el.scrollWidth - 1;
                    this.closeMenu();
                },
                column: function (el) {
                    var td = el.closest('td[data-key]');
                    return td ? cfg.columns[td.getAttribute('data-key')] : null;
                },

                // ---- Cells ----
                edit: function (cell) {
                    if (this._editor) this.commit(this._editor, false);
                    var editor = cell.nextElementSibling;
                    if (!editor || !editor.classList.contains('m-grid-cell-editing')) return;
                    this._editor = editor;
                    this._original = snapshot(editor);
                    cell.hidden = true;
                    editor.hidden = false;
                    var control = editor.querySelector('input, select');
                    if (control) {
                        control.focus();
                        if (control.type === 'text') control.select();
                        if (control.tagName === 'SELECT' || control.type === 'date') { try { control.showPicker(); } catch (e) { /* unsupported, or no user gesture */ } }
                    }
                },
                leave: function (e) {
                    var editor = e.currentTarget;
                    if (e.relatedTarget && editor.contains(e.relatedTarget)) return;
                    this.commit(editor, false);
                },
                commit: function (editor, focusCell) {
                    if (!editor || editor.hidden) return;
                    var cell = editor.previousElementSibling, col = this.column(editor);
                    var value = read(editor, col);
                    var changed = JSON.stringify(snapshot(editor)) !== JSON.stringify(this._original);
                    if (col) {
                        cell.innerHTML = view(col, value);
                        cell.setAttribute('aria-label', 'Edit ' + col.t + ': ' + format(col, value));
                        this.setError(editor.closest('td'), cell, validate(col, value));
                    }
                    editor.hidden = true;
                    cell.hidden = false;
                    if (this._editor === editor) { this._editor = null; this._original = null; }
                    if (changed) this.dirty = true;
                    if (focusCell !== false) cell.focus();
                },
                cancel: function (editor) {
                    if (this._original) restore(editor, this._original);
                    var cell = editor.previousElementSibling;
                    editor.hidden = true;
                    cell.hidden = false;
                    this._editor = null;
                    this._original = null;
                    cell.focus();
                },
                checkbox: function (input) { syncCheckbox(input); },
                setError: function (td, cell, message) {
                    var box = td.querySelector('.m-grid-cell-message');
                    cell.classList.toggle('m-grid-cell-error', !!message);
                    if (message) {
                        cell.setAttribute('title', message);
                        if (!box) { box = document.createElement('div'); box.className = 'm-grid-cell-message'; box.setAttribute('role', 'alert'); td.appendChild(box); }
                        box.textContent = message;
                    } else {
                        cell.removeAttribute('title');
                        if (box) box.remove();
                    }
                },

                // ---- Rows ----
                addRow: function () {
                    var ids = Array.prototype.map.call(this.$root.querySelectorAll('input[name="' + cfg.name + '.index"]'), function (i) { return i.value; });
                    var n = ids.length + 1;
                    while (ids.indexOf('n' + n) >= 0) n++;
                    var html = this.$refs.rowTemplate.innerHTML.split('__id__').join('n' + n);
                    if (this.$refs.empty) this.$refs.empty.remove();
                    this.$refs.body.insertAdjacentHTML('afterbegin', html);
                    this.dirty = true;
                    var row = this.$refs.body.firstElementChild, self = this;
                    this.$nextTick(function () {
                        var first = row.querySelector('.m-grid-cell-editable');
                        if (first) self.edit(first);
                    });
                },
                deleteRow: function (button) {
                    var self = this, row = button.closest('tr');
                    var remove = function () {
                        var id = row.getAttribute('data-row-key');
                        var detail = self.$refs.body.querySelector('tr[data-detail-for="' + CSS.escape(id) + '"]');
                        if (detail) detail.remove();
                        row.remove();
                        self.dirty = true;
                    };
                    var ask = window.Alpine.store('confirm');
                    if (ask && ask.ask) ask.ask('Delete this row?', 'Delete', true).then(function (ok) { if (ok) remove(); });
                    else if (window.confirm('Delete this row?')) remove();
                },
                toggleRow: function (button) {
                    var row = button.closest('tr');
                    var detail = row.nextElementSibling;
                    if (!detail || detail.getAttribute('data-detail-for') !== row.getAttribute('data-row-key')) return;
                    var open = detail.style.display === 'none';
                    detail.style.display = open ? '' : 'none';
                    button.classList.toggle('ant-table-row-expand-icon-expanded', open);
                    button.classList.toggle('ant-table-row-expand-icon-collapsed', !open);
                    button.setAttribute('aria-expanded', open ? 'true' : 'false');
                    button.setAttribute('aria-label', open ? 'Collapse row' : 'Expand row');
                },

                // ---- Columns ----
                openMenu: function (button, key, fixed, first, last) {
                    if (this.menu.key === key) { this.closeMenu(); return; }
                    var r = button.getBoundingClientRect();
                    var left = Math.max(8, Math.min(r.right - 180, window.innerWidth - 188));
                    this.menu = { key: key, fixed: fixed, first: first, last: last, style: 'position: fixed; top: ' + (r.bottom + 4) + 'px; left: ' + left + 'px; min-width: 180px; z-index: 1050;' };
                },
                closeMenu: function () { if (this.menu.key) this.menu = { key: '', fixed: 'none', first: false, last: false, style: '' }; },
                openColumnDialog: function () {
                    var self = this;
                    this.columnOpen = true;
                    this.$nextTick(function () { self.$refs.newTitle.focus(); });
                },
                // Posts the form as if a {name}.op button with this value had been clicked.
                submitOp: function (op) {
                    var form = this.$root.closest('form');
                    if (!form) return;
                    // A hidden field placed first, not a submit button: htmx does not always send the submitter
                    // (e.g. when the submit starts from a checkbox change), and the server reads the first op.
                    var input = document.createElement('input');
                    input.type = 'hidden';
                    input.name = cfg.name + '.op';
                    input.value = op;
                    form.prepend(input);
                    if (form.requestSubmit) form.requestSubmit(); else form.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
                    input.remove();
                }
            };
        });

        // <app-carousel>: slide index, dots, autoplay (paused while hovered or when the tab is hidden).
        Alpine.data('modulusCarousel', function (count, autoplay) {
            return {
                index: 0,
                pause: false,
                init: function () {
                    var self = this;
                    if (autoplay > 0 && count > 1) {
                        var timer = setInterval(function () { if (!self.pause && !document.hidden) self.go(self.index + 1); }, autoplay);
                        this.$cleanup = function () { clearInterval(timer); };
                    }
                },
                destroy: function () { if (this.$cleanup) this.$cleanup(); },
                go: function (i) { this.index = (i + count) % count; },
                trackStyle: function () {
                    return 'width: ' + (count * 100) + '%; transform: translate3d(-' + (this.index * 100 / count) + '%, 0px, 0px); transition: transform 500ms ease 0s;';
                }
            };
        });

        // <app-anchor>: highlights the link of the last section whose top has scrolled past offsetTop.
        Alpine.data('modulusAnchor', function (offsetTop) {
            return {
                active: '',
                ball: '',
                init: function () {
                    var self = this, frame = 0;
                    var update = function () { frame = 0; self.update(); };
                    this.onScroll = function () { if (!frame) frame = requestAnimationFrame(update); };
                    window.addEventListener('scroll', this.onScroll, { passive: true });
                    this.$nextTick(function () { self.update(); });
                },
                destroy: function () { window.removeEventListener('scroll', this.onScroll); },
                update: function () {
                    var links = this.$root.querySelectorAll('.ant-anchor-link[data-href]');
                    var current = '';
                    var atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2;
                    links.forEach(function (link) {
                        var href = link.getAttribute('data-href');
                        var hash = href.indexOf('#') >= 0 ? href.slice(href.indexOf('#') + 1) : '';
                        var target = hash && document.getElementById(decodeURIComponent(hash));
                        if (target && target.getBoundingClientRect().top <= offsetTop + 5) current = href; // 5px: AntD's bounds
                        // At the bottom of the page the last sections can never reach the offset; the one
                        // scrolled to (or the last in view) wins.
                        else if (target && current && atBottom && target.getBoundingClientRect().top < window.innerHeight) current = href;
                    });
                    this.active = current;
                    var title = current && this.$root.querySelector('.ant-anchor-link[data-href="' + CSS.escape(current) + '"] > .ant-anchor-link-title');
                    this.ball = title ? 'top: ' + (title.offsetTop + title.clientHeight / 2 - 4.5) + 'px;' : '';
                }
            };
        });

        // <app-menu>: selected item, open inline submenus, and the popup submenu under the pointer (hover).
        Alpine.data('modulusMenu', function (cfg) {
            return {
                selected: cfg.selected,
                open: cfg.open.slice(),
                hover: '',
                select: function (key) { this.selected = key; this.hover = ''; },
                isOpen: function (key) { return this.open.indexOf(key) >= 0; },
                toggle: function (key) {
                    var i = this.open.indexOf(key);
                    if (i >= 0) this.open.splice(i, 1); else this.open.push(key);
                }
            };
        });

        // <app-splitter>: drag the bar (pointer capture) or nudge it with the arrow keys; sizes are percentages.
        Alpine.data('modulusSplitter', function (cfg) {
            var dim = cfg.vertical ? 'height' : 'width';
            return {
                size: cfg.size,
                clamp: function (v) { return Math.min(cfg.max, Math.max(cfg.min, v)); },
                first: function () { return dim + ': ' + this.size + '%;'; },
                second: function () { return dim + ': ' + (100 - this.size) + '%;'; },
                nudge: function (d) { this.size = this.clamp(this.size + d); },
                start: function (e) {
                    var self = this, bar = e.currentTarget, box = this.$root.getBoundingClientRect();
                    bar.setPointerCapture(e.pointerId);
                    bar.classList.add('ant-splitter-bar-active');
                    var move = function (ev) {
                        var pos = cfg.vertical ? (ev.clientY - box.top) / box.height : (ev.clientX - box.left) / box.width;
                        self.size = self.clamp(Math.round(pos * 1000) / 10);
                    };
                    var up = function () {
                        bar.classList.remove('ant-splitter-bar-active');
                        bar.removeEventListener('pointermove', move);
                        bar.removeEventListener('pointerup', up);
                        bar.removeEventListener('pointercancel', up);
                    };
                    bar.addEventListener('pointermove', move);
                    bar.addEventListener('pointerup', up);
                    bar.addEventListener('pointercancel', up);
                    e.preventDefault();
                }
            };
        });

        // <app-affix>: pins the content (position: fixed) once its placeholder scrolls above offsetTop.
        Alpine.data('modulusAffix', function (offsetTop) {
            return {
                fixed: false,
                style: '',
                placeholder: '',
                init: function () {
                    var self = this, frame = 0;
                    this.onScroll = function () { if (!frame) frame = requestAnimationFrame(function () { frame = 0; self.update(); }); };
                    window.addEventListener('scroll', this.onScroll, { passive: true });
                    window.addEventListener('resize', this.onScroll);
                    this.$nextTick(function () { self.update(); });
                },
                destroy: function () {
                    window.removeEventListener('scroll', this.onScroll);
                    window.removeEventListener('resize', this.onScroll);
                },
                update: function () {
                    var box = this.$root.getBoundingClientRect();
                    var inner = this.$refs.inner.getBoundingClientRect();
                    this.fixed = box.top < offsetTop;
                    this.style = this.fixed ? 'position: fixed; top: ' + offsetTop + 'px; width: ' + box.width + 'px; z-index: 10;' : '';
                    this.placeholder = this.fixed ? 'width: ' + box.width + 'px; height: ' + inner.height + 'px;' : '';
                }
            };
        });

        // <app-back-top>: visible once the page has scrolled visibilityHeight px; scrolls smoothly to the top.
        Alpine.data('modulusBackTop', function (visibilityHeight) {
            return {
                visible: false,
                init: function () {
                    var self = this;
                    this.onScroll = function () { self.visible = window.scrollY > visibilityHeight; };
                    window.addEventListener('scroll', this.onScroll, { passive: true });
                    this.onScroll();
                },
                destroy: function () { window.removeEventListener('scroll', this.onScroll); },
                top: function () { window.scrollTo({ top: 0, behavior: 'smooth' }); }
            };
        });

        // <app-auto-complete>: filters the options by substring while typing.
        Alpine.data('modulusAutoComplete', function (options) {
            return {
                open: false,
                matches: [],
                active: -1,
                filter: function (text) {
                    var q = (text || '').trim().toLowerCase();
                    this.matches = options.filter(function (o) { return !q || o.toLowerCase().indexOf(q) >= 0; }).slice(0, 10);
                    this.active = -1;
                    this.open = this.matches.length > 0 && !(this.matches.length === 1 && this.matches[0] === text);
                },
                move: function (d) {
                    if (!this.open) { this.filter(this.$refs.input.value); return; }
                    var n = this.matches.length;
                    if (n) this.active = (this.active + d + n) % n;
                },
                pick: function (e) {
                    if (this.open && this.active >= 0) { e.preventDefault(); this.choose(this.matches[this.active]); }
                },
                choose: function (value) {
                    var input = this.$refs.input;
                    input.value = value;
                    input.dispatchEvent(new Event('change', { bubbles: true }));
                    this.open = false;
                }
            };
        });

        // <app-mentions>: after the prefix (@), lists the options matching the text typed so far and inserts one.
        Alpine.data('modulusMentions', function (cfg) {
            return {
                open: false,
                matches: [],
                active: 0,
                start: -1,
                update: function () {
                    var el = this.$refs.input, caret = el.selectionStart, before = el.value.slice(0, caret);
                    var at = before.lastIndexOf(cfg.prefix);
                    var query = at >= 0 ? before.slice(at + cfg.prefix.length) : null;
                    // A mention starts at the beginning or after whitespace, and holds no whitespace yet.
                    if (query === null || /\s/.test(query) || (at > 0 && !/\s/.test(before.charAt(at - 1)))) { this.open = false; return; }
                    var q = query.toLowerCase();
                    this.matches = cfg.options.filter(function (o) { return o.value.toLowerCase().indexOf(q) === 0 || o.label.toLowerCase().indexOf(q) >= 0; });
                    this.start = at;
                    this.active = 0;
                    this.open = this.matches.length > 0;
                },
                move: function (e, d) {
                    if (!this.open) return;
                    e.preventDefault();
                    var n = this.matches.length;
                    this.active = (this.active + d + n) % n;
                },
                pick: function (e) {
                    if (!this.open) return;
                    e.preventDefault();
                    this.choose(this.matches[this.active]);
                },
                choose: function (option) {
                    var el = this.$refs.input, caret = el.selectionStart;
                    var text = cfg.prefix + option.value + ' ';
                    el.value = el.value.slice(0, this.start) + text + el.value.slice(caret);
                    var pos = this.start + text.length;
                    el.setSelectionRange(pos, pos);
                    el.focus();
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    this.open = false;
                }
            };
        });

        // <app-image-group>: one preview for the group's images, with previous/next.
        Alpine.data('modulusImageGroup', function (items) {
            return {
                items: items,
                index: 0,
                open: false,
                _opener: null,
                get current() { return this.items[this.index] || { src: '', alt: '' }; },
                show: function (i) {
                    var self = this;
                    this._opener = document.activeElement;
                    this.index = i;
                    this.open = true;
                    setTimeout(function () { if (self.$refs.close) self.$refs.close.focus(); }, 50);
                },
                close: function () {
                    this.open = false;
                    if (this._opener && this._opener.focus) this._opener.focus();
                },
                go: function (d) { this.index = (this.index + d + this.items.length) % this.items.length; }
            };
        });

        // <app-countdown>: time left until `end` (epoch ms), formatted like CountdownTagHelper.FormatSpan.
        Alpine.data('modulusCountdown', function (end, format) {
            var pad = function (n) { return (n < 10 ? '0' : '') + n; };
            return {
                text: '',
                init: function () {
                    var self = this;
                    this.tick();
                    this.timer = setInterval(function () { self.tick(); }, 1000);
                },
                destroy: function () { clearInterval(this.timer); },
                tick: function () {
                    var total = Math.max(0, Math.floor((end - Date.now()) / 1000));
                    var days = Math.floor(total / 86400);
                    var hours = format.indexOf('d') >= 0 ? Math.floor(total / 3600) % 24 : Math.floor(total / 3600);
                    this.text = format.replace('hh', pad(hours)).replace('mm', pad(Math.floor(total / 60) % 60)).replace('ss', pad(total % 60)).replace('d', String(days));
                    if (total === 0) clearInterval(this.timer);
                }
            };
        });
    });
})();
