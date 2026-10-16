// ---------------------------------------------------------------------------------------------
// Sittings page. Nothing on this page reloads. Choosing a day, saving the weekly hours, saving a
// day and pressing a booking button all go to the server in the background, then only the parts
// that changed are swapped in. (The booking buttons themselves are in booking-actions.js, and the
// rules for time and seats boxes, including the red warnings, are in time-boxes.js.)
// ---------------------------------------------------------------------------------------------
var dayForm = null;
var dayRows = null;
var dayNext = 0;
var confirmed = false;
var bkCurrent = null;

function hideWarn() {
    var w = document.getElementById('dayWarn');
    if (w) w.style.display = 'none';
}

// Swap the changed parts of the page in from a freshly fetched copy.
// replaceRows = true also replaces the rows of the day editor (after Save day, Reset and Save weekly hours).
function applyFresh(doc, replaceRows) {
    var cal = doc.getElementById('calWrap');
    if (cal) document.getElementById('calWrap').innerHTML = cal.innerHTML;

    ['bkPanels', 'dayStats', 'dayReset'].forEach(function (id) {
        var fresh = doc.getElementById(id);
        var mine = document.getElementById(id);
        if (fresh && mine) mine.innerHTML = fresh.innerHTML;
    });

    if (replaceRows) {
        var freshRows = doc.getElementById('dayRows');
        if (freshRows && dayRows) {
            dayRows.innerHTML = freshRows.innerHTML;
            dayRows.querySelectorAll('.dy-row').forEach(wireRow);
            dayNext = dayRows.querySelectorAll('.dy-row').length;
            var empty = document.getElementById('dayEmpty');
            if (empty && dayNext > 0) empty.remove();
            confirmed = false;
            hideWarn();
        }
    } else {
        // Only the Bookings numbers change; whatever was typed in the day editor stays.
        document.querySelectorAll('.bk-open').forEach(function (mine) {
            var n = doc.querySelector('.bk-open[data-sitting="' + mine.dataset.sitting + '"]');
            if (n) { mine.textContent = n.textContent; }
            else {
                var s = document.createElement('span');
                s.className = 'text-muted';
                s.textContent = '0';
                mine.replaceWith(s);
            }
        });
    }

    if (bkCurrent !== null && !document.querySelector('.bk-panel[data-sitting="' + bkCurrent + '"]')) bkCurrent = null;
    showBookings(bkCurrent);
}

// Post a form in the background, then refresh the page parts. If anything fails, do a normal save.
function postAndRefresh(form, submitter, replaceRows, done) {
    var scroll = window.scrollY;
    var action = (submitter && submitter.formAction) ? submitter.formAction : form.action;
    fetch(action, { method: 'POST', body: new FormData(form), credentials: 'same-origin' })
        .then(function (r) { return r.text(); })
        .then(function (firstHtml) {
            var first = parseHtml(firstHtml);   // carries the "saved" message
            return fetch(location.href, { credentials: 'same-origin' })
                .then(function (r) { return r.text(); })
                .then(function (html) {
                    var doc = parseHtml(html);
                    applyFresh(doc, replaceRows);
                    showMessage(first);
                    if (done) done();
                    window.scrollTo(0, scroll);
                });
        })
        .catch(function () { form.submit(); });
}

// The booking buttons use this page's own refresh, which also updates the calendar numbers and the Bookings column.
window.bkRefresh = function (form) { postAndRefresh(form, null, false); };

// Open another day without reloading: swap in the month bar, the calendar and the day area.
function loadDay(url, mode) {
    var scroll = window.scrollY;
    fetch(url, { credentials: 'same-origin' })
        .then(function (r) { return r.text(); })
        .then(function (html) {
            var doc = parseHtml(html);
            ['monthNav', 'calWrap', 'dayArea'].forEach(function (id) {
                var fresh = doc.getElementById(id);
                var mine = document.getElementById(id);
                if (fresh && mine) mine.innerHTML = fresh.innerHTML;
            });
            bkCurrent = null;
            initDay();
            if (mode === 'push') history.pushState(null, '', url);
            else history.replaceState(null, '', url);
            window.scrollTo(0, scroll);
        })
        .catch(function () { location.href = url; });
}

// The browser's Back and Forward buttons just load the page for that address.
window.addEventListener('popstate', function () { location.reload(); });

// ---- Weekly hours ----
// Each weekly sitting has an Open / Closed button. Closed greys out and locks From, To and Seats
// (locked, not disabled, so their values are still saved).
document.querySelectorAll('.wk-btn').forEach(function (btn) {
    var cell = btn.closest('td');
    var flag = cell.querySelector('.wk-flag');
    function apply() {
        var open = flag.value === 'true';
        btn.textContent = open ? 'Open' : 'Closed';
        btn.classList.toggle('btn-success', open);
        btn.classList.toggle('btn-secondary', !open);
        cell.querySelectorAll('.wk-field').forEach(function (input) {
            input.readOnly = !open;
            input.style.opacity = open ? '1' : '0.45';
            input.style.backgroundColor = open ? '' : '#e9ecef';
        });
    }
    btn._apply = apply;
    btn.addEventListener('click', function () {
        flag.value = flag.value === 'true' ? 'false' : 'true';
        apply();
    });
    apply();
});

// The Weekly hours button shows an arrow and a hint that changes when the section opens and closes.
var wkPanel = document.getElementById('weeklyHours');
wkPanel.addEventListener('show.bs.collapse', function () {
    document.getElementById('weeklyArrow').innerHTML = '&#9650;';
    document.getElementById('weeklyHint').textContent = 'click to hide';
});
wkPanel.addEventListener('hide.bs.collapse', function () {
    document.getElementById('weeklyArrow').innerHTML = '&#9660;';
    document.getElementById('weeklyHint').textContent = 'click to edit';
});

var wkForm = document.getElementById('weeklyForm');

// Remember the saved values so Undo changes can put them back without reloading the page.
function snapshotWeekly() {
    wkForm.querySelectorAll('.wk-flag, .wk-field').forEach(function (input) {
        input.dataset.orig = input.value;
    });
}
snapshotWeekly();
document.getElementById('weeklyUndo').addEventListener('click', function () {
    wkForm.querySelectorAll('.wk-flag, .wk-field').forEach(function (input) {
        input.value = input.dataset.orig;
        input.classList.remove('wk-bad');
    });
    wkForm.querySelectorAll('.wk-btn').forEach(function (btn) { btn._apply(); });
});

// ---- Weekly hours: the All column ----
// The All column sets one meal's Open / From / To / Seats on all seven days at once. Its boxes have no name,
// so they are never saved themselves; they only copy into the day boxes, which Save weekly hours saves.
function dayCells(tr) { return Array.prototype.slice.call(tr.querySelectorAll('td[data-day]')); }
function allIndex(box) { return Array.prototype.indexOf.call(box.closest('td.wk-all').querySelectorAll('.wk-all-field'), box); }

// In the All column the first two boxes are times and the third is seats. In the day boxes the name says which.
function isTimeBox(box) {
    if (box.classList.contains('wk-all-field')) return allIndex(box) < 2;
    return box.name.indexOf('Time') > -1;
}

// Keep the All column in step with the seven days: the shared value, or blank (--) when the days differ.
function syncAll() {
    wkForm.querySelectorAll('td.wk-all').forEach(function (all) {
        var cells = dayCells(all.closest('tr'));
        var flags = cells.map(function (c) { return c.querySelector('.wk-flag').value === 'true'; });
        var anyOpen = flags.some(function (f) { return f; });
        var allOpen = flags.every(function (f) { return f; });
        var btn = all.querySelector('.wk-all-btn');
        btn.textContent = allOpen ? 'Open' : (anyOpen ? 'Mixed' : 'Closed');
        btn.classList.toggle('btn-success', allOpen);
        btn.classList.toggle('btn-secondary', !anyOpen);
        btn.classList.toggle('btn-outline-secondary', anyOpen && !allOpen);
        all.querySelectorAll('.wk-all-field').forEach(function (input, i) {
            var values = cells.map(function (c) { return c.querySelectorAll('.wk-field')[i].value; });
            var same = values.every(function (v) { return v === values[0]; });
            if (document.activeElement !== input) input.value = same ? values[0] : '';
            input.placeholder = same ? '' : '--';
            input.style.opacity = anyOpen ? '1' : '0.45';
        });
    });
}

// The shared rules (time-boxes.js) apply to the day boxes and the All column.
// A tidied All box also copies to the seven days.
// groups = the open sittings of each day of the week, for the end-after-start and overlap check.
wireTimeBoxes(wkForm, '.wk-field, .wk-all-field', {
    isTime: isTimeBox,
    onTidy: function (box) {
        if (box.classList.contains('wk-all-field')) {
            var index = allIndex(box);
            dayCells(box.closest('tr')).forEach(function (c) {
                var day = c.querySelectorAll('.wk-field')[index];
                day.value = box.value;
                day.classList.remove('wk-bad');
            });
        }
        syncAll();
    },
    groups: function () {
        var byDay = {};
        wkForm.querySelectorAll('td[data-day]').forEach(function (cell) {
            if (cell.querySelector('.wk-flag').value !== 'true') return;
            var f = cell.querySelectorAll('.wk-field');   // start, end, seats
            (byDay[cell.dataset.day] = byDay[cell.dataset.day] || []).push({ start: f[0], end: f[1] });
        });
        return Object.keys(byDay).map(function (k) { return byDay[k]; });
    }
});

// The All Open / Closed button opens or closes that meal on all seven days.
wkForm.addEventListener('click', function (e) {
    var allBtn = e.target.closest('.wk-all-btn');
    if (allBtn) {
        var cells = dayCells(allBtn.closest('tr'));
        var makeOpen = !cells.every(function (c) { return c.querySelector('.wk-flag').value === 'true'; });
        cells.forEach(function (c) {
            c.querySelector('.wk-flag').value = makeOpen ? 'true' : 'false';
            c.querySelector('.wk-btn')._apply();
        });
    }
    if (allBtn || e.target.closest('.wk-btn')) syncAll();
});

// Enter in an All box finishes it. It does not save the form.
wkForm.addEventListener('keydown', function (e) {
    if (e.key === 'Enter' && e.target.closest('.wk-all-field')) {
        e.preventDefault();
        e.target.dispatchEvent(new Event('change', { bubbles: true }));
    }
});

wkForm.addEventListener('input', function (e) {
    e.target.classList.remove('wk-bad');
    if (e.target.closest('.wk-field')) syncAll();
});

document.getElementById('weeklyUndo').addEventListener('click', syncAll);
syncAll();

// Save the weekly hours. time-boxes.js has already stopped the save if anything is red.
wkForm.addEventListener('submit', function (e) {
    e.preventDefault();
    postAndRefresh(wkForm, null, true, snapshotWeekly);
});

// ---- Day editor (under the calendar) ----
// Wire one row: its Open / Closed button greys out and locks From, To and Seats when Closed.
// The remove button (x) takes the row off the page; the server deletes it when you save.
function wireRow(tr) {
    var btn = tr.querySelector('.dy-btn');
    var flag = tr.querySelector('.dy-flag');
    function apply() {
        var open = flag.value === 'true';
        btn.textContent = open ? 'Open' : 'Closed';
        btn.classList.toggle('btn-success', open);
        btn.classList.toggle('btn-secondary', !open);
        tr.querySelectorAll('.dy-field').forEach(function (input) {
            input.readOnly = !open;
            input.style.opacity = open ? '1' : '0.45';
            input.style.backgroundColor = open ? '' : '#e9ecef';
        });
    }
    btn._apply = apply;
    btn.addEventListener('click', function () {
        flag.value = flag.value === 'true' ? 'false' : 'true';
        apply();
        hideWarn();
    });
    var rm = tr.querySelector('.dy-remove');
    if (rm) rm.addEventListener('click', function () { tr.remove(); hideWarn(); });
    apply();
}

// Set up the day editor. Runs when the page opens and again every time another day is swapped in.
function initDay() {
    dayForm = document.getElementById('dayForm');
    dayRows = document.getElementById('dayRows');
    confirmed = false;
    if (!dayForm) return;

    // The shared rules (time-boxes.js). groups = the open sittings of this day.
    wireTimeBoxes(dayForm, '.dy-field', {
        groups: function () {
            var list = [];
            dayRows.querySelectorAll('.dy-row').forEach(function (tr) {
                if (tr.querySelector('.dy-flag').value !== 'true') return;
                var f = tr.querySelectorAll('.dy-field');   // start, end, seats
                list.push({ start: f[0], end: f[1] });
            });
            return [list];
        }
    });

    dayNext = dayRows.querySelectorAll('.dy-row').length;
    dayRows.querySelectorAll('.dy-row').forEach(wireRow);

    // Close whole day: every sitting on this day becomes Closed (then press Save day).
    document.getElementById('dayCloseAll').addEventListener('click', function () {
        dayRows.querySelectorAll('.dy-row').forEach(function (tr) {
            tr.querySelector('.dy-flag').value = 'false';
            tr.querySelector('.dy-btn')._apply();
        });
        hideWarn();
    });

    // + Add sitting: adds a new row at the bottom.
    document.getElementById('dayAddSpecial').addEventListener('click', function () {
        var i = dayNext++;
        var empty = document.getElementById('dayEmpty');
        if (empty) empty.remove();
        var tr = document.createElement('tr');
        tr.className = 'dy-row';
        tr.innerHTML =
            '<td><input type="hidden" name="sittings[' + i + '].SittingId" value="0" />' +
            '<input type="hidden" name="sittings[' + i + '].IsOpen" value="true" class="dy-flag" />' +
            '<input type="text" name="sittings[' + i + '].Label" value="Special" maxlength="40" title="Sitting name" class="form-control form-control-sm py-0 dy-label" style="width:120px;font-size:.75rem;height:22px" /></td>' +
            '<td><button type="button" class="btn btn-sm p-0 dy-btn" style="width:52px;height:22px;font-size:.7rem"></button></td>' +
            '<td><input type="text" name="sittings[' + i + '].StartTime" value="18:00" maxlength="5" pattern="([01][0-9]|2[0-3]):[0-5][0-9]" title="Start time, for example 18:00" class="form-control form-control-sm p-0 text-center dy-field" style="width:56px;font-size:.75rem;height:22px" /></td>' +
            '<td><input type="text" name="sittings[' + i + '].EndTime" value="22:00" maxlength="5" pattern="([01][0-9]|2[0-3]):[0-5][0-9]" title="End time, for example 22:00" class="form-control form-control-sm p-0 text-center dy-field" style="width:56px;font-size:.75rem;height:22px" /></td>' +
            '<td><input type="number" name="sittings[' + i + '].Capacity" value="40" min="1" max="500" title="Seats" class="form-control form-control-sm p-0 text-center dy-field" style="width:46px;font-size:.75rem;height:22px" /></td>' +
            '<td class="text-muted text-center">0</td>' +
            '<td><button type="button" class="btn btn-sm btn-outline-secondary py-0 px-1 dy-remove" style="line-height:1.2" title="Remove this sitting">&times;</button></td>';
        dayRows.appendChild(tr);
        wireRow(tr);
    });

    dayForm.addEventListener('input', function (e) {
        e.target.classList.remove('wk-bad');
        hideWarn();
    });

    document.getElementById('dayBack').addEventListener('click', hideWarn);
    document.getElementById('dayConfirm').addEventListener('click', function () {
        confirmed = true;
        dayForm.requestSubmit();
    });

    // Pending or Confirmed bookings whose time is not inside any open sitting on the page.
    function bookingsToCancel() {
        var spans = [];
        dayRows.querySelectorAll('.dy-row').forEach(function (tr) {
            if (tr.querySelector('.dy-flag').value !== 'true') return;
            var f = tr.querySelectorAll('.dy-field');   // start, end, seats
            spans.push([f[0].value, f[1].value]);
        });
        var result = [];
        document.querySelectorAll('tr[data-booking]').forEach(function (b) {
            if (b.dataset.cancellable !== '1') return;
            var t = b.dataset.start;
            var covered = spans.some(function (s) { return s[0] <= t && t < s[1]; });
            if (!covered) result.push(b);
        });
        return result;
    }

    // Save the day. time-boxes.js has already stopped the save if anything is red.
    // If bookings would be cancelled, a red box lists them and asks you to confirm.
    dayForm.addEventListener('submit', function (e) {
        e.preventDefault();

        // Reset skips the check (it has its own confirm box).
        if (e.submitter && e.submitter.hasAttribute('formnovalidate')) {
            postAndRefresh(dayForm, e.submitter, true);
            return;
        }

        if (!confirmed) {
            var affected = bookingsToCancel();
            if (affected.length > 0) {
                var ul = document.getElementById('dayWarnList');
                ul.innerHTML = '';
                affected.forEach(function (b) {
                    var li = document.createElement('li');
                    li.textContent = b.dataset.start + '  ' + b.dataset.name + ' (' + b.dataset.guests + ' guests)';
                    ul.appendChild(li);
                });
                document.getElementById('dayWarn').style.display = 'block';
                return;
            }
        }

        // Number the rows 0, 1, 2... again so removed rows leave no gaps.
        dayRows.querySelectorAll('.dy-row').forEach(function (tr, idx) {
            tr.querySelectorAll('[name]').forEach(function (el) {
                el.name = el.name.replace(/sittings\[\d+\]/, 'sittings[' + idx + ']');
            });
        });

        postAndRefresh(dayForm, null, true);
    });
}

// ---- Bookings of one sitting (click the Bookings number) ----
function showBookings(id) {
    document.querySelectorAll('.bk-panel').forEach(function (p) {
        p.style.display = (id !== null && p.dataset.sitting === id) ? 'block' : 'none';
    });
    document.querySelectorAll('.bk-open').forEach(function (b) {
        b.classList.toggle('fw-bold', b.dataset.sitting === id);
    });
}

// One click handler for the page, so it keeps working after parts are swapped in place.
document.addEventListener('click', function (e) {
    var t = e.target;

    // A day on the calendar (or the Today button) opens in place.
    var dl = t.closest('a.js-day');
    if (dl && e.button === 0 && !e.ctrlKey && !e.metaKey && !e.shiftKey) {
        e.preventDefault();
        loadDay(dl.href, 'push');
        return;
    }

    var open = t.closest('.bk-open');
    if (open) { bkCurrent = (bkCurrent === open.dataset.sitting) ? null : open.dataset.sitting; showBookings(bkCurrent); return; }
    if (t.closest('.bk-close')) { bkCurrent = null; showBookings(null); }
});

// ---- Start ----
initDay();

// After a normal page load with ?open=<sitting>, the same panel is shown.
var bkOpen = new URLSearchParams(location.search).get('open');
if (bkOpen) { bkCurrent = bkOpen; showBookings(bkOpen); }