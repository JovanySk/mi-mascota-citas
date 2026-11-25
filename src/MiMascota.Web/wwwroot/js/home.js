// Home page behaviour: agenda filters, quick-booking slots (AJAX), count-up numbers
// and the "todos" pets hopping when they scroll into view.
(function () {
    'use strict';

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ── Agenda de hoy: filter chips (client side, the list is already on the page) ──
    const group = document.querySelector('[data-filter-group]');
    const list = document.querySelector('[data-filter-list]');
    if (group && list) {
        const empty = document.querySelector('.filter-empty');
        group.addEventListener('click', function (event) {
            const chip = event.target.closest('[data-filter]');
            if (!chip) return;
            const filter = chip.dataset.filter;
            group.querySelectorAll('[data-filter]').forEach(function (c) {
                const active = c === chip;
                c.classList.toggle('is-active', active);
                c.setAttribute('aria-pressed', active ? 'true' : 'false');
            });
            let visible = 0;
            list.querySelectorAll('li[data-status]').forEach(function (row) {
                const show = filter === 'all' || row.dataset.status === filter;
                row.hidden = !show;
                if (show) visible++;
            });
            if (empty) empty.hidden = visible > 0;
        });
    }

    // ── Nueva cita rápida: reuse GET /Appointments/Availability ──
    const slotsBox = document.getElementById('quick-slots');
    const bookLink = document.getElementById('quick-book');
    if (slotsBox && bookLink) {
        const d = slotsBox.dataset;
        const toMinutes = function (hhmm) { const p = hhmm.split(':'); return (+p[0]) * 60 + (+p[1]); };
        const toTime = function (m) { return String(Math.floor(m / 60)).padStart(2, '0') + ':' + String(m % 60).padStart(2, '0'); };
        const duration = +d.duration;
        const step = +d.step;

        // Every start time on the grid; the ones the server does not return are taken.
        const grid = [];
        for (let m = toMinutes(d.open); m + duration <= toMinutes(d.close); m += step) grid.push(toTime(m));

        function select(button) {
            slotsBox.querySelectorAll('.slot-btn').forEach(function (b) {
                b.classList.toggle('is-selected', b === button);
                b.setAttribute('aria-pressed', b === button ? 'true' : 'false');
            });
            const start = button.dataset.time;
            const end = toTime(toMinutes(start) + duration);
            const url = new URL(d.createUrl, window.location.origin);
            url.searchParams.set('serviceId', d.serviceId);
            url.searchParams.set('date', d.date);
            url.searchParams.set('startTime', start);
            bookLink.href = url.pathname + url.search;
            bookLink.textContent = 'Agendar ' + start + ' – ' + end;
        }

        const url = d.availabilityUrl + '?date=' + encodeURIComponent(d.date) + '&serviceId=' + encodeURIComponent(d.serviceId);
        fetch(url, { headers: { 'Accept': 'application/json' } })
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
            .then(function (data) {
                slotsBox.textContent = '';
                if (!data.slots.length) {
                    const msg = document.createElement('span');
                    msg.className = 'quick-loading';
                    msg.textContent = data.message || 'No quedan horarios para ese día.';
                    slotsBox.appendChild(msg);
                    return;
                }
                const free = new Set(data.slots.map(function (s) { return s.time; }));
                let first = null;
                grid.forEach(function (time) {
                    const b = document.createElement('button');
                    b.type = 'button';
                    b.className = 'slot-btn';
                    b.textContent = time;
                    b.dataset.time = time;
                    if (free.has(time)) {
                        b.setAttribute('aria-pressed', 'false');
                        b.addEventListener('click', function () { select(b); });
                        first = first || b;
                    } else {
                        b.disabled = true;
                        b.classList.add('is-taken');
                        b.setAttribute('aria-label', time + ' (ocupado)');
                    }
                    slotsBox.appendChild(b);
                });
                if (first) select(first);
            })
            .catch(function () {
                slotsBox.innerHTML = '<span class="quick-loading">No se pudieron cargar los horarios.</span>';
            });
    }

    // ── Count-up on the hero info cards ──
    document.querySelectorAll('[data-count]').forEach(function (el) {
        const target = +el.dataset.count;
        if (reduceMotion || !target) return;
        const start = performance.now();
        const ms = 900;
        el.textContent = '0';
        function tick(now) {
            const t = Math.min(1, (now - start) / ms);
            el.textContent = Math.round(target * (1 - Math.pow(1 - t, 3)));
            if (t < 1) requestAnimationFrame(tick);
        }
        setTimeout(function () { requestAnimationFrame(function (now) { tick(now); }); }, 500);
    });

    // ── "todos": pets hop one by one when the section enters the viewport ──
    const stages = document.querySelectorAll('[data-reveal]');
    if (!('IntersectionObserver' in window) || reduceMotion) {
        stages.forEach(function (s) { s.classList.add('is-visible'); });
    } else {
        const io = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    io.unobserve(entry.target);
                }
            });
        }, { threshold: 0.35 });
        stages.forEach(function (s) { io.observe(s); });
    }
})();
