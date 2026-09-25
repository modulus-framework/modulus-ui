/*
 * Page behaviour for the MVC and Razor Pages samples (Alpine components and htmx hooks).
 * Loaded before Alpine; everything registers on alpine:init. The Blazor sample implements
 * the same behaviour in C#.
 */
(function () {
    'use strict';

    var emailPattern = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

    // "Coming soon" subscribe box: validates, then acknowledges with a toast.
    window.notifyLaunch = function (form) {
        var input = form.querySelector('input[type=email]');
        if (!emailPattern.test(input.value)) { window.modulusMessage('error', 'Enter a valid email'); return; }
        input.value = '';
        window.modulusMessage('success', "Thanks! We'll email you at launch.");
    };

    document.addEventListener('alpine:init', function () {
        var Alpine = window.Alpine;

        // Six one-digit boxes feeding a hidden "Code" field (two-step verification).
        Alpine.data('otp', function () {
            return {
                boxes: function () { return Array.prototype.slice.call(this.$root.querySelectorAll('[data-otp]')); },
                input: function (e) {
                    var el = e.target, boxes = this.boxes(), i = boxes.indexOf(el);
                    el.value = el.value.replace(/\D/g, '').slice(-1);
                    if (el.value && i < boxes.length - 1) boxes[i + 1].focus();
                },
                back: function (e) {
                    var boxes = this.boxes(), i = boxes.indexOf(e.target);
                    if (!e.target.value && i > 0) boxes[i - 1].focus();
                },
                paste: function (e) {
                    var digits = (e.clipboardData.getData('text') || '').replace(/\D/g, '').split('');
                    var boxes = this.boxes(), i = boxes.indexOf(e.target);
                    for (var k = i; k < boxes.length && digits.length; k++) boxes[k].value = digits.shift();
                    boxes[Math.min(boxes.length - 1, k)].focus();
                },
                sync: function () { this.$refs.code.value = this.boxes().map(function (b) { return b.value; }).join(''); }
            };
        });

        // Days / hours / minutes / seconds to a UTC timestamp.
        // Invoice editor: line items (add / remove), live totals. Field names follow model binding
        // (Lines[0].Item …); server-side validation messages arrive in `errors` keyed by field name.
        Alpine.data('invoiceForm', function (init) {
            var money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });
            return {
                lines: init.lines.length ? init.lines : [{ item: '', description: '', cost: 0, qty: 1 }],
                discount: init.discount,
                tax: init.tax,
                errors: init.errors || {},
                add: function () { this.lines.push({ item: '', description: '', cost: 0, qty: 1 }); },
                remove: function (i) { this.lines.splice(i, 1); if (!this.lines.length) this.add(); },
                error: function (i, field) { return this.errors['Lines[' + i + '].' + field] || ''; },
                subtotal: function () { return this.lines.reduce(function (sum, l) { return sum + (+l.cost || 0) * (+l.qty || 0); }, 0); },
                discountAmount: function () { return Math.round(this.subtotal() * (+this.discount || 0)) / 100; },
                taxAmount: function () { return Math.round((this.subtotal() - this.discountAmount()) * (+this.tax || 0)) / 100; },
                total: function () { return this.subtotal() - this.discountAmount() + this.taxAmount(); },
                fmt: function (value) { return money.format(value); }
            };
        });

        // Kanban: HTML5 drag and drop between columns; the drop position is posted as (id, column, index)
        // and the server answers with kanban:refresh. Cards also have a keyboard-friendly "Move to" menu.
        Alpine.data('kanbanBoard', function (moveUrl) {
            return {
                dragId: null,
                over: null,
                start: function (e, id) {
                    this.dragId = id;
                    e.dataTransfer.effectAllowed = 'move';
                    e.dataTransfer.setData('text/plain', String(id));
                    e.target.classList.add('dragging');
                },
                end: function (e) { e.target.classList.remove('dragging'); this.dragId = null; this.over = null; },
                drop: function (e, column) {
                    var list = e.currentTarget.querySelector('.m-kanban-list');
                    var cards = Array.prototype.filter.call(list.querySelectorAll('.m-kanban-card'), function (c) { return c.dataset.id !== String(this.dragId); }, this);
                    var index = cards.length;
                    for (var i = 0; i < cards.length; i++) {
                        var r = cards[i].getBoundingClientRect();
                        if (e.clientY < r.top + r.height / 2) { index = i; break; }
                    }
                    var id = this.dragId || e.dataTransfer.getData('text/plain');
                    this.over = null;
                    if (id) window.htmx.ajax('POST', moveUrl, { source: this.$root, values: { id: id, column: column, index: index }, swap: 'none' });
                }
            };
        });

        Alpine.data('countdown', function (target) {
            return {
                parts: [0, 0, 0, 0],
                init: function () { this.tick(); var self = this; this.timer = setInterval(function () { self.tick(); }, 1000); },
                destroy: function () { clearInterval(this.timer); },
                tick: function () {
                    var s = Math.max(0, Math.floor((target - Date.now()) / 1000));
                    this.parts = [Math.floor(s / 86400), Math.floor(s / 3600) % 24, Math.floor(s / 60) % 60, s % 60];
                },
                pad: function (n) { return (n < 10 ? '0' : '') + n; }
            };
        });
    });
})();
