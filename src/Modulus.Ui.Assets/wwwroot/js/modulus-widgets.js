/*
 * Alpine components behind the MVC widgets in AdvancedEntryTagHelpers.cs:
 * app-tree, app-tree-select, app-cascader, app-transfer, app-calendar, app-carousel, app-anchor,
 * app-date-picker and app-range-picker (DataEntryTagHelpers.cs),
 * app-editable-grid (EditableGridTagHelper.cs), app-menu (MenuTagHelpers.cs), app-splitter, app-affix,
 * app-back-top (FrameTagHelpers.cs), app-auto-complete, app-mentions and app-countdown
 * (ExtraTagHelpers.cs), app-color-picker and app-tour (ModernTagHelpers.cs), and the business fields
 * app-money-input, app-quantity-input, app-duration-input, app-number-range, app-phone-input and app-lookup
 * (BusinessFieldTagHelpers.cs; masks, one-time codes, signatures and rich text run in modulus-shell.js).
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

        // <app-date-picker> and <app-range-picker>: Ant Design's picker popup (the DOM and classes AntD Blazor
        // renders), so MVC and Razor Pages pickers look and behave like Blazor's. The text input shows the value the
        // way AntD formats it; hidden inputs post the canonical value (yyyy-MM-dd, yyyy-MM-ddTHH:mm:ss, HH:mm[:ss],
        // yyyy-Www, yyyy-MM, yyyy-Qn, yyyy). cfg: picker, showTime, range, values, format (time columns), min, max,
        // days (disabled weekdays, getDay numbers), presets [{ label, from, to }], message, texts (locale strings).
        Alpine.data('modulusPicker', function (cfg) {
            var T = cfg.texts || {};
            var MONTHS = T.months || ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
            var DAYS = T.days || ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];
            var PICKER = cfg.picker || 'date';
            var TIME = PICKER === 'time';
            var DT = !!cfg.showTime && !TIME;
            var RANGE = !!cfg.range;
            var COLS = TIME || DT ? (cfg.format || 'HH:mm:ss').split(':').slice(0, 3) : [];
            var MIN = cfg.min ? parseAny(cfg.min) : null;
            var MAX = cfg.max ? parseAny(cfg.max) : null;
            var OFF = cfg.days || [];
            var DAY = 864e5;

            function pad(n) { return (n < 10 ? '0' : '') + n; }
            function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
            function ymd(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
            function hms(d) {
                return COLS.map(function (c) { return pad(c[0] === 'H' ? d.getHours() : c[0] === 'm' ? d.getMinutes() : d.getSeconds()); }).join(':');
            }
            function dayOnly(d) { return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }
            function sameDay(a, b) { return !!a && !!b && a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate(); }
            // Week of the year as .NET's GregorianCalendar counts it for en-US (CalendarWeekRule.FirstDay, Sunday).
            function weekOf(d) {
                var jan1 = new Date(d.getFullYear(), 0, 1);
                var days = Math.round((Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()) - Date.UTC(d.getFullYear(), 0, 1)) / DAY);
                return Math.floor((days + jan1.getDay()) / 7) + 1;
            }
            function weekStart(d) { var s = dayOnly(d); s.setDate(s.getDate() - s.getDay()); return s; }
            function parseAny(s) {
                s = String(s || '').trim();
                var m;
                if ((m = /^(\d{4})-(\d{1,2})-(\d{1,2})(?:[T ](\d{1,2}):(\d{2})(?::(\d{2}))?)?$/.exec(s))) {
                    var d = new Date(+m[1], +m[2] - 1, +m[3], +(m[4] || 0), +(m[5] || 0), +(m[6] || 0));
                    return d.getMonth() === +m[2] - 1 ? d : null;
                }
                if ((m = /^(\d{4})-?[Qq]([1-4])$/.exec(s))) return new Date(+m[1], (+m[2] - 1) * 3, 1);
                if ((m = /^(\d{4})-[Ww]?(\d{1,2})(?:Week)?$/i.exec(s)) && (PICKER === 'week' || /[Ww]/.test(s))) {
                    var first = new Date(+m[1], 0, 1);
                    var start = new Date(+m[1], 0, 1 - first.getDay() + (+m[2] - 1) * 7);
                    return start.getFullYear() < +m[1] ? first : start;
                }
                if ((m = /^(\d{4})-(\d{1,2})$/.exec(s)) && +m[2] >= 1 && +m[2] <= 12) return new Date(+m[1], +m[2] - 1, 1);
                if ((m = /^(\d{4})$/.exec(s))) return new Date(+m[1], 0, 1);
                if ((m = /^(\d{1,2}):(\d{2})(?::(\d{2}))?$/.exec(s)) && +m[1] < 24 && +m[2] < 60) {
                    var t = new Date(); t.setHours(+m[1], +m[2], +(m[3] || 0), 0); return t;
                }
                return null;
            }
            // What the input shows (AntD Blazor's formats) and what the hidden input posts.
            function display(d) {
                if (!d) return '';
                if (TIME) return hms(d);
                if (DT) return ymd(d) + ' ' + hms(d);
                switch (PICKER) {
                    case 'week': return d.getFullYear() + '-' + weekOf(d) + (T.week || 'Week');
                    case 'month': return d.getFullYear() + '-' + pad(d.getMonth() + 1);
                    case 'quarter': return d.getFullYear() + '-Q' + (Math.floor(d.getMonth() / 3) + 1);
                    case 'year': return String(d.getFullYear());
                    default: return ymd(d);
                }
            }
            function canonical(d) {
                if (!d) return '';
                if (DT) return ymd(d) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
                if (PICKER === 'week') return d.getFullYear() + '-W' + pad(weekOf(d));
                return display(d);
            }
            // Snaps a picked day to the picker's unit.
            function snap(d) {
                if (!d) return null;
                if (TIME || DT) return d;
                if (PICKER === 'month') return new Date(d.getFullYear(), d.getMonth(), 1);
                if (PICKER === 'quarter') return new Date(d.getFullYear(), Math.floor(d.getMonth() / 3) * 3, 1);
                if (PICKER === 'year') return new Date(d.getFullYear(), 0, 1);
                return dayOnly(d);
            }
            function dayOff(d) {
                var x = dayOnly(d);
                return (MIN && x < dayOnly(MIN)) || (MAX && x > dayOnly(MAX)) || OFF.indexOf(x.getDay()) >= 0;
            }
            // A month, quarter or year is disabled when none of its days can be picked by min / max.
            function spanOff(from, to) { return (MIN && to < dayOnly(MIN)) || (MAX && from > dayOnly(MAX)); }

            var values = (cfg.values || []).map(function (v) { var d = parseAny(v); return d ? snap(d) : null; });
            if (!RANGE) values = [values[0] || null];
            else while (values.length < 2) values.push(null);
            var anchor = values[0] || values[1] || new Date();

            return {
                open: false,
                active: 0,
                values: values,
                pending: null,
                mode: TIME ? 'time' : PICKER === 'date' ? 'date' : PICKER,
                year: anchor.getFullYear(),
                month: anchor.getMonth(),
                hover: null,
                placement: 'bottomLeft',
                texts: values.map(display),
                invalid: [false, false],
                pendingTouched: false,
                bar: '',
                arrow: '',

                init: function () {
                    var self = this;
                    this.$watch('open', function (on) {
                        self.$el.classList.toggle('ant-picker-focused', on);
                        if (on) self.$nextTick(function () { self.place(); self.scrollTime(); });
                    });
                    window.addEventListener('resize', function () { if (self.open) self.place(); });
                    document.addEventListener('scroll', function (e) {
                        if (self.open && !(e.target instanceof Element && self.$refs.drop && self.$refs.drop.contains(e.target))) self.place();
                    }, { capture: true, passive: true });
                    this.moveBar();
                },
                input: function (i) { return this.$root.querySelectorAll('[data-m-picker-text]')[i]; },
                // A press on the picker's frame, icon or separator focuses the input being edited, as in AntD.
                rootDown: function (e) {
                    if (cfg.disabled || !(e.target instanceof Element)) return;
                    if (e.target.closest('input, .ant-picker-clear') || (this.$refs.drop && this.$refs.drop.contains(e.target))) return;
                    e.preventDefault();
                    var i = this.open ? this.active : 0, input = this.input(i);
                    if (input) { input.focus(); this.show(i); }
                },
                show: function (i) {
                    if (cfg.disabled) return;
                    if (i !== undefined) this.active = i;
                    var v = this.values[this.active] || this.values[0] || this.values[1];
                    if (!this.open && v) { this.year = v.getFullYear(); this.month = v.getMonth(); }
                    if (!this.open && RANGE && this.active === 1 && this.values[1]) {
                        // The end month shows on the right-hand panel.
                        var left = new Date(this.values[1].getFullYear(), this.values[1].getMonth() - 1, 1);
                        if (!this.values[0] || this.values[0] < left) { this.year = left.getFullYear(); this.month = left.getMonth(); }
                    }
                    if (!this.open) this.pending = TIME || DT ? new Date((this.values[this.active] || new Date(new Date().setHours(0, 0, 0, 0))).getTime()) : null;
                    this.open = true;
                    this.moveBar();
                },
                close: function (commitTime) {
                    if (!this.open) return;
                    if (commitTime && this.pending && (TIME || DT) && this.pendingTouched) this.commit(this.pending, true);
                    this.open = false;
                    this.hover = null;
                    this.pendingTouched = false;
                    this.mode = TIME ? 'time' : PICKER === 'date' ? 'date' : PICKER;
                    this.texts = this.values.map(display);
                },
                clear: function () {
                    var self = this;
                    this.values = this.values.map(function () { return null; });
                    this.texts = this.values.map(display);
                    this.invalid = [false, false];
                    this.$nextTick(function () { self.raise(0); if (RANGE) self.raise(1); self.validate(); });
                },
                // Hidden inputs raise change, so forms, htmx triggers and Alpine listeners see every new value.
                raise: function (i) {
                    var hidden = this.$root.querySelectorAll('[data-m-picker-value]')[i];
                    if (!hidden) return;
                    hidden.value = canonical(this.values[i]);
                    hidden.dispatchEvent(new Event('change', { bubbles: true }));
                },
                commit: function (d, keepOpen) {
                    var i = this.active, self = this;
                    this.values[i] = d ? snap(d) : null;
                    if (RANGE && this.values[0] && this.values[1] && this.values[0] > this.values[1]) {
                        var t = this.values[0]; this.values[0] = this.values[1]; this.values[1] = t;
                    }
                    this.texts = this.values.map(display);
                    this.invalid[i] = false;
                    this.$nextTick(function () { self.raise(0); if (RANGE) self.raise(1); self.validate(); });
                    if (keepOpen) return;
                    if (RANGE && i === 0 && !this.values[1] || RANGE && i === 1 && !this.values[0]) {
                        this.active = 1 - i;
                        this.pending = DT ? new Date((this.values[this.active] || this.values[i]).getTime()) : null;
                        this.pendingTouched = false;
                        var next = this.input(this.active);
                        if (next) next.focus();
                        this.moveBar();
                        return;
                    }
                    this.close(false);
                    // The editable grid ends a Date cell's edit on a pick, as the Blazor grid's DatePicker does.
                    this.$root.dispatchEvent(new CustomEvent('m-picker-pick', { bubbles: true }));
                },
                // Typed text: a value in any accepted form moves the panel there; Enter keeps it, Escape leaves.
                typed: function (i, e) {
                    this.texts[i] = e.target.value;
                    var d = parseAny(e.target.value);
                    this.invalid[i] = !!e.target.value && (!d || dayOff(d));
                    if (d && !this.invalid[i]) {
                        this.year = d.getFullYear(); this.month = d.getMonth();
                        if (TIME || DT) this.pending = d;
                    }
                },
                keydown: function (i, e) {
                    if (e.key === 'Enter') {
                        e.preventDefault();
                        var d = parseAny(this.texts[i]);
                        if (!this.texts[i]) { this.values[i] = null; this.texts = this.values.map(display); this.raise(i); this.close(false); return; }
                        if (d && !dayOff(d)) { this.active = i; this.commit(d); }
                    } else if (e.key === 'Escape') { this.close(false); }
                    else if (e.key === 'Tab') { this.close(true); }
                    else if (!this.open && e.key !== 'Shift') this.show(i);
                },
                validate: function () {
                    var self = this;
                    this.$root.querySelectorAll('[data-m-picker-text]').forEach(function (input, i) {
                        var bad = self.invalid[i];
                        input.setCustomValidity(bad ? cfg.message || 'This date can’t be picked.' : '');
                    });
                },

                // Positions ---------------------------------------------------------------------------------
                moveBar: function () {
                    if (!RANGE) return;
                    var self = this;
                    this.$nextTick(function () {
                        var input = self.input(self.active);
                        var box = input && input.parentElement;
                        if (!box) return;
                        var left = box.offsetLeft - parseFloat(getComputedStyle(self.$root).paddingLeft || 0);
                        self.bar = 'position: absolute; left: ' + left + 'px; width: ' + box.offsetWidth + 'px;';
                        self.arrow = 'left: ' + left + 'px;';
                    });
                },
                place: function () {
                    var drop = this.$refs.drop;
                    if (!drop) return;
                    var r = this.$root.getBoundingClientRect(), gap = RANGE ? 0 : 4, edge = 8;
                    var vw = document.documentElement.clientWidth, vh = window.innerHeight;
                    var h = drop.offsetHeight, w = drop.offsetWidth;
                    var up = h > vh - r.bottom - gap - edge && r.top - gap - edge > vh - r.bottom - gap - edge;
                    this.placement = up ? 'topLeft' : 'bottomLeft';
                    drop.style.left = Math.round(Math.max(edge, Math.min(r.left, vw - w - edge))) + 'px';
                    drop.style.top = Math.round(up ? r.top - gap - h : r.bottom + gap) + 'px';
                },
                scrollTime: function () {
                    var drop = this.$refs.drop;
                    if (!drop) return;
                    drop.querySelectorAll('.ant-picker-time-panel-column').forEach(function (col) {
                        var sel = col.querySelector('.ant-picker-time-panel-cell-selected');
                        if (sel) col.scrollTop = sel.offsetTop;
                    });
                },

                // Panels ------------------------------------------------------------------------------------
                header: function (view, hidePrev, hideNext) {
                    var hide = ' style="visibility: hidden"';
                    var sprev = '<button type="button" tabindex="-1" class="ant-picker-header-super-prev-btn" data-nav="-12" aria-label="' + esc(T.prevYear || 'Last year') + '"' + (hidePrev ? hide : '') + '><span class="ant-picker-super-prev-icon"></span></button>';
                    var snext = '<button type="button" tabindex="-1" class="ant-picker-header-super-next-btn" data-nav="12" aria-label="' + esc(T.nextYear || 'Next year') + '"' + (hideNext ? hide : '') + '><span class="ant-picker-super-next-icon"></span></button>';
                    if (this.mode === 'year') {
                        var decade = Math.floor(this.year / 10) * 10;
                        return '<div class="ant-picker-header">' + sprev.replace('data-nav="-12"', 'data-nav="-120"') +
                            '<div class="ant-picker-header-view"><button type="button" tabindex="-1" class="ant-picker-decade-btn">' + decade + '-' + (decade + 9) + '</button></div>' +
                            snext.replace('data-nav="12"', 'data-nav="120"') + '</div>';
                    }
                    if (this.mode === 'month' || this.mode === 'quarter') {
                        return '<div class="ant-picker-header">' + sprev +
                            '<div class="ant-picker-header-view"><button type="button" tabindex="-1" class="ant-picker-year-btn" data-mode="year">' + view.getFullYear() + '</button></div>' +
                            snext + '</div>';
                    }
                    return '<div class="ant-picker-header">' + sprev +
                        '<button type="button" tabindex="-1" class="ant-picker-header-prev-btn" data-nav="-1" aria-label="' + esc(T.prevMonth || 'Previous month') + '"' + (hidePrev ? hide : '') + '><span class="ant-picker-prev-icon"></span></button>' +
                        '<div class="ant-picker-header-view"><button type="button" tabindex="-1" class="ant-picker-month-btn" data-mode="month">' + MONTHS[view.getMonth()] + '</button>' +
                        '<button type="button" tabindex="-1" class="ant-picker-year-btn" data-mode="year">' + view.getFullYear() + '</button></div>' +
                        '<button type="button" tabindex="-1" class="ant-picker-header-next-btn" data-nav="1" aria-label="' + esc(T.nextMonth || 'Next month') + '"' + (hideNext ? hide : '') + '><span class="ant-picker-next-icon"></span></button>' +
                        snext + '</div>';
                },
                dateCellClass: function (d, view) {
                    var c = 'ant-picker-cell';
                    if (d.getMonth() === view.getMonth()) c += ' ant-picker-cell-in-view';
                    if (sameDay(d, new Date())) c += ' ant-picker-cell-today';
                    var off = dayOff(d);
                    if (RANGE && !off) {
                        // Picking the end: days before the start can't be picked, and the other way round.
                        var other = this.values[1 - this.active];
                        if (other && !DT) off = this.active === 1 ? dayOnly(d) < dayOnly(other) : dayOnly(d) > dayOnly(other);
                    }
                    if (off) c += ' ant-picker-cell-disabled';
                    if (RANGE) {
                        var a = this.values[0], b = this.values[1];
                        if (sameDay(d, a)) c += ' ant-picker-cell-selected ant-picker-cell-range-start' + (b ? '' : ' ant-picker-cell-range-start-single');
                        if (sameDay(d, b)) c += ' ant-picker-cell-selected ant-picker-cell-range-end' + (a ? '' : ' ant-picker-cell-range-end-single');
                        if (a && b && d > dayOnly(a) && d < dayOnly(b)) c += ' ant-picker-cell-in-range';
                        var h = this.hover, other2 = this.values[1 - this.active];
                        if (h && other2) {
                            var from = this.active === 1 ? dayOnly(other2) : h, to = this.active === 1 ? h : dayOnly(other2);
                            if (from <= to) {
                                if (sameDay(d, from) && !sameDay(d, other2)) c += ' ant-picker-cell-range-hover-start';
                                else if (sameDay(d, to) && !sameDay(d, other2)) c += ' ant-picker-cell-range-hover-end';
                                else if (d > from && d < to) c += ' ant-picker-cell-range-hover';
                            }
                        }
                    } else {
                        var v = TIME || DT ? this.pending : this.values[0];
                        if (PICKER !== 'week' && sameDay(d, v)) c += ' ant-picker-cell-selected';
                    }
                    return c;
                },
                datePanel: function (view, hidePrev, hideNext) {
                    var week = PICKER === 'week' && !RANGE;
                    var first = new Date(view.getFullYear(), view.getMonth(), 1);
                    var day = new Date(first); day.setDate(1 - first.getDay());
                    var html = '<div class="ant-picker-' + (week ? 'week' : 'date') + '-panel">' + this.header(view, hidePrev, hideNext) +
                        '<div class="ant-picker-body"><table class="ant-picker-content"><thead><tr>' + (week ? '<th aria-label="empty cell"></th>' : '');
                    DAYS.forEach(function (d) { html += '<th>' + esc(d) + '</th>'; });
                    html += '</tr></thead><tbody>';
                    var selWeek = week && this.values[0] ? weekStart(this.values[0]).getTime() : null;
                    for (var w = 0; w < 6; w++) {
                        var rowStart = new Date(day);
                        if (week) {
                            var inView = new Date(rowStart); while (inView.getMonth() !== view.getMonth() && inView - rowStart < 6 * DAY) inView.setDate(inView.getDate() + 1);
                            html += '<tr class="ant-picker-week-panel-row' + (selWeek === rowStart.getTime() ? ' ant-picker-week-panel-row-selected' : '') + '">' +
                                '<td class="ant-picker-cell ant-picker-cell-week"><div class="ant-picker-cell-inner">' + weekOf(inView) + '</div></td>';
                        } else html += '<tr>';
                        for (var i = 0; i < 7; i++) {
                            html += '<td title="' + ymd(day) + '" data-v="' + ymd(day) + '" class="' + this.dateCellClass(day, view) + '"><div class="ant-picker-cell-inner">' + day.getDate() + '</div></td>';
                            day.setDate(day.getDate() + 1);
                        }
                        html += '</tr>';
                    }
                    return html + '</tbody></table></div></div>';
                },
                gridPanel: function (kind) {
                    var year = this.year, html = '<div class="ant-picker-' + kind + '-panel">' + this.header(new Date(year, 0, 1)) + '<div class="ant-picker-body"><table class="ant-picker-content"><tbody>';
                    var v = this.values[0], today = new Date(), self = this;
                    function cell(d, text, cls, from, to) {
                        return '<td title="' + esc(text) + '" data-v="' + ymd(d) + '" class="ant-picker-cell' + cls + (spanOff(from, to) ? ' ant-picker-cell-disabled' : '') + '"><div class="ant-picker-cell-inner">' + esc(text) + '</div></td>';
                    }
                    if (kind === 'quarter') {
                        html += '<tr>';
                        for (var q = 0; q < 4; q++) {
                            var qd = new Date(year, q * 3, 1);
                            var qsel = PICKER === 'quarter' && v && v.getFullYear() === year && Math.floor(v.getMonth() / 3) === q;
                            html += cell(qd, 'Q' + (q + 1), ' ant-picker-cell-in-view' + (qsel ? ' ant-picker-cell-selected' : '') +
                                (today.getFullYear() === year && Math.floor(today.getMonth() / 3) === q ? ' ant-picker-cell-today' : ''), qd, new Date(year, q * 3 + 3, 0));
                        }
                        return html + '</tr></tbody></table></div></div>';
                    }
                    for (var r = 0; r < 4; r++) {
                        html += '<tr>';
                        for (var c = 0; c < 3; c++) {
                            if (kind === 'month') {
                                var m = r * 3 + c, md = new Date(year, m, 1);
                                var msel = (PICKER === 'month' ? v : self.values[self.active] || v);
                                html += cell(md, MONTHS[m], ' ant-picker-cell-in-view' +
                                    (msel && msel.getFullYear() === year && msel.getMonth() === m ? ' ant-picker-cell-selected' : '') +
                                    (today.getFullYear() === year && today.getMonth() === m ? ' ant-picker-cell-today' : ''), md, new Date(year, m + 1, 0));
                            } else {
                                var start = Math.floor(year / 10) * 10 - 1, y = start + r * 3 + c, yd = new Date(y, 0, 1);
                                var ysel = self.values[self.active] || v;
                                html += cell(yd, String(y), (y > start && y < start + 11 ? ' ant-picker-cell-in-view' : '') +
                                    (ysel && ysel.getFullYear() === y ? ' ant-picker-cell-selected' : '') +
                                    (today.getFullYear() === y ? ' ant-picker-cell-today' : ''), yd, new Date(y, 11, 31));
                            }
                        }
                        html += '</tr>';
                    }
                    return html + '</tbody></table></div></div>';
                },
                timePanel: function () {
                    var p = this.pending || new Date(new Date().setHours(0, 0, 0, 0));
                    var html = '<div class="ant-picker-time-panel">' + (DT ? '<div class="ant-picker-header"><div class="ant-picker-header-view">' + hms(p) + '</div></div>' : '') + '<div class="ant-picker-content">';
                    COLS.forEach(function (col) {
                        var unit = col[0] === 'H' ? 'h' : col[0] === 'm' ? 'm' : 's', max = unit === 'h' ? 24 : 60;
                        var cur = unit === 'h' ? p.getHours() : unit === 'm' ? p.getMinutes() : p.getSeconds();
                        html += '<ul class="ant-picker-time-panel-column" style="position: relative;">';
                        for (var n = 0; n < max; n++) {
                            html += '<li class="ant-picker-time-panel-cell' + (n === cur ? ' ant-picker-time-panel-cell-selected' : '') + '" data-time="' + unit + n + '"><div class="ant-picker-time-panel-cell-inner">' + pad(n) + '</div></li>';
                        }
                        html += '</ul>';
                    });
                    return html + '</div></div>';
                },
                footer: function () {
                    if (TIME || DT) {
                        return '<div class="ant-picker-footer"><ul class="ant-picker-ranges"><li class="ant-picker-now"><a class="ant-picker-now-btn" data-now>' + esc(T.now || 'Now') + '</a></li>' +
                            '<li class="ant-picker-ok"><button type="button" class="ant-btn ant-btn-primary ant-btn-sm" data-ok><span>' + esc(T.ok || 'Ok') + '</span></button></li></ul></div>';
                    }
                    if (RANGE) {
                        if (!(cfg.presets || []).length) return '';
                        return '<div class="ant-picker-footer"><ul class="ant-picker-ranges">' + cfg.presets.map(function (p, i) {
                            return '<li class="ant-picker-preset"><span class="ant-tag ant-tag-blue" data-preset="' + i + '">' + esc(p.label) + '</span></li>';
                        }).join('') + '</ul></div>';
                    }
                    if (PICKER === 'date' && this.mode === 'date') {
                        var off = dayOff(new Date());
                        return '<div class="ant-picker-footer"><a class="ant-picker-today-btn' + (off ? ' ant-picker-today-btn-disabled' : '') + '"' + (off ? '' : ' data-today') + '>' + esc(T.today || 'Today') + '</a></div>';
                    }
                    return '';
                },
                body: function () {
                    if (this.mode === 'time') return this.timePanel();
                    if (this.mode === 'month' || this.mode === 'quarter' || this.mode === 'year') return this.gridPanel(this.mode);
                    var view = new Date(this.year, this.month, 1);
                    if (DT) return '<div class="ant-picker-datetime-panel">' + this.datePanel(view) + this.timePanel() + '</div>';
                    return this.datePanel(view);
                },
                dropClass: function () {
                    return 'ant-picker-dropdown m-picker-dropdown' + (RANGE ? ' ant-picker-dropdown-range' : '') + ' ant-picker-dropdown-placement-' + this.placement;
                },
                panel: function () {
                    if (RANGE && !DT && this.mode === 'date') {
                        var left = new Date(this.year, this.month, 1), right = new Date(this.year, this.month + 1, 1);
                        return '<div class="ant-picker-range-wrapper ant-picker-date-range-wrapper"><div class="ant-picker-range-arrow" style="' + this.arrow + '"></div>' +
                            '<div class="ant-picker-panel-container"><div class="ant-picker-panels">' +
                            '<div class="ant-picker-panel">' + this.datePanel(left, false, true) + '</div>' +
                            '<div class="ant-picker-panel">' + this.datePanel(right, true, false) + '</div></div>' + this.footer() + '</div></div>';
                    }
                    var inner = '<div class="ant-picker-panel' + (this.open ? ' ant-picker-panel-focused' : '') + '">' + this.body() + this.footer() + '</div>';
                    if (RANGE) return '<div class="ant-picker-range-wrapper ant-picker-date-range-wrapper"><div class="ant-picker-range-arrow" style="' + this.arrow + '"></div><div class="ant-picker-panel-container">' + inner + '</div></div>';
                    return '<div class="ant-picker-panel-container">' + inner + '</div>';
                },

                // Clicks inside the popup -------------------------------------------------------------------
                pick: function (e) {
                    var t = e.target instanceof Element ? e.target : null;
                    if (!t) return;
                    var nav = t.closest('[data-nav]');
                    if (nav) {
                        // Months in the date and week panels (±1, ±12); years elsewhere (±12 is a year, ±120 a decade).
                        var step = +nav.getAttribute('data-nav');
                        var d = this.mode === 'date' || this.mode === 'week'
                            ? new Date(this.year, this.month + step, 1)
                            : new Date(this.year + step / 12, this.month, 1);
                        this.year = d.getFullYear(); this.month = d.getMonth();
                        return;
                    }
                    var mode = t.closest('[data-mode]');
                    if (mode) { this.mode = mode.getAttribute('data-mode'); return; }
                    var preset = t.closest('[data-preset]');
                    if (preset) {
                        var p = cfg.presets[+preset.getAttribute('data-preset')];
                        this.values = [parseAny(p.from), parseAny(p.to)];
                        var self = this;
                        this.texts = this.values.map(display);
                        this.invalid = [false, false];
                        this.$nextTick(function () { self.raise(0); self.raise(1); self.validate(); });
                        this.close(false);
                        return;
                    }
                    if (t.closest('[data-today]')) { this.commit(new Date()); return; }
                    if (t.closest('[data-now]')) { this.commit(new Date()); return; }
                    if (t.closest('[data-ok]')) { this.commit(this.pending || new Date()); return; }
                    var time = t.closest('[data-time]');
                    if (time) {
                        var v = time.getAttribute('data-time'), n = +v.slice(1), pd = new Date((this.pending || new Date(new Date().setHours(0, 0, 0, 0))).getTime());
                        if (v[0] === 'h') pd.setHours(n); else if (v[0] === 'm') pd.setMinutes(n); else pd.setSeconds(n);
                        this.pending = pd;
                        this.pendingTouched = true;
                        this.texts[this.active] = display(pd);
                        // The panel is drawn again, so each column is scrolled back to its selected cell.
                        var self2 = this;
                        this.$nextTick(function () { self2.scrollTime(); });
                        return;
                    }
                    var cell = t.closest('td[data-v]');
                    if (!cell || cell.classList.contains('ant-picker-cell-disabled')) return;
                    var day = parseAny(cell.getAttribute('data-v'));
                    if (this.mode === 'year') {
                        this.year = day.getFullYear();
                        if (PICKER === 'year') { this.commit(day); return; }
                        this.mode = PICKER === 'quarter' ? 'quarter' : 'month';
                        return;
                    }
                    if (this.mode === 'month' && PICKER !== 'month') {
                        this.year = day.getFullYear(); this.month = day.getMonth();
                        this.mode = PICKER === 'week' ? 'week' : 'date';
                        return;
                    }
                    if (DT) {
                        var cur = this.pending || new Date(new Date().setHours(0, 0, 0, 0));
                        this.pending = new Date(day.getFullYear(), day.getMonth(), day.getDate(), cur.getHours(), cur.getMinutes(), cur.getSeconds());
                        this.pendingTouched = true;
                        this.texts[this.active] = display(this.pending);
                        return;
                    }
                    this.commit(day);
                },
                hoverCell: function (e) {
                    if (!RANGE) return;
                    var cell = e.target instanceof Element && e.target.closest('td[data-v]');
                    var h = cell && !cell.classList.contains('ant-picker-cell-disabled') ? parseAny(cell.getAttribute('data-v')) : null;
                    if ((h && h.getTime()) !== (this.hover && this.hover.getTime())) this.hover = h;
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

        // ColumnSearch.ValueList: "=a|b" is one of the values, "!=a|b" none of them ("!=a" alone is "not exactly a").
        function valueList(q) {
            q = String(q || '').trim();
            var exclude = q.indexOf('!=') === 0;
            if (exclude ? q.length < 3 : q.length < 2 || q[0] !== '=' || q.indexOf('|') < 0) return null;
            return { exclude: exclude, values: q.slice(exclude ? 2 : 1).split('|').map(function (v) { return v.trim().toLowerCase(); }) };
        }
        // ColumnSearch.ValueQuery: '' when every value (or none) is picked, else the shorter of "=picked" and "!=others".
        function valueQuery(picked, values) {
            if (!picked.length || picked.length >= values.length) return '';
            var off = values.filter(function (v) { return picked.indexOf(v) < 0; });
            function list(prefix, vs) { return prefix + (vs.length === 1 && vs[0] === '' ? '|' : vs.join('|')); }
            var include = list('=', picked), exclude = list('!=', off);
            return exclude.length < include.length ? exclude : include;
        }
        function markCheck(box) {
            box.closest('.ant-checkbox').classList.toggle('ant-checkbox-checked', box.checked);
            box.closest('.ant-checkbox-wrapper').classList.toggle('ant-checkbox-wrapper-checked', box.checked);
        }

        // The column search box of a table header (ColumnHeader.cs): position: fixed under its icon, so the table's
        // scroll box doesn't clip it. With data-search-href it asks the server (htmx, using the host's hx-target and
        // hx-swap, or the ones it inherits); otherwise it raises m-col-search for the grid around it. With data-values
        // the box is a checkbox list (the Excel-style filter menu): the box narrows it and OK applies the values that
        // are checked and shown, as a value list.
        Alpine.data('columnSearch', function () {
            return {
                open: false,
                style: '',
                init: function () {
                    if (!this.$refs.values) return;
                    this._boxes = Array.prototype.slice.call(this.$refs.values.querySelectorAll('input[data-value]'));
                    this._current = this.$root.getAttribute('data-search') || '';
                    this.sync();
                },
                toggle: function () { if (this.open) this.close(); else this.show(); },
                show: function () {
                    if (this._boxes) { this.pick(this._current); this.$refs.input.value = ''; this.narrow(); }
                    var self = this, r = this.$refs.trigger.getBoundingClientRect(), width = 248;
                    var left = Math.max(8, Math.min(r.right - width, document.documentElement.clientWidth - width - 8));
                    this.style = 'position: fixed; top: ' + Math.round(r.bottom + 6) + 'px; left: ' + Math.round(left) + 'px; width: ' + width + 'px; z-index: 1050;';
                    this.open = true;
                    this._scroll = function (e) { if (!self.$root.contains(e.target)) self.close(); };
                    window.addEventListener('scroll', this._scroll, { capture: true, passive: true });
                    this.$nextTick(function () { self.$refs.input.focus(); self.$refs.input.select(); });
                },
                close: function () {
                    if (!this.open) return;
                    this.open = false;
                    window.removeEventListener('scroll', this._scroll, { capture: true });
                },
                destroy: function () { this.close(); },
                apply: function () {
                    if (!this._boxes) { this.submit(this.$refs.input.value.trim()); return; }
                    var text = function (b) { return b.value; }, shown = this.shown();
                    var picked = shown.filter(function (b) { return b.checked; });
                    if (!picked.length) return;
                    var all = this._boxes.map(text), on = picked.map(text);
                    var off = all.filter(function (v) { return on.indexOf(v) < 0; });
                    var label = function (vs) { return vs.map(function (v) { return v || '(Blanks)'; }).join(', '); };
                    var q = valueQuery(on, all);
                    this.submit(q, q.indexOf('!=') === 0 ? 'Not ' + label(off) : label(on));
                },
                reset: function () { this.$refs.input.value = ''; this.submit(''); },
                // The checkbox list: the boxes not hidden by the search box, which narrows the list by its text.
                shown: function () { return this._boxes.filter(function (b) { return !b.closest('label').hidden; }); },
                narrow: function () {
                    var t = this.$refs.input.value.trim().toLowerCase(), count = 0;
                    this._boxes.forEach(function (b) {
                        var hide = !!t && b.getAttribute('data-text').toLowerCase().indexOf(t) < 0;
                        b.closest('label').hidden = hide;
                        if (!hide) count++;
                    });
                    this.$refs.none.hidden = count > 0;
                    this.sync();
                },
                selectAll: function (on) { this.shown().forEach(function (b) { b.checked = on; }); this.sync(); },
                // The boxes' classes, Select all (checked, or part-checked, for the values shown) and OK (off with none).
                sync: function () {
                    this._boxes.forEach(markCheck);
                    var shown = this.shown(), n = shown.filter(function (b) { return b.checked; }).length, all = this.$refs.all;
                    all.checked = shown.length > 0 && n === shown.length;
                    all.indeterminate = n > 0 && n < shown.length;
                    markCheck(all);
                    all.closest('.ant-checkbox').classList.toggle('ant-checkbox-indeterminate', all.indeterminate);
                    this.$refs.ok.disabled = n === 0;
                },
                // ColumnSearch.Picked: check the values a search keeps (all for none, or a typed one).
                pick: function (q) {
                    q = String(q || '').trim();
                    var list = valueList(q) || (q.length > 1 && q[0] === '=' ? { exclude: false, values: [q.slice(1).trim().toLowerCase()] } : null);
                    this._boxes.forEach(function (b) { b.checked = !list || (list.values.indexOf(b.value.trim().toLowerCase()) >= 0) !== list.exclude; });
                    this.sync();
                },
                // The box in the filter row (Enter, or leaving it): the same search as the popup's.
                applyInline: function () {
                    var value = this.$refs.inline.value.trim();
                    var current = this._boxes ? this._current : this.$refs.trigger.classList.contains('active') ? this.$refs.input.value.trim() : '';
                    if (value === current) return;
                    if (!this._boxes) this.$refs.input.value = value;
                    this.submit(value);
                },
                submit: function (value, label) {
                    if (this._boxes) { this._current = value; this.pick(value); }
                    var host = this.$root, key = host.getAttribute('data-key'), href = host.getAttribute('data-search-href');
                    this.close();
                    if (this.$refs.inline) {
                        var inline = this.$refs.inline;
                        // A select (option columns) shows a query typed in the popup as an extra choice.
                        if (inline.tagName === 'SELECT' && value && !Array.prototype.some.call(inline.options, function (o) { return o.value === value; }))
                            inline.add(new Option(label || value, value));
                        inline.value = value;
                        this.$refs.inline.classList.toggle('m-th-filter-active', !!value);
                    }
                    if (href) {
                        var url = value ? href + (href.indexOf('?') < 0 ? '?' : '&') + 's.' + encodeURIComponent(key) + '=' + encodeURIComponent(value) : href;
                        if (window.htmx) window.htmx.ajax('GET', url, { source: host }); else window.location.href = url;
                        return;
                    }
                    this.$refs.trigger.classList.toggle('active', !!value);
                    host.dispatchEvent(new CustomEvent('m-col-search', { bubbles: true, detail: { key: key, value: value } }));
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
                return '<span class="ant-tag' + color + '">' + (o && o[2] ? o[2] : '') + esc(o ? o[0] : v) + '</span>';
            }
            // GridSchema.Initials and GridSchema.Tint: the same avatar as the server renders.
            function initials(text) {
                return String(text || '').split(' ').filter(function (w) { return w && /[\p{L}\p{N}]/u.test(w[0]); })
                    .slice(0, 2).map(function (w) { return w[0].toUpperCase(); }).join('');
            }
            function tint(text) {
                var h = 17;
                for (var i = 0; i < String(text).length; i++) h = (Math.imul(h, 31) + String(text).charCodeAt(i)) >>> 0;
                return h % 8 + 1;
            }
            function flag(v) { return String(v).toLowerCase() === 'true' || v === 'on'; }
            // A row's current value of a column, read from its posted fields (the editor, or a hidden input).
            function rowValue(tr, key) {
                var fields = tr.querySelectorAll('[name="' + cfg.name + '[' + tr.getAttribute('data-row-key') + '].' + key + '"]');
                if (!fields.length) return null;
                if (fields[0].type === 'checkbox') {
                    return Array.prototype.filter.call(fields, function (f) { return f.checked; }).map(function (f) { return f.value; }).join(',');
                }
                return fields[fields.length - 1].value;
            }
            function csvValue(col, v) {
                switch (col.type) {
                    case 'Select': case 'Tag': return col.o[v] ? col.o[v][0] : (v || '');
                    case 'MultiSelect': return many(v).map(function (x) { return col.o[x] ? col.o[x][0] : x; }).join('; ');
                    case 'Switch': return flag(v) ? 'Yes' : 'No';
                    default: return v == null ? '' : String(v);
                }
            }
            function csvField(s) { return /[",\r\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s; }
            // ColumnSearch.Matches: contains, =exact, !exclude; numbers and dates also >, >=, <, <=, = and a..b ranges.
            function kind(col) {
                return /^(Number|Currency|Percent|Progress|Rating)$/.test(col.type) ? 'Number' : col.type === 'Date' ? 'Date' : 'Text';
            }
            function parseNum(s) { var d = String(s).replace(/[^0-9.\-]/g, ''); if (!d) return null; var n = Number(d); return isFinite(n) ? n : null; }
            function parseDate(s) { return /^\d{4}-\d{2}-\d{2}$/.test(s) ? s : null; }
            function compare(q, v, parse) {
                var at = q.indexOf('..');
                if (at >= 0) {
                    var l = q.slice(0, at).trim(), r = q.slice(at + 2).trim(), from = l ? parse(l) : null, to = r ? parse(r) : null;
                    if ((l && from === null) || (r && to === null) || (from === null && to === null)) return null;
                    return v !== null && (from === null || v >= from) && (to === null || v <= to);
                }
                var op = /^(>=|<=|>|<|=)/.exec(q);
                if (!op) return null;
                var x = parse(q.slice(op[1].length).trim());
                if (x === null) return null;
                if (v === null) return false;
                switch (op[1]) { case '>': return v > x; case '>=': return v >= x; case '<': return v < x; case '<=': return v <= x; default: return v === x; }
            }
            function columnMatch(q, text, k, raw) {
                q = String(q || '').trim();
                if (!q) return true;
                var list = valueList(q);
                if (list) return (list.values.indexOf(String(text || '').trim().toLowerCase()) >= 0) !== list.exclude;
                var result = k === 'Number' ? compare(q, num(raw), parseNum) : k === 'Date' ? compare(q, parseDate(raw || ''), parseDate) : null;
                if (result !== null) return result;
                var t = String(text || '').toLowerCase();
                if (q.length > 1 && q[0] === '=') return t.trim() === q.slice(1).trim().toLowerCase();
                if (q.length > 1 && q[0] === '!') return t.indexOf(q.slice(1).trim().toLowerCase()) < 0;
                return t.indexOf(q.toLowerCase()) >= 0;
            }
            // GridSchema.Sort: numbers and dates by value, the rest by displayed text; empty cells last either way.
            function sortValue(col, raw) {
                if (kind(col) === 'Number') return num(raw);
                if (col.type === 'Date') return parseDate(raw || '');
                if (col.type === 'Switch') return flag(raw) ? 1 : 0;
                return raw === null || raw === '' ? null : String(format(col, raw));
            }
            function order(x, y, desc) {
                if (x === null && y === null) return 0;
                if (x === null) return 1;
                if (y === null) return -1;
                var c = typeof x === 'string' ? x.localeCompare(y, cfg.lang, { sensitivity: 'accent' }) : x < y ? -1 : x > y ? 1 : 0;
                return desc ? -c : c;
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
            function view(col, v, tr) {
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
                if (text === '') return '<span class="m-grid-empty">Add</span>';
                var icon = col.type === 'Select' && col.o[v] && col.o[v][2] ? col.o[v][2] : '';
                if (col.avatar || col.sub) {
                    var sub = col.sub && tr ? rowValue(tr, col.sub) : null;
                    return '<span class="m-grid-entity">' +
                        (col.avatar ? '<span class="m-avatar m-grid-avatar m-av-' + tint(text) + '" aria-hidden="true">' + esc(initials(text)) + '</span>' : '') +
                        '<span class="m-grid-entity-text"><span class="m-grid-entity-title">' + icon + esc(text) + '</span>' +
                        (sub ? '<span class="m-grid-entity-desc">' + esc(sub) + '</span>' : '') + '</span></span>';
                }
                if (icon) return '<span class="m-grid-icon-text">' + icon + esc(text) + '</span>';
                return '<span' + (col.right ? ' class="m-num"' : '') + '>' + esc(text) + '</span>';
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
            // Select, Tag and Multi-select cells edit with a creatable select (modulusCreatableSelect); its state is Alpine's.
            function picker(editor) {
                var el = editor.querySelector('.m-cselect');
                return el && window.Alpine ? window.Alpine.$data(el) : null;
            }
            // Date cells edit with Ant Design's picker (modulusPicker); for a date its text is the posted yyyy-MM-dd.
            function datePicker(editor) {
                var el = editor.querySelector('.m-picker');
                return el && window.Alpine ? window.Alpine.$data(el) : null;
            }
            function read(editor, col) {
                var p = picker(editor);
                if (p) return col.type === 'MultiSelect' ? p.values.join(',') : String(p.value || '').trim();
                var d = datePicker(editor);
                if (d) return String(d.texts[0] || '').trim();
                if (col.type === 'MultiSelect') {
                    return Array.prototype.filter.call(editor.querySelectorAll('input[type=checkbox]'), function (i) { return i.checked; })
                        .map(function (i) { return i.value; }).join(',');
                }
                var control = editor.querySelector('input, select');
                return control ? control.value.trim() : '';
            }
            function snapshot(editor) {
                var p = picker(editor);
                if (p) return [p.value, p.values.slice()];
                var d = datePicker(editor);
                if (d) return [d.values[0] ? d.values[0].getTime() : null, d.texts[0] || ''];
                return Array.prototype.map.call(editor.querySelectorAll('input, select'), function (i) { return i.type === 'checkbox' ? i.checked : i.value; });
            }
            function syncCheckbox(input) {
                var wrapper = input.closest('.ant-checkbox-wrapper');
                if (!wrapper) return;
                wrapper.classList.toggle('ant-checkbox-wrapper-checked', input.checked);
                wrapper.querySelector('.ant-checkbox').classList.toggle('ant-checkbox-checked', input.checked);
            }
            function restore(editor, values) {
                var p = picker(editor);
                if (p) { p.value = values[0]; p.values = values[1].slice(); p.open = false; p.creating = false; return; }
                var d = datePicker(editor);
                if (d) {
                    d.open = false;
                    d.values = [values[0] === null ? null : new Date(values[0])];
                    d.texts = [values[1]];
                    var hidden = editor.querySelector('[data-m-picker-value]');
                    if (hidden) hidden.value = values[1];
                    return;
                }
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
                keyword: '',
                filters: {},
                searches: {},
                sortKey: '',
                sortDesc: false,
                selected: 0,
                searchOpen: false,
                // Paging (Paging in Ui.Abstractions): the rows after search and filters, a page of them at a time.
                page: 1,
                pageSize: cfg.pageSize || 10,
                matched: 0,
                // Rows changed since the page loaded (by id, marked in the grid) and stored rows deleted: "3 unsaved changes".
                changed: {},
                deleted: 0,
                _editor: null,
                _original: null,

                init: function () {
                    var self = this;
                    // The server renders the default density so the first paint is right; Alpine owns it from here.
                    this.$refs.table.classList.remove('ant-table-middle');
                    // The order the rows came in, for "unsorted".
                    this.rows().forEach(function (tr, i) { tr.setAttribute('data-order', i); });
                    // A row dragged to a new place (modulus-plugins.js moved it): that order is the new "unsorted" one,
                    // and the row counts as changed. A nested grid's own moves bubble past and are ignored.
                    this.$root.addEventListener('modulus:sort', function (e) {
                        if (e.target !== self.$refs.body) return;
                        var moved = null;
                        self.rows().forEach(function (tr, i) {
                            tr.setAttribute('data-order', i);
                            if (tr.getAttribute('data-row-key') === e.detail.id) moved = tr;
                        });
                        if (moved) self.touch(moved);
                    });
                    // Search, filters, column searches and the sort survive the grid being rendered again after a post
                    // (kept per grid for the tab).
                    try {
                        var saved = JSON.parse(sessionStorage.getItem(this.storeKey()) || 'null');
                        if (saved) {
                            this.keyword = saved.keyword || '';
                            this.filters = saved.filters || {};
                            this.searches = saved.searches || {};
                            this.sortKey = saved.sortKey || '';
                            this.sortDesc = !!saved.sortDesc;
                            if (saved.pageSize > 0) this.pageSize = saved.pageSize;
                            this.$root.querySelectorAll('select[data-filter]').forEach(function (s) { s.value = self.filters[s.getAttribute('data-filter')] || ''; });
                            this.heads().forEach(function (th) {
                                var value = self.searches[th.getAttribute('data-key')] || '';
                                var input = th.querySelector('.m-col-search input'), trigger = th.querySelector('.m-th-search'), inline = th.querySelector('.m-th-filter');
                                if (input) input.value = value;
                                if (trigger) trigger.classList.toggle('active', !!value);
                                if (inline) { inline.value = value; inline.classList.toggle('m-th-filter-active', !!value); }
                            });
                            if (window.modulusShell && window.modulusShell.syncFilterRow) window.modulusShell.syncFilterRow(this.$root);
                            this.$nextTick(function () { self.applySort(); self.applyFilter(); });
                        }
                    } catch (e) { /* storage blocked */ }
                    // The first page (the server renders every row; the browser pages them).
                    this.$nextTick(function () { self.applyPage(); });
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
                // ---- Toolbar: the search box folds into its icon when empty; the unsaved-changes count ----
                searchShown: function () { return this.searchOpen || !!this.keyword; },
                openSearch: function () {
                    var self = this;
                    this.searchOpen = true;
                    this.$nextTick(function () { if (self.$refs.search) self.$refs.search.focus(); });
                },
                changesText: function () {
                    var n = Object.keys(this.changed).length + this.deleted;
                    return n === 0 ? 'Unsaved changes' : n === 1 ? '1 unsaved change' : n + ' unsaved changes';
                },
                // A change inside this grid (a nested grid's own changes bubble past it and are ignored).
                touch: function (el) {
                    if (!el || el.closest('.m-grid') !== this.$root) return;
                    this.dirty = true;
                    var tr = el.closest('tr[data-row-key]');
                    if (!tr || tr.parentElement !== this.$refs.body) return;
                    this.changed[tr.getAttribute('data-row-key')] = true;
                    if (tr.classList.contains('m-grid-row-new')) return;
                    tr.classList.add('m-grid-row-dirty');
                    // The changed cell itself (tinted, with a corner mark) as the Blazor grid's m-grid-cell-dirty.
                    var td = el.closest('td');
                    if (td && td.parentElement === tr && td.hasAttribute('data-key')) {
                        td.classList.add('m-grid-td-dirty');
                        var cell = td.querySelector('.m-grid-cell-editable');
                        var label = cell && cell.getAttribute('aria-label');
                        if (label && !/ \(changed\)$/.test(label)) cell.setAttribute('aria-label', label + ' (changed)');
                    }
                },
                forget: function (tr) {
                    var id = tr.getAttribute('data-row-key');
                    delete this.changed[id];
                    if (!tr.classList.contains('m-grid-row-new')) this.deleted++;
                    this.dirty = true;
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
                    var p = picker(editor);
                    if (p) {
                        // Opens at once, as the Blazor grid's AppCreatableSelect (DefaultOpen).
                        editor.querySelector('.ant-select-selector').focus({ preventScroll: true });
                        p.show();
                        return;
                    }
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
                    // A click in an open select's list moves focus nowhere; the select ends the edit when it closes.
                    var p = picker(editor);
                    if (p && p.open) return;
                    this.commit(editor, false);
                },
                commit: function (editor, focusCell) {
                    if (!editor || editor.hidden) return;
                    var cell = editor.previousElementSibling, col = this.column(editor);
                    var value = read(editor, col);
                    var changed = JSON.stringify(snapshot(editor)) !== JSON.stringify(this._original);
                    if (col) {
                        cell.innerHTML = view(col, value, editor.closest('tr'));
                        cell.setAttribute('aria-label', 'Edit ' + col.t + ': ' + format(col, value));
                        this.setError(editor.closest('td'), cell, validate(col, value));
                    }
                    editor.hidden = true;
                    cell.hidden = false;
                    if (this._editor === editor) { this._editor = null; this._original = null; }
                    if (changed) this.touch(editor);
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
                    var row = this.$refs.body.firstElementChild, self = this;
                    this.touch(row);
                    // The new row is on top: show the first page.
                    this.page = 1;
                    this.applyPage();
                    this.$nextTick(function () {
                        var first = row.querySelector('.m-grid-cell-editable');
                        if (first) self.edit(first);
                    });
                },
                deleteRow: function (button) {
                    var self = this, row = button.closest('tr');
                    var remove = function () {
                        self.removeRow(row);
                        self.syncSelection();
                    };
                    var ask = window.Alpine.store('confirm');
                    if (ask && ask.ask) ask.ask('Delete this row?', 'Delete', true).then(function (ok) { if (ok) remove(); });
                    else if (window.confirm('Delete this row?')) remove();
                },
                removeRow: function (row) {
                    var id = row.getAttribute('data-row-key');
                    var detail = this.$refs.body.querySelector('tr[data-detail-for="' + CSS.escape(id) + '"]');
                    if (detail) detail.remove();
                    this.forget(row);
                    row.remove();
                    this.applyPage();
                },
                // Only this grid's own rows: a nested grid's child levels sit inside its detail rows.
                rows: function () { return Array.prototype.filter.call(this.$refs.body.children, function (tr) { return tr.hasAttribute('data-row-key'); }); },

                // ---- Search, filters and column searches (GridSchema.Matches): rows that don't match are hidden, not removed, so they still post ----
                storeKey: function () { return 'modulus-grid:' + location.pathname + ':' + this.$root.id; },
                store: function () {
                    try {
                        sessionStorage.setItem(this.storeKey(), JSON.stringify({ keyword: this.keyword, filters: this.filters, searches: this.searches, sortKey: this.sortKey, sortDesc: this.sortDesc, pageSize: this.pageSize }));
                    } catch (e) { /* storage blocked */ }
                },
                // This grid's header cells (a nested grid's own headers sit further down, inside detail rows).
                heads: function () { return Array.prototype.slice.call(this.$refs.table.querySelector('thead').querySelectorAll('th[data-key]')); },
                filtered: function () {
                    var f = this.filters, s = this.searches;
                    return Object.keys(f).some(function (k) { return !!f[k]; }) || Object.keys(s).some(function (k) { return !!s[k]; });
                },
                setFilter: function (key, value) { this.filters[key] = value; this.applyFilter(); },
                // From a header's column search box (m-col-search).
                setSearch: function (detail) {
                    if (!detail || !detail.key) return;
                    this.searches[detail.key] = detail.value || '';
                    this.applyFilter();
                },
                // The toolbar's Clear: the keyword, the filters and the column searches.
                anyFilter: function () { return this.filtered() || !!String(this.keyword || '').trim(); },
                clearFilters: function () {
                    this.filters = {};
                    this.searches = {};
                    this.keyword = '';
                    this.searchOpen = false;
                    this.$root.querySelectorAll('select[data-filter]').forEach(function (s) { s.value = ''; });
                    this.heads().forEach(function (th) {
                        var input = th.querySelector('.m-col-search input'), trigger = th.querySelector('.m-th-search'), inline = th.querySelector('.m-th-filter');
                        if (input) input.value = '';
                        if (trigger) trigger.classList.remove('active');
                        if (inline) { inline.value = ''; inline.classList.remove('m-th-filter-active'); }
                    });
                    this.applyFilter();
                },
                matches: function (tr, k) {
                    var f = this.filters, s = this.searches;
                    for (var key in f) {
                        if (!f[key] || !cfg.columns[key]) continue;
                        var col = cfg.columns[key], raw = rowValue(tr, key);
                        var ok = col.type === 'MultiSelect' ? many(raw).indexOf(f[key]) >= 0 : col.type === 'Switch' ? flag(raw) === flag(f[key]) : raw === f[key];
                        if (!ok) return false;
                    }
                    for (var sk in s) {
                        // A hidden column's search is ignored, as on the server.
                        if (!s[sk] || !cfg.columns[sk] || !tr.querySelector(':scope > td[data-key="' + CSS.escape(sk) + '"]')) continue;
                        var sc = cfg.columns[sk], value = rowValue(tr, sk), text = format(sc, value), sub = sc.sub ? rowValue(tr, sc.sub) : '';
                        // With a subtitle, as GridSchema.ColumnMatches: contains and =exact match the cell or the cell
                        // with its subtitle; !exclude needs neither to contain it.
                        // A value list compares the cell alone.
                        var ok = !sub || valueList(s[sk]) ? columnMatch(s[sk], text, kind(sc), value)
                            : /^\s*!/.test(s[sk]) ? columnMatch(s[sk], text + ' ' + sub, kind(sc), value)
                            : columnMatch(s[sk], text, kind(sc), value) || columnMatch(s[sk], text + ' ' + sub, kind(sc), value);
                        if (!ok) return false;
                    }
                    if (!k) return true;
                    return Array.prototype.some.call(tr.querySelectorAll('td[data-key]'), function (td) {
                        var key = td.getAttribute('data-key'), col = cfg.columns[key];
                        if (!col) return false;
                        if (format(col, rowValue(tr, key)).toLowerCase().indexOf(k) >= 0) return true;
                        return !!col.sub && String(rowValue(tr, col.sub) || '').toLowerCase().indexOf(k) >= 0;
                    });
                },
                applyFilter: function () {
                    var self = this, k = (this.keyword || '').trim().toLowerCase(), shown = 0;
                    this.rows().forEach(function (tr) {
                        // Rows added in the browser stay visible, so a new row never vanishes while it is filled in.
                        var show = tr.classList.contains('m-grid-row-new') || self.matches(tr, k);
                        tr._mMatch = show;
                        if (show) shown++;
                    });
                    // A new search or filter starts again from the first page.
                    this.page = 1;
                    this.applyPage();
                    var none = this.$refs.body.querySelector(':scope > tr.m-grid-no-match');
                    if (!shown && this.rows().length) {
                        if (!none) {
                            var cols = this.$refs.table.querySelector('colgroup').children.length;
                            this.$refs.body.insertAdjacentHTML('beforeend', '<tr class="ant-table-placeholder m-grid-no-match"><td class="ant-table-cell" colspan="' + cols + '"><div class="m-table-empty">No rows match the search or filters.</div></td></tr>');
                        }
                    } else if (none) none.remove();
                    this.syncSelection();
                    this.store();
                },

                // ---- Sorting (a header click: ascending, descending, then the original order); rows added here stay on top ----
                sortBy: function (key) {
                    if (this.sortKey !== key) { this.sortKey = key; this.sortDesc = false; }
                    else if (!this.sortDesc) this.sortDesc = true;
                    else { this.sortKey = ''; this.sortDesc = false; }
                    this.applySort();
                    this.applyPage();
                    this.store();
                },

                // ---- Paging: the matching rows (in their current order), one page shown; rows off the page are hidden, not removed ----
                pages: function () { return cfg.paged === false ? 1 : Math.max(1, Math.ceil(this.matched / this.pageSize)); },
                applyPage: function () {
                    var size = cfg.paged === false ? Infinity : this.pageSize;
                    var list = this.rows().filter(function (tr) { return tr._mMatch !== false; });
                    this.matched = list.length;
                    this.page = Math.min(Math.max(1, this.page), this.pages());
                    var from = (this.page - 1) * (size === Infinity ? 0 : size);
                    var onPage = new Set(list.slice(from, from + size));
                    this.rows().forEach(function (tr) {
                        var show = onPage.has(tr);
                        tr.hidden = !show;
                        var detail = tr.nextElementSibling;
                        if (detail && detail.getAttribute('data-detail-for') === tr.getAttribute('data-row-key')) detail.hidden = !show;
                    });
                    this.syncSelection();
                },
                goPage: function (n) {
                    var target = Math.min(Math.max(1, n), this.pages());
                    if (target === this.page) return;
                    this.page = target;
                    this.applyPage();
                },
                setPageSize: function (value) {
                    var size = parseInt(value, 10);
                    if (!(size > 0)) return;
                    // Keep the first row of the current page in view.
                    var first = (this.page - 1) * this.pageSize;
                    this.pageSize = size;
                    this.page = Math.floor(first / size) + 1;
                    this.applyPage();
                    this.store();
                },
                // "1–10 of 42", as Paging.Range writes it.
                rangeText: function () {
                    var total = this.matched, lang = cfg.lang || undefined;
                    if (!total) return '0 of 0';
                    var size = cfg.paged === false ? total : this.pageSize;
                    var from = (this.page - 1) * size + 1, to = Math.min(total, this.page * size);
                    function n(v) { return v.toLocaleString(lang); }
                    return n(from) + '\u2013' + n(to) + ' of ' + n(total);
                },
                // The page buttons, as Paging.Window picks them: every page up to 7, else the first and last pages, two
                // either side of the current one (five at the ends) and ••• jumps of five pages.
                pageItems: function () {
                    var pages = this.pages(), cur = this.page, nums = [];
                    if (pages <= 7) { for (var i = 1; i <= pages; i++) nums.push(i); }
                    else {
                        var left = Math.max(1, cur - 2), right = Math.min(pages, cur + 2);
                        if (cur - 1 <= 2) right = 5;
                        if (pages - cur <= 2) left = pages - 4;
                        if (left !== 1) { nums.push(1); if (left > 2) nums.push(-1); }
                        for (var j = left; j <= right; j++) nums.push(j);
                        if (right !== pages) { if (right < pages - 1) nums.push(-2); nums.push(pages); }
                    }
                    return nums.map(function (p) {
                        if (p === -1) return { key: 'jump-prev', page: 0, to: Math.max(1, cur - 5), active: false, title: 'Previous 5 pages', cls: 'ant-pagination-jump-prev ant-pagination-jump-prev-custom-icon' };
                        if (p === -2) return { key: 'jump-next', page: 0, to: Math.min(pages, cur + 5), active: false, title: 'Next 5 pages', cls: 'ant-pagination-jump-next ant-pagination-jump-next-custom-icon' };
                        return { key: 'p' + p, page: p, to: p, active: p === cur, title: 'Page ' + p, cls: 'ant-pagination-item ant-pagination-item-' + p + (p === cur ? ' ant-pagination-item-active' : '') };
                    });
                },
                applySort: function () {
                    var self = this, body = this.$refs.body, key = this.sortKey, col = key ? cfg.columns[key] : null;
                    if (key && !col) this.sortKey = key = '';
                    // Each row moves with its detail row.
                    var pairs = this.rows().map(function (tr) {
                        var detail = tr.nextElementSibling;
                        return { tr: tr, detail: detail && detail.getAttribute('data-detail-for') === tr.getAttribute('data-row-key') ? detail : null, value: col ? sortValue(col, rowValue(tr, key)) : null };
                    });
                    pairs.sort(function (a, b) {
                        var na = a.tr.classList.contains('m-grid-row-new'), nb = b.tr.classList.contains('m-grid-row-new');
                        if (na !== nb) return na ? -1 : 1;
                        return (col ? order(a.value, b.value, self.sortDesc) : 0) || (+(a.tr.getAttribute('data-order') || 0) - +(b.tr.getAttribute('data-order') || 0));
                    });
                    var none = body.querySelector(':scope > tr.m-grid-no-match');
                    pairs.forEach(function (p) { body.appendChild(p.tr); if (p.detail) body.appendChild(p.detail); });
                    if (none) body.appendChild(none);
                    // The headers' carets and aria-sort, and the sorted column's tint, as the server renders them.
                    this.heads().forEach(function (th) {
                        var on = th.getAttribute('data-key') === key, sorters = th.querySelector('.m-th-sort');
                        if (!sorters) return;
                        th.classList.toggle('ant-table-column-sort', on);
                        th.setAttribute('aria-sort', on ? (self.sortDesc ? 'descending' : 'ascending') : 'none');
                        th.querySelector('.ant-table-column-sorter-up').classList.toggle('active', on && !self.sortDesc);
                        th.querySelector('.ant-table-column-sorter-down').classList.toggle('active', on && self.sortDesc);
                        var title = th.querySelector('.ant-table-column-title').textContent.replace('*', '');
                        sorters.setAttribute('aria-label', 'Sort by ' + title + (on ? (self.sortDesc ? ' (descending)' : ' (ascending)') : ''));
                    });
                    // A column sort decides the order, so the drag handles rest until it is cleared.
                    var locked = !!key || !!cfg.dragLocked;
                    if (this.$root.hasAttribute('data-m-sort-rows')) this.$root.toggleAttribute('data-m-sort-disabled', locked);
                    this.rows().forEach(function (tr) {
                        Array.prototype.forEach.call(tr.children, function (td) {
                            if (td.hasAttribute('data-key')) td.classList.toggle('ant-table-column-sort', td.getAttribute('data-key') === key);
                            var handle = td.classList.contains('m-row-drag-cell') && td.querySelector('[data-m-sort-row]');
                            if (!handle) return;
                            if (locked) { handle.setAttribute('aria-disabled', 'true'); handle.tabIndex = -1; handle.title = 'Clear the column sort to drag rows'; }
                            else { handle.removeAttribute('aria-disabled'); handle.tabIndex = 0; handle.title = 'Drag to reorder'; }
                        });
                    });
                },

                // ---- Selection and the batch bar ----
                boxes: function () { return this.rows().map(function (tr) { return tr.querySelector('input[data-select-row]'); }).filter(Boolean); },
                selectedRows: function () { return this.boxes().filter(function (b) { return b.checked; }).map(function (b) { return b.closest('tr'); }); },
                syncSelection: function () {
                    var boxes = this.boxes(), visible = boxes.filter(function (b) { return !b.closest('tr').hidden; });
                    this.selected = boxes.filter(function (b) { return b.checked; }).length;
                    var all = this.$root.querySelector('input[data-select-all]');
                    if (all) {
                        var on = visible.length > 0 && visible.every(function (b) { return b.checked; });
                        var some = !on && visible.some(function (b) { return b.checked; });
                        all.checked = on;
                        syncCheckbox(all);
                        all.closest('.ant-checkbox').classList.toggle('ant-checkbox-indeterminate', some);
                    }
                },
                selectRow: function (box) {
                    syncCheckbox(box);
                    box.closest('tr').classList.toggle('ant-table-row-selected', box.checked);
                    this.syncSelection();
                },
                // Select all checks the rows the search and filters show; clearing unchecks every row.
                selectAll: function (on) {
                    this.boxes().forEach(function (b) {
                        var tr = b.closest('tr');
                        b.checked = on && !tr.hidden ? true : on ? b.checked : false;
                        syncCheckbox(b);
                        tr.classList.toggle('ant-table-row-selected', b.checked);
                    });
                    this.syncSelection();
                },
                deleteSelected: function () {
                    var self = this, rows = this.selectedRows();
                    if (!rows.length) return;
                    var remove = function () { rows.forEach(function (r) { self.removeRow(r); }); self.syncSelection(); };
                    var text = 'Delete ' + rows.length + ' ' + (rows.length === 1 ? 'row' : 'rows') + '?';
                    var ask = window.Alpine.store('confirm');
                    if (ask && ask.ask) ask.ask(text, 'Delete', true).then(function (ok) { if (ok) remove(); });
                    else if (window.confirm(text)) remove();
                },
                // "Set value": puts one option (or Yes / No) into a column of every selected row.
                setSelected: function (select) {
                    var choice = select.value, at = choice.indexOf('='), self = this;
                    select.value = '';
                    if (at <= 0) return;
                    var key = choice.slice(0, at), value = choice.slice(at + 1), col = cfg.columns[key];
                    if (!col) return;
                    this.selectedRows().forEach(function (tr) {
                        var td = tr.querySelector('td[data-key="' + CSS.escape(key) + '"]');
                        if (col.type === 'Switch') {
                            var sw = tr.querySelector('td[data-key="' + CSS.escape(key) + '"] [role=switch]') || null;
                            if (sw && window.Alpine.$data(sw)) window.Alpine.$data(sw).on = flag(value);
                            else {
                                var hidden = tr.querySelector('[name="' + cfg.name + '[' + tr.getAttribute('data-row-key') + '].' + key + '"]');
                                if (hidden) hidden.value = flag(value) ? 'true' : 'false';
                            }
                            return;
                        }
                        var editor = td && td.querySelector('.m-grid-cell-editing');
                        var control = editor ? editor.querySelector('select') : tr.querySelector('[name="' + cfg.name + '[' + tr.getAttribute('data-row-key') + '].' + key + '"]');
                        if (!control) return;
                        control.value = value;
                        if (editor) {
                            var cell = editor.previousElementSibling;
                            cell.innerHTML = view(col, value, tr);
                            cell.setAttribute('aria-label', 'Edit ' + col.t + ': ' + format(col, value));
                            self.setError(td, cell, validate(col, value));
                        }
                        self.touch(tr);
                    });
                    this.dirty = true;
                },

                // ---- Export: the visible columns of the rows shown (or selected), as GridSchema.Csv writes them ----
                exportCsv: function (onlySelected) {
                    var keys = Array.prototype.map.call(this.$refs.table.querySelector('thead').querySelectorAll('th[data-key]'), function (th) { return th.getAttribute('data-key'); })
                        .filter(function (k) { return cfg.columns[k]; });
                    var rows = onlySelected ? this.selectedRows() : this.rows().filter(function (tr) { return !tr.hidden; });
                    var lines = [keys.map(function (k) { return csvField(cfg.columns[k].t); }).join(',')];
                    rows.forEach(function (tr) {
                        lines.push(keys.map(function (k) { return csvField(csvValue(cfg.columns[k], rowValue(tr, k))); }).join(','));
                    });
                    window.modulusShell.download(cfg.file, lines.join('\r\n') + '\r\n');
                },

                // ---- Resizing: the new width goes into the posted schema, so Save keeps it ----
                resized: function (detail) {
                    var input = this.$refs.schema;
                    if (!input || !detail || !detail.key) return;
                    try {
                        var columns = JSON.parse(input.value);
                        columns.forEach(function (c) { if (c.key === detail.key) c.width = Math.max(60, Math.min(600, detail.width)); });
                        input.value = JSON.stringify(columns);
                    } catch (e) { /* malformed schema: the server falls back to its own */ }
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
                creating: false,
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
                colorName: function (c) { return (cfg.colorNames && cfg.colorNames[c]) || c; },
                place: function () {
                    var root = this.$root;
                    this.$nextTick(function () { if (window.modulusShell && window.modulusShell.placeSelect) window.modulusShell.placeSelect(root); });
                },
                // The create form takes the list's place; Cancel (or Escape) goes back to the list.
                startCreate: function () {
                    var self = this;
                    this.creating = true;
                    this.error = '';
                    this.place();
                    this.$nextTick(function () { if (self.$refs.add) self.$refs.add.focus(); });
                },
                cancelCreate: function () {
                    this.creating = false;
                    this.newLabel = '';
                    this.picked = null;
                    this.error = '';
                    this.place();
                    this.$root.querySelector('.ant-select-selector').focus({ preventScroll: true });
                },
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
                    this.creating = false;
                    this.newLabel = '';
                    this.picked = null;
                    this.error = '';
                    this.$root.querySelector('.ant-select-selector').focus({ preventScroll: true });
                    // The editable grid ends the cell's edit when its select closes (a pick, Escape, Tab or a click away).
                    this.$root.dispatchEvent(new CustomEvent('m-cselect-close', { bubbles: true }));
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
                    // Keys the select handles stop here, so a form (or a grid cell's editor) doesn't act on them too.
                    if (!this.open) {
                        if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'Enter' || e.key === ' ') { e.preventDefault(); e.stopPropagation(); this.show(); }
                        return;
                    }
                    if (e.key === 'ArrowDown' && n) { e.preventDefault(); this.active = (this.active + 1) % n; }
                    else if (e.key === 'ArrowUp' && n) { e.preventDefault(); this.active = (this.active - 1 + n) % n; }
                    else if ((e.key === 'Enter' || e.key === ' ') && this.active >= 0 && this.active < n) { e.preventDefault(); e.stopPropagation(); this.pick(this.options[this.active].value); }
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
                    this.creating = false;
                    this.place();
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
                    // In an editable grid the name and colour post with the grid (add-option:{key}:{row}), so the
                    // option joins the column's schema; the grid's editor hears m-cselect-add and submits the operation.
                    if (cfg.gridOp) {
                        this.$root.dispatchEvent(new CustomEvent('m-cselect-add', { bubbles: true, detail: cfg.gridOp }));
                        return;
                    }
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

        // ---- Business fields (BusinessFieldTagHelpers.cs) ----
        // The typed text is formatted with the rules of Modulus.Ui.Abstractions.Forms (modulusShell.fields) and the
        // hidden input posts the invariant value, so the model binds "1234.5" whatever the page shows.
        function fx() { return window.modulusShell.fields; }
        function changed(input) { if (input) input.dispatchEvent(new Event('change', { bubbles: true })); }

        // app-money-input, app-quantity-input. cfg: { value, decimals, fixed (explicit decimals), min, max,
        // digits: { code: decimals } for the currency or unit list, labels: { code: text }, unit }.
        Alpine.data('modulusNumberField', function (cfg) {
            return {
                value: cfg.value, unit: cfg.unit || '',
                get digits() {
                    if (cfg.fixed != null) return cfg.fixed;
                    return cfg.digits && Object.prototype.hasOwnProperty.call(cfg.digits, this.unit) ? cfg.digits[this.unit] : cfg.decimals;
                },
                get raw() { return fx().invariant(this.value); },
                get unitText() { return (cfg.labels && cfg.labels[this.unit]) || this.unit; },
                show: function () {
                    this.$refs.input.value = fx().formatNumber(this.value, this.digits);
                    var hidden = this.$refs.value;
                    this.$nextTick(function () { changed(hidden); });
                },
                commit: function () {
                    var t = this.$refs.input.value;
                    if (!t.trim()) this.value = null;
                    else {
                        var v = fx().parseNumber(t);
                        if (v != null) {
                            if (cfg.min != null && v < cfg.min) v = cfg.min;
                            if (cfg.max != null && v > cfg.max) v = cfg.max;
                            this.value = fx().round(v, this.digits);
                        }
                    }
                    this.show();
                },
                setUnit: function (code) {
                    this.unit = code;
                    if (this.value != null) this.value = fx().round(this.value, this.digits);
                    this.show();
                }
            };
        });

        // app-duration-input. cfg: { value (minutes), max }.
        Alpine.data('modulusDuration', function (cfg) {
            return {
                value: cfg.value,
                get raw() { return this.value == null ? '' : String(this.value); },
                commit: function () {
                    var t = this.$refs.input.value;
                    if (!t.trim()) this.value = null;
                    else {
                        var m = fx().parseDuration(t);
                        if (m != null) this.value = Math.min(m, cfg.max);
                    }
                    this.$refs.input.value = fx().formatDuration(this.value);
                    var hidden = this.$refs.value;
                    this.$nextTick(function () { changed(hidden); });
                }
            };
        });

        // app-number-range. cfg: { from, to, decimals }.
        Alpine.data('modulusNumberRange', function (cfg) {
            return {
                from: cfg.from, to: cfg.to,
                get invalid() { return this.from != null && this.to != null && this.from > this.to; },
                raw: function (v) { return fx().invariant(v); },
                commit: function (which) {
                    var input = this.$refs[which], t = input.value;
                    if (!t.trim()) this[which] = null;
                    else {
                        var v = fx().parseNumber(t);
                        if (v != null) this[which] = fx().round(v, cfg.decimals);
                    }
                    input.value = fx().formatNumber(this[which], cfg.decimals);
                    var hidden = this.$refs[which + 'Value'];
                    this.$nextTick(function () { changed(hidden); });
                }
            };
        });

        // app-phone-input: PhoneNumber.Split / Join. cfg: { country, national, home (default country), dials: { ISO code: calling code } }.
        Alpine.data('modulusPhone', function (cfg) {
            function clean(s) {
                var out = '';
                String(s || '').trim().split('').forEach(function (c) {
                    if (/[0-9().-]/.test(c)) out += c;
                    else if (/\s/.test(c) && out && out[out.length - 1] !== ' ') out += ' ';
                });
                return out.trim();
            }
            return {
                country: cfg.country, national: cfg.national,
                dial: function (code) { return (cfg.dials && cfg.dials[code]) || ''; },
                get prefix() { return this.country ? this.country + ' +' + this.dial(this.country) : '+'; },
                get raw() {
                    var n = clean(this.national);
                    if (!/[0-9]/.test(n)) return '';
                    return this.country ? '+' + this.dial(this.country) + ' ' + n : n;
                },
                // A number typed or pasted with "+": pick the country with the longest matching calling code,
                // preferring the current one, then the default one, for shared codes (+1, +7).
                split: function (s) {
                    var digits = s.replace(/[^0-9]/g, ''), codes = Object.keys(cfg.dials || {}), self = this;
                    for (var len = Math.min(4, digits.length); len >= 1; len--) {
                        var code = digits.slice(0, len);
                        var matches = codes.filter(function (c) { return cfg.dials[c] === code; });
                        if (!matches.length) continue;
                        this.country = matches.indexOf(self.country) >= 0 ? self.country : matches.indexOf(cfg.home) >= 0 ? cfg.home : matches[0];
                        var seen = 0;
                        for (var i = 0; i < s.length; i++) {
                            if (!/[0-9]/.test(s[i])) continue;
                            if (++seen === len) return clean(s.slice(i + 1));
                        }
                        return '';
                    }
                    return s.slice(1).trim();
                },
                commit: function () {
                    var t = this.$refs.input.value.trim();
                    this.national = t[0] === '+' ? this.split(t) : clean(t);
                    this.$refs.input.value = this.national;
                    this.sync();
                },
                sync: function () { var hidden = this.$refs.value; this.$nextTick(function () { changed(hidden); }); }
            };
        });

        // app-lookup: LookupSearch.Filter / Resolve for a list given with the page, or a JSON endpoint that takes
        // ?q=text&max=n and returns [{ value, label, description, fields }].
        // cfg: { value, label, items: [{ v, l, d, f }] | null, href, max, page, columns: [key, …] }.
        var SEP = String.fromCharCode(31);
        function lookupHay(i) {
            var f = i.f ? Object.keys(i.f).map(function (k) { return i.f[k]; }) : [];
            return [i.v, i.l, i.d || ''].concat(f).join(SEP).toLowerCase();
        }
        function lookupFilter(items, text, max) {
            var q = String(text || '').trim().toLowerCase();
            if (!q) return items.slice(0, max);
            var terms = q.split(/\s+/);
            return items
                .map(function (item, index) { return { item: item, index: index }; })
                .filter(function (x) { var h = lookupHay(x.item); return terms.every(function (t) { return h.indexOf(t) >= 0; }); })
                .map(function (x) {
                    x.rank = x.item.l.toLowerCase().indexOf(q) === 0 ? 0 : x.item.v.toLowerCase().indexOf(q) === 0 ? 1 : 2;
                    return x;
                })
                .sort(function (a, b) { return a.rank - b.rank || a.index - b.index; })
                .slice(0, max)
                .map(function (x) { return x.item; });
        }
        function lookupResolve(items, text) {
            var q = String(text || '').trim().toLowerCase();
            if (!q) return null;
            var exact = items.filter(function (i) { return i.l.toLowerCase() === q || i.v.toLowerCase() === q; })[0];
            if (exact) return exact;
            var m = lookupFilter(items, q, 2);
            return m.length === 1 ? m[0] : null;
        }
        Alpine.data('modulusLookup', function (cfg) {
            var timer = 0, seq = 0;
            return {
                value: cfg.value || '', label: cfg.label || '', text: cfg.label || '', cols: cfg.columns || [],
                open: false, active: -1, list: [], loading: false,
                dialog: false, query: '', rows: [], total: 0, row: -1,
                // total is -1 when a server search found more than it returned (its real count is unknown).
                get countText() {
                    if (this.total < 0) return 'Showing the first ' + this.rows.length + '. Refine the search to see more.';
                    if (this.total > this.rows.length) return 'Showing ' + this.rows.length + ' of ' + this.total + '. Refine the search to see more.';
                    return this.rows.length === 1 ? '1 record' : this.rows.length + ' records';
                },
                cell: function (item, key) {
                    return key === 'value' ? item.v : key === 'label' ? item.l : key === 'description' ? (item.d || '') : (item.f && item.f[key]) || '';
                },
                // Resolves to { items, total }.
                find: function (text, max) {
                    if (!cfg.href) {
                        var all = lookupFilter(cfg.items || [], text, Infinity);
                        return Promise.resolve({ items: all.slice(0, max), total: all.length });
                    }
                    var url = cfg.href + (cfg.href.indexOf('?') >= 0 ? '&' : '?') + 'q=' + encodeURIComponent(text || '') + '&max=' + (max + 1);
                    return fetch(url, { headers: { Accept: 'application/json' } })
                        .then(function (r) { return r.ok ? r.json() : []; })
                        .then(function (data) {
                            var items = (data || []).map(function (i) { return { v: String(i.value), l: String(i.label), d: i.description || null, f: i.fields || null }; });
                            return { items: items.slice(0, max), total: items.length > max ? -1 : items.length };
                        })
                        .catch(function () { return { items: [], total: 0 }; });
                },
                place: function () {
                    var root = this.$root;
                    this.$nextTick(function () {
                        if (window.modulusShell) { window.modulusShell.placeSelect(root); window.modulusShell.scrollLookupRow(root); }
                    });
                },
                suggest: function (text, keepActive) {
                    var self = this, mine = ++seq;
                    clearTimeout(timer);
                    this.loading = true;
                    var run = function () {
                        self.find(text, cfg.max).then(function (r) {
                            if (mine !== seq) return;
                            self.list = r.items;
                            self.loading = false;
                            var i = keepActive ? r.items.findIndex(function (x) { return x.v === self.value; }) : -1;
                            self.active = i >= 0 ? i : r.items.length ? 0 : -1;
                            self.place();
                        });
                    };
                    if (cfg.href) timer = setTimeout(run, 200); else run();
                },
                typed: function () {
                    if (!this.open) { this.open = true; this.list = []; }
                    this.suggest(this.text, false);
                },
                openList: function () {
                    if (this.open || cfg.disabled) return;
                    this.open = true;
                    this.list = [];
                    // Opened on the current value: show everything, not just the current label.
                    this.suggest(this.text === this.label ? '' : this.text, true);
                },
                close: function () { this.open = false; this.active = -1; seq++; clearTimeout(timer); },
                revert: function () { this.text = this.label; this.close(); },
                pick: function (item) {
                    var fromDialog = this.dialog;
                    this.close();
                    this.dialog = false;
                    var before = this.value;
                    this.value = item ? item.v : '';
                    this.label = this.text = item ? item.l : '';
                    if (this.value !== before) {
                        var hidden = this.$refs.value, root = this.$root;
                        this.$nextTick(function () {
                            changed(hidden);
                            root.dispatchEvent(new CustomEvent('m-lookup-change', { bubbles: true, detail: item ? { value: item.v, label: item.l, description: item.d, fields: item.f } : null }));
                        });
                    }
                    if (fromDialog) { var input = this.$refs.input; this.$nextTick(function () { input.focus(); }); }
                },
                blur: function () {
                    if (this.dialog) return;
                    var t = this.text.trim();
                    if (!t) { if (this.value) this.pick(null); else this.close(); return; }
                    if (t === this.label) { this.revert(); return; }
                    var match = lookupResolve(cfg.href ? this.list : cfg.items || [], t);
                    if (match) this.pick(match); else this.revert();
                },
                key: function (e) {
                    // modulus-shell.js stops these keys scrolling the page or submitting the form.
                    var n = this.list.length;
                    if (e.key === 'F4') this.openDialog();
                    else if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && !this.open) this.openList();
                    else if (e.key === 'ArrowDown' && n) { this.active = (this.active + 1) % n; this.place(); }
                    else if (e.key === 'ArrowUp' && n) { this.active = (this.active - 1 + n) % n; this.place(); }
                    else if (e.key === 'Enter' && this.open && this.active >= 0 && this.active < n) this.pick(this.list[this.active]);
                    else if (e.key === 'Escape') this.revert();
                },
                openDialog: function () {
                    if (cfg.disabled) return;
                    this.close();
                    this.query = this.text === this.label ? '' : this.text;
                    this.dialog = true;
                    this.rows = [];
                    this.search(true);
                    var search = this.$refs.search;
                    this.$nextTick(function () { window.modulusShell.focus(search); });
                },
                closeDialog: function () {
                    seq++;
                    this.dialog = false;
                    this.text = this.label;
                    var input = this.$refs.input;
                    this.$nextTick(function () { input.focus(); });
                },
                search: function (first) {
                    var self = this, mine = ++seq;
                    clearTimeout(timer);
                    this.loading = true;
                    var run = function () {
                        self.find(self.query, cfg.page).then(function (r) {
                            if (mine !== seq) return;
                            self.rows = r.items;
                            self.total = r.total;
                            self.loading = false;
                            var i = first ? r.items.findIndex(function (x) { return x.v === self.value; }) : -1;
                            self.row = i >= 0 ? i : r.items.length ? 0 : -1;
                            var root = self.$root;
                            self.$nextTick(function () { window.modulusShell.scrollLookupRow(root); });
                        });
                    };
                    if (cfg.href && !first) timer = setTimeout(run, 250); else run();
                },
                dialogKey: function (e) {
                    var n = this.rows.length, root = this.$root;
                    if (e.key === 'ArrowDown' && n) this.row = (this.row + 1) % n;
                    else if (e.key === 'ArrowUp' && n) this.row = (this.row - 1 + n) % n;
                    else if (e.key === 'Enter' && this.row >= 0 && this.row < n) { this.pick(this.rows[this.row]); return; }
                    else return;
                    this.$nextTick(function () { window.modulusShell.scrollLookupRow(root); });
                }
            };
        });
    });
})();
