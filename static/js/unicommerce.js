// GHC Uni-Commerce module view — invoice builder.
// Same wiring pattern as flat_order.js but against the invoice/RowItems schema.
// Every endpoint is prefixed with window.MODULE_BASE ("/modules/ghc_unicommerce").

class UnicommerceTool {
    constructor() {
        this.currentTheme = localStorage.getItem('theme') || 'light';
        this.currentColor = localStorage.getItem('themeColor') || 'blue';
        this.base = (window.MODULE_BASE || '').replace(/\/$/, '');
        this.init();
    }

    url(path) { return this.base + path; }

    init() {
        this.initializeTheme();
        this.setupEventListeners();
        this.setupApiEventListeners();
        this.updateEndpointUrl();
        this.recalculateTotals();
        console.log('UnicommerceTool initialized');
    }

    // ----- Alerts -----
    showAlert(message, type = 'danger') {
        try {
            const div = document.createElement('div');
            div.className = `alert alert-${type} alert-dismissible fade show`;
            div.innerHTML = `<i class="bi ${this.getAlertIcon(type)} me-2"></i>${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>`;
            const main = document.querySelector('.main-content');
            if (main) {
                const existing = main.querySelectorAll('.alert');
                if (existing.length) main.insertBefore(div, existing[0]);
                else main.insertBefore(div, main.firstChild);
                setTimeout(() => { if (div.parentNode) new bootstrap.Alert(div).close(); }, 5000);
            }
        } catch (e) { alert(`${type.toUpperCase()}: ${message}`); }
    }

    getAlertIcon(type) {
        return { success: 'bi-check-circle', danger: 'bi-exclamation-triangle', warning: 'bi-exclamation-circle', info: 'bi-info-circle' }[type] || 'bi-info-circle';
    }

    // ----- Theme -----
    toggleTheme() {
        const html = document.documentElement;
        this.currentTheme = this.currentTheme === 'dark' ? 'light' : 'dark';
        html.setAttribute('data-bs-theme', this.currentTheme);
        const btn = document.querySelector('.theme-toggle-btn');
        if (btn) {
            btn.querySelector('i').className = this.currentTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
            btn.querySelector('span').textContent = this.currentTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
        }
        localStorage.setItem('theme', this.currentTheme);
    }

    initializeTheme() {
        const html = document.documentElement;
        html.setAttribute('data-bs-theme', this.currentTheme);
        const savedColor = localStorage.getItem('themeColor') || 'blue';
        if (savedColor !== 'blue') html.classList.add(savedColor);
        const btn = document.querySelector('.theme-toggle-btn');
        if (btn) {
            btn.querySelector('i').className = this.currentTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
            btn.querySelector('span').textContent = this.currentTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
        }
    }

    // ----- Field persistence -----
    postField(endpoint, field, value) {
        return fetch(this.url(endpoint), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ field, value })
        }).then(r => r.json()).catch(e => console.error('field update failed', e));
    }

    bindFieldGroup(selector, endpoint, onAfter) {
        document.querySelectorAll(selector).forEach(el => {
            el.addEventListener('change', () => {
                const value = el.type === 'checkbox' ? el.checked : el.value;
                this.postField(endpoint, el.dataset.field, value).then(() => {
                    if (onAfter) onAfter();
                    this.markUpdated(el);
                });
            });
        });
    }

    markUpdated(el) {
        el.classList.add('field-updated');
        setTimeout(() => el.classList.remove('field-updated'), 800);
    }

    // ----- Totals (computed by backend serializer) -----
    recalculateTotals() {
        fetch(this.url('/calculate-invoice-totals'))
            .then(r => r.json())
            .then(data => {
                if (data.error) return;
                const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = parseFloat(v).toFixed(2); };
                set('grossAmount', data.gross_amount);
                set('totalDiscount', data.total_discount);
                set('totalVat', data.total_vat);
                set('netAmount', data.net_amount);
                set('customerCreditAmount', data.customer_credit_amount);
                const sidebar = document.getElementById('sidebar-total');
                if (sidebar) sidebar.textContent = parseFloat(data.net_amount).toFixed(2);
            })
            .catch(e => console.error('totals failed', e));
    }

    // ----- Return toggle -----
    setupReturnToggle() {
        const toggle = document.getElementById('isReturnToggle');
        const wrapper = document.getElementById('parentRefWrapper');
        if (!toggle || !wrapper) return;
        toggle.addEventListener('change', () => {
            wrapper.style.display = toggle.checked ? 'block' : 'none';
            this.postField('/update-invoice-field', 'is_return', toggle.checked);
        });
    }

    // ----- Consumer lookup -----
    handleConsumerLookup() {
        const input = document.getElementById('consumerLookupPhone');
        const status = document.getElementById('consumerLookupStatus');
        const phone = (input?.value || '').trim();
        if (!phone) { this.showAlert('Enter a phone number to look up', 'warning'); return; }
        if (status) { status.textContent = 'Searching…'; status.className = 'text-muted'; }

        fetch(this.url('/get-consumer-details?phone=') + encodeURIComponent(phone))
            .then(r => { if (r.status === 404) return { found: false }; if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(data => {
                if (data.error) throw new Error(data.error);
                if (!data.found) {
                    if (status) { status.textContent = 'No consumer found — enter details manually.'; status.className = 'text-warning'; }
                    return;
                }
                this.prefillConsumer(data.consumer || {});
                if (status) { status.textContent = 'Consumer loaded.'; status.className = 'text-success'; }
            })
            .catch(e => { if (status) { status.textContent = 'Lookup failed: ' + e.message; status.className = 'text-danger'; } });
    }

    prefillConsumer(c) {
        const set = (id, val) => { const el = document.getElementById(id); if (el && val) el.value = val; };
        set('consFirstName', c.first_name);
        set('consMiddleName', c.middle_name);
        set('consLastName', c.last_name);
        set('consCode', c.consumer_code);
        set('consGender', c.gender);
        if (c.birth_date) set('consBirthDate', ('' + c.birth_date).slice(0, 10));
        set('consPhone', c.primary_phone_number);
        set('consEmail', c.email);
        set('consNationalId', c.national_id);
        set('consNationality', c.nationality);

        // Persist each consumer field
        document.querySelectorAll('.consumer-field').forEach(el => {
            this.postField('/update-consumer-field', el.dataset.field, el.value);
        });
    }

    // ----- Item lookup -----
    handleItemLookup() {
        const form = document.getElementById('itemLookupForm');
        if (!form) return;
        const fd = new FormData(form);
        const mat = fd.get('material_number');
        if (!mat || !/^\d{6}$/.test(mat)) { this.showAlert('Material number must be exactly 6 digits', 'warning'); return; }
        const params = new URLSearchParams(fd);
        this.showLoadingState('itemLookupForm', true);
        fetch(this.url('/get-item-details?') + params)
            .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(data => { if (data.error) throw new Error(data.error); this.displayItemDetails(data); })
            .catch(e => this.showAlert('Error fetching item: ' + e.message, 'danger'))
            .finally(() => this.showLoadingState('itemLookupForm', false));
    }

    displayItemDetails(data) {
        const tbody = document.getElementById('itemDetails');
        const results = document.getElementById('itemResults');
        if (!tbody || !results) return;
        tbody.innerHTML = `
            <tr><th>Material Number</th><td>${this.escapeHtml(data.material_number)}</td></tr>
            <tr><th>Barcode</th><td>${this.escapeHtml(data.barcode)}</td></tr>
            <tr><th>Name</th><td>${this.escapeHtml(data.item_name)}</td></tr>
            <tr><th>Unit Price</th><td>${parseFloat(data.item_price).toFixed(2)}</td></tr>
            <tr><th>VAT %</th><td>${parseFloat(data.vat_percentage).toFixed(2)}%</td></tr>
        `;
        results.style.display = 'block';
        const btn = document.getElementById('addItemToOrder');
        if (btn) btn.onclick = () => this.addItemFromDb(data);
    }

    addItemFromDb(data) {
        this.showLoadingState('addItemToOrder', true);
        fetch(this.url('/add-row-item-from-db'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        })
            .then(r => r.json())
            .then(res => {
                if (res.success) { this.showAlert('Row item added!', 'success'); setTimeout(() => window.location.reload(), 900); }
                else throw new Error(res.error || 'Unknown error');
            })
            .catch(e => this.showAlert('Error adding row item: ' + e.message, 'danger'))
            .finally(() => this.showLoadingState('addItemToOrder', false));
    }

    // ----- Row item edit/remove -----
    editRowItem(index) {
        fetch(this.url('/get-row-item/') + index)
            .then(r => r.json())
            .then(data => {
                if (data.error) throw new Error(data.error);
                const set = (id, v) => { const el = document.getElementById(id); if (el) el.value = v || ''; };
                set('editMaterialNumber', data.material_number);
                set('editBarcode', data.barcode);
                set('editQuantity', data.quantity);
                set('editItemPrice', data.item_price);
                set('editItemDiscount', data.item_discount);
                set('editVatPercentage', data.vat_percentage);
                set('editBatchNumber', data.batch_number);
                set('editExpireDate', (data.expire_date || '').slice(0, 10));
                set('editSerialNumber', data.serial_number);
                set('editScannedCode', data.scanned_code);
                set('editOfferIdentifier', data.offer_identifier);
                document.getElementById('editRowItemForm').action = this.url('/update-row-item/') + index;
                new bootstrap.Modal(document.getElementById('editRowItemModal')).show();
            })
            .catch(e => this.showAlert('Error loading row item: ' + e.message, 'danger'));
    }

    removeRowItem(index) {
        if (!confirm('Remove this row item?')) return;
        fetch(this.url('/remove-row-item/') + index, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(() => window.location.reload())
            .catch(e => this.showAlert('Error removing row item: ' + e.message, 'danger'));
    }

    // ----- DB test -----
    testDatabaseConnection() {
        const form = document.getElementById('databaseForm');
        if (!form) return;
        const data = Object.fromEntries(new FormData(form).entries());
        this.showLoadingState('testDatabase', true);
        fetch(this.url('/test-database-connection'), {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data)
        })
            .then(r => r.json())
            .then(d => { if (d.success) this.showAlert('Database connection successful!', 'success'); else throw new Error(d.message || 'failed'); })
            .catch(e => this.showAlert('DB test failed: ' + e.message, 'danger'))
            .finally(() => this.showLoadingState('testDatabase', false));
    }

    // ----- API response / preview / send -----
    displayApiResponse(responseData) {
        const display = document.getElementById('responseDisplay');
        const statusCode = document.getElementById('statusCode');
        const requestUrl = document.getElementById('requestUrl');
        if (!display || !statusCode || !requestUrl) return;
        let formatted;
        try { formatted = JSON.stringify(JSON.parse(responseData.response_text), null, 2); }
        catch (e) { formatted = responseData.response_text; }
        display.textContent = `=== API Response - ${new Date().toLocaleString()} ===\nStatus: ${responseData.status_code}\nURL: ${responseData.url_sent}\nSuccess: ${responseData.success}\n\nResponse Body:\n${formatted}\n`;
        statusCode.value = responseData.status_code;
        statusCode.className = 'form-control ' + (responseData.status_code >= 200 && responseData.status_code < 300 ? 'status-success' : responseData.status_code >= 400 ? 'status-error' : 'status-warning');
        requestUrl.value = responseData.url_sent;
        if (responseData.success) this.showAlert(`Request sent! Status: ${responseData.status_code}`, 'success');
        else this.showAlert(`Request failed: ${responseData.status_code}`, 'danger');
    }

    clearResponseDisplay() {
        const d = document.getElementById('responseDisplay');
        if (d) d.textContent = 'No response yet. Send a request to see the response here.';
        const sc = document.getElementById('statusCode'); if (sc) { sc.value = ''; sc.className = 'form-control'; }
        const ru = document.getElementById('requestUrl'); if (ru) ru.value = '';
    }

    copyResponseToClipboard() {
        const d = document.getElementById('responseDisplay');
        if (d && d.textContent) navigator.clipboard.writeText(d.textContent).then(() => this.showAlert('Response copied!', 'success'));
    }

    previewRequest() {
        fetch(this.url('/export-json'))
            .then(r => r.json())
            .then(payload => {
                const formatted = JSON.stringify(payload, null, 2);
                const html = `
                    <div class="modal fade" id="requestPreviewModal" tabindex="-1">
                        <div class="modal-dialog modal-xl"><div class="modal-content">
                            <div class="modal-header"><h5 class="modal-title">Request Preview</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button></div>
                            <div class="modal-body">
                                <div class="alert alert-info"><i class="bi bi-info-circle me-2"></i>JSON that will be sent to the API.</div>
                                <pre style="max-height:500px;overflow-y:auto;background:#f8f9fa;padding:1rem;border-radius:.375rem;font-size:.875rem;">${this.escapeHtml(formatted)}</pre>
                            </div>
                            <div class="modal-footer"><button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button></div>
                        </div></div>
                    </div>`;
                document.getElementById('requestPreviewModal')?.remove();
                document.body.insertAdjacentHTML('beforeend', html);
                new bootstrap.Modal(document.getElementById('requestPreviewModal')).show();
            })
            .catch(() => this.showAlert('Error previewing request', 'danger'));
    }

    // ----- API URL management -----
    updateEndpointUrl() {
        const select = document.getElementById('apiEndpoint');
        const span = document.getElementById('selectedUrl');
        if (select && span) {
            const opt = select.options[select.selectedIndex];
            span.textContent = (opt && opt.value && window.apiUrls[opt.value]) ? window.apiUrls[opt.value] : '';
        }
    }

    // ----- Event listeners -----
    setupEventListeners() {
        const on = (id, ev, fn) => { const el = document.getElementById(id); if (el) el.addEventListener(ev, fn); };

        const themeBtn = document.querySelector('.theme-toggle-btn');
        if (themeBtn) themeBtn.addEventListener('click', () => this.toggleTheme());

        // Field groups -> persist + recalc
        this.bindFieldGroup('.invoice-field', '/update-invoice-field', () => this.recalculateTotals());
        this.bindFieldGroup('.consumer-field', '/update-consumer-field');
        this.bindFieldGroup('.delivery-field', '/update-delivery-field', () => this.recalculateTotals());

        this.setupReturnToggle();

        on('consumerLookupBtn', 'click', () => this.handleConsumerLookup());
        on('consumerLookupPhone', 'keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); this.handleConsumerLookup(); } });

        const lookupForm = document.getElementById('itemLookupForm');
        if (lookupForm) lookupForm.addEventListener('submit', e => { e.preventDefault(); this.handleItemLookup(); });

        on('testDatabase', 'click', () => this.testDatabaseConnection());

        document.querySelectorAll('.edit-row-item').forEach(b => b.addEventListener('click', () => this.editRowItem(parseInt(b.dataset.index))));
        document.querySelectorAll('.remove-row-item').forEach(b => b.addEventListener('click', () => this.removeRowItem(parseInt(b.dataset.index))));

        // Reload after add/edit modals submit (server redirects back)
        ['addRowItemForm', 'editRowItemForm'].forEach(id => {
            const f = document.getElementById(id);
            if (f) f.addEventListener('submit', () => { /* normal POST -> redirect */ });
        });

        // Tab persistence
        document.querySelectorAll('a[data-bs-toggle="tab"]').forEach(tab => {
            tab.addEventListener('shown.bs.tab', e => localStorage.setItem('activeTab', e.target.getAttribute('href')));
        });
        const activeTab = localStorage.getItem('activeTab');
        if (activeTab) { const tab = document.querySelector(`a[href="${activeTab}"]`); if (tab) new bootstrap.Tab(tab).show(); }

        document.addEventListener('keydown', e => {
            if (e.ctrlKey && e.key === 't') { e.preventDefault(); this.toggleTheme(); }
        });
    }

    setupApiEventListeners() {
        const on = (id, ev, fn) => { const el = document.getElementById(id); if (el) el.addEventListener(ev, fn); };
        const endpointSelect = document.getElementById('apiEndpoint');
        if (endpointSelect) endpointSelect.addEventListener('change', () => this.updateEndpointUrl());
        on('clearResponse', 'click', () => this.clearResponseDisplay());
        on('copyResponse', 'click', () => this.copyResponseToClipboard());
        on('previewRequest', 'click', () => this.previewRequest());

        const customUrl = document.getElementById('customUrl');
        if (customUrl) customUrl.addEventListener('input', () => {
            if (customUrl.value && endpointSelect) { endpointSelect.value = ''; const s = document.getElementById('selectedUrl'); if (s) s.textContent = ''; }
        });

        const form = document.getElementById('apiRequestForm');
        if (form) form.addEventListener('submit', (e) => {
            e.preventDefault();
            const ep = document.getElementById('apiEndpoint');
            const cu = document.getElementById('customUrl');
            const validate = document.getElementById('validateBeforeSend');
            if (!ep.value && !cu.value) { this.showAlert('Select an endpoint or enter a custom URL', 'warning'); return; }

            const submitBtn = document.getElementById('sendRequestBtn');
            if (submitBtn) { submitBtn.disabled = true; submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Sending...'; }

            const formData = new FormData(form);
            if (validate && validate.checked) formData.set('validateBeforeSend', 'true'); else formData.delete('validateBeforeSend');

            fetch(this.url('/send-request'), { method: 'POST', body: formData, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(r => r.json().then(data => ({ status: r.status, body: data })))
                .then(({ status, body }) => {
                    this.displayApiResponse(body);
                    if (status === 400 && body.response_text) {
                        try { const p = JSON.parse(body.response_text); if (p.validation_errors) this.showAlert('Validation failed. Please check errors.', 'danger'); } catch (e) { }
                    }
                })
                .catch(err => this.showAlert('Error sending request: ' + err.message, 'danger'))
                .finally(() => { if (submitBtn) { submitBtn.disabled = false; submitBtn.innerHTML = '<i class="bi bi-send me-2"></i>Send Request'; } });
        });
    }

    // ----- Utilities -----
    showLoadingState(id, isLoading) {
        const el = document.getElementById(id);
        if (!el) return;
        if (isLoading) { el.disabled = true; el.setAttribute('data-original-text', el.innerHTML); el.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Loading...'; }
        else { el.disabled = false; const t = el.getAttribute('data-original-text'); if (t) el.innerHTML = t; }
    }

    escapeHtml(s) {
        if (s === null || s === undefined) return '';
        return s.toString().replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#039;');
    }
}

document.addEventListener('DOMContentLoaded', function () {
    window.unicommerceTool = new UnicommerceTool();
    if (window.responseData) window.unicommerceTool.displayApiResponse(window.responseData);
    if (window.cancelResponseData) window.unicommerceTool.displayApiResponse(window.cancelResponseData);
});
