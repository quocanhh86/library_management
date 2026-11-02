(function ($) {
    'use strict';
    function revealCurrentNavigation() {
        const nav = document.querySelector('.staff-sidebar__inner > nav');
        const current = nav?.querySelector('[aria-current="page"]');
        if (!nav || !current) return;
        const item = current.getBoundingClientRect();
        const bounds = nav.getBoundingClientRect();
        if (item.bottom > bounds.bottom) nav.scrollTop += item.bottom - bounds.bottom + 12;
        else if (item.top < bounds.top) nav.scrollTop += item.top - bounds.top - 12;
    }
    revealCurrentNavigation();
    document.getElementById('staffSidebar')?.addEventListener('shown.bs.offcanvas', revealCurrentNavigation);
    const editor = document.querySelector('[data-admin-editor]');
    const editorTitle = document.getElementById('adminEditorTitle');
    const fieldLabels = {
        code: 'Mã định danh', title: 'Tên / tiêu đề', category: 'Chuyên ngành', year: 'Năm xuất bản', copies: 'Sẵn sàng / tổng', authors: 'Tác giả', publisher: 'Nhà xuất bản', isbn: 'ISBN',
        shelf: 'Kệ sách', type: 'Loại độc giả', expiry: 'Hạn thẻ', borrowed: 'Đang mượn / ngày mượn', fine: 'Nợ phạt', email: 'Email', reader: 'Độc giả', due: 'Hạn trả', barcode: 'Mã vạch',
        book: 'Tài liệu', days: 'Số ngày', amount: 'Phạt dự kiến', role: 'Vai trò', max: 'Số sách tối đa', count: 'Số lượng', renewals: 'Số lần gia hạn', renewDays: 'Số ngày gia hạn', cardMonths: 'Hiệu lực thẻ (tháng)'
    };
    function resetValidation(form) {
        form.classList.remove('was-validated');
        const message = form.querySelector('[data-admin-validation]');
        if (message) message.hidden = true;
    }
    function populateEditor(record) {
        if (!editor) return;
        editor.reset();
        resetValidation(editor);
        editorTitle.textContent = record ? editorTitle.dataset.editTitle : editorTitle.dataset.createTitle;
        editor.querySelectorAll('[data-record-field]').forEach(input => {
            let value = record?.Cells?.[input.dataset.recordField] || '';
            if (record && input.dataset.recordField === 'recordStatus') value = record.Badge.Code === 'EXPIRING' ? 'ACTIVE' : record.Badge.Code;
            if (input.type === 'date' && /^\d{2}\/\d{2}\/\d{4}$/.test(value)) value = value.split('/').reverse().join('-');
            if (input.type === 'number' && value) value = value.replace(/[^\d-]/g, '');
            input.value = value;
        });
    }
    $(document).on('click', '[data-admin-create]', () => populateEditor(null));
    $(document).on('click', '[data-admin-edit]', function () {
        populateEditor(JSON.parse(this.dataset.adminEdit));
    });
    $(document).on('click', '[data-admin-detail-record]', function () {
        const record = JSON.parse(this.dataset.adminDetailRecord);
        const target = document.querySelector('[data-admin-detail]');
        target.replaceChildren();
        document.querySelector('[data-detail-status]').textContent = this.dataset.statusLabel;
        Object.entries(record.Cells).filter(([key]) => key !== 'group').forEach(([key, value]) => {
            const label = document.createElement('dt');
            label.textContent = fieldLabels[key] || key;
            if (key === 'borrowed') label.textContent = record.Cells.due ? 'Ngày mượn' : 'Đang mượn';
            const text = document.createElement('dd');
            text.textContent = value || 'Chưa được cập nhật';
            target.append(label, text);
        });
    });
    document.getElementById('adminEditor')?.addEventListener('shown.bs.modal', () => editor?.querySelector('input, select')?.focus());
    document.querySelectorAll('[data-admin-editor], [data-admin-settings]').forEach(form => {
        form.addEventListener('submit', event => {
            event.preventDefault();
            const message = form.querySelector('[data-admin-validation]');
            form.classList.add('was-validated');
            if (!form.checkValidity()) {
                message.hidden = true;
                form.querySelector(':invalid')?.focus();
                return;
            }
            form.classList.remove('was-validated');
            message.hidden = false;
        });
        form.addEventListener('input', () => {
            const message = form.querySelector('[data-admin-validation]');
            if (message) message.hidden = true;
        });
        form.addEventListener('reset', () => resetValidation(form));
    });

    // Progressive enhancement: all list routes also render complete pages without JavaScript.
    const filter = document.querySelector('[data-admin-filter]');
    const results = document.querySelector('[data-admin-results]');
    let pending = null;
    let requestId = 0;
    function loadList(url, focusResults = false) {
        const id = ++requestId;
        pending?.abort();
        const panel = results.closest('.admin-directory');
        const error = document.querySelector('[data-admin-list-error]');
        const announcement = document.querySelector('[data-admin-list-status]');
        panel.classList.add('is-loading');
        results.setAttribute('aria-busy', 'true');
        error.hidden = true;
        if (announcement) announcement.textContent = 'Đang tải danh sách.';
        pending = $.ajax({ url, method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .done((html, textStatus, xhr) => {
                if (id !== requestId) return;
                if (xhr.getResponseHeader('X-Admin-Partial') !== '1') {
                    if (announcement) announcement.textContent = '';
                    error.textContent = 'Chưa tải được danh sách. Mở lại trang để kiểm tra phiên đăng nhập.';
                    error.hidden = false;
                    return;
                }
                results.innerHTML = html;
                if (announcement) announcement.textContent = results.querySelector('.admin-result-head p')?.textContent || 'Đã tải danh sách.';
                history.pushState({ adminList: true }, '', url);
                if (focusResults) {
                    const heading = results.querySelector('.admin-result-head');
                    heading?.setAttribute('tabindex', '-1');
                    heading?.focus({ preventScroll: true });
                }
            })
            .fail((xhr) => {
                if (xhr.statusText !== 'abort' && id === requestId) {
                    error.hidden = false;
                    if (announcement) announcement.textContent = '';
                }
            })
            .always(() => {
                if (id !== requestId) return;
                panel.classList.remove('is-loading');
                results.removeAttribute('aria-busy');
            });
    }
    if (filter && results) {
        filter.addEventListener('submit', event => {
            event.preventDefault();
            const params = new URLSearchParams(new FormData(filter));
            params.set('pageNo', '1');
            loadList(filter.action + '?' + params.toString());
        });
        $(document).on('click', '[data-admin-results] .pagination a', function (event) {
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            loadList(this.href, true);
        });
        window.addEventListener('popstate', () => location.reload());
    }

    const circulation = document.querySelector('[data-admin-circulation]');
    if (!circulation) return;
    if (window.matchMedia('(pointer: fine)').matches) {
        const scan = document.getElementById('copyBarcode');
        const target = scan && !scan.matches(':disabled') ? scan : document.getElementById('readerCard');
        target?.focus();
        target?.select();
    }
    const validate = document.querySelector('[data-draft-validate]');
    const count = document.querySelector('[data-draft-count]');
    const result = document.querySelector('[data-draft-result]');
    const formatMoney = value => new Intl.NumberFormat('vi-VN').format(value) + ' ₫';
    if (circulation.dataset.returning === 'true') {
        const rows = [...document.querySelectorAll('[data-return-row]')];
        const all = document.querySelector('[data-return-all]');
        function updateReturn() {
            const selected = rows.filter(row => row.querySelector('[data-return-check]').checked);
            count.textContent = String(selected.length);
            validate.disabled = selected.length === 0;
            document.querySelector('[data-return-total]').textContent = formatMoney(selected.reduce((sum, row) => sum + Number(row.dataset.fine), 0));
            rows.forEach(row => row.querySelector('[data-condition-note]').hidden = row.querySelector('[data-return-condition]').value === 'normal');
            if (all) {
                all.checked = selected.length === rows.length && rows.length > 0;
                all.indeterminate = selected.length > 0 && selected.length < rows.length;
            }
            result.hidden = true;
        }
        rows.forEach(row => row.addEventListener('change', updateReturn));
        all?.addEventListener('change', () => {
            rows.forEach(row => row.querySelector('[data-return-check]').checked = all.checked);
            updateReturn();
        });
        validate.addEventListener('click', () => {
            const selected = rows.filter(row => row.querySelector('[data-return-check]').checked);
            if (!selected.length) return;
            const damaged = selected.some(row => row.querySelector('[data-return-condition]').value !== 'normal');
            result.textContent = `Đã kiểm tra ${selected.length} tài liệu nhận trả. ${damaged ? 'Có tài liệu hỏng hoặc mất cần xác nhận thêm. ' : ''}Đây là bản xem thử; chưa ghi nhận trả sách hoặc thu phạt.`;
            result.hidden = false;
        });
        return;
    }
    const draft = new Map();
    const add = document.querySelector('[data-draft-add]');
    const barcode = document.getElementById('copyBarcode');
    const body = document.querySelector('[data-draft-rows]');
    const error = document.querySelector('[data-draft-error]');
    const empty = document.querySelector('[data-draft-empty]');
    function renderDraft() {
        body.replaceChildren();
        draft.forEach((entry, key) => {
            const row = document.createElement('tr');
            [key, entry.title, circulation.dataset.due].forEach(value => {
                const cell = document.createElement('td');
                cell.textContent = value;
                row.append(cell);
            });
            const cell = document.createElement('td');
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'admin-icon-button';
            remove.textContent = '×';
            remove.setAttribute('aria-label', 'Bỏ ' + entry.title + ' khỏi phiếu');
            remove.addEventListener('click', () => { draft.delete(key); renderDraft(); barcode.focus(); });
            cell.append(remove);
            row.append(cell);
            body.append(row);
        });
        count.textContent = String(draft.size);
        empty.hidden = draft.size > 0;
        validate.disabled = draft.size === 0;
        result.hidden = true;
    }
    function showScanError(message) {
        error.textContent = message;
        error.hidden = false;
        barcode.setAttribute('aria-invalid', 'true');
        barcode.focus();
        barcode.select();
    }
    add.addEventListener('submit', event => {
        event.preventDefault();
        if (barcode.disabled || barcode.closest('fieldset').disabled) return;
        const code = barcode.value.trim().toUpperCase();
        const option = [...document.querySelectorAll('#sampleBookCopies option')].find(item => item.value === code);
        if (!option) { showScanError('Không tìm thấy bản sao sẵn sàng với mã này. Chọn một mã trong gợi ý.'); return; }
        if (draft.has(code)) { showScanError('Bản sao đã có trong phiếu. Quét một mã khác.'); return; }
        if (draft.size >= Number(circulation.dataset.limit)) { showScanError('Phiếu đã đạt hạn mức của độc giả. Bỏ bớt tài liệu trước khi thêm.'); return; }
        draft.set(code, { title: option.dataset.title });
        error.hidden = true;
        barcode.removeAttribute('aria-invalid');
        barcode.value = '';
        renderDraft();
        barcode.focus();
    });
    barcode.addEventListener('input', () => { error.hidden = true; barcode.removeAttribute('aria-invalid'); });
    validate.addEventListener('click', () => {
        if (!draft.size) return;
        result.textContent = `Phiếu gồm ${draft.size} tài liệu, hạn trả dự kiến ${circulation.dataset.due}. Đây là bản xem thử; chưa ghi nhận mượn sách.`;
        result.hidden = false;
    });
})(jQuery);
