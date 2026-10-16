// ---------------------------------------------------------------------------------------------
// Shared rules for time and seats boxes. Any form can use them with one line:
//     wireTimeBoxes(form, '.some-box-class');
//   - a short time is understood: 12 -> 12:00, 9 -> 09:00, 930 -> 09:30
//   - clicking into a box selects everything in it, so what you type replaces it
//   - the browser's own format message is switched off; a box that cannot be understood turns red instead
//   - before saving, every named box is tidied, and nothing is saved while one is wrong
// options.isTime(box)  says whether a box is a time (default: its name contains "Time"); otherwise it is seats
// options.onTidy(box)  runs after a box was tidied (for example to copy it somewhere)
// ---------------------------------------------------------------------------------------------

// Turns what was typed into a time. Gives '' when it is not a time.
function toTime(text) {
    var m = /^(\d{1,2})(?::?(\d{2}))?$/.exec(text.trim().replace('.', ':'));
    if (!m) return '';
    var h = parseInt(m[1], 10), min = m[2] ? parseInt(m[2], 10) : 0;
    if (h > 23 || min > 59) return '';
    return (h < 10 ? '0' : '') + h + ':' + (min < 10 ? '0' : '') + min;
}

// Tidy one box. A box that cannot be understood turns red.
function tidyBox(box, isTime) {
    if (isTime) {
        var time = toTime(box.value);
        if (time === '') { box.classList.add('wk-bad'); return false; }
        box.value = time;
    } else {
        var seats = parseInt(box.value, 10);
        if (!(seats >= 1 && seats <= 500)) { box.classList.add('wk-bad'); return false; }
        box.value = seats;
    }
    box.classList.remove('wk-bad');
    return true;
}

function wireTimeBoxes(form, selector, options) {
    options = options || {};
    var isTime = options.isTime || function (box) { return box.name.indexOf('Time') > -1; };

    form.noValidate = true;

    // When a box is finished (Enter or click away), tidy it.
    form.addEventListener('change', function (e) {
        var box = e.target;
        if (!box.matches(selector)) return;
        if (tidyBox(box, isTime(box)) && options.onTidy) options.onTidy(box);
    });

    // Clicking or tabbing into a box selects everything in it.
    form.addEventListener('focusin', function (e) {
        var box = e.target;
        if (box.matches(selector)) { box.select(); box._justFocused = true; }
    });
    form.addEventListener('mouseup', function (e) {
        var box = e.target;
        if (box._justFocused) { e.preventDefault(); box._justFocused = false; }
    });

    // Before saving, tidy every box that is saved (the ones with a name). If one is wrong, nothing is saved.
    form.addEventListener('submit', function (e) {
        if (e.submitter && e.submitter.hasAttribute('formnovalidate')) return;
        var ok = true;
        form.querySelectorAll(selector).forEach(function (box) {
            if (box.name && !tidyBox(box, isTime(box))) ok = false;
        });
        if (!ok) { e.preventDefault(); e.stopImmediatePropagation(); }
    });
}