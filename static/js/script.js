// Enhanced JavaScript with better error handling and functionality
// Online Order Tool - JavaScript Module

// Global variables
let currentTheme = localStorage.getItem('theme') || 'light';
let currentColor = localStorage.getItem('themeColor') || 'blue';

// Initialize payment options
window.paymentOptions = window.paymentOptions || {
    'cash': ['cash'],
    'credit_card': ['visa', 'mastercard', 'mada', 'amex'],
    'PostToCredit': ['PostToCredit'],
    'Points': ['Points'],
    'Tamara': ['tamara'],
    'Tabby': ['tabby'],
    'MisPay': ['mispay'],
    'Emkan': ['emkan'],
    'YouGotaGift': ['yougotagift'],
    'OgMoney': ['ogmoney']
};

// Alert system
function showAlert(message, type = 'danger') {
    try {
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
        alertDiv.innerHTML = `
            <i class="bi ${getAlertIcon(type)} me-2"></i>
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;

        const mainContent = document.querySelector('.main-content');
        if (mainContent) {
            const existingAlerts = mainContent.querySelectorAll('.alert');
            if (existingAlerts.length > 0) {
                mainContent.insertBefore(alertDiv, existingAlerts[0]);
            } else {
                mainContent.insertBefore(alertDiv, mainContent.firstChild);
            }

            // Auto-remove after 5 seconds
            setTimeout(() => {
                if (alertDiv.parentNode) {
                    const bsAlert = new bootstrap.Alert(alertDiv);
                    bsAlert.close();
                }
            }, 5000);
        }
    } catch (error) {
        console.error('Error showing alert:', error);
        // Fallback to native alert
        alert(`${type.toUpperCase()}: ${message}`);
    }
}

function getAlertIcon(type) {
    const icons = {
        'success': 'bi-check-circle',
        'danger': 'bi-exclamation-triangle',
        'warning': 'bi-exclamation-circle',
        'info': 'bi-info-circle'
    };
    return icons[type] || 'bi-info-circle';
}

// Theme management
function setThemeColor(color) {
    try {
        const html = document.documentElement;

        // Remove all color themes
        html.classList.remove('blue', 'purple', 'green', 'orange', 'red');

        // Add selected color theme
        if (color !== 'blue') {
            html.classList.add(color);
        }

        // Update active state
        document.querySelectorAll('.theme-color').forEach(el => {
            el.classList.remove('active');
        });

        const activeColor = document.querySelector(`.theme-color.${color}`);
        if (activeColor) {
            activeColor.classList.add('active');
        }

        // Save preference
        localStorage.setItem('themeColor', color);
        currentColor = color;
    } catch (error) {
        console.error('Error setting theme color:', error);
    }
}

function toggleTheme() {
    try {
        const html = document.documentElement;
        currentTheme = currentTheme === 'dark' ? 'light' : 'dark';
        html.setAttribute('data-bs-theme', currentTheme);

        // Update icon and text
        const themeToggleBtn = document.querySelector('.theme-toggle-btn');
        if (themeToggleBtn) {
            const icon = themeToggleBtn.querySelector('i');
            const text = themeToggleBtn.querySelector('span');
            icon.className = currentTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
            text.textContent = currentTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
        }

        // Save preference
        localStorage.setItem('theme', currentTheme);
    } catch (error) {
        console.error('Error toggling theme:', error);
    }
}

// Initialize theme on page load
function initializeTheme() {
    try {
        // Set theme
        document.documentElement.setAttribute('data-bs-theme', currentTheme);

        // Set color
        setThemeColor(currentColor);

        // Update theme toggle button
        const themeToggleBtn = document.querySelector('.theme-toggle-btn');
        if (themeToggleBtn) {
            const icon = themeToggleBtn.querySelector('i');
            const text = themeToggleBtn.querySelector('span');
            icon.className = currentTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
            text.textContent = currentTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
        }
    } catch (error) {
        console.error('Error initializing theme:', error);
    }
}

// Calculate totals
function calculateTotals() {
    showLoadingState('calculateTotals', true);

    fetch('/calculate-totals')
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            if (data.error) {
                throw new Error(data.error);
            }
            updateTotalsDisplay(data);
        })
        .catch(error => {
            console.error('Error calculating totals:', error);
            showAlert('Error calculating totals: ' + error.message, 'danger');
        })
        .finally(() => {
            showLoadingState('calculateTotals', false);
        });
}

function updateTotalsDisplay(data) {
    try {
        const elements = {
            'productsTotal': data.products_total,
            'orderDiscount': data.order_discount,
            'deliveryCost': data.delivery_cost,
            'finalTotal': data.final_total,
            'totalPaid': data.total_paid,
            'remainingAmount': data.remaining_amount
        };

        Object.keys(elements).forEach(id => {
            const element = document.getElementById(id);
            if (element) {
                element.textContent = parseFloat(elements[id]).toFixed(2);
            }
        });

        // Update sidebar total
        const sidebarTotal = document.getElementById('sidebar-total');
        if (sidebarTotal) {
            sidebarTotal.textContent = parseFloat(data.final_total).toFixed(2);
        }
    } catch (error) {
        console.error('Error updating totals display:', error);
    }
}

// Product calculations
function calculateEstimatedTotal() {
    try {
        const quantity = parseFloat(document.querySelector('input[name="quantity"]')?.value) || 0;
        const unitPrice = parseFloat(document.querySelector('input[name="unit_price"]')?.value) || 0;
        const vatPercent = parseFloat(document.querySelector('input[name="vat_percentage"]')?.value) || 0;
        const discount = parseFloat(document.querySelector('input[name="discount"]')?.value) || 0;

        // Normalize VAT percentage - default to 0
        let vatDecimal = vatPercent;
        if (vatPercent > 100) {
            vatDecimal = vatPercent / 100;
        } else if (vatPercent > 1) {
            vatDecimal = vatPercent / 100;
        }
        // If vatPercent is already decimal (e.g., 0.15), use as is

        const vatAmount = (quantity * unitPrice - discount) * vatDecimal;
        const total = (quantity * unitPrice - discount) + vatAmount;

        const estimatedTotal = document.getElementById('estimatedTotal');
        if (estimatedTotal) {
            estimatedTotal.value = '$' + total.toFixed(2);
        }
    } catch (error) {
        console.error('Error calculating estimated total:', error);
    }
}

function calculateEditEstimatedTotal() {
    try {
        const quantity = parseFloat(document.getElementById('editQuantity')?.value) || 0;
        const unitPrice = parseFloat(document.getElementById('editUnitPrice')?.value) || 0;
        const vatPercent = parseFloat(document.getElementById('editVatPercentage')?.value) || 0;
        const discount = parseFloat(document.getElementById('editDiscount')?.value) || 0;

        // Normalize VAT percentage - default to 0
        let vatDecimal = vatPercent;
        if (vatPercent > 100) {
            vatDecimal = vatPercent / 100;
        } else if (vatPercent > 1) {
            vatDecimal = vatPercent / 100;
        }

        const vatAmount = (quantity * unitPrice - discount) * vatDecimal;
        const total = (quantity * unitPrice - discount) + vatAmount;

        const estimatedTotal = document.getElementById('editEstimatedTotal');
        if (estimatedTotal) {
            estimatedTotal.value = '$' + total.toFixed(2);
        }
    } catch (error) {
        console.error('Error calculating edit estimated total:', error);
    }
}

// Real-time form updates
function setupRealTimeUpdates() {
    // Order field updates
    document.querySelectorAll('.order-field').forEach(field => {
        field.addEventListener('change', function() {
            updateOrderField(this.dataset.field, this.value);
        });

        // Optional: Update on input for immediate feedback
        if (field.type === 'text' || field.type === 'textarea') {
            field.addEventListener('input', function() {
                updateOrderField(this.dataset.field, this.value);
            });
        }
    });
}

function updateOrderField(field, value) {
    fetch('/update-order-field', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ field: field, value: value })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // Show visual feedback
            const element = document.querySelector(`[data-field="${field}"]`);
            if (element) {
                element.classList.add('field-updated');
                setTimeout(() => {
                    element.classList.remove('field-updated');
                }, 1000);
            }
        } else {
            console.error('Error updating field:', data.error);
        }
    })
    .catch(error => {
        console.error('Error updating field:', error);
    });
}

// Payment methods
function updatePaymentOptions(method, isEdit = false) {
    try {
        const prefix = isEdit ? 'edit' : '';
        const optionSelect = document.getElementById(`${prefix}PaymentOption`);
        const creditCustomerInfo = document.getElementById(`${prefix}CreditCustomerInfo`);
        const paymentStatusSelect = document.getElementById(`${prefix}PaymentStatus`);
        const paymentAmountField = document.getElementById(`${prefix}PaymentAmount`);

        if (!optionSelect) return;

        // Clear existing options
        optionSelect.innerHTML = '<option value="">Select Option</option>';

        // Get payment options from global variable
        const paymentOptions = window.paymentOptions || {};

        // Populate payment options based on selected method
        if (method && paymentOptions[method]) {
            paymentOptions[method].forEach(option => {
                const optionElement = document.createElement('option');
                optionElement.value = option;
                optionElement.textContent = option.charAt(0).toUpperCase() + option.slice(1);
                optionSelect.appendChild(optionElement);
            });
        }

        // Show/hide credit customer info for PostToCredit
        if (creditCustomerInfo) {
            creditCustomerInfo.style.display = method === 'PostToCredit' ? 'block' : 'none';
        }

        // Auto-set payment status based on method
        if (paymentStatusSelect) {
            if (method === 'PostToCredit') {
                paymentStatusSelect.value = 'not_payment';
            } else if (method === 'credit_card' || method === 'Points') {
                paymentStatusSelect.value = 'done_payment';
            } else {
                paymentStatusSelect.value = '';
            }
        }

        // Set default payment amount to remaining amount for new payments
        if (paymentAmountField && !isEdit) {
            fetch('/get-remaining-amount')
                .then(response => response.json())
                .then(data => {
                    if (data.remaining_amount > 0) {
                        paymentAmountField.value = data.remaining_amount.toFixed(2);
                    }
                })
                .catch(error => {
                    console.error('Error getting remaining amount:', error);
                });
        }
    } catch (error) {
        console.error('Error updating payment options:', error);
    }
}

// Database and API functions
function handleItemLookup() {
    const form = document.getElementById('itemLookupForm');
    if (!form) return;

    const formData = new FormData(form);
    const params = new URLSearchParams(formData);

    // Validate material number
    const materialNumber = formData.get('material_number');
    if (!materialNumber || !materialNumber.match(/^\d{6}$/)) {
        showAlert('Material number must be exactly 6 digits', 'warning');
        return;
    }

    showLoadingState('itemLookupForm', true);

    fetch('/get-item-details?' + params)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            if (data.error) {
                throw new Error(data.error);
            }
            displayItemDetails(data);
        })
        .catch(error => {
            console.error('Error fetching item details:', error);
            showAlert('Error fetching item details: ' + error.message, 'danger');
        })
        .finally(() => {
            showLoadingState('itemLookupForm', false);
        });
}

function displayItemDetails(data) {
    try {
        const itemDetails = document.getElementById('itemDetails');
        const itemResults = document.getElementById('itemResults');

        if (!itemDetails || !itemResults) return;

        itemDetails.innerHTML = `
            <tr><th>Item Code</th><td>${escapeHtml(data.item_code)}</td></tr>
            <tr><th>Barcode</th><td>${escapeHtml(data.item_Barcode)}</td></tr>
            <tr><th>English Name</th><td>${escapeHtml(data.item_EN_Name)}</td></tr>
            <tr><th>Arabic Name</th><td>${escapeHtml(data.item_AR_Name)}</td></tr>
            <tr><th>Unit Price</th><td>${parseFloat(data.unit_price).toFixed(2)}</td></tr>
            <tr><th>VAT %</th><td>${parseFloat(data.vat_percentage).toFixed(2)}%</td></tr>
            <tr><th>Net Price</th><td>${parseFloat(data.net_price).toFixed(2)}</td></tr>
        `;

        itemResults.style.display = 'block';

        // Set up add to order button
        const addButton = document.getElementById('addItemToOrder');
        if (addButton) {
            addButton.onclick = function () {
                addItemToOrder(data);
            };
        }
    } catch (error) {
        console.error('Error displaying item details:', error);
        showAlert('Error displaying item details', 'danger');
    }
}

function addItemToOrder(itemData) {
    showLoadingState('addItemToOrder', true);

    fetch('/add-product-from-db', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(itemData)
    })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(result => {
            if (result.success) {
                showAlert('Product added successfully!', 'success');
                setTimeout(() => {
                    window.location.reload();
                }, 1500);
            } else {
                throw new Error(result.error || 'Unknown error adding product');
            }
        })
        .catch(error => {
            console.error('Error adding product:', error);
            showAlert('Error adding product: ' + error.message, 'danger');
        })
        .finally(() => {
            showLoadingState('addItemToOrder', false);
        });
}

function testDatabaseConnection() {
    const form = document.getElementById('databaseForm');
    if (!form) return;

    const formData = new FormData(form);
    const data = Object.fromEntries(formData.entries());

    // Validate required fields
    if (!data.server || !data.database || !data.username) {
        showAlert('Please fill in all required database fields', 'warning');
        return;
    }

    showLoadingState('testDatabase', true);

    fetch('/test-database-connection', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(data)
    })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                showAlert('Database connection successful!', 'success');
            } else {
                throw new Error(data.message || 'Database connection failed');
            }
        })
        .catch(error => {
            console.error('Error testing database connection:', error);
            showAlert('Error testing database connection: ' + error.message, 'danger');
        })
        .finally(() => {
            showLoadingState('testDatabase', false);
        });
}

// Edit product and payment functions
function editProduct(index) {
    if (index === undefined || index === null) {
        showAlert('Invalid product index', 'danger');
        return;
    }

    fetch('/get-product/' + index)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            if (data.error) {
                throw new Error(data.error);
            }
            populateEditProductForm(data);
        })
        .catch(error => {
            console.error('Error loading product:', error);
            showAlert('Error loading product: ' + error.message, 'danger');
        });
}

function populateEditProductForm(data) {
    try {
        document.getElementById('editItemCode').value = data.item_code || '';
        document.getElementById('editItemName').value = data.item_name || '';
        document.getElementById('editQuantity').value = data.quantity || 0;
        document.getElementById('editUnitPrice').value = data.unit_price || 0;
        document.getElementById('editVatPercentage').value = data.vat_percentage || 0;
        document.getElementById('editDiscount').value = data.row_total_discount || 0;
        document.getElementById('editOfferCode').value = data.offer_code || '';
        document.getElementById('editOfferMessage').value = data.offer_message || '';

        document.getElementById('editProductForm').action = '/update-product/' + data.index;

        const editProductModal = new bootstrap.Modal(document.getElementById('editProductModal'));
        editProductModal.show();

        calculateEditEstimatedTotal();
    } catch (error) {
        console.error('Error populating edit product form:', error);
        showAlert('Error loading product details', 'danger');
    }
}

function editPayment(index) {
    if (index === undefined || index === null) {
        showAlert('Invalid payment index', 'danger');
        return;
    }

    fetch('/get-payment/' + index)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            if (data.error) {
                throw new Error(data.error);
            }
            populateEditPaymentForm(data, index);
        })
        .catch(error => {
            console.error('Error loading payment:', error);
            showAlert('Error loading payment: ' + error.message, 'danger');
        });
}

function populateEditPaymentForm(data, index) {
    try {
        document.getElementById('editPaymentMethod').value = data.payment_method || '';
        document.getElementById('editPaymentStatus').value = data.payment_status || '';
        document.getElementById('editPaymentAmount').value = data.payment_amount || 0;
        document.getElementById('editTransactionId').value = data.transaction_id || '';
        document.getElementById('editPaymentOption').value = data.payment_option || '';
        document.getElementById('editOptionCommission').value = data.option_commission || 0;

        // Handle credit customer info
        if (data.credit_customer_info) {
            document.getElementById('editCustomerNumber').value = data.credit_customer_info.customer_number || '';
            document.getElementById('editCustomerName').value = data.credit_customer_info.customer_name || '';
        } else {
            document.getElementById('editCustomerNumber').value = '';
            document.getElementById('editCustomerName').value = '';
        }

        // Update payment options and show/hide credit customer fields
        updatePaymentOptions(data.payment_method, true);

        document.getElementById('editPaymentForm').action = '/update-payment/' + index;

        const editPaymentModal = new bootstrap.Modal(document.getElementById('editPaymentModal'));
        editPaymentModal.show();
    } catch (error) {
        console.error('Error populating edit payment form:', error);
        showAlert('Error loading payment details', 'danger');
    }
}

// API endpoint testing
function testAllEndpoints() {
    const endpointsTable = document.getElementById('endpointsTable');
    if (!endpointsTable) return;

    const rows = endpointsTable.querySelectorAll('tr');
    const testPromises = [];

    rows.forEach(row => {
        const statusCell = row.querySelector('td:nth-child(3)');
        const testButton = row.querySelector('.test-single-endpoint');

        if (statusCell && testButton) {
            statusCell.innerHTML = '<span class="badge bg-info">Testing...</span>';
            testButton.disabled = true;

            const name = testButton.dataset.name;
            const url = testButton.dataset.url;

            testPromises.push(
                testSingleEndpoint(name, url).then(result => {
                    statusCell.innerHTML = `<span class="badge bg-${result.status === 'Online' ? 'success' : 'danger'}">${result.status}</span>`;
                    testButton.disabled = false;
                })
            );
        }
    });

    Promise.all(testPromises)
        .then(() => {
            showAlert('All endpoints tested successfully!', 'success');
        })
        .catch(error => {
            console.error('Error testing endpoints:', error);
            showAlert('Error testing some endpoints', 'warning');
        });
}

function testSingleEndpoint(name, url) {
    return fetch('/test-single-endpoint', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ name, url })
    })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            if (data.status === 'Online') {
                showAlert(`Endpoint "${name}" is online`, 'success');
            } else {
                showAlert(`Endpoint "${name}" is offline`, 'warning');
            }
            return data;
        })
        .catch(error => {
            console.error(`Error testing endpoint ${name}:`, error);
            showAlert(`Error testing endpoint "${name}"`, 'danger');
            return { status: 'Error', error: error.message };
        });
}

// Utility functions
function showLoadingState(elementId, isLoading) {
    const element = document.getElementById(elementId);
    if (!element) return;

    if (isLoading) {
        element.disabled = true;
        const originalText = element.innerHTML;
        element.setAttribute('data-original-text', originalText);
        element.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Loading...';
    } else {
        element.disabled = false;
        const originalText = element.getAttribute('data-original-text');
        if (originalText) {
            element.innerHTML = originalText;
        }
    }
}

function escapeHtml(unsafe) {
    if (unsafe === null || unsafe === undefined) return '';
    return unsafe
        .toString()
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

function setupAutoCalculation() {
    // Listen for changes in order information fields
    const orderInputs = document.querySelectorAll('#order-tab input, #order-tab select');
    orderInputs.forEach(input => {
        input.addEventListener('change', calculateTotals);
        input.addEventListener('input', calculateTotals);
    });

    // Listen for changes in product and payment modals
    document.addEventListener('change', function (e) {
        if (e.target.closest('.modal')) {
            calculateTotals();
        }
    });

    // Auto-calculate on page load
    setTimeout(calculateTotals, 1000);
}

function setupEventListeners() {
    try {
        // Calculate totals button
        const calculateTotalsBtn = document.getElementById('calculateTotals');
        if (calculateTotalsBtn) {
            calculateTotalsBtn.addEventListener('click', calculateTotals);
        }

        // Theme toggle button
        const themeToggleBtn = document.querySelector('.theme-toggle-btn');
        if (themeToggleBtn) {
            themeToggleBtn.addEventListener('click', toggleTheme);
        }

        // Theme color buttons
        document.querySelectorAll('.theme-color').forEach(button => {
            button.addEventListener('click', function () {
                setThemeColor(this.dataset.color);
            });
        });

        // Payment method change events
        const paymentMethodSelect = document.getElementById('paymentMethod');
        if (paymentMethodSelect) {
            paymentMethodSelect.addEventListener('change', function () {
                updatePaymentOptions(this.value, false);
            });
        }

        const editPaymentMethod = document.getElementById('editPaymentMethod');
        if (editPaymentMethod) {
            editPaymentMethod.addEventListener('change', function () {
                updatePaymentOptions(this.value, true);
            });
        }

        // Product form calculation events
        const productInputs = document.querySelectorAll('#addProductModal input');
        productInputs.forEach(input => {
            if (input.name === 'quantity' || input.name === 'unit_price' || input.name === 'vat_percentage' || input.name === 'discount') {
                input.addEventListener('input', calculateEstimatedTotal);
            }
        });

        const editProductInputs = document.querySelectorAll('#editProductModal input');
        editProductInputs.forEach(input => {
            if (input.name === 'quantity' || input.name === 'unit_price' || input.name === 'vat_percentage' || input.name === 'discount') {
                input.addEventListener('input', calculateEditEstimatedTotal);
            }
        });

        // Item lookup form
        const itemLookupForm = document.getElementById('itemLookupForm');
        if (itemLookupForm) {
            itemLookupForm.addEventListener('submit', function (e) {
                e.preventDefault();
                handleItemLookup();
            });
        }

        // Database test connection
        const testDatabaseBtn = document.getElementById('testDatabase');
        if (testDatabaseBtn) {
            testDatabaseBtn.addEventListener('click', testDatabaseConnection);
        }

        // Test all endpoints
        const testAllEndpointsBtn = document.getElementById('testAllEndpoints');
        if (testAllEndpointsBtn) {
            testAllEndpointsBtn.addEventListener('click', testAllEndpoints);
        }

        // Single endpoint test buttons
        document.querySelectorAll('.test-single-endpoint').forEach(button => {
            button.addEventListener('click', function () {
                const name = this.dataset.name;
                const url = this.dataset.url;
                testSingleEndpoint(name, url).then(result => {
                    const statusCell = this.closest('tr').querySelector('td:nth-child(3)');
                    if (statusCell) {
                        statusCell.innerHTML = `<span class="badge bg-${result.status === 'Online' ? 'success' : 'danger'}">${result.status}</span>`;
                    }
                });
            });
        });

        // Edit product buttons
        document.querySelectorAll('.edit-product').forEach(button => {
            button.addEventListener('click', function () {
                const index = parseInt(this.dataset.index);
                if (!isNaN(index)) {
                    editProduct(index);
                }
            });
        });

        // Edit payment buttons
        document.querySelectorAll('.edit-payment').forEach(button => {
            button.addEventListener('click', function () {
                const index = parseInt(this.dataset.index);
                if (!isNaN(index)) {
                    editPayment(index);
                }
            });
        });

        // Form validation
        const forms = document.querySelectorAll('form');
        forms.forEach(form => {
            form.addEventListener('submit', function (e) {
                const requiredFields = form.querySelectorAll('[required]');
                let valid = true;

                requiredFields.forEach(field => {
                    if (!field.value.trim()) {
                        valid = false;
                        field.classList.add('is-invalid');
                    } else {
                        field.classList.remove('is-invalid');
                    }
                });

                if (!valid) {
                    e.preventDefault();
                    showAlert('Please fill in all required fields', 'warning');
                }
            });
        });

        // Tab persistence
        const tabLinks = document.querySelectorAll('a[data-bs-toggle="tab"]');
        tabLinks.forEach(tab => {
            tab.addEventListener('shown.bs.tab', function (e) {
                localStorage.setItem('activeTab', e.target.getAttribute('href'));
            });
        });

        // Restore active tab
        const activeTab = localStorage.getItem('activeTab');
        if (activeTab) {
            const tab = document.querySelector(`a[href="${activeTab}"]`);
            if (tab) {
                new bootstrap.Tab(tab).show();
            }
        }

        // Real-time updates
        setupRealTimeUpdates();

        // Set default VAT to 0
        const vatField = document.querySelector('input[name="vat_percentage"]');
        if (vatField && !vatField.value) {
            vatField.value = '0';
        }

        const editVatField = document.getElementById('editVatPercentage');
        if (editVatField && !editVatField.value) {
            editVatField.value = '0';
        }

    } catch (error) {
        console.error('Error setting up event listeners:', error);
    }
}

// Initialize application
document.addEventListener('DOMContentLoaded', function () {
    try {
        initializeTheme();
        setupEventListeners();
        setupAutoCalculation();

        // Initialize payment options if there's a selected value
        const paymentMethodSelect = document.getElementById('paymentMethod');
        if (paymentMethodSelect && paymentMethodSelect.value) {
            updatePaymentOptions(paymentMethodSelect.value, false);
        }

        // Initialize edit payment options if modal is open
        const editPaymentMethod = document.getElementById('editPaymentMethod');
        if (editPaymentMethod && editPaymentMethod.value) {
            updatePaymentOptions(editPaymentMethod.value, true);
        }

        // Add keyboard shortcuts
        document.addEventListener('keydown', function (e) {
            // Ctrl + T to toggle theme
            if (e.ctrlKey && e.key === 't') {
                e.preventDefault();
                toggleTheme();
            }
            // Ctrl + R to calculate totals
            if (e.ctrlKey && e.key === 'r') {
                e.preventDefault();
                calculateTotals();
            }
        });

        console.log('Online Order Tool initialized successfully');
    } catch (error) {
        console.error('Error initializing application:', error);
        showAlert('Error initializing application. Please refresh the page.', 'danger');
    }
});

// Export functions for potential module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        showAlert,
        calculateTotals,
        updatePaymentOptions,
        handleItemLookup,
        testDatabaseConnection,
        editProduct,
        editPayment
    };
}