$(function () {
    'use strict';
    var $ = window.jQuery; // runs after jquery.validate.unobtrusive has parsed the form

    var CHECK_SVG = '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3.5 8.5l3 3 6-7"/></svg>';
    var TOTAL_STEPS = 4;
    var current = 1;

    var $form = $('#registerForm');
    var $dob = $('#DateOfBirth');
    var $age = $('#Age');
    var $philhealth = $('#PhilHealthNo');

    /* ---------- Age ---------- */
    function parseLocalDate(value) {
        // "yyyy-MM-dd" -> local date (avoids the UTC shift of new Date(string)).
        var p = value.split('-');
        return p.length === 3 ? new Date(+p[0], +p[1] - 1, +p[2]) : null;
    }

    function computeAge(dob) {
        var today = new Date();
        var age = today.getFullYear() - dob.getFullYear();
        var m = today.getMonth() - dob.getMonth();
        if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) age--;
        return age;
    }

    function startOfToday() {
        var t = new Date();
        return new Date(t.getFullYear(), t.getMonth(), t.getDate());
    }

    function updateAge() {
        var dob = parseLocalDate($dob.val());
        if (!dob || isNaN(dob) || dob > startOfToday()) {
            $age.val('');
            return;
        }
        var years = computeAge(dob);
        $age.val(years + (years === 1 ? ' year old' : ' years old'));
    }

    // Reject future dates in jQuery Validation (also flags it as soon as the value changes).
    $.validator.addMethod('notfuture', function (value, element) {
        if (!value) return true;
        var d = parseLocalDate(value);
        return !!d && !isNaN(d) && d <= startOfToday();
    }, 'Date of birth cannot be in the future.');

    $dob.rules('add', { notfuture: true });

    $dob.on('change input', function () {
        updateAge();
        $dob.valid();
    });

    /* ---------- PhilHealth No. mask: XX-XXXXXXXXX-X ---------- */
    function formatPhilHealth(raw) {
        var d = raw.replace(/\D/g, '').slice(0, 12);
        if (d.length > 11) return d.slice(0, 2) + '-' + d.slice(2, 11) + '-' + d.slice(11);
        if (d.length > 2) return d.slice(0, 2) + '-' + d.slice(2);
        return d;
    }

    $philhealth.on('input', function () {
        this.value = formatPhilHealth(this.value);
    });
    $philhealth.val(formatPhilHealth($philhealth.val() || ''));

    /* ---------- Stepper ---------- */
    function renderStepper() {
        $('#stepper .pr-step').each(function () {
            var n = +$(this).data('step');
            var $circle = $(this).find('.pr-step-circle');
            $(this).toggleClass('active', n === current).toggleClass('completed', n < current);
            $circle.html(n < current ? CHECK_SVG : n);
        });

        $('.pr-section').each(function () {
            $(this).toggleClass('d-none', +$(this).data('step') !== current);
        });

        $('#btnBack').toggleClass('d-none', current === 1);
        $('#btnNext').toggleClass('d-none', current === TOTAL_STEPS);
        $('#btnSubmit').toggleClass('d-none', current !== TOTAL_STEPS);
    }

    function validateCurrentStep() {
        var validator = $form.validate();
        var ok = true;
        // Only the inputs inside the visible section; other steps must not block progress.
        $('.pr-section[data-step="' + current + '"]').find('input, select, textarea').each(function () {
            if (!validator.element(this)) ok = false;
        });
        return ok;
    }

    $('#btnNext').on('click', function () {
        if (!validateCurrentStep()) {
            $('.pr-section[data-step="' + current + '"] .input-validation-error').first().trigger('focus');
            return;
        }
        if (current < TOTAL_STEPS) {
            current++;
            renderStepper();
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }
    });

    $('#btnBack').on('click', function () {
        if (current > 1) {
            current--;
            renderStepper();
        }
    });

    $('#btnCancel').on('click', function () {
        if (window.confirm('Cancel this registration? Any information you entered will be lost.')) {
            window.location.href = window.patientRegister.directoryUrl;
        }
    });

    // If the server re-rendered the page with errors, they're all on step 1.
    updateAge();
    renderStepper();
});
