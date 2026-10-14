// ---------------------------------------------------------------------------------------------
// The booking buttons, shared by the Sittings page and the Reservations page.
// Edit and Tables open a form under the booking, and every button saves in the background.
// ---------------------------------------------------------------------------------------------
function parseHtml(text) { return new DOMParser().parseFromString(text, 'text/html'); }

// A small message at the top, taken from the page the server sent back.
function showMessage(doc) {
    var a = Array.prototype.filter.call(doc.querySelectorAll('.alert'), function (x) { return !x.closest('#dayWarn'); })[0];
    if (!a) return;
    var t = document.createElement('div');
    t.className = a.className + ' shadow';
    t.style.cssText = 'position:fixed;top:70px;left:50%;transform:translateX(-50%);z-index:2000;max-width:90%';
    t.textContent = a.textContent.replace('\u00d7', '').trim();
    document.body.appendChild(t);
    setTimeout(function () { t.remove(); }, 3500);
}

// Edit and Tables each open a form under the booking. Only one is open at a time.
function toggleForm(kind, rid) {
    var row = document.querySelector('tr.' + kind + '[data-rid="' + rid + '"]');
    var other = document.querySelector('tr.' + (kind === 'bk-edit' ? 'bk-assign' : 'bk-edit') + '[data-rid="' + rid + '"]');
    if (other) other.style.display = 'none';
    if (row) row.style.display = row.style.display === 'none' ? '' : 'none';
}

document.addEventListener('click', function (e) {
    var t = e.target;
    var ed = t.closest('.bk-edit-btn');
    if (ed) { toggleForm('bk-edit', ed.dataset.rid); return; }
    var as = t.closest('.bk-assign-btn');
    if (as) { toggleForm('bk-assign', as.dataset.rid); return; }
    if (t.closest('.bk-form-cancel')) { t.closest('tr').style.display = 'none'; }
});

// Save in the background, then fetch the page again and swap in every area marked class="bk-area" (matched by id).
// A page can replace this by setting window.bkRefresh itself (the Sittings page does).
window.bkRefresh = function (form) {
    var scroll = window.scrollY;
    fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin' })
        .then(function (r) { return r.text(); })
        .then(function (firstHtml) {
            var first = parseHtml(firstHtml);   // carries the "saved" message
            return fetch(location.href, { credentials: 'same-origin' })
                .then(function (r) { return r.text(); })
                .then(function (html) {
                    var doc = parseHtml(html);
                    document.querySelectorAll('.bk-area').forEach(function (mine) {
                        var fresh = mine.id ? doc.getElementById(mine.id) : null;
                        if (fresh) mine.innerHTML = fresh.innerHTML;
                    });
                    showMessage(first);
                    window.scrollTo(0, scroll);
                });
        })
        .catch(function () { form.submit(); });   // if anything fails, do a normal save
};

// Every booking button inside a .bk-area saves in the background.
document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!form.closest('.bk-area')) return;
    e.preventDefault();
    window.bkRefresh(form);
});