/* ==========================================================================
   SchoolEase — client behaviour
   Sidebar, toasts, confirmation dialogs, loading states, charts, tables.
   ========================================================================== */
(function () {
    "use strict";

    const SE = window.SE = window.SE || {};

    /* ---------------------------------------------------------------- Helpers */
    SE.qs = (selector, root) => (root || document).querySelector(selector);
    SE.qsa = (selector, root) => Array.from((root || document).querySelectorAll(selector));

    /* --------------------------------------------------------------- Sidebar */
    function initSidebar() {
        const sidebar = SE.qs("#seSidebar");
        const backdrop = SE.qs("#seBackdrop");
        const toggles = SE.qsa("[data-se-sidebar-toggle]");

        if (!sidebar) return;

        const open = () => {
            sidebar.classList.add("show");
            if (backdrop) backdrop.classList.add("show");
        };
        const close = () => {
            sidebar.classList.remove("show");
            if (backdrop) backdrop.classList.remove("show");
        };

        toggles.forEach(btn => btn.addEventListener("click", e => {
            e.preventDefault();
            sidebar.classList.contains("show") ? close() : open();
        }));

        if (backdrop) backdrop.addEventListener("click", close);

        document.addEventListener("keydown", e => {
            if (e.key === "Escape") close();
        });

        window.addEventListener("resize", () => {
            if (window.innerWidth > 991) close();
        });
    }

    /* ---------------------------------------------------------------- Toasts */
    SE.toast = function (message, type, title) {
        let host = SE.qs("#seToasts");
        if (!host) {
            host = document.createElement("div");
            host.id = "seToasts";
            host.className = "se-toasts";
            document.body.appendChild(host);
        }

        type = type || "info";
        const icons = {
            success: "bi-check-circle-fill",
            warning: "bi-exclamation-triangle-fill",
            danger: "bi-x-octagon-fill",
            info: "bi-info-circle-fill"
        };

        const el = document.createElement("div");
        el.className = "se-toast se-toast--" + type;
        el.setAttribute("role", "status");
        el.innerHTML =
            '<i class="bi ' + (icons[type] || icons.info) + '"></i>' +
            '<div><div class="se-toast__title">' + (title || SE.titleCase(type)) + '</div>' +
            '<div class="se-toast__msg"></div></div>' +
            '<button type="button" class="se-toast__close" aria-label="Close">&times;</button>';

        // textContent keeps any server-provided text safe from HTML injection
        el.querySelector(".se-toast__msg").textContent = message || "";

        const remove = () => {
            el.style.transition = "opacity .18s ease, transform .18s ease";
            el.style.opacity = "0";
            el.style.transform = "translateX(16px)";
            setTimeout(() => el.remove(), 190);
        };

        el.querySelector(".se-toast__close").addEventListener("click", remove);
        host.appendChild(el);
        setTimeout(remove, 5200);

        return el;
    };

    SE.titleCase = value => value.charAt(0).toUpperCase() + value.slice(1);

    /* ---------------------------------------------------- Server side flash */
    function initFlash() {
        const node = SE.qs("#seFlash");
        if (!node) return;

        try {
            const payload = JSON.parse(node.textContent || "{}");
            if (payload.success) SE.toast(payload.success, "success", "Success");
            if (payload.error) SE.toast(payload.error, "danger", "Something went wrong");
            if (payload.warning) SE.toast(payload.warning, "warning", "Heads up");
            if (payload.info) SE.toast(payload.info, "info", "Notice");
        } catch (err) {
            // ignore malformed payload
        }
    }

    /* -------------------------------------------------- Confirmation dialogs */
    function initConfirm() {
        document.addEventListener("submit", e => {
            const form = e.target;
            if (!form.matches("form[data-se-confirm]")) return;
            if (form.dataset.seConfirmed === "1") return;

            e.preventDefault();
            const message = form.dataset.seConfirm || "Are you sure you want to continue?";
            const tone = form.dataset.seConfirmTone || "danger";
            SE.confirm(message, tone).then(ok => {
                if (!ok) return;
                form.dataset.seConfirmed = "1";
                showLoading(form.dataset.seLoading || "Processing…");
                form.submit();
            });
        });

        document.addEventListener("click", e => {
            const link = e.target.closest("a[data-se-confirm]");
            if (!link) return;

            e.preventDefault();
            SE.confirm(link.dataset.seConfirm || "Are you sure?", link.dataset.seConfirmTone || "danger")
                .then(ok => { if (ok) window.location.href = link.href; });
        });
    }

    /** Promise-based confirmation modal (returns true/false). */
    SE.confirm = function (message, tone, opts) {
        opts = opts || {};
        return new Promise(resolve => {
            const id = "seConfirmModal";
            let modal = document.getElementById(id);
            if (!modal) {
                modal = document.createElement("div");
                modal.id = id;
                modal.className = "modal fade";
                modal.tabIndex = -1;
                modal.innerHTML =
                    '<div class="modal-dialog modal-dialog-centered">' +
                    '<div class="modal-content">' +
                    '<div class="modal-header"><h5 class="modal-title">Confirm action</h5>' +
                    '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div>' +
                    '<div class="modal-body"><div class="d-flex gap-3 align-items-start">' +
                    '<i class="bi bi-exclamation-triangle-fill fs-4 text-warning"></i>' +
                    '<p class="mb-0 se-confirm-msg"></p></div></div>' +
                    '<div class="modal-footer">' +
                    '<button type="button" class="btn btn-light" data-bs-dismiss="modal">Cancel</button>' +
                    '<button type="button" class="btn se-confirm-ok"></button>' +
                    "</div></div></div>";
                document.body.appendChild(modal);
            }

            const okBtn = modal.querySelector(".se-confirm-ok");
            okBtn.className = "btn btn-" + (tone || "danger") + " se-confirm-ok";
            okBtn.textContent = opts.okText || "Yes, continue";
            modal.querySelector(".se-confirm-msg").textContent = message;
            modal.querySelector(".modal-title").textContent = opts.title || "Confirm action";

            const instance = bootstrap.Modal.getOrCreateInstance(modal);
            let answered = false;

            const onOk = () => { answered = true; instance.hide(); };
            okBtn.addEventListener("click", onOk, { once: true });
            modal.addEventListener("hidden.bs.modal", () => {
                okBtn.removeEventListener("click", onOk);
                resolve(answered);
            }, { once: true });

            instance.show();
        });
    };

    /* --------------------------------------------------------- Loading state */
    function showLoading(text) {
        if (SE.qs("#seLoading")) return;
        const el = document.createElement("div");
        el.id = "seLoading";
        el.className = "se-loading";
        el.innerHTML =
            '<div class="se-loading__box">' +
            '<div class="spinner-border" role="status"></div>' +
            "<p></p></div>";
        el.querySelector("p").textContent = text || "Working…";
        document.body.appendChild(el);
    }
    SE.showLoading = showLoading;
    SE.hideLoading = () => { const el = SE.qs("#seLoading"); if (el) el.remove(); };

    function initForms() {
        // Prevent double submits and show a loading state on long forms.
        document.addEventListener("submit", e => {
            const form = e.target;
            if (!form.matches("form") || form.dataset.seConfirmed === "1") return;
            if (form.dataset.seNoLoading === "1") return;
            if (form.dataset.seConfirm) return; // handled by the confirm flow

            const submitter = e.submitter || form.querySelector("[type=submit]");
            if (submitter) submitter.disabled = true;
            showLoading(form.dataset.seLoading || "Saving…");
        });

        // Live character counters: <textarea data-se-counter="#target">
        SE.qsa("[data-se-counter]").forEach(input => {
            const out = SE.qs(input.dataset.seCounter);
            if (!out) return;
            const update = () => { out.textContent = input.value.length + " characters"; };
            input.addEventListener("input", update);
            update();
        });
    }

    /* --------------------------------------------------------------- Filters */
    function initFilters() {
        // Auto-submit filter forms when a select changes.
        document.addEventListener("change", e => {
            const el = e.target;
            if (!el.matches("[data-se-autosubmit]")) return;
            const form = el.closest("form");
            if (form) form.submit();
        });

        // Reset button clears the filter form and reloads the clean URL.
        document.addEventListener("click", e => {
            const btn = e.target.closest("[data-se-reset]");
            if (!btn) return;
            e.preventDefault();
            window.location.href = btn.dataset.seReset || window.location.pathname;
        });
    }

    /* -------------------------------------------------- Cascading selects */
    // <select data-se-cascade="#subjectSelect"> narrows the target select to the
    // options whose data-se-group matches the parent's value. Options without a
    // group — and any option while the parent has no value — stay visible.
    function applyCascade(parent) {
        const target = SE.qs(parent.dataset.seCascade);
        if (!target) return;

        const parentGroup = parent.value || "";
        let resetSelection = false;

        Array.from(target.options).forEach(option => {
            if (option.value === "") return;                // keep the placeholder
            const group = option.dataset.seGroup || "";
            const visible = !parentGroup || !group || group === parentGroup;
            option.hidden = !visible;
            option.disabled = !visible;
            if (!visible && option.selected) resetSelection = true;
        });

        // A previously chosen value that no longer applies is cleared so it is
        // never submitted against the wrong grade level.
        if (resetSelection) target.value = "";
    }

    function initCascades() {
        SE.qsa("select[data-se-cascade]").forEach(parent => {
            applyCascade(parent);
            parent.addEventListener("change", () => applyCascade(parent));
        });
    }

    /* ----------------------------------------------------------------- Tables */
    function initTables() {
        // Client side quick search: <input data-se-table-filter="#tableId">
        SE.qsa("[data-se-table-filter]").forEach(input => {
            const table = SE.qs(input.dataset.seTableFilter);
            if (!table) return;

            input.addEventListener("input", () => {
                const term = input.value.trim().toLowerCase();
                let visible = 0;

                SE.qsa("tbody tr", table).forEach(row => {
                    const match = row.textContent.toLowerCase().indexOf(term) > -1;
                    row.style.display = match ? "" : "none";
                    if (match) visible++;
                });

                const empty = SE.qs("[data-se-table-empty]", table.closest(".se-card") || document);
                if (empty) empty.style.display = visible === 0 ? "" : "none";
            });
        });

        // "Select all" checkbox helper.
        SE.qsa("[data-se-check-all]").forEach(master => {
            const scope = SE.qs(master.dataset.seCheckAll) || document;
            master.addEventListener("change", () => {
                SE.qsa("input[type=checkbox][data-se-row]", scope)
                    .forEach(cb => { cb.checked = master.checked; });
                updateBulkBar();
            });
        });

        document.addEventListener("change", e => {
            if (e.target.matches("input[type=checkbox][data-se-row]")) updateBulkBar();
        });

        function updateBulkBar() {
            const checked = SE.qsa("input[type=checkbox][data-se-row]:checked").length;
            SE.qsa("[data-se-bulk-bar]").forEach(bar => {
                bar.classList.toggle("d-none", checked === 0);
                const count = SE.qs("[data-se-bulk-count]", bar);
                if (count) count.textContent = checked;
            });
        }
    }

    /* ----------------------------------------------------------------- Charts */
    /**
     * Renders a Chart.js chart from a JSON <script type="application/json"> block.
     * Usage: <div class="se-chart"><canvas data-se-chart="#chartData"></canvas></div>
     */
    SE.renderCharts = function () {
        if (typeof Chart === "undefined") return;

        SE.qsa("canvas[data-se-chart]").forEach(canvas => {
            const source = SE.qs(canvas.dataset.seChart);
            if (!source) return;

            let spec;
            try { spec = JSON.parse(source.textContent || "{}"); } catch (err) { return; }

            const palette = spec.colors && spec.colors.length
                ? spec.colors
                : ["#800000", "#c9a227", "#12a37a", "#e8930c", "#dc3545", "#6f42c1", "#0ea5c6", "#5b6b7c"];

            const type = spec.type || "bar";
            const dataset = {
                label: spec.datasetLabel || "Total",
                data: spec.values || [],
                backgroundColor: type === "line" ? "rgba(128,0,0,.14)" : palette,
                borderColor: type === "line" ? "#800000" : "#fff",
                borderWidth: type === "line" ? 3 : 1,
                borderRadius: type === "bar" ? 7 : 0,
                maxBarThickness: 46,
                fill: type === "line",
                tension: .35,
                pointBackgroundColor: "#800000",
                pointRadius: 4,
                pointHoverRadius: 6
            };

            const options = {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 550 },
                plugins: {
                    legend: {
                        display: type === "doughnut" || type === "pie",
                        position: "bottom",
                        labels: { boxWidth: 10, usePointStyle: true, font: { size: 11 } }
                    },
                    tooltip: {
                        backgroundColor: "#16202c",
                        padding: 10,
                        cornerRadius: 8,
                        displayColors: type !== "bar" && type !== "line"
                    },
                    title: { display: false }
                },
                scales: type === "doughnut" || type === "pie"
                    ? {}
                    : {
                        y: {
                            beginAtZero: true,
                            grid: { color: "#eef1f7", drawBorder: false },
                            ticks: { precision: 0, font: { size: 11 }, color: "#6b7a90" }
                        },
                        x: {
                            grid: { display: false },
                            ticks: { font: { size: 11 }, color: "#6b7a90", maxRotation: 0, autoSkip: true }
                        }
                    }
            };

            new Chart(canvas.getContext("2d"), {
                type: type,
                data: { labels: spec.labels || [], datasets: [dataset] },
                options: options
            });
        });
    };

    /* --------------------------------------------------------- Notifications */
    function initNotifications() {
        const btn = SE.qs("[data-se-mark-all-read]");
        if (!btn) return;

        btn.addEventListener("click", async e => {
            e.preventDefault();
            try {
                const response = await fetch(btn.dataset.seMarkAllRead, {
                    method: "POST",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                if (!response.ok) throw new Error("Request failed");
                SE.qsa(".se-notif__item--unread").forEach(el => el.classList.remove("se-notif__item--unread"));
                const dot = SE.qs("#seNotifDot");
                if (dot) dot.remove();
                SE.toast("All notifications marked as read.", "success", "Notifications");
            } catch (err) {
                SE.toast("Could not update notifications. Please try again.", "danger", "Error");
            }
        });
    }

    /* ---------------------------------------------------------------- Startup */
    document.addEventListener("DOMContentLoaded", function () {
        initSidebar();
        initFlash();
        initConfirm();
        initForms();
        initFilters();
        initCascades();
        initTables();
        initNotifications();
        SE.renderCharts();
    });

    // Re-render charts after Bootstrap tab switches (canvas sizing).
    document.addEventListener("shown.bs.tab", function () {
        SE.renderCharts();
    });
})();
