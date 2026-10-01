(() => {
    "use strict";

    // Native modal dialogs provide Escape handling and contain keyboard focus.
    // Only a confirmed, originally valid form submission is replayed.
    const pending = new WeakMap();
    const approvedForms = new WeakSet();

    function openConfirmation(trigger, form = null) {
        const dialog = document.getElementById(trigger.dataset.confirmDialog);
        if (!(dialog instanceof HTMLDialogElement) || !dialog.matches("[data-confirm-dialog-panel]")) {
            return;
        }
        if (dialog.open) return;
        pending.set(dialog, { trigger, form, confirmed: false });
        dialog.showModal();
        dialog.querySelector("[data-dialog-cancel]").focus();
    }

    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (form.dataset.submitting === "true") {
            event.preventDefault();
            return;
        }
        if (approvedForms.delete(form)) return;
        const trigger = event.submitter;
        if (!trigger?.matches("[data-confirm-dialog]")) return;
        event.preventDefault();
        openConfirmation(trigger, form);
    });

    document.addEventListener("click", (event) => {
        const trigger = event.target.closest("button[type='button'][data-confirm-dialog]");
        if (trigger) openConfirmation(trigger);
    });

    document.querySelectorAll("[data-confirm-dialog-panel]").forEach((dialog) => {
        dialog.addEventListener("keydown", (event) => {
            if (event.key !== "Tab") return;
            const cancel = dialog.querySelector("[data-dialog-cancel]");
            const confirm = dialog.querySelector("[data-dialog-confirm]");
            if (event.shiftKey && document.activeElement === cancel) {
                event.preventDefault();
                confirm.focus();
            } else if (!event.shiftKey && document.activeElement === confirm) {
                event.preventDefault();
                cancel.focus();
            }
        });
        dialog.querySelector("[data-dialog-cancel]").addEventListener("click", () => dialog.close());
        dialog.querySelector("[data-dialog-confirm]").addEventListener("click", () => {
            const action = pending.get(dialog);
            if (action) action.confirmed = true;
            dialog.close();
        });
        dialog.addEventListener("close", () => {
            const action = pending.get(dialog);
            pending.delete(dialog);
            if (!action) return;
            if (action.trigger.isConnected) action.trigger.focus();
            if (!action.confirmed) return;
            if (action.form?.isConnected && action.trigger.isConnected) {
                approvedForms.add(action.form);
                try {
                    action.form.requestSubmit(action.trigger);
                } finally {
                    approvedForms.delete(action.form);
                }
            } else {
                action.trigger.dispatchEvent(new CustomEvent("ui:confirmed", { bubbles: true }));
            }
        });
    });

    // Switch only between caller-supplied dates. Hidden slots cannot be submitted.
    document.querySelectorAll("[data-slot-picker]").forEach((picker) => {
        picker.addEventListener("click", (event) => {
            const date = event.target.closest("[data-slot-date]");
            if (!date || date.matches(":disabled") || date.getAttribute("aria-pressed") === "true") return;
            picker.querySelectorAll("[data-slot-date]").forEach((choice) => {
                const selected = choice === date;
                choice.setAttribute("aria-pressed", String(selected));
                choice.querySelector(".date-selection-label").hidden = !selected;
            });
            picker.querySelectorAll("[data-slot-panel]").forEach((panel) => {
                const selected = panel.id === date.getAttribute("aria-controls");
                panel.hidden = !selected;
                panel.disabled = !selected;
                panel.querySelectorAll("input[type='radio']").forEach((radio) => { radio.checked = false; });
            });
        });
    });

    // Run after confirmation and form validation. Keep the submitter enabled so its
    // name/value is included; guard further submissions while navigation is pending.
    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!form.matches("[data-single-submit]") || event.defaultPrevented) return;
        if (form.dataset.submitting === "true") {
            event.preventDefault();
            return;
        }
        queueMicrotask(() => {
            if (event.defaultPrevented) return;
            form.dataset.submitting = "true";
            form.setAttribute("aria-busy", "true");
        });
    });
    window.addEventListener("pageshow", () => {
        document.querySelectorAll("[data-single-submit]").forEach((form) => {
            delete form.dataset.submitting;
            form.removeAttribute("aria-busy");
        });
    });
    const summary = document.querySelector("[data-validation-summary].validation-summary-errors");
    if (summary) summary.focus();
})();
