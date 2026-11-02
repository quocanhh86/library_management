// Trang nhập mã xác thực: 6 ô số, đồng hồ hết hạn mã, khóa nút "Gửi lại mã" trong thời gian chờ.
(function () {
    "use strict";

    const root = document.querySelector("[data-otp-timer]");
    if (!root) return;

    const boxes = Array.from(root.querySelectorAll(".otp__box"));
    const hidden = root.querySelector("[data-otp-value]");
    const group = root.querySelector("[data-otp-boxes]");
    const errorBox = root.querySelector("[data-otp-error]");
    const form = root.querySelector("#otpForm");

    // ---------- 6 ô số ----------
    function fill(from, digits) {
        let i = from;
        for (const d of digits) {
            if (i >= boxes.length) break;
            boxes[i++].value = d;
        }
        boxes[Math.min(i, boxes.length - 1)].focus();
    }

    boxes.forEach((box, i) => {
        box.addEventListener("input", () => {
            const digits = box.value.replace(/\D/g, "");
            box.value = "";
            if (digits) fill(i, digits);
        });
        box.addEventListener("keydown", (e) => {
            if (e.key === "Backspace" && !box.value && i > 0) {
                e.preventDefault();
                boxes[i - 1].value = "";
                boxes[i - 1].focus();
            } else if (e.key === "ArrowLeft" && i > 0) {
                boxes[i - 1].focus();
            } else if (e.key === "ArrowRight" && i < boxes.length - 1) {
                boxes[i + 1].focus();
            }
        });
        box.addEventListener("paste", (e) => {
            const digits = (e.clipboardData || window.clipboardData).getData("text").replace(/\D/g, "");
            if (!digits) return;
            e.preventDefault();
            fill(i, digits);
        });
        box.addEventListener("focus", () => box.select());
    });

    function showError(text) {
        group.classList.add("otp--invalid");
        errorBox.classList.remove("d-none");
        errorBox.textContent = text;
    }

    // Không tự gửi khi đủ 6 số: người dùng xem lại rồi bấm "Xác nhận mã".
    form.addEventListener("submit", (e) => {
        const code = boxes.map((b) => b.value).join("");
        if (!/^\d{6}$/.test(code)) {
            e.preventDefault();
            showError("Nhập đủ 6 số của mã xác thực.");
            boxes.find((b) => !b.value)?.focus();
            return;
        }
        hidden.value = code;
    });

    // ---------- Đồng hồ ----------
    // mốc gửi mã lưu ở sessionStorage khi trang mở lần đầu, nên lệch vài giây so với server;
    // muốn chính xác thì server truyền thời điểm hết hạn của mã (otp_entries.expires_at) vào View.
    const validity = Number(root.dataset.validity);
    const cooldown = Number(root.dataset.cooldown);
    const key = root.dataset.key;
    const expiryEl = root.querySelector("[data-otp-expiry]");
    const expiryText = root.querySelector("[data-otp-expiry-text]");
    const resendBtn = root.querySelector("[data-otp-resend]");
    const cooldownEl = root.querySelector("[data-otp-cooldown]");

    let sentAt = Date.now();
    try {
        const saved = Number(sessionStorage.getItem(key));
        if (saved && Date.now() - saved < validity * 1000) sentAt = saved;
        else sessionStorage.setItem(key, String(sentAt));
    } catch { /* sessionStorage bị chặn: tính từ lúc mở trang */ }

    root.querySelector("[data-otp-resend-form]").addEventListener("submit", () => {
        try { sessionStorage.removeItem(key); } catch { /* bỏ qua */ }
    });

    const mmss = (s) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;

    function tick() {
        const elapsed = Math.floor((Date.now() - sentAt) / 1000);
        const left = validity - elapsed;
        const wait = cooldown - elapsed;

        if (left > 0) {
            expiryEl.textContent = mmss(left);
        } else {
            expiryText.textContent = "Mã đã hết hạn. Bấm Gửi lại mã để nhận mã mới.";
            boxes.forEach((b) => (b.disabled = true));
        }

        if (wait > 0) {
            resendBtn.disabled = true;
            cooldownEl.textContent = `(${mmss(wait)})`;
        } else {
            resendBtn.disabled = false;
            cooldownEl.textContent = "";
        }

        if (left > 0) setTimeout(tick, 1000);
    }
    tick();
})();
