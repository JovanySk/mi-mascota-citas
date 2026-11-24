// Any form with a data-confirm attribute asks for confirmation before submitting.
$(document).on('submit', 'form[data-confirm]', function () {
    return confirm($(this).data('confirm'));
});
