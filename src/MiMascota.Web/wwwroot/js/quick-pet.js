// "Registrar mascota nueva" modal of the booking form.
// Sends the form (with the photo and the antiforgery token) to POST /Pets/QuickCreate with fetch + FormData.
// On success the new pet is added to the drop-down and selected; on error the server's Spanish messages
// are shown next to each field.
$(function () {
    const modalEl = document.getElementById('quick-pet-modal');
    if (!modalEl) return;

    const $form = $('#quick-pet-form');
    const $submit = $form.find('[data-submit]');
    const $summary = $form.find('[data-error-summary]');
    const $petSelect = $('#PetId');
    const $petAvatar = $('[data-pet-avatar] img');
    const $success = $('#quick-pet-success');
    const submitHtml = $submit.html();

    window.MiMascotaPetForm.init($form);

    // This form is validated by the server (JSON errors), not by jquery-validation-unobtrusive.
    $form.off('.validate').off('.unobtrusiveValidation').removeData('validator').removeData('unobtrusiveValidation');

    // Avatar next to the drop-down follows the selected pet.
    function syncAvatar() {
        const avatar = $petSelect.find('option:selected').data('avatar');
        $petAvatar.attr('src', avatar || $petAvatar.data('empty'));
    }
    $petSelect.on('change', syncAvatar);

    // "Ya es cliente" / "Dueño nuevo"
    function syncOwnerMode() {
        const isNew = $form.find('input[name="NewOwner"]:checked').val() === 'true';
        $form.find('[data-owner-existing]').prop('hidden', isNew);
        $form.find('[data-owner-new]').prop('hidden', !isNew);
    }
    $form.find('input[name="NewOwner"]').on('change', function () {
        syncOwnerMode();
        $form.find(this.value === 'true' ? '#qp-owner-name' : '#qp-owner').trigger('focus');
    });
    syncOwnerMode();

    function clearErrors() {
        $form.find('[data-error-for]').text('');
        $form.find('.input-validation-error').removeClass('input-validation-error');
        $form.find('[data-photo-error]').text('');
        $summary.prop('hidden', true).empty();
    }

    function showErrors(errors) {
        const loose = [];
        $.each(errors || {}, function (field, messages) {
            const text = messages.join(' ');
            const $slot = field === 'Photo'
                ? $form.find('[data-photo-error]')
                : $form.find('[data-error-for="' + field + '"]');
            if ($slot.length) {
                $slot.text(text);
                $form.find('[name="' + field + '"]').not(':radio').addClass('input-validation-error');
            } else {
                loose.push(text);
            }
        });
        if (loose.length) {
            $summary.text(loose.join(' ')).prop('hidden', false);
        }
        const $first = $form.find('.input-validation-error').first();
        if ($first.length) $first.trigger('focus');
    }

    $form.on('submit', function (e) {
        e.preventDefault();
        clearErrors();
        $submit.prop('disabled', true).text('Guardando…');

        fetch(this.action, {
            method: 'POST',
            body: new FormData(this), // includes __RequestVerificationToken and the photo
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (response) {
                return response.json().then(function (data) { return { ok: response.ok, data: data }; },
                    function () { return { ok: false, data: null }; });
            })
            .then(function (result) {
                if (!result.ok) {
                    if (result.data && result.data.errors) {
                        showErrors(result.data.errors);
                    } else {
                        $summary.text('No se pudo registrar la mascota. Intenta de nuevo.').prop('hidden', false);
                    }
                    return;
                }

                const pet = result.data;
                $('<option>', { value: pet.id, text: pet.label, 'data-avatar': pet.avatar }).appendTo($petSelect);
                $petSelect.val(String(pet.id)).trigger('change');
                $petSelect.removeClass('input-validation-error');
                $('[data-valmsg-for="PetId"]').text('');

                $success.text(pet.message).prop('hidden', false);
                bootstrap.Modal.getOrCreateInstance(modalEl).hide();
            })
            .catch(function () {
                $summary.text('No hay conexión con el servidor. Intenta de nuevo.').prop('hidden', false);
            })
            .finally(function () {
                $submit.prop('disabled', false).html(submitHtml);
            });
    });

    // Every time the modal opens it starts empty.
    modalEl.addEventListener('show.bs.modal', function () {
        $form[0].reset();
        clearErrors();
        window.MiMascotaPetForm.reset($form);
        syncOwnerMode();
    });
    modalEl.addEventListener('shown.bs.modal', function () {
        $('#qp-name').trigger('focus');
    });
});
