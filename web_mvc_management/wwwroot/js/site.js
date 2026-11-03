// Hành vi dùng chung: toast, modal xác nhận, phím tắt, gợi ý tìm kiếm, phân trang.
(function ($) {
    "use strict";

    const App = (window.App = window.App || {});
    const icons = { success: "check-circle", error: "exclamation-octagon", warning: "exclamation-triangle", info: "info-circle" };

    // ---------- Toast ----------
    App.toast = function (text, tone = "success") {
        const tpl = document.getElementById("toastTemplate");
        const stack = document.getElementById("toastStack");
        if (!tpl || !stack) return;
        const el = tpl.content.firstElementChild.cloneNode(true);
        el.classList.add("toast--" + tone);
        el.setAttribute("role", tone === "error" ? "alert" : "status");
        el.querySelector(".bi").classList.add("bi-" + (icons[tone] || icons.info));
        el.querySelector(".toast__text").textContent = text;
        stack.appendChild(el);
        el.addEventListener("hidden.bs.toast", () => el.remove());
        // Lỗi không tự ẩn: người dùng cần đọc cách xử lý
        bootstrap.Toast.getOrCreateInstance(el, { autohide: tone !== "error", delay: 5000 }).show();
    };

    $(function () {
        document.querySelectorAll(".toast[data-autoshow]").forEach((el) => {
            const isError = el.classList.contains("toast--error");
            bootstrap.Toast.getOrCreateInstance(el, { autohide: !isError, delay: 5000 }).show();
        });
    });

    // ---------- Modal xác nhận (thay confirm()) ----------
    // App.confirm({ title, detail, ok, cancel, danger }) -> Promise<boolean>
    App.confirm = function (opts) {
        const modalEl = document.getElementById("confirmModal");
        const okBtn = document.getElementById("confirmModalOk");
        document.getElementById("confirmModalTitle").textContent = opts.title;
        document.getElementById("confirmModalDetail").textContent = opts.detail || "";
        document.getElementById("confirmModalCancel").textContent = opts.cancel || "Quay lại";
        okBtn.textContent = opts.ok;
        okBtn.className = "btn " + (opts.danger ? "btn-danger" : "btn-primary");

        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        return new Promise((resolve) => {
            let confirmed = false;
            const onOk = () => { confirmed = true; modal.hide(); };
            okBtn.addEventListener("click", onOk, { once: true });
            modalEl.addEventListener("shown.bs.modal", () => okBtn.focus(), { once: true });
            modalEl.addEventListener("hidden.bs.modal", () => {
                okBtn.removeEventListener("click", onOk);
                resolve(confirmed);
            }, { once: true });
            modal.show();
        });
    };

    function confirmOptions(el) {
        return {
            title: el.dataset.confirm,
            detail: el.dataset.confirmDetail,
            ok: el.dataset.confirmOk || el.textContent.trim(),
            cancel: el.dataset.confirmCancel,
            danger: el.hasAttribute("data-confirm-danger")
        };
    }

    $(document).on("submit", "form[data-confirm]", function (e) {
        if (this.dataset.confirmed === "1") return;
        e.preventDefault();
        App.confirm(confirmOptions(this)).then((ok) => {
            if (!ok) return;
            this.dataset.confirmed = "1";
            this.requestSubmit ? this.requestSubmit() : this.submit();
        });
    });

    $(document).on("click", "a[data-confirm], button[data-confirm]:not([type=submit])", function (e) {
        e.preventDefault();
        const el = this;
        App.confirm(confirmOptions(el)).then((ok) => {
            if (!ok) return;
            if (el.href) window.location.href = el.href;
            else el.dispatchEvent(new CustomEvent("confirmed", { bubbles: true }));
        });
    });

    // ---------- Modal xác nhận XÓA CỨNG 3 BƯỚC (Đổi Status -> Delete -> Confirm) ----------
    let activeHardDeleteForm = null;

    $(document).on("input", "#hardDeleteConfirmInput", function () {
        const val = (this.value || "").trim().toUpperCase();
        const okBtn = document.getElementById("hardDeleteOkBtn");
        if (okBtn) okBtn.disabled = val !== "XOA";
    });

    $(document).on("click", "#hardDeleteOkBtn", function () {
        if (!activeHardDeleteForm) return;
        const inputEl = document.getElementById("hardDeleteConfirmInput");
        if (!inputEl || inputEl.value.trim().toUpperCase() !== "XOA") return;

        let hidden = activeHardDeleteForm.querySelector("input[name='confirmCode']");
        if (!hidden) {
            hidden = document.createElement("input");
            hidden.type = "hidden";
            hidden.name = "confirmCode";
            activeHardDeleteForm.appendChild(hidden);
        }
        hidden.value = "XOA";
        activeHardDeleteForm.dataset.hardConfirmed = "1";

        const modalEl = document.getElementById("hardDeleteModal");
        bootstrap.Modal.getOrCreateInstance(modalEl).hide();
        activeHardDeleteForm.requestSubmit ? activeHardDeleteForm.requestSubmit() : activeHardDeleteForm.submit();
    });

    $(document).on("submit", "form[data-hard-delete]", function (e) {
        if (this.dataset.hardConfirmed === "1") return;
        e.preventDefault();
        activeHardDeleteForm = this;

        const modalEl = document.getElementById("hardDeleteModal");
        const targetLabel = document.getElementById("hardDeleteTargetName");
        const confirmInput = document.getElementById("hardDeleteConfirmInput");
        const okBtn = document.getElementById("hardDeleteOkBtn");

        if (targetLabel) targetLabel.textContent = this.dataset.hardDelete || "Bản ghi đã chọn";
        if (confirmInput) confirmInput.value = "";
        if (okBtn) okBtn.disabled = true;

        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modalEl.addEventListener("shown.bs.modal", () => confirmInput?.focus(), { once: true });
        modal.show();
    });

    // ---------- Phím tắt ----------
    // <input data-shortcut-focus="F2">  focus ô khi bấm phím
    // <button data-shortcut-click="F9"> bấm nút khi bấm phím
    // Phím "/" focus ô tìm kiếm nếu không đang gõ trong ô nhập.
    function isTyping(target) {
        return target.closest("input, textarea, select, [contenteditable=true]");
    }

    document.addEventListener("keydown", (e) => {
        if (e.ctrlKey || e.altKey || e.metaKey) return;

        if (e.key === "/" && !isTyping(e.target)) {
            const search = document.querySelector("[data-search-input]");
            if (search) { e.preventDefault(); search.focus(); search.select(); }
            return;
        }

        if (!/^F\d{1,2}$/.test(e.key)) return;
        const focusEl = document.querySelector(`[data-shortcut-focus="${e.key}"]:not(:disabled)`);
        if (focusEl) { e.preventDefault(); focusEl.focus(); focusEl.select?.(); return; }
        const clickEl = document.querySelector(`[data-shortcut-click="${e.key}"]:not(:disabled)`);
        if (clickEl) { e.preventDefault(); clickEl.click(); }
    });

    // ---------- Gợi ý tìm kiếm (combobox ARIA) ----------
    $(".searchbar[data-suggest-url]").each(function () {
        const form = this;
        const input = form.querySelector("[data-search-input]");
        const list = form.querySelector(".search-suggest");
        let timer = null;
        let request = null;
        let activeIndex = -1;

        const options = () => Array.from(list.querySelectorAll("[role=option]:not([aria-disabled])"));

        function close() {
            list.hidden = true;
            input.setAttribute("aria-expanded", "false");
            input.removeAttribute("aria-activedescendant");
            activeIndex = -1;
        }

        function setActive(i) {
            const opts = options();
            if (!opts.length) return;
            activeIndex = (i + opts.length) % opts.length;
            opts.forEach((o, idx) => o.setAttribute("aria-selected", idx === activeIndex ? "true" : "false"));
            input.setAttribute("aria-activedescendant", opts[activeIndex].id);
        }

        input.addEventListener("input", () => {
            clearTimeout(timer);
            request?.abort();
            close();
            input.removeAttribute("aria-busy");
            const status = form.querySelector("[data-search-status]");
            if (status) status.textContent = "";
            const term = input.value.trim();
            if (term.length < 2) { close(); return; }
            timer = setTimeout(() => {
                input.setAttribute("aria-busy", "true");
                if (status) status.textContent = "Đang tìm gợi ý sách.";
                request = $.get(form.dataset.suggestUrl, { keyword: term })
                    .done((html) => {
                        if (input.value.trim() !== term || document.activeElement !== input) return;
                        list.innerHTML = html;
                        list.hidden = false;
                        input.setAttribute("aria-expanded", "true");
                        activeIndex = -1;
                        if (status) status.textContent = options().length ? `Có ${options().length} gợi ý. Dùng phím mũi tên để chọn.` : "Không có gợi ý phù hợp. Bạn có thể nhấn Tìm để tra cứu.";
                    })
                    .fail((xhr) => {
                        if (xhr.statusText !== "abort") {
                            close();
                            if (status) status.textContent = "Chưa tải được gợi ý. Nhấn Tìm để tra cứu trực tiếp.";
                        }
                    })
                    .always(() => input.removeAttribute("aria-busy"));
            }, 250);
        });

        input.addEventListener("keydown", (e) => {
            if (list.hidden) return;
            if (e.key === "ArrowDown") { e.preventDefault(); setActive(activeIndex + 1); }
            else if (e.key === "ArrowUp") { e.preventDefault(); setActive(activeIndex - 1); }
            else if (e.key === "Escape") { close(); }
            else if (e.key === "Enter" && activeIndex >= 0) {
                e.preventDefault();
                window.location.href = options()[activeIndex].href;
            }
        });

        input.addEventListener("blur", () => setTimeout(close, 150));
    });

    // ---------- Hiện / ẩn mật khẩu ----------
    $(document).on("click", "[data-password-toggle]", function () {
        const input = this.closest(".password-field").querySelector("input");
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        this.setAttribute("aria-pressed", String(show));
        this.querySelector(".bi").className = "bi " + (show ? "bi-eye-slash" : "bi-eye");
        this.querySelector(".visually-hidden").textContent = show ? "Ẩn mật khẩu" : "Hiện mật khẩu";
    });

    // ---------- Phân trang ----------
    $(document).on("change", "[data-pager-size]", function () {
        window.location.href = this.value;
    });
})(jQuery);
