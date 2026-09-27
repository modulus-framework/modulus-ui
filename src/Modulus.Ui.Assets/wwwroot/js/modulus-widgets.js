/*
 * Alpine components behind the MVC widgets in AdvancedEntryTagHelpers.cs:
 * app-tree, app-tree-select, app-cascader, app-transfer, app-calendar, app-carousel, app-anchor,
 * app-editable-grid (EditableGridTagHelper.cs), app-menu (MenuTagHelpers.cs), app-splitter, app-affix,
 * app-back-top (FrameTagHelpers.cs), app-auto-complete, app-mentions, app-image-group and app-countdown
 * (ExtraTagHelpers.cs), app-color-picker and app-tour (ModernTagHelpers.cs).
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
                    this._close = function () { self.closeMenu(); self.placeAdd(self._editor); };
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
                    this.placeAdd(this._editor);
                },
                // The "Add option" panel of a Select, Multi-select or Tag editor is position: fixed under (or above) the editor.
                placeAdd: function (editor) {
                    var panel = editor && !editor.hidden && editor.querySelector('.m-grid-add-option');
                    if (!panel) return;
                    var r = editor.getBoundingClientRect(), h = panel.offsetHeight, vw = document.documentElement.clientWidth;
                    var top = r.bottom + 4;
                    if (top + h > window.innerHeight - 8 && r.top - h - 4 > 8) top = r.top - h - 4;
                    panel.style.top = Math.round(top) + 'px';
                    panel.style.left = Math.round(Math.max(8, Math.min(r.left, vw - panel.offsetWidth - 8))) + 'px';
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
                    this.placeAdd(editor);
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

        // <app-color-picker> (ModernTagHelpers.cs): HSV state mirroring Modulus.Ui.Abstractions.Theming.HsvColor.
        // cfg = { value, format: 'hex' | 'rgb', alpha: bool }. The hidden input posts the formatted colour ('' when cleared).
        function hsvFromRgb(r, g, b, a) {
            r /= 255; g /= 255; b /= 255;
            var max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min, h = 0;
            if (d > 0) {
                if (max === r) h = ((g - b) / d) % 6;
                else if (max === g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
                if (h < 0) h += 360;
            }
            return { h: h, s: max === 0 ? 0 : d / max, v: max, a: a };
        }

        function parseColor(value) {
            if (!value || !String(value).trim()) return null;
            var s = String(value).trim();
            if (/^rgba?\(/i.test(s)) {
                var m = s.slice(s.indexOf('(') + 1, s.indexOf(')')).split(',').map(function (p) { return p.trim(); });
                if (m.length < 3 || m.length > 4) return null;
                var rgb = m.slice(0, 3).map(function (p) { return /^\d+$/.test(p) ? parseInt(p, 10) : NaN; });
                if (rgb.some(function (n) { return isNaN(n) || n > 255; })) return null;
                var a = m.length === 4 ? parseFloat(m[3]) : 1;
                if (isNaN(a)) return null;
                return hsvFromRgb(rgb[0], rgb[1], rgb[2], Math.min(1, Math.max(0, a)));
            }
            if (s[0] === '#') s = s.slice(1);
            if (s.length === 3 || s.length === 4) s = s.split('').map(function (c) { return c + c; }).join('');
            if (!/^[0-9a-f]{6}([0-9a-f]{2})?$/i.test(s)) return null;
            var hex = function (i) { return parseInt(s.substr(i, 2), 16); };
            return hsvFromRgb(hex(0), hex(2), hex(4), s.length === 8 ? hex(6) / 255 : 1);
        }

        function toRgb(c) {
            var k = c.v * c.s, x = k * (1 - Math.abs((c.h / 60) % 2 - 1)), m = c.v - k, h = c.h % 360, t;
            t = h < 60 ? [k, x, 0] : h < 120 ? [x, k, 0] : h < 180 ? [0, k, x] : h < 240 ? [0, x, k] : h < 300 ? [x, 0, k] : [k, 0, x];
            return t.map(function (n) { return Math.round(Math.min(1, Math.max(0, n + m)) * 255); });
        }

        function byteHex(n) { return (n < 16 ? '0' : '') + n.toString(16); }

        function formatColor(c, fmt) {
            var rgb = toRgb(c);
            if (fmt === 'rgb') {
                return c.a >= 1 ? 'rgb(' + rgb.join(', ') + ')' : 'rgba(' + rgb.join(', ') + ', ' + Math.round(c.a * 100) / 100 + ')';
            }
            return '#' + rgb.map(byteHex).join('') + (c.a >= 1 ? '' : byteHex(Math.round(c.a * 255)));
        }

        // <app-creatable-select>: the Blazor AppCreatableSelect in Alpine. New options follow GridSchema.AddOption
        // (trimmed, unique label, capped count) and GridSchema.UniqueKey for the value, unless add-href saves them.
        Alpine.data('modulusCreatableSelect', function (cfg) {
            function esc(s) { return String(s == null ? '' : s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
            function uniqueKey(title, taken) {
                var words = title.replace(/[^A-Za-z0-9]/g, ' ').split(' ').filter(Boolean);
                var key = words.map(function (w, i) { return i === 0 ? w.toLowerCase() : w[0].toUpperCase() + w.slice(1).toLowerCase(); }).join('');
                if (!key || /^\d/.test(key)) key = 'col' + key;
                var lower = taken.map(function (t) { return t.toLowerCase(); }), candidate = key;
                for (var n = 2; lower.indexOf(candidate.toLowerCase()) >= 0; n++) candidate = key + n;
                return candidate;
            }
            function token() {
                try { return JSON.parse(document.body.getAttribute('hx-headers') || '{}'); } catch (e) { return {}; }
            }

            return {
                open: false,
                active: -1,
                options: cfg.options || [],
                value: cfg.value || '',
                values: (cfg.values || []).slice(),
                colors: cfg.colors || [],
                showColors: cfg.showColors !== false,
                newLabel: '',
                picked: null,
                error: '',
                busy: false,
                waiting: false,

                init: function () {
                    var self = this;
                    this.$watch('open', function (open) {
                        if (!open) return;
                        self.$nextTick(function () { if (window.modulusShell && window.modulusShell.placeSelect) window.modulusShell.placeSelect(self.$root); });
                    });
                },
                find: function (v) { return this.options.find(function (o) { return o.value === v; }); },
                sel: function (v) { return cfg.multiple ? this.values.indexOf(v) >= 0 : this.value === v; },
                has: function () { return cfg.multiple ? this.values.length > 0 : !!this.value; },
                color: function () { return this.picked || this.colors[this.options.length % this.colors.length]; },
                dot: function (o) {
                    return this.showColors && o && o.color && o.color !== 'default' ? '<span class="m-cselect-dot ant-tag-' + esc(o.color) + '" aria-hidden="true"></span>' : '';
                },
                inputs: function () {
                    var name = esc(cfg.name);
                    var list = cfg.multiple ? this.values : [this.value];
                    return list.map(function (v) { return '<input type="hidden" name="' + name + '" value="' + esc(v) + '" />'; }).join('');
                },
                selection: function () {
                    var self = this;
                    var placeholder = cfg.placeholder ? '<span class="ant-select-selection-placeholder">' + esc(cfg.placeholder) + '</span>' : '';
                    if (!cfg.multiple) {
                        if (!this.value) return placeholder;
                        var o = this.find(this.value), label = o ? o.label : this.value;
                        return '<span class="ant-select-selection-item" title="' + esc(label) + '">' + this.dot(o) + esc(label) + '</span>';
                    }
                    var tags = this.values.map(function (v) {
                        var o = self.find(v), label = o ? o.label : v;
                        var tint = o && o.color && o.color !== 'default' ? ' ant-tag-' + esc(o.color) : '';
                        return '<span class="ant-select-selection-overflow-item"><span class="ant-select-selection-item m-cselect-tag' + tint + '" title="' + esc(label) + '">' +
                            '<span class="ant-select-selection-item-content">' + esc(label) + '</span>' +
                            '<span class="ant-select-selection-item-remove" role="button" aria-label="Remove ' + esc(label) + '" data-remove="' + esc(v) + '">' +
                            '<span role="img" aria-label="close" class="anticon anticon-close"><svg viewBox="64 64 896 896" width="1em" height="1em" fill="currentColor" aria-hidden="true"><path d="M563.8 512l262.5-312.9c4.4-5.2.7-13.1-6.1-13.1h-79.8c-4.7 0-9.2 2.1-12.3 5.7L511.6 449.8 295.1 191.7c-3-3.6-7.5-5.7-12.3-5.7H203c-6.8 0-10.5 7.9-6.1 13.1L459.4 512 196.9 824.9A7.95 7.95 0 00203 838h79.8c4.7 0 9.2-2.1 12.3-5.7l216.5-258.1 216.5 258.1c3 3.6 7.5 5.7 12.3 5.7h79.8c6.8 0 10.5-7.9 6.1-13.1L563.8 512z"></path></svg></span>' +
                            '</span></span></span>';
                    }).join('');
                    return '<div class="ant-select-selection-overflow">' + tags + (this.values.length ? '' : placeholder) + '</div>';
                },
                changed: function () {
                    var root = this.$root;
                    this.$nextTick(function () { root.dispatchEvent(new Event('change', { bubbles: true })); });
                },
                show: function () {
                    if (this.open) return;
                    var current = cfg.multiple ? this.values[0] : this.value, self = this;
                    this.active = this.options.findIndex(function (o) { return o.value === current; });
                    if (this.active < 0) this.active = this.options.length ? 0 : -1;
                    this.error = '';
                    this.waiting = false;
                    this.open = true;
                    this.$nextTick(function () {
                        var el = self.$root.querySelector('.ant-select-item-option-active');
                        if (el) el.scrollIntoView({ block: 'nearest' });
                    });
                },
                close: function () {
                    if (!this.open) return;
                    this.open = false;
                    this.newLabel = '';
                    this.picked = null;
                    this.error = '';
                    this.$root.querySelector('.ant-select-selector').focus({ preventScroll: true });
                },
                onSelector: function (e) {
                    var remove = e.target.closest('[data-remove]');
                    if (remove) { this.remove(remove.getAttribute('data-remove')); return; }
                    if (this.open) this.close(); else this.show();
                },
                pick: function (v) {
                    if (cfg.multiple) {
                        var i = this.values.indexOf(v);
                        if (i >= 0) this.values.splice(i, 1); else this.values.push(v);
                        this.changed();
                        return;
                    }
                    this.value = v;
                    this.changed();
                    this.close();
                },
                remove: function (v) {
                    this.values = this.values.filter(function (x) { return x !== v; });
                    this.changed();
                },
                clear: function () {
                    if (cfg.multiple) this.values = []; else this.value = '';
                    this.changed();
                },
                key: function (e) {
                    var n = this.options.length;
                    if (!this.open) {
                        if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Enter' || e.key === ' ') { e.preventDefault(); this.show(); }
                        return;
                    }
                    if (e.key === 'ArrowDown' && n) { e.preventDefault(); this.active = (this.active + 1) % n; }
                    else if (e.key === 'ArrowUp' && n) { e.preventDefault(); this.active = (this.active - 1 + n) % n; }
                    else if ((e.key === 'Enter' || e.key === ' ') && this.active >= 0 && this.active < n) { e.preventDefault(); this.pick(this.options[this.active].value); }
                    else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); this.close(); return; }
                    else if (e.key === 'Tab') { this.close(); return; }
                    var el = this.$root.querySelectorAll('.ant-select-item-option')[this.active];
                    if (el) el.scrollIntoView({ block: 'nearest' });
                },
                // add-modal: "Add new" closes the list and opens the <app-modal> that creates the record. The server
                // answers a save with HxOptionAdded, which every select of that list hears; the one that asked selects it.
                addNew: function () {
                    this.close();
                    this.waiting = true;
                    window.dispatchEvent(new CustomEvent('modulus:modal-open', { detail: cfg.addModal }));
                },
                added: function (d) {
                    if (!d || d.list !== cfg.addModal || !d.value) return;
                    var o = { value: String(d.value), label: d.label || String(d.value), color: d.color || null };
                    if (!this.find(o.value)) this.options.unshift(o); // newest first, where the user looks
                    if (!this.waiting) return;
                    this.waiting = false;
                    if (cfg.multiple) { if (this.values.indexOf(o.value) < 0) this.values.push(o.value); this.changed(); }
                    else { this.value = o.value; this.changed(); }
                },
                accept: function (o) {
                    this.options.push(o);
                    this.newLabel = '';
                    this.picked = null;
                    if (cfg.multiple) { this.values.push(o.value); this.active = this.options.length - 1; this.changed(); }
                    else this.pick(o.value);
                },
                add: function () {
                    var label = (this.newLabel || '').trim(), self = this;
                    if (!label) { this.error = 'Enter a name.'; return; }
                    if (label.length > cfg.maxLabel) { this.error = 'Use ' + cfg.maxLabel + ' characters or fewer.'; return; }
                    if (this.options.some(function (o) { return o.label.toLowerCase() === label.toLowerCase(); })) { this.error = '“' + label + '” is already in the list.'; return; }
                    if (this.options.length >= cfg.maxOptions) { this.error = 'A list holds up to ' + cfg.maxOptions + ' options.'; return; }
                    var color = this.color();
                    if (!cfg.addHref) {
                        this.accept({ value: uniqueKey(label, this.options.map(function (o) { return o.value; })), label: label, color: color });
                        return;
                    }
                    var body = new FormData();
                    body.append('label', label);
                    body.append('color', color);
                    this.busy = true;
                    fetch(cfg.addHref, { method: 'POST', body: body, headers: token(), credentials: 'same-origin' })
                        .then(function (r) { return r.json(); })
                        .then(function (res) {
                            if (res && res.error) self.error = res.error;
                            else if (res && res.value) self.accept({ value: res.value, label: res.label, color: res.color });
                            else self.error = 'Could not add it. Try again.';
                        })
                        .catch(function () { self.error = 'Could not add it. Try again.'; })
                        .finally(function () { self.busy = false; });
                },
            };
        });

        Alpine.data('modulusColorPicker', function (cfg) {
            var start = parseColor(cfg.value) || parseColor('#1677ff');
            if (!cfg.alpha) start.a = 1;
            var clamp = function (n) { return Math.min(1, Math.max(0, n)); };
            return {
                open: false,
                cleared: !parseColor(cfg.value),
                h: start.h, s: start.s, v: start.v, a: start.a,
                fmt: cfg.format === 'rgb' ? 'rgb' : 'hex',
                dragging: null,
                get color() { return { h: this.h, s: this.s, v: this.v, a: this.a }; },
                get value() { return this.cleared ? '' : formatColor(this.color, cfg.format === 'rgb' ? 'rgb' : 'hex'); },
                get text() { return formatColor(this.color, this.fmt); },
                get css() { return formatColor(this.color, 'rgb'); },
                get hueCss() { return formatColor({ h: this.h, s: 1, v: 1, a: 1 }, 'hex'); },
                get alphaTrack() {
                    var rgb = toRgb(this.color).join(',');
                    return 'linear-gradient(to right, rgba(' + rgb + ',0), rgb(' + rgb + '))';
                },
                get pct() { return Math.round(this.a * 100); },
                is: function (hex) {
                    var c = parseColor(hex);
                    return !this.cleared && !!c && formatColor(c, 'hex') === formatColor(this.color, 'hex');
                },
                set: function (c) {
                    this.h = Math.min(360, Math.max(0, c.h));
                    this.s = clamp(c.s);
                    this.v = clamp(c.v);
                    this.a = cfg.alpha ? clamp(c.a) : 1;
                    this.cleared = false;
                    this.changed();
                },
                changed: function () {
                    var input = this.$refs.input;
                    this.$nextTick(function () { input.dispatchEvent(new Event('change', { bubbles: true })); });
                },
                pick: function (hex) { var c = parseColor(hex); if (c) this.set(c); },
                clear: function () { this.cleared = true; this.changed(); },
                down: function (kind, e) {
                    if (e.target.setPointerCapture) e.target.setPointerCapture(e.pointerId);
                    this.dragging = kind;
                    this.at(e);
                },
                move: function (e) { if (this.dragging) this.at(e); },
                up: function () { this.dragging = null; },
                at: function (e) {
                    var r = this.$refs[this.dragging].getBoundingClientRect();
                    var x = clamp((e.clientX - r.left) / r.width), y = clamp((e.clientY - r.top) / r.height);
                    var c = this.color;
                    if (this.dragging === 'palette') { c.s = x; c.v = 1 - y; }
                    else if (this.dragging === 'hue') c.h = x * 360;
                    else c.a = x;
                    this.set(c);
                },
                key: function (kind, e) {
                    var d = e.shiftKey ? 10 : 1, c = this.color;
                    var dir = { ArrowLeft: -1, ArrowDown: -1, ArrowRight: 1, ArrowUp: 1 }[e.key];
                    if (!dir) return;
                    if (kind === 'palette') {
                        if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') c.s += dir * d / 100; else c.v += dir * d / 100;
                    } else if (kind === 'hue') c.h += dir * d;
                    else c.a += dir * d / 100;
                    this.set(c);
                },
                typed: function (e) {
                    var c = parseColor(e.target.value);
                    if (c) this.set(c); else e.target.value = this.text;
                },
                typedAlpha: function (e) {
                    var n = parseFloat(e.target.value);
                    if (!isNaN(n)) { var c = this.color; c.a = n / 100; this.set(c); }
                }
            };
        });

        // <app-tour> (ModernTagHelpers.cs): opened by $dispatch('modulus-tour-open', name); modulusShell.tour positions
        // the card. cfg = { name, steps: [{ title, description, target, cover, placement }], open }.
        Alpine.data('modulusTour', function (cfg) {
            return {
                open: false,
                current: 0,
                steps: cfg.steps,
                get step() { return this.steps[this.current] || {}; },
                get last() { return this.current === this.steps.length - 1; },
                init: function () { if (cfg.open) this.start(); },
                start: function (detail) {
                    if (detail !== undefined && detail !== cfg.name) return;
                    if (!this.steps.length) return;
                    this.current = 0;
                    this.open = true;
                    this.place(true);
                },
                place: function (focus) {
                    var root = this.$refs.root;
                    this.$nextTick(function () { window.modulusShell && window.modulusShell.tour(root, focus); });
                },
                go: function (i) { this.current = i; this.place(false); },
                next: function () {
                    if (!this.last) { this.go(this.current + 1); return; }
                    this.open = false;
                    this.$dispatch('modulus-tour-finish', cfg.name);
                },
                prev: function () { if (this.current > 0) this.go(this.current - 1); },
                close: function () {
                    this.open = false;
                    this.$dispatch('modulus-tour-close', cfg.name);
                }
            };
        });
    });
})();
