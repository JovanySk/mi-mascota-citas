// Loads the free slots for the chosen date + service with AJAX (no page reload)
// and renders them as radio buttons named "StartTime".
$(function () {
    const $date = $('#Date');
    const $service = $('#ServiceId');
    const $slots = $('#slots');
    const $message = $('#slots-message');
    const availabilityUrl = $slots.data('availability-url');
    let preselected = $slots.data('selected'); // keeps the choice after a failed submit

    function showMessage(text, isError) {
        $message.text(text).toggleClass('text-danger', !!isError);
    }

    function loadSlots() {
        const date = $date.val();
        const serviceId = $service.val();
        $slots.empty();

        if (!date || !serviceId) {
            showMessage('Elige un servicio y una fecha para ver los horarios disponibles.');
            return;
        }

        showMessage('Cargando horarios…');

        $.getJSON(availabilityUrl, { date: date, serviceId: serviceId })
            .done(function (data) {
                if (!data.slots.length) {
                    showMessage(data.message || 'No hay horarios disponibles.', true);
                    return;
                }

                data.slots.forEach(function (slot, index) {
                    const id = 'slot-' + index;
                    $('<input>', {
                        type: 'radio', name: 'StartTime', id: id, value: slot.time,
                        'class': 'btn-check', autocomplete: 'off',
                        checked: slot.time === preselected
                    }).appendTo($slots);
                    $('<label>', {
                        'for': id, 'class': 'slot-btn', title: slot.label, text: slot.time
                    }).appendTo($slots);
                });

                showMessage(data.slots.length + ' horario(s) disponible(s). Elige uno.');
            })
            .fail(function () {
                showMessage('No se pudieron cargar los horarios. Intenta de nuevo.', true);
            });
    }

    $date.on('change', function () { preselected = null; loadSlots(); });
    $service.on('change', function () { preselected = null; loadSlots(); });
    loadSlots();
});
