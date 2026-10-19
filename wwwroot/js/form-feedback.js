(() => {
  "use strict";
  const $ = window.jQuery;
  if (!$?.validator || !window.bootstrap) return;
  const elements = (scope, selector) => [
    ...(scope.matches?.(selector) ? [scope] : []), ...scope.querySelectorAll(selector)
  ];
  const toasts = document.createElement("div");
  toasts.className = "toast-container position-fixed top-0 end-0 p-3 app-feedback-toasts";
  toasts.setAttribute("aria-live", "polite");
  toasts.setAttribute("aria-atomic", "false");
  document.body.append(toasts);
  const toast = (messages, error = true, persistent = false) => {
    const list = [...new Set(messages.filter(Boolean))];
    if (!list.length) return;
    const element = document.createElement("div");
    element.className = `toast ${error ? "app-feedback-error" : "app-feedback-success"}`;
    element.setAttribute("role", error ? "alert" : "status");
    const header = document.createElement("div");
    header.className = "toast-header";
    const title = document.createElement("strong");
    title.className = "me-auto";
    title.textContent = error ? "Revisa el formulario" : "Operacion completada";
    const close = document.createElement("button");
    close.type = "button";
    close.className = "btn-close";
    close.setAttribute("data-bs-dismiss", "toast");
    close.setAttribute("aria-label", "Cerrar aviso");
    header.append(title, close);
    const body = document.createElement("div");
    body.className = "toast-body";
    const ul = document.createElement("ul");
    ul.className = "mb-0 ps-3";
    for (const message of list.slice(0, 5)) {
      const li = document.createElement("li");
      li.textContent = message;
      ul.append(li);
    }
    if (list.length > 5) {
      const li = document.createElement("li");
      li.textContent = `Hay ${list.length - 5} errores adicionales. Revisa los campos marcados.`;
      ul.append(li);
    }
    body.append(ul);
    element.append(header, body);
    if (error) for (const previous of toasts.querySelectorAll(".app-feedback-error")) {
      window.bootstrap.Toast.getInstance(previous)?.dispose(); previous.remove();
    }
    toasts.append(element);
    const instance = new window.bootstrap.Toast(element, { autohide: !error && !persistent, delay: 7000 });
    element.addEventListener("hidden.bs.toast", () => { instance.dispose(); element.remove(); });
    instance.show();
  };
  const modal = document.createElement("div");
  modal.className = "modal fade";
  modal.tabIndex = -1;
  modal.setAttribute("aria-labelledby", "form-confirm-title");
  modal.setAttribute("aria-describedby", "form-confirm-message");
  modal.innerHTML = '<div class="modal-dialog modal-dialog-centered"><div class="modal-content"><div class="modal-header"><h2 id="form-confirm-title" class="modal-title fs-5">Confirmar accion</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Cerrar"></button></div><div class="modal-body" id="form-confirm-message"></div><div class="modal-footer"><button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal" data-confirm-cancel>Cancelar</button><button type="button" class="btn btn-danger" data-confirm-accept>Confirmar</button></div></div></div>';
  document.body.append(modal);
  const dialog = new window.bootstrap.Modal(modal);
  let confirmationPending = false;
  const confirm = (message) => new Promise(resolve => {
    if (confirmationPending) { resolve(false); return; }
    confirmationPending = true;
    let accepted = false;
    const accept = () => { accepted = true; dialog.hide(); };
    const acceptButton = modal.querySelector("[data-confirm-accept]");
    modal.querySelector("#form-confirm-message").textContent = message;
    acceptButton.addEventListener("click", accept);
    modal.addEventListener("shown.bs.modal", () => modal.querySelector("[data-confirm-cancel]").focus(), { once: true });
    modal.addEventListener("hidden.bs.modal", () => {
      acceptButton.removeEventListener("click", accept);
      confirmationPending = false; resolve(accepted);
    }, { once: true });
    dialog.show();
  });
  const value = (form, field) => form.elements.namedItem(`Input.${field}`)?.value.trim() ?? "";
  const originalValue = (original, field) => original?.[field] == null ? "" :
    field === "FechaVerificacion" ? original[field].slice(0, 10) : String(original[field]);
  const changed = (form, original, field) => !original ||
    value(form, field).toUpperCase() !== originalValue(original, field).trim().toUpperCase();
  const caseError = (element) => {
    const form = element.form, field = element.name.split(".").pop(), original = form.caseOriginal;
    const current = element.value.trim(), different = changed(form, original, field);
    if (different && !current) return ({ Curp: "La CURP es obligatoria.", Cct: "La CCT es obligatoria.",
      NombreCompleto: "El nombre completo es obligatorio.", PeriodoInicio: "Captura el periodo de inicio.",
      PeriodoFin: "Captura el periodo de fin.", FechaVerificacion: "La fecha de verificacion es obligatoria." })[field];
    if (different && field === "Curp") {
      const text = current.toUpperCase();
      if (!new RegExp(form.dataset.curpPattern).test(text)) return "Captura una CURP con formato valido de 18 caracteres y fecha valida.";
      const year = Number(text.slice(4, 6)) + (/\d/.test(text[16]) ? 1900 : 2000);
      const month = Number(text.slice(6, 8)), day = Number(text.slice(8, 10));
      const date = new Date(Date.UTC(year, month - 1, day));
      if (date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) return "La fecha incluida en la CURP no es valida.";
    }
    if (different && field === "Cct" && !new RegExp(form.dataset.cctPattern).test(current.toUpperCase()))
      return "Captura una CCT como 30PBH0442H, 184-D2 o 327-DV.";
    if (field === "PeriodoFin" && (different || changed(form, original, "PeriodoInicio")) && current &&
        value(form, "PeriodoInicio") && Number(current) < Number(value(form, "PeriodoInicio")))
      return "El periodo fin no puede ser menor al periodo inicio.";
    if (different && field === "FechaVerificacion" && current) {
      if (current > form.dataset.today) return "La fecha de verificacion no puede ser futura.";
      if (current < form.dataset.officeDate) return "La fecha de verificacion no puede ser anterior a la fecha del oficio.";
    }
    if (field === "NivelEducativo" && (form.usedLevels ?? []).includes(current))
      return `Ya existe un caso de ${current} para este oficio.`;
    return null;
  };
  $.validator.addMethod("casefield", (_, element) => !caseError(element), (_, element) => caseError(element));
  $.validator.addMethod("officedate", (current, element) => !current || current === element.form.dataset.originalDate ||
    current <= element.form.dataset.today, "La fecha del oficio no puede ser futura.");
  $.validator.addMethod("officenumber", (_, element) => !element.numberError, (_, element) => element.numberError);
  const fileError = element => {
    const files = [...element.files];
    if (!files.length) return "Selecciona al menos un archivo.";
    const types = element.dataset.fileTypes.split(",");
    if (files.some(file => !file.size)) return "No se permiten archivos vacios.";
    if (files.some(file => !types.some(type => file.name.toLowerCase().endsWith(type))))
      return `Tipos permitidos: ${types.join(", ")}.`;
    if (files.some(file => file.size > Number(element.dataset.fileMax))) return "Un archivo excede el tamano maximo indicado.";
    return null;
  };
  $.validator.addMethod("fileupload", (_, element) => !fileError(element), (_, element) => fileError(element));
  $.validator.addMethod("httpurl", function (current, element) {
    if (this.optional(element)) return true;
    try { const url = new URL(current); return ["http:", "https:"].includes(url.protocol) && !!url.hostname; }
    catch { return false; }
  });
  $.validator.unobtrusive.adapters.addBool("httpurl");
  $.extend($.validator.messages, {
    required: "Este campo es obligatorio.", number: "Captura un numero valido.", digits: "Captura solo digitos.",
    date: "Captura una fecha valida.", email: "Captura un correo valido.", url: "Captura una URL valida (http/https).",
    maxlength: $.validator.format("Captura hasta {0} caracteres."), minlength: $.validator.format("Captura al menos {0} caracteres."),
    range: $.validator.format("Captura un valor entre {0} y {1}."), equalTo: "Los valores no coinciden."
  });
  const numberChecks = new WeakMap();
  const checkNumber = async (form) => {
    const input = form.elements.namedItem("Input.NumeroOficio");
    if (!input || !form.dataset.numberCheck || !input.value.trim()) return true;
    const key = input.value.trim();
    if (numberChecks.get(input)?.key === key) return numberChecks.get(input).promise;
    const promise = (async () => {
      try {
        const url = new URL(form.dataset.numberCheck, window.location.origin);
        url.searchParams.set("numero", key);
        url.searchParams.set("id", form.elements.namedItem("Input.Id")?.value ?? "0");
        const response = await fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } });
        if (!response.ok || !response.headers.get("content-type")?.includes("application/json")) return true;
        const result = await response.json();
        if (input.value.trim() !== key || !form.isConnected) return false;
        input.numberError = result === true ? null : String(result);
        $(form).validate().element(input);
        return result === true;
      } catch { return true; } // The POST always validates against the database.
    })();
    numberChecks.set(input, { key, promise });
    return promise;
  };
  const initialize = (scope = document) => {
    if (!scope.isConnected && scope !== document) scope = document;
    for (const form of elements(scope, "form")) {
      if (form.method.toLowerCase() !== "post") continue;
      $.validator.unobtrusive.parse(form);
      const validator = $(form).validate();
      // Without JavaScript the server summary remains available in the HTML.
      for (const summary of form.querySelectorAll("[data-valmsg-summary]")) summary.hidden = true;
      validator.settings.onfocusout = function (element) { this.element(element); };
      validator.settings.onkeyup = false;
      validator.settings.normalizer = function (current) { return this.type === "password" ? current : current.trim(); };
      validator.settings.highlight = element => { element.classList.add("is-invalid"); element.setAttribute("aria-invalid", "true"); };
      validator.settings.unhighlight = element => { element.classList.remove("is-invalid"); element.setAttribute("aria-invalid", "false"); };
      $(form).off("invalid-form.formFeedback").on("invalid-form.formFeedback", (_, current) => {
        form.classList.remove("is-submitting"); toast(current.errorList.map(error => error.message));
      });
      if (form.dataset.caseForm) {
        form.caseOriginal = JSON.parse(form.dataset.caseOriginal ?? "null");
        form.usedLevels = JSON.parse(form.dataset.usedLevels ?? "[]");
        for (const field of ["Curp", "Cct", "NombreCompleto", "PeriodoInicio", "PeriodoFin", "FechaVerificacion", "NivelEducativo"]) {
          const input = form.elements.namedItem(`Input.${field}`);
          if (input) $(input).rules("add", { casefield: true });
        }
      }
      if (form.dataset.officeOriginal) {
        const original = JSON.parse(form.dataset.officeOriginal);
        if (original) for (const field of ["NumeroOficio", "Asunto"]) {
          const input = form.elements.namedItem(`Input.${field}`);
          $(input).rules("add", { required: { depends: () => input.value.trim() !== original[field].trim() } });
        }
      }
      if (form.elements.namedItem("Input.FechaOficio") && form.dataset.today)
        $(form.elements.namedItem("Input.FechaOficio")).rules("add", { officedate: true });
      if (form.dataset.numberCheck) $(form.elements.namedItem("Input.NumeroOficio")).rules("add", { officenumber: true });
      for (const file of form.querySelectorAll("input[type=file][data-file-types]")) $(file).rules("add", { fileupload: true });
    }
    for (const feedback of elements(scope, "[data-feedback]")) {
      if (feedback.dataset.feedbackShown) continue;
      feedback.dataset.feedbackShown = "true";
      toast([feedback.textContent.trim()], feedback.dataset.feedback !== "success", feedback.hasAttribute("data-feedback-persistent")); feedback.hidden = true;
    }
    for (const errors of elements(scope, "[data-form-errors]")) {
      if (errors.dataset.feedbackShown) continue;
      errors.dataset.feedbackShown = "true";
      toast([...errors.querySelectorAll("span")].map(span => span.textContent));
    }
  };
  document.addEventListener("focusout", event => {
    const input = event.target;
    if (!input.form) return;
    if (input.name === "Input.NumeroOficio") void checkNumber(input.form);
    if (input.name === "Input.PeriodoInicio" && input.form.dataset.caseForm)
      $(input.form).validate().element(input.form.elements.namedItem("Input.PeriodoFin"));
  });
  document.addEventListener("input", event => {
    if (event.target.name === "Input.NumeroOficio") { event.target.numberError = null; numberChecks.delete(event.target); }
  });
  const approved = new WeakSet(), submitting = new WeakSet();
  // Capture before HTMX/loading handlers: invalid/cancelled forms send no POST.
  document.addEventListener("submit", event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || form.method.toLowerCase() !== "post") return;
    const validator = $(form).data("validator");
    if (validator && !validator.form()) {
      event.preventDefault(); event.stopImmediatePropagation(); validator.focusInvalid(); return;
    }
    if (approved.has(form)) { approved.delete(form); return; }
    if (!form.dataset.confirm && !form.dataset.numberCheck) return;
    event.preventDefault(); event.stopImmediatePropagation(); form.classList.remove("is-submitting");
    if (submitting.has(form)) return;
    submitting.add(form);
    const submitter = event.submitter;
    void (async () => {
      try {
        if (!await checkNumber(form)) { validator?.focusInvalid(); toast(["Revisa el numero de oficio."]); return; }
        if (form.dataset.confirm && !await confirm(form.dataset.confirm)) { submitter?.focus(); return; }
        if (!form.isConnected) return;
        approved.add(form); form.requestSubmit(submitter?.isConnected ? submitter : undefined);
      } finally { submitting.delete(form); }
    })();
  }, true);
  initialize();
  document.addEventListener("htmx:afterSwap", event => initialize(event.detail.target ?? document));
  document.addEventListener("htmx:historyRestore", () => initialize());
})();
