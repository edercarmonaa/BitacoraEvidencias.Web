(() => {
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const auditViewerState = new WeakMap();
  const closeAdminDropdowns = () => {
    for (const openMenu of document.querySelectorAll(".app-admin-menu.show")) {
      openMenu.classList.remove("show");
      openMenu.closest(".dropdown")?.querySelector(".app-admin-toggle")?.setAttribute("aria-expanded", "false");
    }
  };

  const bindAdminDropdownFallback = () => {
    const toggles = document.querySelectorAll(".app-admin-toggle");
    for (const toggle of toggles) {
      if (toggle.dataset.adminDropdownFallbackBound === "true") {
        continue;
      }

      toggle.dataset.adminDropdownFallbackBound = "true";
      toggle.addEventListener("click", () => {
        window.setTimeout(() => {
          const menu = toggle.closest(".dropdown")?.querySelector(".app-admin-menu");
          if (!menu || menu.classList.contains("show")) {
            return;
          }

          closeAdminDropdowns();
          menu.classList.add("show");
          toggle.setAttribute("aria-expanded", "true");
        }, 0);
      });
    }
  };
  const bindLoadingForms = (scope = document) => {
    const forms = scope.querySelectorAll("form[data-ui-loading]");
    for (const form of forms) {
      if (form.dataset.loadingBound === "true") {
        continue;
      }

      form.dataset.loadingBound = "true";
      form.addEventListener("submit", (event) => {
        if (event.defaultPrevented) {
          return;
        }

        form.classList.add("is-submitting");
      });

      form.addEventListener("invalid", () => {
        form.classList.remove("is-submitting");
      }, true);
    }
  };

  const ensureAuditViewerState = (viewer) => {
    const existingState = auditViewerState.get(viewer);
    if (existingState) {
      return existingState;
    }

    const mainImage = viewer.querySelector("[data-audit-main-image]");
    const stage = viewer.querySelector("[data-audit-stage]");
    const canvas = viewer.querySelector("[data-audit-canvas]");
    if (!mainImage) {
      return null;
    }

    const state = {
      mainImage,
      stage,
      canvas,
      caption: viewer.querySelector("[data-audit-caption]"),
      thumbs: Array.from(viewer.querySelectorAll("[data-audit-thumb]")),
      scale: 1,
      rotation: 0,
    };

    if (mainImage.dataset.auditImageBound !== "true") {
      mainImage.dataset.auditImageBound = "true";
      mainImage.addEventListener("load", () => {
        applyAuditTransform(viewer);
      });
    }

    auditViewerState.set(viewer, state);
    return state;
  };

  const getAuditStageBounds = (stage) => {
    if (!stage) {
      return { width: 0, height: 0 };
    }

    const styles = window.getComputedStyle(stage);
    const horizontalPadding =
      Number.parseFloat(styles.paddingLeft || "0") +
      Number.parseFloat(styles.paddingRight || "0");
    const verticalPadding =
      Number.parseFloat(styles.paddingTop || "0") +
      Number.parseFloat(styles.paddingBottom || "0");

    return {
      width: Math.max(stage.clientWidth - horizontalPadding, 0),
      height: Math.max(stage.clientHeight - verticalPadding, 0),
    };
  };

  const applyAuditTransform = (viewer) => {
    const state = ensureAuditViewerState(viewer);
    if (!state || !state.stage || !state.canvas) {
      return;
    }

    const { mainImage, stage, canvas } = state;
    const naturalWidth = mainImage.naturalWidth;
    const naturalHeight = mainImage.naturalHeight;
    if (!naturalWidth || !naturalHeight) {
      return;
    }

    const normalizedRotation = ((state.rotation % 360) + 360) % 360;
    if (state.scale === 1 && normalizedRotation === 0) {
      canvas.style.width = "";
      canvas.style.height = "";
      canvas.style.transform = "";
      mainImage.style.width = "";
      mainImage.style.height = "";
      mainImage.style.maxWidth = "";
      mainImage.style.maxHeight = "";
      mainImage.style.transform = "";
      return;
    }

    const isQuarterTurn = normalizedRotation % 180 !== 0;
    const stageBounds = getAuditStageBounds(stage);
    if (stageBounds.width <= 0 || stageBounds.height <= 0) {
      return;
    }

    const rotatedWidth = isQuarterTurn ? naturalHeight : naturalWidth;
    const rotatedHeight = isQuarterTurn ? naturalWidth : naturalHeight;
    const containScale = Math.min(
      stageBounds.width / rotatedWidth,
      stageBounds.height / rotatedHeight,
      1
    );

    const imageWidth = Math.max(naturalWidth * containScale * state.scale, 1);
    const imageHeight = Math.max(naturalHeight * containScale * state.scale, 1);
    const canvasWidth = isQuarterTurn ? imageHeight : imageWidth;
    const canvasHeight = isQuarterTurn ? imageWidth : imageHeight;

    canvas.style.width = `${canvasWidth}px`;
    canvas.style.height = `${canvasHeight}px`;
    canvas.style.transform = "";
    mainImage.style.width = `${imageWidth}px`;
    mainImage.style.height = `${imageHeight}px`;
    mainImage.style.maxWidth = "none";
    mainImage.style.maxHeight = "none";
    mainImage.style.transform = `rotate(${normalizedRotation}deg)`;
  };

  const resetAuditTransform = (viewer) => {
    const state = ensureAuditViewerState(viewer);
    if (!state) {
      return;
    }

    state.scale = 1;
    state.rotation = 0;
    if (state.stage) {
      state.stage.scrollLeft = 0;
      state.stage.scrollTop = 0;
    }
    applyAuditTransform(viewer);
  };

  const selectAuditThumb = (viewer, thumb) => {
    const state = ensureAuditViewerState(viewer);
    if (!state) {
      return;
    }

    for (const currentThumb of state.thumbs) {
      currentThumb.classList.toggle("is-active", currentThumb === thumb);
    }

    state.mainImage.src = thumb.dataset.fullSrc ?? "";
    state.mainImage.alt = thumb.dataset.alt ?? "";
    if (state.caption) {
      state.caption.textContent = thumb.dataset.caption ?? "";
    }

    if (state.stage) {
      state.stage.scrollLeft = 0;
      state.stage.scrollTop = 0;
    }

    resetAuditTransform(viewer);
  };

  const bindAuditViewers = (scope = document) => {
    const viewers = scope.querySelectorAll("[data-audit-viewer]");
    for (const viewer of viewers) {
      const state = ensureAuditViewerState(viewer);
      if (!state) {
        continue;
      }

      applyAuditTransform(viewer);
    }
  };

  document.addEventListener("click", (event) => {
    const togglePanel = event.target.closest("[data-audit-toggle-panel]");
    if (togglePanel) {
      const panelId = togglePanel.getAttribute("data-audit-toggle-panel");
      const panel = panelId ? document.getElementById(panelId) : null;
      if (!panel) {
        return;
      }

      event.preventDefault();

      const isHidden = panel.classList.toggle("is-hidden");
      togglePanel.setAttribute("aria-expanded", isHidden ? "false" : "true");
      panel.setAttribute("aria-hidden", isHidden ? "true" : "false");

      if (!isHidden) {
        const observacion = panel.querySelector("[data-audit-observacion]");
        if (observacion instanceof HTMLElement) {
          observacion.focus();
        }
      }

      return;
    }

    const action = event.target.closest("[data-audit-action]");
    if (action) {
      const viewer = action.closest("[data-audit-viewer]");
      const state = viewer ? ensureAuditViewerState(viewer) : null;
      if (!viewer || !state) {
        return;
      }

      event.preventDefault();

      switch (action.dataset.auditAction) {
        case "zoom-in":
          state.scale = Math.min(state.scale + 0.2, 3);
          applyAuditTransform(viewer);
          break;
        case "zoom-out":
          state.scale = Math.max(state.scale - 0.2, 0.6);
          applyAuditTransform(viewer);
          break;
        case "rotate-left":
          state.rotation -= 90;
          applyAuditTransform(viewer);
          break;
        case "rotate-right":
          state.rotation += 90;
          applyAuditTransform(viewer);
          break;
        case "reset":
          resetAuditTransform(viewer);
          break;
      }

      return;
    }

    const thumb = event.target.closest("[data-audit-thumb]");
    if (thumb) {
      const viewer = thumb.closest("[data-audit-viewer]");
      if (!viewer) {
        return;
      }

      event.preventDefault();
      selectAuditThumb(viewer, thumb);
    }
  });

  const syncAuditPanelHeights = (scope = document) => {
    const layouts = scope.querySelectorAll("[data-audit-layout]");
    const topbar = document.querySelector(".app-topbar");
    const topbarHeight = topbar ? Math.ceil(topbar.getBoundingClientRect().height) : 0;

    for (const layout of layouts) {
      const listPanel = layout.querySelector("[data-audit-list-panel]");
      if (!listPanel) {
        continue;
      }

      if (window.innerWidth <= 768) {
        layout.style.removeProperty("--audit-sticky-top");
        layout.style.removeProperty("--audit-list-height");
        continue;
      }

      // Keep the office list visually stable below the sticky application header.
      const stickyTop = topbarHeight + 16;
      const targetHeight = Math.max(window.innerHeight - stickyTop - 24, 360);

      layout.style.setProperty("--audit-sticky-top", `${stickyTop}px`);
      layout.style.setProperty("--audit-list-height", `${targetHeight}px`);
    }
  };

  if (!reducedMotion && "startViewTransition" in document) {
    document.documentElement.classList.add("vt-enabled");
  }

  bindLoadingForms();
  bindAdminDropdownFallback();
  bindAuditViewers();
  syncAuditPanelHeights();
  window.addEventListener("pageshow", () => {
    bindLoadingForms();
    bindAdminDropdownFallback();
    bindAuditViewers();
    syncAuditPanelHeights();
  });
  window.addEventListener("resize", () => {
    bindAuditViewers();
    syncAuditPanelHeights();
  });

  document.addEventListener("htmx:configRequest", (event) => {
    event.detail.headers["X-Requested-With"] = "XMLHttpRequest";
  });

  document.addEventListener("htmx:beforeRequest", (event) => {
    const elt = event.detail.elt;
    if (elt && elt.matches("[data-busy]")) {
      elt.setAttribute("aria-busy", "true");
    }
  });

  document.addEventListener("htmx:afterRequest", (event) => {
    const elt = event.detail.elt;
    if (elt && elt.matches("[data-busy]")) {
      elt.removeAttribute("aria-busy");
    }

    if (elt && elt.matches("form[data-ui-loading]")) {
      elt.classList.remove("is-submitting");
    }
  });

  document.addEventListener("htmx:afterSwap", (event) => {
    const target = event.detail.target;
    if (!target || !window.jQuery || !window.jQuery.validator || !window.jQuery.validator.unobtrusive) {
      bindLoadingForms(target ?? document);
      bindAdminDropdownFallback();
      bindAuditViewers(target ?? document);
      syncAuditPanelHeights(target ?? document);
      return;
    }

    const forms = target.querySelectorAll("form");
    for (const form of forms) {
      const $form = window.jQuery(form);
      $form.removeData("validator");
      $form.removeData("unobtrusiveValidation");
      window.jQuery.validator.unobtrusive.parse(form);
    }

    bindLoadingForms(target);
    bindAdminDropdownFallback();
    bindAuditViewers(target);
    syncAuditPanelHeights(target);
  });

  document.addEventListener("click", (event) => {
    if (event.target.closest(".app-admin-toggle") || event.target.closest(".app-admin-menu")) {
      return;
    }

    closeAdminDropdowns();
  });
})();
