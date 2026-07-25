// UPC-only Order Validation tab — search sent orders, inspect their status,
// and resend an eligible order to a different branch.
// Every endpoint is prefixed with window.MODULE_BASE ("/modules/upc_ecommerce").
// Loaded after flat_order.js, only on the UPC template; reuses
// window.flatOrderTool.showAlert / displayApiResponse when available.

const UPC_ORDER_STATUS_BADGE = {
    1: 'secondary',  // New
    2: 'info',       // Confirmed
    3: 'info',       // Ready
    4: 'primary',    // With_Delegate
    5: 'danger',     // Rejected
    6: 'danger',     // CanceledByClient
    7: 'danger',     // CanceledByAdmin
    8: 'warning',    // Processing
    9: 'success',    // Done
};

class UpcValidationTool {
    constructor() {
        this.base = (window.MODULE_BASE || '').replace(/\/$/, '');
        this.lastDetails = null; // last order-details payload shown in the modal
        this.init();
    }

    url(path) { return this.base + path; }

    init() {
        this.setupEventListeners();
        console.log('UpcValidationTool initialized');
    }

    showAlert(message, type = 'danger') {
        if (window.flatOrderTool && typeof window.flatOrderTool.showAlert === 'function') {
            window.flatOrderTool.showAlert(message, type);
        } else {
            alert(`${type.toUpperCase()}: ${message}`);
        }
    }

    setupEventListeners() {
        const searchForm = document.getElementById('orderSearchForm');
        if (searchForm) searchForm.addEventListener('submit', (e) => {
            e.preventDefault();
            this.search();
        });

        const clearBtn = document.getElementById('clearOrderSearch');
        if (clearBtn) clearBtn.addEventListener('click', () => {
            searchForm.reset();
            this.renderResults([]);
        });

        const resultsBody = document.getElementById('validationResultsBody');
        if (resultsBody) resultsBody.addEventListener('click', (e) => {
            const detailsBtn = e.target.closest('.view-order-details');
            if (detailsBtn) {
                this.openDetails(detailsBtn.dataset.orderNumber, detailsBtn.dataset.headerId);
                return;
            }
            const resendBtn = e.target.closest('.resend-order-btn');
            if (resendBtn) {
                this.openResendModal(resendBtn.dataset.orderNumber);
            }
        });

        const detailsFooter = document.getElementById('orderDetailsFooter');
        if (detailsFooter) detailsFooter.addEventListener('click', (e) => {
            const resendBtn = e.target.closest('.resend-order-btn');
            if (resendBtn) this.openResendModal(resendBtn.dataset.orderNumber);
        });

        const resendForm = document.getElementById('resendOrderForm');
        if (resendForm) resendForm.addEventListener('submit', (e) => {
            e.preventDefault();
            this.submitResend();
        });
    }

    // ----- Search -----
    search() {
        const filters = {
            order_number: (document.getElementById('searchOrderNumber').value || '').trim(),
            phone: (document.getElementById('searchPhone').value || '').trim(),
            branch_code: (document.getElementById('searchBranchCode').value || '').trim(),
            status: document.getElementById('searchStatus').value || null,
            date_from: document.getElementById('searchDateFrom').value || '',
            date_to: document.getElementById('searchDateTo').value || '',
        };

        const tbody = document.getElementById('validationResultsBody');
        if (tbody) tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-4">' +
            '<span class="spinner-border spinner-border-sm"></span> Searching…</td></tr>';

        fetch(this.url('/search-orders'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(filters)
        })
            .then(r => r.json().then(data => ({ ok: r.ok, data })))
            .then(({ ok, data }) => {
                if (!ok) throw new Error(data.error || 'Search failed');
                this.renderResults(data.results || []);
            })
            .catch(e => {
                this.showAlert('Order search failed: ' + e.message, 'danger');
                this.renderResults([]);
            });
    }

    renderResults(results) {
        const tbody = document.getElementById('validationResultsBody');
        const count = document.getElementById('validationResultCount');
        if (count) count.textContent = results.length;
        if (!tbody) return;

        if (!results.length) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-4">No orders found</td></tr>';
            return;
        }

        tbody.innerHTML = results.map(o => this.renderResultRow(o)).join('');
    }

    renderResultRow(o) {
        const badge = UPC_ORDER_STATUS_BADGE[o.status] || 'secondary';
        const statusCell = `<span class="badge bg-${badge}">${o.status} - ${o.status_label}</span>`;
        const comparisonCell = this.creationVsInvoiceCell(o.order_date, o.invoice_date);
        const resendBtn = o.resend_allowed
            ? `<button type="button" class="btn btn-sm btn-outline-warning resend-order-btn"
                    data-order-number="${this.esc(o.order_number)}" title="Resend to another branch">
                    <i class="bi bi-arrow-repeat"></i>
                </button>`
            : '';

        return `
            <tr>
                <td>${this.esc(o.order_number)}</td>
                <td>${this.esc(o.branch_code)}${o.branch_name ? ' <small class="text-muted">(' + this.esc(o.branch_name) + ')</small>' : ''}</td>
                <td>${statusCell}</td>
                <td>${this.fmtDate(o.order_date)}</td>
                <td>${o.invoice_barcode ? this.esc(o.invoice_barcode) : '<span class="text-muted">—</span>'}</td>
                <td>${o.invoice_date ? this.fmtDate(o.invoice_date) : '<span class="text-muted">—</span>'}</td>
                <td>${comparisonCell}</td>
                <td>
                    <div class="btn-group">
                        <button type="button" class="btn btn-sm btn-outline-primary view-order-details"
                            data-order-number="${this.esc(o.order_number)}" data-header-id="${o.order_header_id}">
                            <i class="bi bi-eye"></i>
                        </button>
                        ${resendBtn}
                    </div>
                </td>
            </tr>`;
    }

    creationVsInvoiceCell(orderDate, invoiceDate) {
        if (!invoiceDate) return '<span class="text-muted">Not invoiced yet</span>';
        if (!orderDate) return this.fmtDate(invoiceDate);

        const created = new Date(orderDate);
        const invoiced = new Date(invoiceDate);
        const diffMs = invoiced - created;
        const diffHours = diffMs / (1000 * 60 * 60);

        if (diffMs < 0) {
            return '<span class="badge bg-danger" title="Invoice predates order creation">Invoice before order?</span>';
        }
        if (diffHours < 24) {
            return `<span class="badge bg-success">Same day (${diffHours.toFixed(1)}h)</span>`;
        }
        const days = (diffHours / 24).toFixed(1);
        return `<span class="badge bg-info">${days}d later</span>`;
    }

    // ----- Details -----
    openDetails(orderNumber, headerId) {
        const body = document.getElementById('orderDetailsBody');
        const footer = document.getElementById('orderDetailsFooter');
        if (body) body.innerHTML = '<div class="text-center text-muted py-4"><span class="spinner-border spinner-border-sm"></span> Loading…</div>';

        const modalEl = document.getElementById('orderDetailsModal');
        const modal = modalEl ? new bootstrap.Modal(modalEl) : null;
        if (modal) modal.show();

        const qs = headerId ? `?header_id=${encodeURIComponent(headerId)}` : '';
        fetch(this.url(`/order-details/${encodeURIComponent(orderNumber)}${qs}`))
            .then(r => r.json().then(data => ({ ok: r.ok, data })))
            .then(({ ok, data }) => {
                if (!ok || !data.found) throw new Error(data.error || 'Order not found');
                this.lastDetails = data.order;
                this.renderDetails(data.order);
            })
            .catch(e => {
                if (body) body.innerHTML = `<div class="alert alert-danger">${this.esc(e.message)}</div>`;
                if (footer) footer.querySelectorAll('.resend-order-btn').forEach(b => b.remove());
            });
    }

    renderDetails(order) {
        const body = document.getElementById('orderDetailsBody');
        const footer = document.getElementById('orderDetailsFooter');
        if (!body) return;

        const badge = UPC_ORDER_STATUS_BADGE[order.status] || 'secondary';

        const itemsRows = (order.details || []).map(d => `
            <tr>
                <td>${this.esc(d.item_name)}</td>
                <td>${this.esc(d.material_number)}</td>
                <td>${d.quantity}</td>
                <td>${d.unit_price.toFixed(2)}</td>
                <td>${d.total_discount.toFixed(2)}</td>
                <td>${d.total_price.toFixed(2)}</td>
            </tr>`).join('') || '<tr><td colspan="6" class="text-center text-muted">No line items</td></tr>';

        const txnRows = (order.transactions || []).map(t => `
            <tr>
                <td>${this.esc(t.payment_method)}</td>
                <td>${this.esc(t.payment_option)}</td>
                <td>${this.esc(t.payment_status)}</td>
                <td>${t.payment_amount.toFixed(2)}</td>
                <td>${this.esc(t.transaction_code)}</td>
            </tr>`).join('') || '<tr><td colspan="5" class="text-center text-muted">No transactions</td></tr>';

        body.innerHTML = `
            <div class="row g-3 mb-3">
                <div class="col-md-4"><strong>Order #</strong><br>${this.esc(order.order_number)}</div>
                <div class="col-md-4"><strong>Branch</strong><br>${this.esc(order.branch_code)} ${order.branch_name ? '(' + this.esc(order.branch_name) + ')' : ''}</div>
                <div class="col-md-4"><strong>Status</strong><br><span class="badge bg-${badge}">${order.status} - ${order.status_label}</span></div>
                <div class="col-md-4"><strong>Order Date</strong><br>${this.fmtDate(order.order_date)}</div>
                <div class="col-md-4"><strong>Invoice Barcode</strong><br>${order.invoice_barcode ? this.esc(order.invoice_barcode) : '—'}</div>
                <div class="col-md-4"><strong>Invoice Date</strong><br>${order.invoice_date ? this.fmtDate(order.invoice_date) : '—'}</div>
                <div class="col-md-6"><strong>Consumer Mobile</strong><br>${this.esc(order.consumer_mobile || '—')}</div>
                <div class="col-md-6"><strong>Address</strong><br>${this.esc(order.address || '—')}</div>
                <div class="col-md-4"><strong>Gross Total</strong><br>${order.gross_total.toFixed(2)}</div>
                <div class="col-md-4"><strong>Net Total</strong><br>${order.net_total.toFixed(2)}</div>
                <div class="col-md-4"><strong>Total Discount</strong><br>${order.total_discount.toFixed(2)}</div>
                ${order.rejection_message ? `<div class="col-12"><strong>Rejection Message</strong><br><span class="text-danger">${this.esc(order.rejection_message)}</span></div>` : ''}
                ${order.order_note ? `<div class="col-12"><strong>Note</strong><br>${this.esc(order.order_note)}</div>` : ''}
            </div>

            <h6 class="mt-4">Line Items (${(order.details || []).length})</h6>
            <div class="table-responsive">
                <table class="table table-sm table-striped">
                    <thead><tr><th>Item</th><th>Material #</th><th>Qty</th><th>Unit Price</th><th>Discount</th><th>Total</th></tr></thead>
                    <tbody>${itemsRows}</tbody>
                </table>
            </div>

            <h6 class="mt-4">Transactions (${(order.transactions || []).length})</h6>
            <div class="table-responsive">
                <table class="table table-sm table-striped">
                    <thead><tr><th>Method</th><th>Option</th><th>Status</th><th>Amount</th><th>Transaction Code</th></tr></thead>
                    <tbody>${txnRows}</tbody>
                </table>
            </div>`;

        if (footer) {
            footer.querySelectorAll('.resend-order-btn').forEach(b => b.remove());
            if (order.resend_allowed) {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'btn btn-warning resend-order-btn';
                btn.dataset.orderNumber = order.order_number;
                btn.innerHTML = '<i class="bi bi-arrow-repeat me-2"></i>Resend to Another Branch';
                footer.insertBefore(btn, footer.firstChild);
            }
        }
    }

    // ----- Resend -----
    openResendModal(orderNumber) {
        const orderNumberField = document.getElementById('resendOrderNumber');
        const branchField = document.getElementById('resendBranchCode');
        const errorEl = document.getElementById('resendOrderError');
        if (orderNumberField) orderNumberField.value = orderNumber;
        if (branchField) branchField.value = '';
        if (errorEl) { errorEl.style.display = 'none'; errorEl.textContent = ''; }

        const modalEl = document.getElementById('resendOrderModal');
        if (modalEl) new bootstrap.Modal(modalEl).show();
    }

    submitResend() {
        const orderNumber = document.getElementById('resendOrderNumber').value;
        const branchCode = (document.getElementById('resendBranchCode').value || '').trim();
        const errorEl = document.getElementById('resendOrderError');
        const submitBtn = document.querySelector('#resendOrderForm button[type="submit"]');

        if (!branchCode) {
            if (errorEl) { errorEl.textContent = 'Branch code is required'; errorEl.style.display = 'block'; }
            return;
        }

        if (submitBtn) { submitBtn.disabled = true; submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Resending...'; }

        fetch(this.url('/resend-order'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ order_number: orderNumber, branch_code: branchCode })
        })
            .then(r => r.json().then(data => ({ ok: r.ok, data })))
            .then(({ ok, data }) => {
                if (!ok || data.success === false && data.error) {
                    throw new Error(data.error || 'Resend failed');
                }

                const modalEl = document.getElementById('resendOrderModal');
                const modal = modalEl && bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();

                const detailsModalEl = document.getElementById('orderDetailsModal');
                const detailsModal = detailsModalEl && bootstrap.Modal.getInstance(detailsModalEl);
                if (detailsModal) detailsModal.hide();

                // Reuse the API tab's response display + post-send status
                // panel so the resend result looks exactly like a normal send.
                if (window.flatOrderTool && typeof window.flatOrderTool.displayApiResponse === 'function') {
                    window.flatOrderTool.displayApiResponse(data);
                }
                this.showAlert(`Order resent to branch ${branchCode}.`, data.success ? 'success' : 'warning');

                const apiTabLink = document.querySelector('a[href="#api-tab"]');
                if (apiTabLink) new bootstrap.Tab(apiTabLink).show();

                this.search(); // refresh the grid so the new branch attempt shows up
            })
            .catch(e => {
                if (errorEl) { errorEl.textContent = e.message; errorEl.style.display = 'block'; }
                else this.showAlert('Resend failed: ' + e.message, 'danger');
            })
            .finally(() => {
                if (submitBtn) { submitBtn.disabled = false; submitBtn.innerHTML = 'Resend'; }
            });
    }

    // ----- Helpers -----
    fmtDate(iso) {
        if (!iso) return '—';
        const d = new Date(iso);
        if (isNaN(d.getTime())) return this.esc(iso);
        return d.toLocaleString();
    }

    esc(s) {
        if (s === null || s === undefined) return '';
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }
}

document.addEventListener('DOMContentLoaded', function () {
    window.upcValidationTool = new UpcValidationTool();
});
