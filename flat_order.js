// Flat-order module view (GHC/UPC E-Commerce) — ported from the pre-refactor
// script.js. Every endpoint is prefixed with window.MODULE_BASE
// ("/modules/<module_key>") so the same file serves both flat-order modules.

class FlatOrderTool {
    constructor() {
        this.currentTheme = localStorage.getItem('theme') || 'light';
        this.currentColor = localStorage.getItem('themeColor') || 'blue';
        this.calculationTimeout = null;

        // Base path for module-scoped endpoints, e.g. "/modules/ghc_ecommerce"
        this.base = (window.MODULE_BASE || '').replace(/\/$/, '');

        this.paymentOptions = window.paymentOptions || {};

        this.init();
    }

    // Build a module-scoped URL: this.url('/calculate-totals')
    url(path) {
        return this.base + path;
    }

    init() {
        this.initializeTheme();
        this.setupEventListeners();
        this.setupAutoCalculation();
        this.initializeApiUrls();
        this.setupApiEventListeners();
        this.updateEndpointUrl();

        console.log('FlatOrderTool initialized for', window.MODULE_KEY);
    }

    // ----- Alert system -----
    showAlert(message, type = 'danger') {
        try {
            const alertDiv = document.createElement('div');
            alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
            alertDiv.innerHTML = `
                <i class="bi ${this.getAlertIcon(type)} me-2"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            `;
            const mainContent = document.querySelector('.main-content');
            if (mainContent) {
                const existing = mainContent.querySelectorAll('.alert');
                if (existing.length > 0) {
                    mainContent.insertBefore(alertDiv, existing[0]);
                } else {
                    mainContent.insertBefore(alertDiv, mainContent.firstChild);
                }
                setTimeout(() => {
                    if (alertDiv.parentNode) {
                        new bootstrap.Alert(alertDiv).close();
                    }
                }, 5000);
            }
        } catch (e) {
            alert(`${type.toUpperCase()}: ${message}`);
        }
    }

    getAlertIcon(type) {
        return {
            success: 'bi-check-circle',
            danger: 'bi-exclamation-triangle',
            warning: 'bi-exclamation-circle',
            info: 'bi-info-circle'
        }[type] || 'bi-info-circle';
    }

    // ----- Theme -----
    setThemeColor(color) {
        const html = document.documentElement;
        html.classList.remove('blue', 'purple', 'green', 'orange', 'red');
        if (color !== 'blue') html.classList.add(color);
        localStorage.setItem('themeColor', color);
        this.currentColor = color;
    }

    toggleTheme() {
        const html = document.documentElement;
        this.currentTheme = this.currentTheme === 'dark' ? 'light' : 'dark';
        html.setAttribute('data-bs-theme', this.currentTheme);
        const btn = document.querySelector('.theme-toggle-btn');
        if (btn) {
            const icon = btn.querySelector('i');
            const text = btn.querySelector('span');
            icon.className = this.currentTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
            text.textContent = this.currentTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
        }
        localStorage.setItem('theme', this.currentTheme);
    }

    initializeTheme() {
        const html = document.documentElement;
        html.setAttribute('data-bs-theme', this.currentTheme);
        this.setThemeColor(this.currentColor);
        const btn = document.querySelector('.theme-toggle-btn');
        if (btn) {
            const icon = btn.querySelector('i');
            const text = btn.querySelector('span');
            icon.className = this.currentTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
            text.textContent = this.currentTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
        }
    }

    // ----- Calculations -----
    calculateTotals() {
        this.showLoadingState('calculateTotals', true);
        fetch(this.url('/calculate-totals'))
            .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(data => {
                if (data.error) throw new Error(data.error);
                this.updateTotalsDisplay(data);
            })
            .catch(e => this.showAlert('Error calculating totals: ' + e.message, 'danger'))
            .finally(() => this.showLoadingState('calculateTotals', false));
    }

    updateTotalsDisplay(data) {
        const elements = {
            productsTotal: data.products_total,
            orderDiscount: data.order_discount,
            deliveryCost: data.delivery_cost,
            finalTotal: data.final_total,
            totalPaid: data.total_paid,
            remainingAmount: data.remaining_amount
        };
        Object.keys(elements).forEach(id => {
            const el = document.getElementById(id);
            if (el) el.textContent = parseFloat(elements[id]).toFixed(2);
        });
        const sidebarTotal = document.getElementById('sidebar-total');
        if (sidebarTotal) sidebarTotal.textContent = parseFloat(data.final_total).toFixed(2);
    }

    calculateEstimatedTotal() {
        const q = parseFloat(document.querySelector('#addProductModal input[name="quantity"]')?.value) || 0;
        const p = parseFloat(document.querySelector('#addProductModal input[name="unit_price"]')?.value) || 0;
        const v = parseFloat(document.querySelector('#addProductModal input[name="vat_percentage"]')?.value) || 0;
        const d = parseFloat(document.querySelector('#addProductModal input[name="discount"]')?.value) || 0;
        let vat = v > 1 ? v / 100 : v;
        const total = (q * p - d) + (q * p - d) * vat;
        const el = document.getElementById('estimatedTotal');
        if (el) el.value = '$' + total.toFixed(2);
    }

    calculateEditEstimatedTotal() {
        const q = parseFloat(document.getElementById('editQuantity')?.value) || 0;
        const p = parseFloat(document.getElementById('editUnitPrice')?.value) || 0;
        const v = parseFloat(document.getElementById('editVatPercentage')?.value) || 0;
        const d = parseFloat(document.getElementById('editDiscount')?.value) || 0;
        let vat = v > 1 ? v / 100 : v;
        const total = (q * p - d) + (q * p - d) * vat;
        const el = document.getElementById('editEstimatedTotal');
        if (el) el.value = '$' + total.toFixed(2);
    }

    setupAutoCalculation() {
        setTimeout(() => this.calculateTotals(), 800);
    }

    // ----- Real-time field updates -----
    setupRealTimeUpdates() {
        document.querySelectorAll('.order-field').forEach(field => {
            field.addEventListener('change', () => {
                this.updateOrderField(field.dataset.field, field.value);
            });
        });
    }

    updateOrderField(field, value) {
        fetch(this.url('/update-order-field'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ field, value })
        })
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    const el = document.querySelector(`[data-field="${field}"]`);
                    if (el) {
                        el.classList.add('field-updated');
                        setTimeout(() => el.classList.remove('field-updated'), 1000);
                    }
                }
            })
            .catch(e => console.error('Error updating field:', e));
    }

    // ----- Payment options/validation -----
    updatePaymentOptions(method, isEdit = false) {
        const getId = (b) => isEdit ? 'edit' + b.charAt(0).toUpperCase() + b.slice(1)
            : b.charAt(0).toLowerCase() + b.slice(1);
        const optionSelect = document.getElementById(getId('paymentOption'));
        const creditInfo = document.getElementById(getId('creditCustomerInfo'));
        const statusSelect = document.getElementById(getId('paymentStatus'));
        const amountField = document.getElementById(getId('paymentAmount'));
        if (!optionSelect) return;

        optionSelect.innerHTML = '<option value="">Select Option</option>';
        if (method && this.paymentOptions[method]) {
            this.paymentOptions[method].forEach(opt => {
                const o = document.createElement('option');
                o.value = opt;
                o.textContent = opt.charAt(0).toUpperCase() + opt.slice(1);
                optionSelect.appendChild(o);
            });
            if (this.paymentOptions[method].length === 1) {
                optionSelect.value = this.paymentOptions[method][0];
            }
        }
        if (creditInfo) {
            creditInfo.style.display = ['COD', 'Visa', 'PostToCredit'].includes(method) ? 'block' : 'none';
        }
        if (statusSelect && amountField) {
            if (method === 'COD') {
                if (!statusSelect.value) statusSelect.value = 'not_payment';
                if (!amountField.value) this.setPaymentAmountToFullTotal(amountField);
            } else if (['Visa', 'Tamara', 'Tabby', 'MisPay', 'Emkan', 'YouGotaGift', 'OgMoney', 'RajhiPoints', 'QitafPoints', 'NeqatyPoints'].includes(method)) {
                if (!statusSelect.value) statusSelect.value = 'done_payment';
                if (!amountField.value) this.setPaymentAmountToFullTotal(amountField);
            }
        }
        this.validatePaymentForm(isEdit);
    }

    validatePaymentForm(isEdit) {
        const methodId = isEdit ? 'editPaymentMethod' : 'paymentMethod';
        const statusId = isEdit ? 'editPaymentStatus' : 'paymentStatus';
        const amountId = isEdit ? 'editPaymentAmount' : 'paymentAmount';
        const errorId = isEdit ? 'editPaymentError' : 'addPaymentError';
        const btnSel = isEdit ? '#editPaymentModal button[type="submit"]' : '#addPaymentModal button[type="submit"]';

        const method = document.getElementById(methodId)?.value;
        const status = document.getElementById(statusId)?.value;
        const amount = parseFloat(document.getElementById(amountId)?.value || 0);
        const errorEl = document.getElementById(errorId);
        const orderTotal = parseFloat(document.getElementById('finalTotal')?.textContent || 0) || 0;
        if (!method) return;

        let isValid = true, message = '';
        if (method === 'COD') {
            if (status && status !== 'not_payment') { isValid = false; message = 'COD must be "Not Payment"'; }
        } else {
            if (status && status !== 'done_payment') { isValid = false; message = method + ' must be "Done Payment"'; }
            if (Math.abs(amount - orderTotal) > 0.01) { isValid = false; message = `Amount must equal Order Total (${orderTotal})`; }
        }
        if (errorEl) { errorEl.textContent = message; errorEl.style.display = isValid ? 'none' : 'block'; }
        const btn = document.querySelector(btnSel);
        if (btn) btn.disabled = !isValid;
    }

    setPaymentAmountToFullTotal(field) {
        const el = document.getElementById('finalTotal');
        if (el && field) field.value = (parseFloat(el.textContent) || 0).toFixed(2);
    }

    // ----- Item lookup (by code) -----
    handleItemLookup() {
        const form = document.getElementById('itemLookupForm');
        if (!form) return;
        const formData = new FormData(form);
        const materialNumber = formData.get('material_number');
        if (!materialNumber || !/^\d{6}$/.test(materialNumber)) {
            this.showAlert('Material number must be exactly 6 digits', 'warning');
            return;
        }
        const params = new URLSearchParams(formData);
        this.showLoadingState('itemLookupForm', true);
        fetch(this.url('/get-item-details?') + params)
            .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(data => {
                if (data.error) throw new Error(data.error);
                this.displayItemDetails(data);
            })
            .catch(e => this.showAlert('Error fetching item details: ' + e.message, 'danger'))
            .finally(() => this.showLoadingState('itemLookupForm', false));
    }

    displayItemDetails(data) {
        const itemDetails = document.getElementById('itemDetails');
        const itemResults = document.getElementById('itemResults');
        if (!itemDetails || !itemResults) return;
        itemDetails.innerHTML = `
            <tr><th>Item Code</th><td>${this.escapeHtml(data.item_code)}</td></tr>
            <tr><th>Barcode</th><td>${this.escapeHtml(data.item_Barcode)}</td></tr>
            <tr><th>English Name</th><td>${this.escapeHtml(data.item_EN_Name)}</td></tr>
            <tr><th>Arabic Name</th><td>${this.escapeHtml(data.item_AR_Name)}</td></tr>
            <tr><th>Unit Price</th><td>${parseFloat(data.unit_price).toFixed(2)}</td></tr>
            <tr><th>VAT %</th><td>${parseFloat(data.vat_percentage).toFixed(2)}%</td></tr>
            <tr><th>Net Price</th><td>${parseFloat(data.net_price).toFixed(2)}</td></tr>
        `;
        itemResults.style.display = 'block';
        const btn = document.getElementById('addItemToOrder');
        if (btn) btn.onclick = () => this.addItemToOrder(data);
    }

    addItemToOrder(itemData) {
        this.showLoadingState('addItemToOrder', true);
        fetch(this.url('/add-product-from-db'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(itemData)
        })
            .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(result => {
                if (result.success) {
                    this.showAlert('Product added successfully!', 'success');
                    setTimeout(() => window.location.reload(), 1200);
                } else {
                    throw new Error(result.error || 'Unknown error adding product');
                }
            })
            .catch(e => this.showAlert('Error adding product: ' + e.message, 'danger'))
            .finally(() => this.showLoadingState('addItemToOrder', false));
    }

    // ----- Consumer lookup (by phone) -----
    handleConsumerLookup() {
        const input = document.getElementById('consumerLookupPhone');
        const status = document.getElementById('consumerLookupStatus');
        const phone = (input?.value || '').trim();
        if (!phone) {
            this.showAlert('Enter a phone number to look up', 'warning');
            return;
        }
        if (status) { status.textContent = 'Searching…'; status.className = 'text-muted'; }

        fetch(this.url('/get-consumer-details?phone=') + encodeURIComponent(phone))
            .then(r => {
                if (r.status === 404) return { found: false };
                if (!r.ok) throw new Error(`HTTP ${r.status}`);
                return r.json();
            })
            .then(data => {
                if (data.error) throw new Error(data.error);
                if (!data.found) {
                    if (status) { status.textContent = 'No consumer found for that phone.'; status.className = 'text-warning'; }
                    return;
                }
                this.prefillConsumer(data.consumer || {});
                if (status) { status.textContent = 'Consumer loaded.'; status.className = 'text-success'; }
            })
            .catch(e => {
                if (status) { status.textContent = 'Lookup failed: ' + e.message; status.className = 'text-danger'; }
            });
    }

    prefillConsumer(c) {
        const set = (id, val) => { const el = document.getElementById(id); if (el && val !== undefined && val !== null && val !== '') el.value = val; };
        set('clientFirstName', c.first_name);
        set('clientMiddleName', c.middle_name);
        set('clientLastName', c.last_name);
        set('clientEmail', c.email);
        set('clientPhone', c.phone);
        set('clientGender', c.gender);
        if (c.birthdate) set('clientBirthdate', ('' + c.birthdate).slice(0, 10));

        // Persist each prefilled field back to the draft
        ['first_name', 'middle_name', 'last_name', 'email', 'phone', 'gender', 'birthdate'].forEach(f => {
            const el = document.querySelector(`[data-field="${f}"]`);
            if (el) this.updateOrderField(f, el.value);
        });
    }

    // ----- Database test -----
    testDatabaseConnection() {
        const form = document.getElementById('databaseForm');
        if (!form) return;
        const data = Object.fromEntries(new FormData(form).entries());
        if (!data.server || !data.database || !data.username) {
            this.showAlert('Please fill in all required database fields', 'warning');
            return;
        }
        this.showLoadingState('testDatabase', true);
        fetch(this.url('/test-database-connection'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        })
            .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(d => {
                if (d.success) this.showAlert('Database connection successful!', 'success');
                else throw new Error(d.message || 'Database connection failed');
            })
            .catch(e => this.showAlert('Error testing database connection: ' + e.message, 'danger'))
            .finally(() => this.showLoadingState('testDatabase', false));
    }

    // ----- Edit product/payment -----
    editProduct(index) {
        if (index === undefined || index === null) return;
        fetch(this.url('/get-product/') + index)
            .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(data => { if (data.error) throw new Error(data.error); this.populateEditProductForm(data); })
            .catch(e => this.showAlert('Error loading product: ' + e.message, 'danger'));
    }

    populateEditProductForm(data) {
        document.getElementById('editItemCode').value = data.item_code || '';
        document.getElementById('editItemName').value = data.item_name || '';
        document.getElementById('editQuantity').value = data.quantity || 0;
        document.getElementById('editUnitPrice').value = data.unit_price || 0;
        document.getElementById('editVatPercentage').value = data.vat_percentage || 0;
        document.getElementById('editDiscount').value = data.row_total_discount || 0;
        document.getElementById('editOfferCode').value = data.offer_code || '';
        document.getElementById('editOfferMessage').value = data.offer_message || '';
        document.getElementById('editProductForm').action = this.url('/update-product/') + data.index;
        new bootstrap.Modal(document.getElementById('editProductModal')).show();
    }

    editPayment(index) {
        if (index === undefined || index === null) return;
        fetch(this.url('/get-payment/') + index)
            .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(data => { if (data.error) throw new Error(data.error); this.populateEditPaymentForm(data, index); })
            .catch(e => this.showAlert('Error loading payment: ' + e.message, 'danger'));
    }

    populateEditPaymentForm(data, index) {
        document.getElementById('editPaymentMethod').value = data.payment_method || '';
        document.getElementById('editPaymentStatus').value = data.payment_status || '';
        document.getElementById('editPaymentAmount').value = data.payment_amount || 0;
        document.getElementById('editTransactionId').value = data.transaction_id || '';
        document.getElementById('editPaymentOption').value = data.payment_option || '';
        document.getElementById('editOptionCommission').value = data.option_commission || 0;
        document.getElementById('editCreditCustomerInfo').style.display = 'block';
        if (data.credit_customer_info) {
            document.getElementById('editCustomerName').value = data.credit_customer_info.customer_name || '';
            document.getElementById('editCustomerNumber').value = data.credit_customer_info.customer_number || '';
        }
        this.updatePaymentOptions(data.payment_method, true);
        document.getElementById('editPaymentForm').action = this.url('/update-payment/') + index;
        new bootstrap.Modal(document.getElementById('editPaymentModal')).show();
    }

    // ----- Endpoint testing -----
    testAllEndpoints() {
        const table = document.getElementById('endpointsTable');
        if (!table) return;
        const rows = table.querySelectorAll('tr');
        const promises = [];
        rows.forEach(row => {
            const statusCell = row.querySelector('td:nth-child(3)');
            const btn = row.querySelector('.test-single-endpoint');
            if (statusCell && btn) {
                statusCell.innerHTML = '<span class="badge bg-info">Testing...</span>';
                btn.disabled = true;
                promises.push(
                    this.testSingleEndpoint(btn.dataset.name, btn.dataset.url).then(result => {
                        statusCell.innerHTML = `<span class="badge bg-${result.status === 'Online' ? 'success' : 'danger'}">${result.status}</span>`;
                        btn.disabled = false;
                    })
                );
            }
        });
        Promise.all(promises).then(() => this.showAlert('All endpoints tested!', 'success'));
    }

    testSingleEndpoint(name, url) {
        return fetch(this.url('/test-endpoint'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ url })
        })
            .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
            .then(data => data)
            .catch(e => ({ status: 'Error', error: e.message }));
    }

    // ----- API response display -----
    displayApiResponse(responseData) {
        const display = document.getElementById('responseDisplay');
        const statusCode = document.getElementById('statusCode');
        const requestUrl = document.getElementById('requestUrl');
        if (!display || !statusCode || !requestUrl) return;

        let formatted;
        try { formatted = JSON.stringify(JSON.parse(responseData.response_text), null, 2); }
        catch (e) { formatted = responseData.response_text; }

        const ts = new Date().toLocaleString();
        display.textContent =
            `=== API Response - ${ts} ===\n` +
            `Status: ${responseData.status_code}\n` +
            `URL: ${responseData.url_sent}\n` +
            `Success: ${responseData.success}\n\n` +
            `Response Body:\n${formatted}\n\n` + '='.repeat(50) + '\n\n';

        statusCode.value = responseData.status_code;
        statusCode.className = 'form-control ' + (
            responseData.status_code >= 200 && responseData.status_code < 300 ? 'status-success'
                : responseData.status_code >= 400 ? 'status-error' : 'status-warning');
        requestUrl.value = responseData.url_sent;

        const autoScroll = document.getElementById('autoScroll');
        if (autoScroll && autoScroll.checked) display.scrollTop = 0;

        if (responseData.success) this.showAlert(`Request sent! Status: ${responseData.status_code}`, 'success');
        else this.showAlert(`Request failed: ${responseData.status_code}`, 'danger');
    }

    clearResponseDisplay() {
        const display = document.getElementById('responseDisplay');
        const statusCode = document.getElementById('statusCode');
        const requestUrl = document.getElementById('requestUrl');
        if (display) display.textContent = 'No response yet. Send a request to see the response here.';
        if (statusCode) { statusCode.value = ''; statusCode.className = 'form-control'; }
        if (requestUrl) requestUrl.value = '';
    }

    copyResponseToClipboard() {
        const display = document.getElementById('responseDisplay');
        if (display && display.textContent) {
            navigator.clipboard.writeText(display.textContent)
                .then(() => this.showAlert('Response copied!', 'success'))
                .catch(() => this.showAlert('Failed to copy response', 'danger'));
        }
    }

    copyRequestToClipboard() {
        const pre = document.querySelector('#requestPreviewModal pre');
        if (pre && pre.textContent) {
            navigator.clipboard.writeText(pre.textContent).then(() => {
                this.showAlert('Request data copied!', 'success');
                const modal = bootstrap.Modal.getInstance(document.getElementById('requestPreviewModal'));
                if (modal) modal.hide();
            }).catch(() => this.showAlert('Failed to copy request data', 'danger'));
        }
    }

    previewRequest() {
        fetch(this.url('/export-json'))
            .then(r => r.json())
            .then(orderData => {
                const formatted = JSON.stringify(orderData, null, 2);
                const html = `
                    <div class="modal fade" id="requestPreviewModal" tabindex="-1">
                        <div class="modal-dialog modal-xl">
                            <div class="modal-content">
                                <div class="modal-header">
                                    <h5 class="modal-title">Request Preview</h5>
                                    <div>
                                        <button type="button" class="btn btn-success btn-sm me-2" id="copyRequestBtn">
                                            <i class="bi bi-clipboard-check me-1"></i>Copy Request
                                        </button>
                                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                    </div>
                                </div>
                                <div class="modal-body">
                                    <div class="alert alert-info">
                                        <i class="bi bi-info-circle me-2"></i>
                                        This is the JSON data that will be sent to the API endpoint.
                                    </div>
                                    <pre style="max-height:500px;overflow-y:auto;background:#f8f9fa;padding:1rem;border-radius:.375rem;font-size:.875rem;">${this.escapeHtml(formatted)}</pre>
                                </div>
                                <div class="modal-footer">
                                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                                    <button type="button" class="btn btn-primary" id="copyRequestBtnFooter">
                                        <i class="bi bi-clipboard-check me-1"></i>Copy Request Data
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>`;
                document.getElementById('requestPreviewModal')?.remove();
                document.body.insertAdjacentHTML('beforeend', html);
                const modal = new bootstrap.Modal(document.getElementById('requestPreviewModal'));
                document.getElementById('copyRequestBtn')?.addEventListener('click', () => this.copyRequestToClipboard());
                document.getElementById('copyRequestBtnFooter')?.addEventListener('click', () => this.copyRequestToClipboard());
                modal.show();
            })
            .catch(e => this.showAlert('Error previewing request', 'danger'));
    }

    // ----- Utilities -----
    showLoadingState(elementId, isLoading) {
        const el = document.getElementById(elementId);
        if (!el) return;
        if (isLoading) {
            el.disabled = true;
            el.setAttribute('data-original-text', el.innerHTML);
            el.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Loading...';
        } else {
            el.disabled = false;
            const t = el.getAttribute('data-original-text');
            if (t) el.innerHTML = t;
        }
    }

    escapeHtml(s) {
        if (s === null || s === undefined) return '';
        return s.toString()
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
    }

    // ----- API URL management -----
    initializeApiUrls() {
        window.apiUrls = window.apiUrls || {};
    }

    updateEndpointUrl() {
        const select = document.getElementById('apiEndpoint');
        const custom = document.getElementById('customUrl');
        const span = document.getElementById('selectedUrl');
        if (select && span) {
            const opt = select.options[select.selectedIndex];
            if (opt && opt.value && window.apiUrls[opt.value]) {
                span.textContent = window.apiUrls[opt.value];
                if (custom) custom.value = '';
            } else {
                span.textContent = '';
            }
        }
    }

    // ----- Event listeners -----
    setupEventListeners() {
        const on = (id, ev, fn) => { const el = document.getElementById(id); if (el) el.addEventListener(ev, fn); };

        on('calculateTotals', 'click', () => this.calculateTotals());

        const themeBtn = document.querySelector('.theme-toggle-btn');
        if (themeBtn) themeBtn.addEventListener('click', () => this.toggleTheme());

        const pm = document.getElementById('paymentMethod');
        if (pm) pm.addEventListener('change', () => this.updatePaymentOptions(pm.value, false));
        const epm = document.getElementById('editPaymentMethod');
        if (epm) epm.addEventListener('change', () => this.updatePaymentOptions(epm.value, true));

        ['paymentStatus', 'paymentAmount'].forEach(id => on(id, 'change', () => this.validatePaymentForm(false)));
        ['paymentStatus', 'paymentAmount'].forEach(id => on(id, 'input', () => this.validatePaymentForm(false)));
        ['editPaymentStatus', 'editPaymentAmount'].forEach(id => on(id, 'change', () => this.validatePaymentForm(true)));
        ['editPaymentStatus', 'editPaymentAmount'].forEach(id => on(id, 'input', () => this.validatePaymentForm(true)));

        document.querySelectorAll('#addProductModal input').forEach(i => {
            if (['quantity', 'unit_price', 'vat_percentage', 'discount'].includes(i.name)) {
                i.addEventListener('input', () => this.calculateEstimatedTotal());
            }
        });
        document.querySelectorAll('#editProductModal input').forEach(i => {
            if (['quantity', 'unit_price', 'vat_percentage', 'discount'].includes(i.name)) {
                i.addEventListener('input', () => this.calculateEditEstimatedTotal());
            }
        });

        const lookupForm = document.getElementById('itemLookupForm');
        if (lookupForm) lookupForm.addEventListener('submit', e => { e.preventDefault(); this.handleItemLookup(); });

        on('testDatabase', 'click', () => this.testDatabaseConnection());
        on('testAllEndpoints', 'click', () => this.testAllEndpoints());
        on('consumerLookupBtn', 'click', () => this.handleConsumerLookup());
        on('consumerLookupPhone', 'keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); this.handleConsumerLookup(); } });

        document.querySelectorAll('.test-single-endpoint').forEach(btn => {
            btn.addEventListener('click', () => {
                this.testSingleEndpoint(btn.dataset.name, btn.dataset.url).then(result => {
                    const cell = btn.closest('tr').querySelector('td:nth-child(3)');
                    if (cell) cell.innerHTML = `<span class="badge bg-${result.status === 'Online' ? 'success' : 'danger'}">${result.status}</span>`;
                });
            });
        });

        document.querySelectorAll('.edit-product').forEach(btn => {
            btn.addEventListener('click', () => this.editProduct(parseInt(btn.dataset.index)));
        });
        document.querySelectorAll('.edit-payment').forEach(btn => {
            btn.addEventListener('click', () => this.editPayment(parseInt(btn.dataset.index)));
        });

        // Tab persistence
        document.querySelectorAll('a[data-bs-toggle="tab"]').forEach(tab => {
            tab.addEventListener('shown.bs.tab', e => localStorage.setItem('activeTab', e.target.getAttribute('href')));
        });
        const activeTab = localStorage.getItem('activeTab');
        if (activeTab) {
            const tab = document.querySelector(`a[href="${activeTab}"]`);
            if (tab) new bootstrap.Tab(tab).show();
        }

        this.setupRealTimeUpdates();

        document.addEventListener('keydown', e => {
            if (e.ctrlKey && e.key === 't') { e.preventDefault(); this.toggleTheme(); }
            if (e.ctrlKey && e.key === 'r') { e.preventDefault(); this.calculateTotals(); }
        });
    }

    setupApiEventListeners() {
        const endpointSelect = document.getElementById('apiEndpoint');
        if (endpointSelect) endpointSelect.addEventListener('change', () => this.updateEndpointUrl());

        const on = (id, ev, fn) => { const el = document.getElementById(id); if (el) el.addEventListener(ev, fn); };
        on('clearResponse', 'click', () => this.clearResponseDisplay());
        on('copyResponse', 'click', () => this.copyResponseToClipboard());
        on('previewRequest', 'click', () => this.previewRequest());

        const customUrl = document.getElementById('customUrl');
        if (customUrl) customUrl.addEventListener('input', () => {
            if (customUrl.value && endpointSelect) {
                endpointSelect.value = '';
                const span = document.getElementById('selectedUrl');
                if (span) span.textContent = '';
            }
        });

        const form = document.getElementById('apiRequestForm');
        if (form) form.addEventListener('submit', (e) => {
            e.preventDefault();
            const ep = document.getElementById('apiEndpoint');
            const cu = document.getElementById('customUrl');
            const validate = document.getElementById('validateBeforeSend');

            if (!ep.value && !cu.value) {
                this.showAlert('Please select an API endpoint or enter a custom URL', 'warning');
                return;
            }

            const submitBtn = document.getElementById('sendRequestBtn');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Sending...';
            }

            const formData = new FormData(form);
            if (validate && validate.checked) formData.set('validateBeforeSend', 'true');
            else formData.delete('validateBeforeSend');

            fetch(this.url('/send-request'), {
                method: 'POST',
                body: formData,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(r => r.json().then(data => ({ status: r.status, body: data })))
                .then(({ status, body }) => {
                    this.displayApiResponse(body);
                    if (status === 400 && body.response_text) {
                        try {
                            const parsed = JSON.parse(body.response_text);
                            if (parsed.validation_errors) this.showAlert('Validation failed. Please check errors.', 'danger');
                        } catch (e) { }
                    }
                })
                .catch(err => this.showAlert('Error sending request: ' + err.message, 'danger'))
                .finally(() => {
                    if (submitBtn) {
                        submitBtn.disabled = false;
                        submitBtn.innerHTML = '<i class="bi bi-send me-2"></i>Send Request';
                    }
                });
        });
    }
}

document.addEventListener('DOMContentLoaded', function () {
    window.flatOrderTool = new FlatOrderTool();

    if (window.responseData) window.flatOrderTool.displayApiResponse(window.responseData);
    if (window.cancelResponseData) window.flatOrderTool.displayApiResponse(window.cancelResponseData);
});
