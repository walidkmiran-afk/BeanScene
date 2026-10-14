(function () {
    'use strict';
    if (window.bkAddBooking) return;   // load once
    window.bkAddBooking = true;

    var DURATIONS = [30, 45, 60, 90, 120, 150, 180];
    var DEFAULT_DURATION = 90;

    function toMin(t) { var p = t.split(':'); return parseInt(p[0], 10) * 60 + parseInt(p[1], 10); }
    function pad(n) { return (n < 10 ? '0' : '') + n; }
    function hourLabel(h) { return (h % 12 === 0 ? 12 : h % 12) + ' ' + (h >= 12 ? 'PM' : 'AM'); }
    function durationLabel(m) {
        return m < 60 ? m + ' minutes'
            : (Math.floor(m / 60) + (m >= 120 ? ' hours' : ' hour') + (m % 60 ? ' ' + (m % 60) + ' min' : ''));
    }
    function addOption(sel, value, text) {
        var o = document.createElement('option');
        o.value = value;
        o.textContent = text;
        sel.appendChild(o);
    }

    function build(form) {
        var hourSel = form.querySelector('.add-hour');
        var minSel = form.querySelector('.add-minute');
        var durSel = form.querySelector('.add-duration');
        var note = form.querySelector('.add-none');
        var submit = form.querySelector('[type=submit]');
        var hidden = form.elements.StartTime;
        var start = toMin(form.dataset.start);
        var end = toMin(form.dataset.end);
        var close = toMin(form.dataset.close);
        var wantedDuration = DEFAULT_DURATION;
        var slots = [];

        function fillDurations() {
            var from = parseInt(hourSel.value, 10) * 60 + parseInt(minSel.value, 10);
            var longest = close - from;   // the booking has to finish before the restaurant closes
            var options = DURATIONS.filter(function (d) { return d <= longest; });
            durSel.innerHTML = '';
            options.forEach(function (d) { addOption(durSel, d, durationLabel(d)); });
            durSel.value = String(options.indexOf(wantedDuration) >= 0 ? wantedDuration : options[options.length - 1]);
        }

        function commit() {
            hidden.value = pad(parseInt(hourSel.value, 10)) + ':' + pad(parseInt(minSel.value, 10));
            fillDurations();
        }

        function fillMinutes(wantedMinute) {
            var h = parseInt(hourSel.value, 10);
            var mins = slots.filter(function (m) { return Math.floor(m / 60) === h; }).map(function (m) { return m % 60; });
            minSel.innerHTML = '';
            mins.forEach(function (m) { addOption(minSel, m, pad(m)); });
            minSel.value = String(mins.indexOf(wantedMinute) >= 0 ? wantedMinute : mins[0]);
            commit();
        }

        function fillHours(wantedHour, wantedMinute) {
            var hours = [];
            slots.forEach(function (m) { var h = Math.floor(m / 60); if (hours.indexOf(h) < 0) hours.push(h); });
            hourSel.innerHTML = '';
            hours.forEach(function (h) { addOption(hourSel, h, hourLabel(h)); });
            hourSel.value = String(wantedHour);
            fillMinutes(wantedMinute);
        }

        function none() {
            hidden.value = '';
            [hourSel, minSel, durSel].forEach(function (s) { s.innerHTML = '<option>--</option>'; s.disabled = true; });
            note.hidden = false;
            submit.disabled = true;
        }

        // Works out the allowed start times from the sitting and the clock. Never a time that has passed.
        function refresh() {
            var now = new Date();
            var todayText = now.getFullYear() + '-' + pad(now.getMonth() + 1) + '-' + pad(now.getDate());
            var from = start;
            if (form.dataset.date < todayText) from = end;   // the day has passed: nothing to offer
            else if (form.dataset.date === todayText) from = Math.max(from, Math.ceil((now.getHours() * 60 + now.getMinutes()) / 15) * 15);

            slots = [];
            for (var m = from; m + 30 <= end; m += 15) slots.push(m);
            if (slots.length === 0) { none(); return; }

            note.hidden = true;
            submit.disabled = false;
            [hourSel, minSel, durSel].forEach(function (s) { s.disabled = false; });

            var current = hidden.value ? toMin(hidden.value) : -1;
            if (slots.indexOf(current) < 0) current = slots[0];
            fillHours(Math.floor(current / 60), current % 60);
        }

        hourSel.addEventListener('change', function () { fillMinutes(parseInt(minSel.value, 10)); });
        minSel.addEventListener('change', commit);
        durSel.addEventListener('change', function () { wantedDuration = parseInt(durSel.value, 10); });

        return { refresh: refresh };
    }

    // The times are worked out each time the form is opened, so they always match the clock.
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (!d || d.tagName !== 'DETAILS' || !d.open) return;
        var form = d.querySelector('form.bk-add-form');
        if (!form) return;
        if (!form._add) form._add = build(form);
        form._add.refresh();
    }, true);
})();