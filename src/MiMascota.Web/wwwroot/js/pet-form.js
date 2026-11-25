// Pet form helpers, shared by Pets/Form and the quick-registration modal of Nueva cita:
//  - species chips: "Otro" shows a text input; without a photo the preview shows the species' avatar.
//  - photo: live round preview with FileReader, size/type check before uploading, "Quitar foto".
(function ($) {
    const MAX_BYTES = 5 * 1024 * 1024;
    const TYPES = ['image/jpeg', 'image/png', 'image/webp'];

    function init(root) {
        const $root = $(root);
        const $field = $root.find('[data-photo-field]');
        const $input = $field.find('[data-photo-input]');
        const $img = $field.find('[data-photo-preview] img');
        const $preview = $field.find('[data-photo-preview]');
        const $remove = $field.find('[data-photo-remove]');
        const $removeFlag = $field.find('[data-photo-remove-flag]');
        const $error = $field.find('[data-photo-error]');
        const $pickText = $field.find('[data-photo-pick-text]');
        const $other = $root.find('[data-species-other]');

        function hasPhoto() {
            return $preview.hasClass('has-photo');
        }

        function illustration() {
            const $checked = $root.find('[data-species-picker] input:checked');
            return $checked.data('avatar') || $img.data('illustration');
        }

        function showIllustration() {
            $preview.removeClass('has-photo');
            $img.attr('src', illustration());
            $remove.prop('hidden', true);
            $pickText.text('Tomar o elegir foto');
        }

        function showError(text) {
            $error.text(text || '').toggleClass('field-validation-error', !!text);
        }

        $root.find('[data-species-picker] input').on('change', function () {
            const isOther = this.value === 'Otro';
            $other.prop('hidden', !isOther);
            if (isOther) $other.find('input').trigger('focus');
            if (!hasPhoto()) $img.attr('src', illustration());
        });

        $input.on('change', function () {
            const file = this.files && this.files[0];
            showError('');
            if (!file) return;

            if (TYPES.indexOf(file.type) < 0) {
                showError('El archivo no es una imagen válida. Usa una foto JPG, PNG o WebP.');
                this.value = '';
                return;
            }
            if (file.size > MAX_BYTES) {
                showError('La foto pesa más de 5 MB. Elige una imagen más ligera.');
                this.value = '';
                return;
            }

            const reader = new FileReader();
            reader.onload = function (e) {
                $img.attr('src', e.target.result);
                $preview.addClass('has-photo');
                $remove.prop('hidden', false);
                $removeFlag.val('false');
                $pickText.text('Cambiar foto');
            };
            reader.readAsDataURL(file);
        });

        $remove.on('click', function () {
            $input.val('');
            showError('');
            // A photo already saved on the server is deleted when the form is saved.
            if ($img.data('saved')) $removeFlag.val('true');
            showIllustration();
        });
    }

    /** Back to the initial state (used when the modal is reopened). */
    function reset(root) {
        const $root = $(root);
        $root.find('[data-species-other]').prop('hidden', true);
        $root.find('[data-photo-input]').val('');
        $root.find('[data-photo-remove-flag]').val('false');
        $root.find('[data-photo-error]').text('');
        const $img = $root.find('[data-photo-preview] img');
        $root.find('[data-photo-preview]').removeClass('has-photo');
        $img.attr('src', $img.data('illustration'));
        $root.find('[data-photo-remove]').prop('hidden', true);
        $root.find('[data-photo-pick-text]').text('Tomar o elegir foto');
    }

    window.MiMascotaPetForm = { init: init, reset: reset };

    $(function () {
        $('[data-pet-form]').each(function () { init(this); });
    });
})(jQuery);
