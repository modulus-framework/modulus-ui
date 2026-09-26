/*
 * Alpine components behind the MVC widgets in AdvancedEntryTagHelpers.cs:
 * app-tree, app-tree-select, app-cascader, app-transfer, app-calendar, app-carousel, app-anchor.
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
    });
})();
