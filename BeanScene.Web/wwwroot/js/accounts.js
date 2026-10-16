(function () {
    'use strict';

    const $ = (s, r) => (r || document).querySelector(s);
    const $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
    const KEYS = ['staff', 'members'];
    const search = $('#acctSearch');
    const msg = $('#acctMsg');
    let tab = location.hash === '#members' ? 'members' : 'staff';
    let panelRow = null;   // the open panel row (Edit, Reset, Delete, Bookings or Book), if any
    let reopen = null;     // after a booking is saved, this member's Bookings panel opens again

    function area() { return $('#acctArea'); }
    function token() { return $('input[name="__RequestVerificationToken"]', area()).value; }

    function say(text, ok) {
        msg.className = 'alert alert-' + (ok ? 'success' : 'danger') + ' py-2';
        msg.textContent = text;
        msg.hidden = false;
    }

    function setTab(key) {
        tab = key;
        history.replaceState(null, '', '#' + key);
    }

    // ---- search + tabs ---------------------------------------------------------
    function applyFilter(autoSwitch) {
        const q = search.value.trim().toLowerCase();
        const d = q.replace(/\D/g, '');
        const total = {}, shown = {};

        KEYS.forEach(key => {
            const rows = $$('[data-pane="' + key + '"] tr.acct-row');
            total[key] = rows.length;
            shown[key] = 0;
            rows.forEach(r => {
                const hit = !q || r.dataset.q.includes(q) || (d.length >= 3 && r.dataset.d.includes(d));
                r.hidden = !hit;
                if (hit) shown[key]++;
            });
        });

        // typing a search that only matches the other tab jumps to that tab
        if (autoSwitch && q) {
            const other = tab === 'staff' ? 'members' : 'staff';
            if (!shown[tab] && shown[other]) setTab(other);
        }

        KEYS.forEach(key => {
            const pane = $('[data-pane="' + key + '"]');
            const label = key === 'staff' ? 'staff' : 'member';
            pane.hidden = key !== tab;
            const empty = $('.acct-empty', pane);
            empty.hidden = shown[key] > 0;
            $('td', empty).textContent = q
                ? 'No ' + label + ' accounts match "' + search.value.trim() + '".'
                : 'No ' + label + ' accounts yet.';
            $('[data-count="' + key + '"]').textContent = q ? shown[key] + ' of ' + total[key] : total[key];
            $('[data-tab="' + key + '"]').classList.toggle('active', key === tab);
        });

        $$('tr.acct-panel').forEach(p => { p.hidden = p.previousElementSibling.hidden; });
    }

    // ---- the one inline panel (Edit / Reset password / Delete) ------------------
    function closePanel() {
        if (panelRow) {
            panelRow.previousElementSibling.classList.remove('table-active');
            panelRow.remove();
            panelRow = null;
        }
    }

    function openPanel(kind, row) {
        const same = panelRow && panelRow.previousElementSibling === row && panelRow.dataset.kind === kind;
        closePanel();
        $('#newPanel').hidden = true;
        if (same) return;   // clicking the same button again closes it

        const tr = document.createElement('tr');
        tr.className = 'acct-panel';
        tr.dataset.kind = kind;
        const td = document.createElement('td');
        td.colSpan = 5;
        td.appendChild($('#tpl-' + kind).content.cloneNode(true));
        tr.appendChild(td);
        row.after(tr);
        row.classList.add('table-active');
        panelRow = tr;

        const f = $('form', td);
        f.elements.UserId.value = row.dataset.id;
        $('.acct-for', td).textContent = row.dataset.name;
        if (kind === 'edit') {
            const staff = row.dataset.staff === '1';
            f.elements.FirstName.value = row.dataset.first;
            f.elements.LastName.value = row.dataset.last;
            f.elements.Email.value = row.dataset.email;
            f.elements.PhoneNumber.value = row.dataset.phone;
            f.elements.username.value = row.dataset.username;
            f.elements.username.required = staff;
            // staff change their username, members change their email
            $$('.acct-staff-only', td).forEach(e => { e.hidden = !staff; });
            $$('.acct-member-only', td).forEach(e => { e.hidden = staff; });
        }
        const first = $('input:not([type=hidden])', f);
        if (first) first.focus();
    }

    // ---- the panels that come from the server (Bookings / Book) -----------------
    // Scripts in HTML that is put in with innerHTML do not run, so each one is replaced by a fresh copy that does.
    function runScripts(root) {
        $$('script', root).forEach(old => {
            const s = document.createElement('script');
            s.text = old.textContent;
            old.replaceWith(s);
        });
    }

    async function loadRemote(kind, row, td) {
        const base = kind === 'book' ? area().dataset.bookUrl : area().dataset.bookingsUrl;
        try {
            const res = await fetch(base + '?userId=' + encodeURIComponent(row.dataset.id), { credentials: 'same-origin' });
            td.innerHTML = await res.text();
            runScripts(td);
        } catch (e) {
            td.textContent = 'Something went wrong. Refresh the page and try again.';
        }
    }

    function openRemote(kind, row) {
        const same = panelRow && panelRow.previousElementSibling === row && panelRow.dataset.kind === kind;
        closePanel();
        $('#newPanel').hidden = true;
        if (same) return;   // clicking the same button again closes it

        const tr = document.createElement('tr');
        tr.className = 'acct-panel';
        tr.dataset.kind = kind;
        const td = document.createElement('td');
        td.colSpan = 5;
        td.textContent = 'Loading...';
        tr.appendChild(td);
        row.after(tr);
        row.classList.add('table-active');
        panelRow = tr;
        loadRemote(kind, row, td);
    }

    // The booking buttons (Edit, Tables, Confirm, Seat, Complete, Cancel, Delete) in a member's list save in the
    // background, show a small message, then the list is loaded again.
    window.bkRefresh = function (form) {
        const row = panelRow ? panelRow.previousElementSibling : null;
        const td = panelRow ? panelRow.firstElementChild : null;
        fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin' })
            .then(r => r.text())
            .then(html => {
                const doc = parseHtml(html);
                doc.querySelectorAll('.acct-err').forEach(x => x.remove());   // this page's own hidden error boxes are not the message
                showMessage(doc);
                if (row && td && td.isConnected) return loadRemote('bookings', row, td);
            })
            .catch(() => form.submit());
    };

    // ---- save in the background, then refresh the lists ------------------------
    async function reload() {
        const res = await fetch(location.pathname + location.search, { headers: { 'X-Requested-With': 'fetch' } });
        const doc = new DOMParser().parseFromString(await res.text(), 'text/html');
        const fresh = doc.getElementById('acctArea');
        if (!fresh) return;
        area().replaceWith(fresh);
        panelRow = null;
        applyFilter(false);

        // a booking was just made: show that member's bookings
        if (reopen) {
            const row = $$('tr.acct-row').find(r => r.dataset.id === reopen);
            reopen = null;
            if (row) openRemote('bookings', row);
        }
    }

    async function send(url, data, form, onOk) {
        msg.hidden = true;
        const err = form ? $('.acct-err', form) : null;
        if (err) err.hidden = true;
        const buttons = form ? $$('button', form) : [];
        buttons.forEach(b => { b.disabled = true; });
        try {
            const res = await fetch(url, { method: 'POST', body: data, headers: { 'X-Requested-With': 'fetch' } });
            const json = await res.json();
            if (!json.ok) {
                if (err) { err.textContent = json.message; err.hidden = false; }
                else say(json.message, false);
                return;
            }
            if (onOk) onOk();
            say(json.message, true);
            await reload();
        } catch (e) {
            const text = 'Something went wrong. Refresh the page and try again.';
            if (err) { err.textContent = text; err.hidden = false; } else say(text, false);
        } finally {
            buttons.forEach(b => { b.disabled = false; });
        }
    }

    // ---- events ----------------------------------------------------------------
    document.addEventListener('click', e => {
        const el = e.target.closest('[data-act]');
        if (!el) return;
        const act = el.dataset.act;

        if (act === 'tab') {
            setTab(el.dataset.tab);
            applyFilter(false);
        } else if (act === 'new') {
            const p = $('#newPanel');
            closePanel();
            p.hidden = !p.hidden;
            if (!p.hidden) { const i = $('input:not([type=hidden])', p); if (i) i.focus(); }
        } else if (act === 'edit' || act === 'reset' || act === 'delete') {
            openPanel(act, el.closest('tr'));
        } else if (act === 'bookings' || act === 'book') {
            openRemote(act, el.closest('tr'));
        } else if (act === 'cancel') {
            const np = el.closest('#newPanel');
            if (np) { np.hidden = true; $('form', np).reset(); $('.acct-err', np).hidden = true; }
            else closePanel();
        } else if (act === 'toggle') {
            const row = el.closest('tr');
            const fd = new FormData();
            fd.append('id', row.dataset.id);
            fd.append('__RequestVerificationToken', token());
            el.disabled = true;
            send(area().dataset.toggleUrl, fd, null).finally(() => { el.disabled = false; });
        }
    });

    document.addEventListener('submit', e => {
        const f = e.target.closest('form[data-acct]');
        if (!f) return;
        e.preventDefault();
        const kind = f.dataset.acct;
        const after = kind === 'create' ? () => setTab('staff')
            : kind === 'book' ? () => { reopen = f.elements.userId.value; }
            : null;
        send(f.action, new FormData(f), f, after);
    });

    search.addEventListener('input', () => applyFilter(true));

    applyFilter(false);
})();