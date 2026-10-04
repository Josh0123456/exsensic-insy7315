(() => {
    "use strict";

    // Native modal dialogs provide Escape handling and contain keyboard focus.
    // Only a confirmed, originally valid form submission is replayed.
    const pending = new WeakMap();
    const approvedForms = new WeakSet();
    const buttonContents = new WeakMap();

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
        if (form.dataset.submitting === "true" || Number(form.dataset.retryUntil || 0) > Date.now()) {
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
            form.querySelectorAll("button[type=submit]").forEach((button) => {
                buttonContents.set(button, [...button.childNodes]);
                button.setAttribute("aria-disabled", "true");
                button.textContent = "Please wait…";
            });
            document.getElementById("action-status").textContent = "Submitting your request. Please wait.";
        });
    });
    window.addEventListener("pageshow", () => {
        document.querySelectorAll("[data-single-submit]").forEach((form) => {
            delete form.dataset.submitting;
            form.removeAttribute("aria-busy");
            form.querySelectorAll("button[type=submit]").forEach((button) => {
                button.removeAttribute("aria-disabled");
                if (buttonContents.has(button)) button.replaceChildren(...buttonContents.get(button));
                buttonContents.delete(button);
            });
        });
    });
    // Honor a server retry window without automatically repeating any mutation.
    const retrySeconds = Number(document.querySelector("[data-retry-seconds]")?.dataset.retrySeconds || 0);
    if (retrySeconds > 0) {
        const until = Date.now() + retrySeconds * 1000;
        const buttons = [...document.querySelectorAll("form[data-single-submit] button[type=submit]:not(:disabled)")];
        document.querySelectorAll("form[data-single-submit]").forEach(form => { form.dataset.retryUntil = String(until); });
        buttons.forEach(button => { button.disabled = true; });
        window.setTimeout(() => {
            buttons.forEach(button => { button.disabled = false; });
            document.getElementById("action-status").textContent = "You can try your request again now.";
        }, Math.min(retrySeconds * 1000, 2147483647));
    }

    // Focus a visible field even when its server key uses a prefix, dictionary key or radio group.
    document.addEventListener("click", event => {
        const link = event.target.closest("[data-error-field]");
        if (!link) return;
        const control = [...document.querySelectorAll("input, select, textarea")]
            .find(input => input.name.toLowerCase() === link.dataset.errorField.toLowerCase()
                && input.type !== "hidden" && !input.matches(":disabled"));
        if (control) { event.preventDefault(); control.focus(); }
    });

    function syncFieldErrors(form) {
        form.querySelectorAll("[data-valmsg-for]").forEach(message => {
            const controls = [...form.elements].filter(input => input.name === message.dataset.valmsgFor);
            if (!message.id && controls[0]?.id) message.id = controls[0].id + "-error";
            controls.forEach(control => {
                if (control.type === "hidden") return;
                const describedBy = new Set((control.getAttribute("aria-describedby") || "").split(/\s+/).filter(Boolean));
                if (message.id) describedBy.add(message.id);
                control.setAttribute("aria-describedby", [...describedBy].join(" "));
                control.setAttribute("aria-invalid", String(message.classList.contains("field-validation-error")));
            });
        });
    }

    // Template and slot forms use native constraints rather than duplicate client-side rules.
    document.addEventListener("invalid", event => {
        const control = event.target;
        if (!control.form) return;
        control.setAttribute("aria-invalid", "true");
        const errorId = (control.getAttribute("aria-describedby") || "").split(/\s+/).find(id => id.endsWith("-error"));
        const error = errorId && document.getElementById(errorId);
        if (error) { error.textContent = control.validationMessage; error.dataset.nativeError = "true"; }
        queueMicrotask(() => {
            const summary = document.querySelector("[data-validation-summary]");
            if (!summary) return;
            const list = document.createElement("ul"), names = new Set();
            [...control.form.elements].filter(input => input.willValidate && !input.validity.valid).forEach(input => {
                if (names.has(input.name)) return;
                names.add(input.name);
                const item = document.createElement("li"), link = document.createElement("a");
                link.href = "#" + input.id; link.dataset.errorField = input.name;
                const label = input.labels?.[0]?.textContent?.trim();
                link.textContent = (label ? label + ": " : "") + input.validationMessage;
                item.append(link); list.append(item);
            });
            summary.replaceChildren(list);
            summary.classList.remove("validation-summary-valid");
            summary.classList.add("validation-summary-errors");
            summary.focus();
        });
    }, true);
    document.addEventListener("input", event => {
        const control = event.target;
        if (!control.validity?.valid) return;
        control.setAttribute("aria-invalid", "false");
        for (const id of (control.getAttribute("aria-describedby") || "").split(/\s+/)) {
            const error = document.getElementById(id);
            if (error?.dataset.nativeError === "true") { error.textContent = ""; delete error.dataset.nativeError; }
        }
    });

    // Unobtrusive validation loads after this script. Bind when the complete page is ready.
    document.addEventListener("DOMContentLoaded", () => {
        document.querySelectorAll("form").forEach(syncFieldErrors);
        if (!window.jQuery) return;
        window.jQuery("form").on("invalid-form.validate", function (_event, validator) {
            const form = this;
            queueMicrotask(() => {
                syncFieldErrors(form);
                const summary = document.querySelector("[data-validation-summary]");
                if (!summary) return;
                const list = document.createElement("ul");
                validator.errorList.forEach(error => {
                    const item = document.createElement("li"), link = document.createElement("a");
                    link.href = "#" + error.element.id;
                    link.dataset.errorField = error.element.name;
                    link.textContent = error.message;
                    item.append(link); list.append(item);
                });
                summary.replaceChildren(list);
                summary.classList.remove("validation-summary-valid");
                summary.classList.add("validation-summary-errors");
                summary.focus();
            });
        });
        window.jQuery("form").on("focusout keyup change", "input,select,textarea", function () {
            queueMicrotask(() => syncFieldErrors(this.form));
        });
    });

    // Collapse the mobile navigation with Escape and return focus to its trigger.
    const navigation = document.getElementById("primary-navigation");
    navigation?.addEventListener("keydown", event => {
        if (event.key === "Escape" && navigation.classList.contains("show")) {
            window.bootstrap.Collapse.getOrCreateInstance(navigation).hide();
            document.querySelector(".navigation-toggle").focus();
        }
    });
    const summary = document.querySelector("[data-validation-summary].validation-summary-errors");
    if (summary) summary.focus();
})();
