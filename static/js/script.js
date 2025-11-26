// Enhanced JavaScript with better error handling and functionality

// Global variables
let currentTheme = localStorage.getItem('theme') || 'light';
let currentColor = localStorage.getItem('themeColor') || 'blue';

// Replace all alert() calls with Bootstrap alerts
function showAlert(message, type = 'danger') {
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
    alertDiv.innerHTML = `
        <i class="bi ${getAlertIcon(type)} me-2"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;

    const mainContent = document.querySelector('.main-content');
    const existingAlerts = mainContent.querySelectorAll('.alert');
    if (existingAlerts.length > 0) {
        mainContent.insertBefore(alertDiv, existingAlerts[0]);
    } else {
        mainContent.insertBefore(alertDiv, mainContent.firstChild);
    }

    setTimeout(() => {
        if (alertDiv.parentNode) {
            alertDiv.remove();
        }
    }, 5000);
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
}

function toggleTheme() {
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
}

// Initialize theme on page load
function initializeTheme() {
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
}

// Calculate totals
function calculateTotals() {
    fetch('/calculate-totals')
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (!data.error) {
                updateTotalsDisplay(data);
            } else {
                showAlert('Error calculating totals: ' + data.error, 'danger');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showAlert('Error calculating totals', 'danger');
        });
}

function updateTotalsDisplay(data) {
    const elements = {
        'productsTotal': data.products_total,
        'orderDiscount': data.order_discount,
        'deliveryCost': data.delivery_cost,
        'finalTotal': data.final_total
    };

    Object.keys(elements).forEach(id => {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = elements[id].toFixed(2);
        }
    });

    // Update sidebar total
    const sidebarTotal = document.getElementById('sidebar-total');
    if (sidebarTotal) {
        sidebarTotal.textContent = data.final_total.toFixed(2);
    }
}

// Product calculations
function calculateEstimatedTotal() {
    const quantity = parseFloat(document.querySelector('input[name="quantity"]')?.value) || 0;
    const unitPrice = parseFloat(document.querySelector('input[name="unit_price"]')?.value) || 0;
    const vatPercent = parseFloat(document.querySelector('input[name="vat_percentage"]')?.value) || 0;
    const discount = parseFloat(document.querySelector('input[name="discount"]')?.value) || 0;

    const vatDecimal = vatPercent > 1 ? vatPercent / 100 : vatPercent;
    const vatAmount = (quantity * unitPrice - discount) * vatDecimal;
    const total = (quantity * unitPrice - discount) + vatAmount;

    const estimatedTotal = document.getElementById('estimatedTotal');
    if (estimatedTotal) {
        estimatedTotal.value = '$' + total.toFixed(2);
    }
}

function calculateEditEstimatedTotal() {
    const quantity = parseFloat(document.getElementById('editQuantity')?.value) || 0;
    const unitPrice = parseFloat(document.getElementById('editUnitPrice')?.value) || 0;
    const vatPercent = parseFloat(document.getElementById('editVatPercentage')?.value) || 0;
    const discount = parseFloat(document.getElementById('editDiscount')?.value) || 0;

    const vatDecimal = vatPercent > 1 ? vatPercent / 100 : vatPercent;
    const vatAmount = (quantity * unitPrice - discount) * vatDecimal;
    const total = (quantity * unitPrice - discount) + vatAmount;

    const estimatedTotal = document.getElementById('editEstimatedTotal');
    if (estimatedTotal) {
        estimatedTotal.value = '$' + total.toFixed(2);
    }
}

// Payment methods
function updatePaymentOptions(method, isEdit = false) {
    const prefix = isEdit ? 'edit' : '';
    const optionSelect = document.getElementById(`${prefix}PaymentOption`);
    const creditCustomerInfo = document.getElementById(`${prefix}CreditCustomerInfo`);

    if (!optionSelect) return;

    const paymentOptionsData = document.getElementById('paymentOptionsData');
    if (!paymentOptionsData) return;

    try {
        const options = JSON.parse(paymentOptionsData.textContent);

        optionSelect.innerHTML = '<option value="">Select Option</option>';

        if (method && options[method]) {
            options[method].forEach(option => {
                const optionElement = document.createElement('option');
                optionElement.value = option;
                optionElement.textContent = option;
                optionSelect.appendChild(optionElement);
            });
        }

        // Show/hide credit customer info
        if (creditCustomerInfo) {
            creditCustomerInfo.style.display = method === 'PostToCredit' ? 'block' : 'none';
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

    showLoadingState('itemLookupForm', true);

    fetch('/get-item-details?' + params)
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (data.error) {
                showAlert('Error: ' + data.error, 'danger');
            } else {
                displayItemDetails(data);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showAlert('Error fetching item details', 'danger');
        })
        .finally(() => {
            showLoadingState('itemLookupForm', false);
        });
}

function displayItemDetails(data) {
    const itemDetails = document.getElementById('itemDetails');
    const itemResults = document.getElementById('itemResults');

    if (!itemDetails || !itemResults) return;

    itemDetails.innerHTML = `
        <tr><th>Item Code</th><td>${data.item_code}</td></tr>
        <tr><th>Barcode</th><td>${data.item_Barcode}</td></tr>
        <tr><th>English Name</th><td>${data.item_EN_Name}</td></tr>
        <tr><th>Arabic Name</th><td>${data.item_AR_Name}</td></tr>
        <tr><th>Unit Price</th><td>${data.unit_price}</td></tr>
        <tr><th>VAT %</th><td>${data.vat_percentage}</td></tr>
        <tr><th>Net Price</th><td>${data.net_price}</td></tr>
    `;

    itemResults.style.display = 'block';

    // Set up add to order button
    const addButton = document.getElementById('addItemToOrder');
    if (addButton) {
        addButton.onclick = function() {
            addItemToOrder(data);
        };
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
            throw new Error('Network response was not ok');
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
            showAlert('Error adding product: ' + result.error, 'danger');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showAlert('Error adding product', 'danger');
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
            throw new Error('Network response was not ok');
        }
        return response.json();
    })
    .then(data => {
        if (data.success) {
            showAlert('Database connection successful!', 'success');
        } else {
            showAlert('Database connection failed: ' + data.message, 'danger');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showAlert('Error testing database connection', 'danger');
    })
    .finally(() => {
        showLoadingState('testDatabase', false);
    });
}

// Edit product and payment functions
function editProduct(index) {
fetch('/get-product/' + index)
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (!data.error) {
                populateEditProductForm(data);
            } else {
                showAlert('Error loading product: ' + data.error, 'danger');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showAlert('Error loading product details', 'danger');
        });
}

function populateEditProductForm(data) {
    document.getElementById('editItemCode').value = data.item_code;
    document.getElementById('editItemName').value = data.item_name;
    document.getElementById('editQuantity').value = data.quantity;
    document.getElementById('editUnitPrice').value = data.unit_price;
    document.getElementById('editVatPercentage').value = data.vat_percentage;
    document.getElementById('editDiscount').value = data.row_total_discount;
    document.getElementById('editOfferCode').value = data.offer_code;
    document.getElementById('editOfferMessage').value = data.offer_message;

    document.getElementById('editProductForm').action = '/update-product/' + data.index;

    const editProductModal = new bootstrap.Modal(document.getElementById('editProductModal'));
    editProductModal.show();

    calculateEditEstimatedTotal();
}

function editPayment(index) {
    fetch('/update-payment/' + index)
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (!data.error) {
                populateEditPaymentForm(data, index);
            } else {
                showAlert('Error loading payment: ' + data.error, 'danger');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showAlert('Error loading payment details', 'danger');
        });
}

function populateEditPaymentForm(data, index) {
    document.getElementById('editPaymentMethod').value = data.payment_method;
    document.getElementById('editPaymentStatus').value = data.payment_status;
    document.getElementById('editPaymentAmount').value = data.payment_amount;
    document.getElementById('editTransactionId').value = data.transaction_id;
    document.getElementById('editPaymentOption').value = data.payment_option;
    document.getElementById('editOptionCommission').value = data.option_commission;

    if (data.credit_customer_info) {
        document.getElementById('editCustomerNumber').value = data.credit_customer_info.customer_number;
        document.getElementById('editCustomerName').value = data.credit_customer_info.customer_name;
        document.getElementById('editCreditCustomerInfo').style.display = 'block';
    } else {
        document.getElementById('editCreditCustomerInfo').style.display = 'none';
    }

    updatePaymentOptions(data.payment_method, true);

    document.getElementById('editPaymentForm').action = '/update-payment/' + index;

    const editPaymentModal = new bootstrap.Modal(document.getElementById('editPaymentModal'));
    editPaymentModal.show();
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

    Promise.all(testPromises).catch(error => {
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
            throw new Error('Network response was not ok');
        }
        return response.json();
    })
    .then(data => data)
    .catch(error => {
        console.error('Error:', error);
        return { status: 'Error', error: error.message };
    });
}

// Utility functions
function showLoadingState(elementId, isLoading) {
    const element = document.getElementById(elementId);
    if (!element) return;

    if (isLoading) {
        element.disabled = true;
        element.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Loading...';
    } else {
        element.disabled = false;
        // Restore original content - you might need to store this
        const originalText = element.getAttribute('data-original-text') || element.textContent;
        element.textContent = originalText;
    }
}

function setupAutoCalculation() {
    // Listen for changes in order information fields
    const orderInputs = document.querySelectorAll('#order-tab input, #order-tab select');
    orderInputs.forEach(input => {
        input.addEventListener('change', calculateTotals);
    });

    // Listen for changes in product and payment modals
    document.addEventListener('change', function(e) {
        if (e.target.closest('.modal')) {
            calculateTotals();
        }
    });
}

function setupEventListeners() {
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
        button.addEventListener('click', function() {
            setThemeColor(this.dataset.color);
        });
    });

    // Payment method change events
    const paymentMethodSelect = document.getElementById('paymentMethod');
    if (paymentMethodSelect) {
        paymentMethodSelect.addEventListener('change', function() {
            updatePaymentOptions(this.value);
        });
    }

    const editPaymentMethod = document.getElementById('editPaymentMethod');
    if (editPaymentMethod) {
        editPaymentMethod.addEventListener('change', function() {
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
        itemLookupForm.addEventListener('submit', function(e) {
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
        button.addEventListener('click', function() {
            testSingleEndpoint(this.dataset.name, this.dataset.url);
        });
    });

    // Edit product buttons
    document.querySelectorAll('.edit-product').forEach(button => {
        button.addEventListener('click', function() {
            editProduct(this.dataset.index);
        });
    });

    // Edit payment buttons
    document.querySelectorAll('.edit-payment').forEach(button => {
        button.addEventListener('click', function() {
            editPayment(this.dataset.index);
        });
    });
}

// Initialize application
document.addEventListener('DOMContentLoaded', function() {
    initializeTheme();
    setupEventListeners();
    setupAutoCalculation();
    calculateTotals();

    // Add payment options data to the page
    const paymentOptionsData = document.createElement('div');
    paymentOptionsData.id = 'paymentOptionsData';
    paymentOptionsData.style.display = 'none';
    paymentOptionsData.textContent = JSON.stringify(window.paymentOptions || {});
    document.body.appendChild(paymentOptionsData);

    // Trigger change event on page load if there's a selected value
    const paymentMethodSelect = document.getElementById('paymentMethod');
    if (paymentMethodSelect && paymentMethodSelect.value) {
        paymentMethodSelect.dispatchEvent(new Event('change'));
    }
});