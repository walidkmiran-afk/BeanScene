// ---------------------------------------------------------------------------------------------
// Shared rules for time and seats boxes. Any form can use them with one call:
//     wireTimeBoxes(form, '.some-box-class', { groups: function () { ... } });
//   - a short time is understood: 12 -> 12:00, 9 -> 09:00, 930 -> 09:30
//   - clicking into a box selects everything in it, so what you type replaces it
//   - the browser's own format message is switched off; a box that cannot be understood turns red
//   - an open sitting that ends before it starts, or overlaps another one that day, turns red right away
//   - before saving, every named box is checked, and nothing is saved while one is red
// options.isTime(box)  says whether a box is a time (default: its name contains "Time"); otherwise it is seats
// options.onTidy(box)  runs after a box was tidied (for example to copy it somewhere)
// options.groups()     gives the open sittings, one list per day, each sitting as { start: box, end: box }
// ---------------------------------------------------------------------------------------------

// Turns what was typed into a time. Gives '' when it is not a time.
function toTime(text) {
    var m = /^(\d{1,2})(?::?(\d{2}))?$/.exec(text.trim().replace('.', ':'));
    if (!m) return '';
    var h = parseInt(m[1], 10), min = m[2] ? parseInt(m[2], 10) : 0;
    if (h > 23 || min > 59) return '';
    return (h < 10 ? '0' : '') + h + ':' + (min < 10 ? '0' : '') + min;
}

// Is this box understood? Times must be a time, seats must be 1 to 500.
function boxOk(box, isTime) {
    if (isTime) return toTime(box.value) !== '';
    var seats = parseInt(box.value, 10);
    return seats >= 1 && seats <= 500;
}

// Tidy one box. A box that cannot be understood turns red.
function tidyBox(box, isTime) {
    if (!boxOk(box, isTime)) { box.classList.add('wk-bad'); return false; }
    box.value = isTime ? toTime(box.value) : parseInt(box.value, 10);
    box.classList.remove('wk-bad');
    return true;
}

// Marks the sittings that end before they start, or overlap another sitting on the same day.
// lists = one list per day, each sitting as { start: box, end: box }. Gives true when something was marked.
function markClashes(lists) {
    var bad = false;
    function mark(input) { input.classList.add('wk-bad'); bad = true; }
    lists.forEach(function (list) {
        list = list.filter(function (it) { return toTime(it.start.value) !== '' && toTime(it.end.value) !== ''; });
        list.sort(function (a, b) {
            var x = toTime(a.start.value), y = toTime(b.start.value);
            return x < y ? -1 : (x > y ? 1 : 0);
        });
        list.forEach(function (it, i) {
            if (toTime(it.end.value) <= toTime(it.start.value)) {
                mark(it.start); mark(it.end);
            } else if (i > 0 && toTime(it.start.value) < toTime(list[i - 1].end.value)) {
                mark(list[i - 1].end); mark(it.start);
            }
        });
    });
    return bad;
}

function wireTimeBoxes(form, selector, options) {
    options = options || {};
    var isTime = options.isTime || function (box) { return box.name.indexOf('Time') > -1; };

    form.noValidate = true;

    // Check the sittings: clear the red from boxes that are fine, then mark clashes.
    // Gives true when something is still red in a box that is saved (the ones with a name).
    function check() {
        form.querySelectorAll(selector).forEach(function (box) {
            if (box.name && boxOk(box, isTime(box))) box.classList.remove('wk-bad');
        });
        var clash = options.groups ? markClashes(options.groups()) : false;
        return clash || !!form.querySelector('.wk-bad[name]');
    }

    // When a box is finished (Enter or click away), tidy it, then check.
    form.addEventListener('change', function (e) {
        var box = e.target;
        if (box.matches(selector) && tidyBox(box, isTime(box)) && options.onTidy) options.onTidy(box);
        check();
    });

    // Open / Closed, remove and the like: check once the click has finished.
    form.addEventListener('click', function () { setTimeout(check, 0); });

    // Clicking or tabbing into a box selects everything in it.
    form.addEventListener('focusin', function (e) {
        var box = e.target;
        if (box.matches(selector)) { box.select(); box._justFocused = true; }
    });
    form.addEventListener('mouseup', function (e) {
        var box = e.target;
        if (box._justFocused) { e.preventDefault(); box._justFocused = false; }
    });

    // Before saving, tidy every box that is saved. If one is wrong, nothing is saved.
    form.addEventListener('submit', function (e) {
        if (e.submitter && e.submitter.hasAttribute('formnovalidate')) return;
        var ok = true;
        form.querySelectorAll(selector).forEach(function (box) {
            if (box.name && !tidyBox(box, isTime(box))) ok = false;
        });
        if (!ok || check()) { e.preventDefault(); e.stopImmediatePropagation(); }
    });
}