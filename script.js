// Enhanced JavaScript with better organization and fixed functionality
// Online Order Tool - JavaScript Module

class OnlineOrderTool {
    constructor() {
        this.currentTheme = localStorage.getItem('theme') || 'light';
        this.currentColor = localStorage.getItem('themeColor') || 'blue';
        this.calculationTimeout = null;

        // Payment options configuration - Use window.paymentOptions from template if available
        this.paymentOptions = window.paymentOptions || {
            'COD': ['COD'],
            'Visa': ['visa', 'mastercard', 'mada', 'amex'],
            'RajhiPoints': ['RajhiPoints'],
            'Tamara': ['tamara'],
            'Tabby': ['tabby'],
            'NeqatyPoints': ['NeqatyPoints'],
            'QitafPoints': ['QitafPoints'],
            'MisPay': ['mispay'],
            'Emkan': ['emkan'],
            'YouGotaGift': ['yougotagift'],
            'OgMoney': ['ogmoney']
        };

        this.init();
    }

    init() {
        this.initializeTheme();
        this.setupEventListeners();
        this.setupAutoCalculation();
        this.initializeApiUrls();
        this.setupApiEventListeners();
        this.updateEndpointUrl();

        console.log('Online Order Tool initialized successfully');
    }

    // Alert System
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
            alert(`${type.toUpperCase()}: ${message}`);
        }
    }

    getAlertIcon(type) {
        const icons = {
            'success': 'bi-check-circle',
            'danger': 'bi-exclamation-triangle',
            'warning': 'bi-exclamation-circle',
            'info': 'bi-info-circle'
        };
        return icons[type] || 'bi-info-circle';
    }

    // Theme Management
    setThemeColor(color) {
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
            this.currentColor = color;
        } catch (error) {
            console.error('Error setting theme color:', error);
        }
    }

    toggleTheme() {
        try {
            const html = document.documentElement;
            this.currentTheme = this.currentTheme === 'dark' ? 'light' : 'dark';
            html.setAttribute('data-bs-theme', this.currentTheme);

            // Update CSS variables
            this.updateCssVariables();

            // Update icon and text
            const themeToggleBtn = document.querySelector('.theme-toggle-btn');
            if (themeToggleBtn) {
                const icon = themeToggleBtn.querySelector('i');
                const text = themeToggleBtn.querySelector('span');
                icon.className = this.currentTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
                text.textContent = this.currentTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
            }

            // Save preference
            localStorage.setItem('theme', this.currentTheme);
        } catch (error) {
            console.error('Error toggling theme:', error);
        }
    }

    initializeTheme() {
        try {
            const html = document.documentElement;

            // Set theme
            html.setAttribute('data-bs-theme', this.currentTheme);

            // Set color
            this.setThemeColor(this.currentColor);

            // Update theme toggle button
            const themeToggleBtn = document.querySelector('.theme-toggle-btn');
            if (themeToggleBtn) {
                const icon = themeToggleBtn.querySelector('i');
                const text = themeToggleBtn.querySelector('span');
                icon.className = this.currentTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
                text.textContent = this.currentTheme === 'dark' ? 'Dark Mode' : 'Light Mode';
            }

            // Force CSS variable update
            this.updateCssVariables();

        } catch (error) {
            console.error('Error initializing theme:', error);
        }
    }

    updateCssVariables() {
        const root = document.documentElement;
        const isDark = this.currentTheme === 'dark';

        // Update CSS custom properties based on theme
        if (isDark) {
            root.style.setProperty('--bs-body-bg', '#1a1d21');
            root.style.setProperty('--bs-body-color', '#e9ecef');
            root.style.setProperty('--bs-border-color', '#2c3034');
        } else {
            root.style.setProperty('--bs-body-bg', '#f8f9fa');
            root.style.setProperty('--bs-body-color', '#212529');
            root.style.setProperty('--bs-border-color', '#dee2e6');
        }
    }

    // Calculation Functions
    calculateTotals() {
        this.showLoadingState('calculateTotals', true);

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
                this.updateTotalsDisplay(data);
            })
            .catch(error => {
                console.error('Error calculating totals:', error);
                this.showAlert('Error calculating totals: ' + error.message, 'danger');
            })
            .finally(() => {
                this.showLoadingState('calculateTotals', false);
            });
    }

    updateTotalsDisplay(data) {
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

    calculateEstimatedTotal() {
        try {
            const quantity = parseFloat(document.querySelector('input[name="quantity"]')?.value) || 0;
            const unitPrice = parseFloat(document.querySelector('input[name="unit_price"]')?.value) || 0;
            const vatPercent = parseFloat(document.querySelector('input[name="vat_percentage"]')?.value) || 0;
            const discount = parseFloat(document.querySelector('input[name="discount"]')?.value) || 0;

            // Normalize VAT percentage
            let vatDecimal = vatPercent;
            if (vatPercent > 100) {
                vatDecimal = vatPercent / 100;
            } else if (vatPercent > 1) {
                vatDecimal = vatPercent / 100;
            }

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

    calculateEditEstimatedTotal() {
        try {
            const quantity = parseFloat(document.getElementById('editQuantity')?.value) || 0;
            const unitPrice = parseFloat(document.getElementById('editUnitPrice')?.value) || 0;
            const vatPercent = parseFloat(document.getElementById('editVatPercentage')?.value) || 0;
            const discount = parseFloat(document.getElementById('editDiscount')?.value) || 0;

            // Normalize VAT percentage
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

    // Real-time Updates
    setupRealTimeUpdates() {
        // Order field updates
        document.querySelectorAll('.order-field').forEach(field => {
            field.addEventListener('change', () => {
                this.updateOrderField(field.dataset.field, field.value);
            });

            // Optional: Update on input for immediate feedback
            if (field.type === 'text' || field.type === 'textarea') {
                field.addEventListener('input', () => {
                    this.updateOrderField(field.dataset.field, field.value);
                });
            }
        });
    }

    updateOrderField(field, value) {
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

    // Payment Methods - UPDATED WITH API RULES
    updatePaymentOptions(method, isEdit = false) {
        try {
            const prefix = isEdit ? 'edit' : '';

            // Helper to get correct ID (Add: camelCase, Edit: edit+PascalCase)
            const getId = (baseName) => {
                if (isEdit) return 'edit' + baseName.charAt(0).toUpperCase() + baseName.slice(1);
                return baseName.charAt(0).toLowerCase() + baseName.slice(1);
            };

            const optionSelect = document.getElementById(getId('paymentOption'));
            const creditCustomerInfo = document.getElementById(getId('creditCustomerInfo'));
            const paymentStatusSelect = document.getElementById(getId('paymentStatus'));
            const paymentAmountField = document.getElementById(getId('paymentAmount'));

            if (!optionSelect) return;

            // Clear existing options
            optionSelect.innerHTML = '<option value="">Select Option</option>';

            // Populate payment options based on selected method
            if (method && this.paymentOptions[method]) {
                const options = this.paymentOptions[method];
                options.forEach(option => {
                    const optionElement = document.createElement('option');
                    optionElement.value = option;
                    optionElement.textContent = option.charAt(0).toUpperCase() + option.slice(1);
                    optionSelect.appendChild(optionElement);
                });

                // Auto-select if there's only one option (e.g., COD, Tamara)
                if (options.length === 1) {
                    optionSelect.value = options[0];
                }
            }
            // Show customer info fields for COD, Visa, and PostToCredit
            if (creditCustomerInfo) {
                creditCustomerInfo.style.display = (['COD', 'Visa', 'PostToCredit'].includes(method)) ? 'block' : 'none';
            }

            // Default logic for status and amount (only if empty)
            if (paymentStatusSelect && paymentAmountField) {
                if (method === 'COD') {
                    if (!paymentStatusSelect.value) paymentStatusSelect.value = 'not_payment';
                    if (!paymentAmountField.value) this.setPaymentAmountToFullTotal(paymentAmountField);
                } else if (['Visa', 'Tamara', 'Tabby', 'MisPay', 'Emkan', 'YouGotaGift', 'OgMoney', 'RajhiPoints', 'QitafPoints', 'NeqatyPoints'].includes(method)) {
                    if (!paymentStatusSelect.value) paymentStatusSelect.value = 'done_payment';
                    if (!paymentAmountField.value) this.setPaymentAmountToFullTotal(paymentAmountField);
                }
            }

            // Trigger Validation
            this.validatePaymentForm(isEdit);

        } catch (error) {
            console.error('Error updating payment options:', error);
        }
    }

    validatePaymentForm(isEdit) {
        const prefix = isEdit ? 'edit' : '';

        let methodId = isEdit ? 'editPaymentMethod' : 'paymentMethod';
        let statusId = isEdit ? 'editPaymentStatus' : 'paymentStatus';
        let amountId = isEdit ? 'editPaymentAmount' : 'paymentAmount';
        let errorId = isEdit ? 'editPaymentError' : 'addPaymentError';
        let submitBtnSelector = isEdit ? '#editPaymentModal button[type="submit"]' : '#addPaymentModal button[type="submit"]';

        const method = document.getElementById(methodId)?.value;
        const status = document.getElementById(statusId)?.value;
        const amount = parseFloat(document.getElementById(amountId)?.value || 0);
        const errorEl = document.getElementById(errorId);

        const finalTotalElement = document.getElementById('finalTotal');
        const orderTotal = parseFloat(finalTotalElement ? finalTotalElement.textContent : 0) || 0;

        if (!method) return;

        let isValid = true;
        let message = '';

        // Helper to get customer field IDs
        const getId = (baseName) => {
            if (isEdit) return 'edit' + baseName.charAt(0).toUpperCase() + baseName.slice(1);
            return baseName.charAt(0).toLowerCase() + baseName.slice(1);
        };

        // Validate customer fields for COD, Visa, and PostToCredit
        // FIELDS ARE OPTIONAL as per new requirement
        if (['COD', 'Visa', 'PostToCredit'].includes(method)) {
            // No strict validation required for customer name/number
        }

        // Only check other validations if customer fields are valid
        if (isValid) {
            if (method === 'COD') {
                if (status && status !== 'not_payment') {
                    isValid = false;
                    message = 'COD must be "Not Payment"';
                }
            } else {
                // Digital / Points
                if (status && status !== 'done_payment') {
                    isValid = false;
                    message = method + ' must be "Done Payment"';
                }
                // Strict amount check
                if (Math.abs(amount - orderTotal) > 0.01) {
                    isValid = false;
                    message = `Amount must equal Order Total (${orderTotal})`;
                }
            }
        }

        if (errorEl) {
            errorEl.textContent = message;
            errorEl.style.display = isValid ? 'none' : 'block';
        }

        const btn = document.querySelector(submitBtnSelector);
        if (btn) btn.disabled = !isValid;
    }

    setPaymentAmountToFullTotal(paymentAmountField) {
        // Get the current order total and set it as payment amount
        const finalTotalElement = document.getElementById('finalTotal');
        if (finalTotalElement && paymentAmountField) {
            const total = parseFloat(finalTotalElement.textContent) || 0;
            paymentAmountField.value = total.toFixed(2);
        } else {
            // Fallback: calculate from quick stats
            setTimeout(() => {
                const finalTotalElement = document.getElementById('finalTotal');
                if (finalTotalElement && paymentAmountField) {
                    const total = parseFloat(finalTotalElement.textContent) || 0;
                    paymentAmountField.value = total.toFixed(2);
                }
            }, 500);
        }
    }

    // Database and API Functions
    handleItemLookup() {
        const form = document.getElementById('itemLookupForm');
        if (!form) return;

        const formData = new FormData(form);
        const params = new URLSearchParams(formData);

        // Validate material number
        const materialNumber = formData.get('material_number');
        if (!materialNumber || !materialNumber.match(/^\d{6}$/)) {
            this.showAlert('Material number must be exactly 6 digits', 'warning');
            return;
        }

        this.showLoadingState('itemLookupForm', true);

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
                this.displayItemDetails(data);
            })
            .catch(error => {
                console.error('Error fetching item details:', error);
                this.showAlert('Error fetching item details: ' + error.message, 'danger');
            })
            .finally(() => {
                this.showLoadingState('itemLookupForm', false);
            });
    }

    displayItemDetails(data) {
        try {
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

            // Set up add to order button
            const addButton = document.getElementById('addItemToOrder');
            if (addButton) {
                addButton.onclick = () => {
                    this.addItemToOrder(data);
                };
            }
        } catch (error) {
            console.error('Error displaying item details:', error);
            this.showAlert('Error displaying item details', 'danger');
        }
    }

    addItemToOrder(itemData) {
        this.showLoadingState('addItemToOrder', true);

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
                    this.showAlert('Product added successfully!', 'success');
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                } else {
                    throw new Error(result.error || 'Unknown error adding product');
                }
            })
            .catch(error => {
                console.error('Error adding product:', error);
                this.showAlert('Error adding product: ' + error.message, 'danger');
            })
            .finally(() => {
                this.showLoadingState('addItemToOrder', false);
            });
    }

    testDatabaseConnection() {
        const form = document.getElementById('databaseForm');
        if (!form) return;

        const formData = new FormData(form);
        const data = Object.fromEntries(formData.entries());

        // Validate required fields
        if (!data.server || !data.database || !data.username) {
            this.showAlert('Please fill in all required database fields', 'warning');
            return;
        }

        this.showLoadingState('testDatabase', true);

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
                    this.showAlert('Database connection successful!', 'success');
                } else {
                    throw new Error(data.message || 'Database connection failed');
                }
            })
            .catch(error => {
                console.error('Error testing database connection:', error);
                this.showAlert('Error testing database connection: ' + error.message, 'danger');
            })
            .finally(() => {
                this.showLoadingState('testDatabase', false);
            });
    }

    // Edit Functions
    editProduct(index) {
        if (index === undefined || index === null) {
            this.showAlert('Invalid product index', 'danger');
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
                this.populateEditProductForm(data);
            })
            .catch(error => {
                console.error('Error loading product:', error);
                this.showAlert('Error loading product: ' + error.message, 'danger');
            });
    }

    populateEditProductForm(data) {
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

            this.calculateEditEstimatedTotal();
        } catch (error) {
            console.error('Error populating edit product form:', error);
            this.showAlert('Error loading product details', 'danger');
        }
    }

    editPayment(index) {
        if (index === undefined || index === null) {
            this.showAlert('Invalid payment index', 'danger');
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
                this.populateEditPaymentForm(data, index);
            })
            .catch(error => {
                console.error('Error loading payment:', error);
                this.showAlert('Error loading payment: ' + error.message, 'danger');
            });
    }

    populateEditPaymentForm(data, index) {
        try {
            document.getElementById('editPaymentMethod').value = data.payment_method || '';
            document.getElementById('editPaymentStatus').value = data.payment_status || '';
            document.getElementById('editPaymentAmount').value = data.payment_amount || 0;
            document.getElementById('editTransactionId').value = data.transaction_id || '';
            document.getElementById('editPaymentOption').value = data.payment_option || '';
            document.getElementById('editOptionCommission').value = data.option_commission || 0;

            // Show customer info fields for ALL payment methods
            document.getElementById('editCreditCustomerInfo').style.display = 'block';

            // Populate customer info if available
            if (data.credit_customer_info) {
                document.getElementById('editCustomerName').value = data.credit_customer_info.customer_name || '';
                document.getElementById('editCustomerNumber').value = data.credit_customer_info.customer_number || '';
            }

            // Update payment options and show/hide credit customer fields
            this.updatePaymentOptions(data.payment_method, true);

            document.getElementById('editPaymentForm').action = '/update-payment/' + index;

            const editPaymentModal = new bootstrap.Modal(document.getElementById('editPaymentModal'));
            editPaymentModal.show();
        } catch (error) {
            console.error('Error populating edit payment form:', error);
            this.showAlert('Error loading payment details', 'danger');
        }
    }

    // API Endpoint Testing
    testAllEndpoints() {
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
                    this.testSingleEndpoint(name, url).then(result => {
                        statusCell.innerHTML = `<span class="badge bg-${result.status === 'Online' ? 'success' : 'danger'}">${result.status}</span>`;
                        testButton.disabled = false;
                    })
                );
            }
        });

        Promise.all(testPromises)
            .then(() => {
                this.showAlert('All endpoints tested successfully!', 'success');
            })
            .catch(error => {
                console.error('Error testing endpoints:', error);
                this.showAlert('Error testing some endpoints', 'warning');
            });
    }

    testSingleEndpoint(name, url) {
        return fetch('/test-endpoint', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ url: url })
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response.json();
            })
            .then(data => {
                if (data.status === 'Online') {
                    this.showAlert(`Endpoint "${name}" is online`, 'success');
                } else {
                    this.showAlert(`Endpoint "${name}" is offline: ${data.error}`, 'warning');
                }
                return data;
            })
            .catch(error => {
                console.error(`Error testing endpoint ${name}:`, error);
                this.showAlert(`Error testing endpoint "${name}"`, 'danger');
                return { status: 'Error', error: error.message };
            });
    }

    // API Response Functions
    displayApiResponse(responseData) {
        try {
            const responseDisplay = document.getElementById('responseDisplay');
            const statusCode = document.getElementById('statusCode');
            const requestUrl = document.getElementById('requestUrl');

            if (!responseDisplay || !statusCode || !requestUrl) return;

            // Format the response for better readability
            let formattedResponse;
            try {
                // Try to parse and format JSON
                const parsedResponse = JSON.parse(responseData.response_text);
                formattedResponse = JSON.stringify(parsedResponse, null, 2);
            } catch (e) {
                // If not JSON, use as-is
                formattedResponse = responseData.response_text;
            }

            // Create timestamp
            const timestamp = new Date().toLocaleString();

            // Build the response text
            const responseText = `=== API Response - ${timestamp} ===\n` +
                `Status: ${responseData.status_code}\n` +
                `URL: ${responseData.url_sent}\n` +
                `Success: ${responseData.success}\n\n` +
                `Response Body:\n${formattedResponse}\n\n` +
                '='.repeat(50) + '\n\n';

            // Update display elements
            responseDisplay.textContent = responseText;

            // Style status code based on response
            statusCode.value = responseData.status_code;
            statusCode.className = 'form-control ';
            if (responseData.status_code >= 200 && responseData.status_code < 300) {
                statusCode.classList.add('status-success');
            } else if (responseData.status_code >= 400) {
                statusCode.classList.add('status-error');
            } else {
                statusCode.classList.add('status-warning');
            }

            requestUrl.value = responseData.url_sent;

            // Auto-scroll if enabled
            const autoScroll = document.getElementById('autoScroll');
            if (autoScroll && autoScroll.checked) {
                responseDisplay.scrollTop = 0;
            }

            // Show success/error message
            if (responseData.success) {
                this.showAlert(`Request sent successfully! Status: ${responseData.status_code}`, 'success');
            } else {
                this.showAlert(`Request failed with status: ${responseData.status_code}`, 'danger');
            }

        } catch (error) {
            console.error('Error displaying API response:', error);
            this.showAlert('Error displaying API response', 'danger');
        }
    }

    clearResponseDisplay() {
        const responseDisplay = document.getElementById('responseDisplay');
        const statusCode = document.getElementById('statusCode');
        const requestUrl = document.getElementById('requestUrl');

        if (responseDisplay) responseDisplay.textContent = 'No response yet. Send a request to see the response here.';
        if (statusCode) {
            statusCode.value = '';
            statusCode.className = 'form-control';
        }
        if (requestUrl) requestUrl.value = '';
    }

    copyResponseToClipboard() {
        const responseDisplay = document.getElementById('responseDisplay');
        if (responseDisplay && responseDisplay.textContent) {
            navigator.clipboard.writeText(responseDisplay.textContent).then(() => {
                this.showAlert('Response copied to clipboard!', 'success');
            }).catch(err => {
                console.error('Failed to copy response: ', err);
                this.showAlert('Failed to copy response', 'danger');
            });
        }
    }

    // NEW: Copy Request to Clipboard from Preview
    copyRequestToClipboard() {
        const previewContent = document.querySelector('#requestPreviewModal pre');
        if (previewContent && previewContent.textContent) {
            navigator.clipboard.writeText(previewContent.textContent).then(() => {
                this.showAlert('Request data copied to clipboard!', 'success');
                // Close the modal after copying
                const modal = bootstrap.Modal.getInstance(document.getElementById('requestPreviewModal'));
                if (modal) {
                    modal.hide();
                }
            }).catch(err => {
                console.error('Failed to copy request: ', err);
                this.showAlert('Failed to copy request data', 'danger');
            });
        }
    }

    previewRequest() {
        // Get current order data and format it for preview
        fetch('/export-json')
            .then(response => response.json())
            .then(orderData => {
                const formattedData = JSON.stringify(orderData, null, 2);

                // Create preview modal with COPY button
                const previewHtml = `
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
                                    <pre style="max-height: 500px; overflow-y: auto; background: #f8f9fa; padding: 1rem; border-radius: 0.375rem; font-size: 0.875rem;">${formattedData}</pre>
                                </div>
                                <div class="modal-footer">
                                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                                    <button type="button" class="btn btn-primary" id="copyRequestBtnFooter">
                                        <i class="bi bi-clipboard-check me-1"></i>Copy Request Data
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                `;

                // Remove existing modal if any
                const existingModal = document.getElementById('requestPreviewModal');
                if (existingModal) {
                    existingModal.remove();
                }

                // Add new modal to DOM and show it
                document.body.insertAdjacentHTML('beforeend', previewHtml);
                const modal = new bootstrap.Modal(document.getElementById('requestPreviewModal'));

                // Add copy functionality to both buttons
                const copyBtn = document.getElementById('copyRequestBtn');
                const copyBtnFooter = document.getElementById('copyRequestBtnFooter');

                if (copyBtn) {
                    copyBtn.addEventListener('click', () => this.copyRequestToClipboard());
                }
                if (copyBtnFooter) {
                    copyBtnFooter.addEventListener('click', () => this.copyRequestToClipboard());
                }

                modal.show();
            })
            .catch(error => {
                console.error('Error previewing request:', error);
                this.showAlert('Error previewing request', 'danger');
            });
    }

    // Utility Functions
    showLoadingState(elementId, isLoading) {
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

    escapeHtml(unsafe) {
        if (unsafe === null || unsafe === undefined) return '';
        return unsafe
            .toString()
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    setupAutoCalculation() {
        // Listen for changes in order information fields
        const orderInputs = document.querySelectorAll('#order-tab input, #order-tab select');
        orderInputs.forEach(input => {
            // Use debouncing to prevent too many rapid calculations
            input.addEventListener('change', () => {
                clearTimeout(this.calculationTimeout);
                this.calculationTimeout = setTimeout(() => this.calculateTotals(), 500);

                // Specifically handle delivery cost updates
                if (input.name === 'delivery_cost') {
                    this.updateOrderField('delivery_cost', input.value);
                }
            });

            input.addEventListener('input', () => {
                clearTimeout(this.calculationTimeout);
                this.calculationTimeout = setTimeout(() => this.calculateTotals(), 1000);
            });
        });

        // Specifically listen for delivery cost changes
        const deliveryCostField = document.querySelector('input[name="delivery_cost"]');
        if (deliveryCostField) {
            deliveryCostField.addEventListener('change', () => {
                this.updateOrderField('delivery_cost', deliveryCostField.value);
                clearTimeout(this.calculationTimeout);
                this.calculationTimeout = setTimeout(() => this.calculateTotals(), 500);
            });

            deliveryCostField.addEventListener('input', () => {
                clearTimeout(this.calculationTimeout);
                this.calculationTimeout = setTimeout(() => this.calculateTotals(), 1000);
            });
        }

        // Listen for changes in product and payment modals
        document.addEventListener('change', (e) => {
            if (e.target.closest('.modal')) {
                clearTimeout(this.calculationTimeout);
                this.calculationTimeout = setTimeout(() => this.calculateTotals(), 500);
            }
        });

        // Auto-calculate on page load
        setTimeout(() => this.calculateTotals(), 1000);
    }

    // API URL Management
    initializeApiUrls() {
        // This will be populated from the template
        window.apiUrls = window.apiUrls || {};
    }

    updateEndpointUrl() {
        const endpointSelect = document.getElementById('apiEndpoint');
        const customUrlInput = document.getElementById('customUrl');
        const selectedUrlSpan = document.getElementById('selectedUrl');

        if (endpointSelect && selectedUrlSpan) {
            const selectedOption = endpointSelect.options[endpointSelect.selectedIndex];
            if (selectedOption.value && window.apiUrls[selectedOption.value]) {
                selectedUrlSpan.textContent = window.apiUrls[selectedOption.value];
                // Clear custom URL when selecting predefined endpoint
                if (customUrlInput) {
                    customUrlInput.value = '';
                }
            } else {
                selectedUrlSpan.textContent = '';
            }
        }
    }

    // Event Listeners Setup
    setupEventListeners() {
        try {
            // Calculate totals button
            const calculateTotalsBtn = document.getElementById('calculateTotals');
            if (calculateTotalsBtn) {
                calculateTotalsBtn.addEventListener('click', () => this.calculateTotals());
            }

            // Theme toggle button
            const themeToggleBtn = document.querySelector('.theme-toggle-btn');
            if (themeToggleBtn) {
                themeToggleBtn.addEventListener('click', () => this.toggleTheme());
            }

            // Theme color buttons
            document.querySelectorAll('.theme-color').forEach(button => {
                button.addEventListener('click', () => {
                    this.setThemeColor(button.dataset.color);
                });
            });

            // Payment method change events
            const paymentMethodSelect = document.getElementById('paymentMethod');
            if (paymentMethodSelect) {
                paymentMethodSelect.addEventListener('change', () => {
                    this.updatePaymentOptions(paymentMethodSelect.value, false);
                });
                // Initialize with current value
                if (paymentMethodSelect.value) {
                    this.updatePaymentOptions(paymentMethodSelect.value, false);
                }
            }

            if (editPaymentMethod) {
                editPaymentMethod.addEventListener('change', () => {
                    this.updatePaymentOptions(editPaymentMethod.value, true);
                });
            }

            // Payment Validation - Add Modal
            const addStatus = document.getElementById('paymentStatus');
            const addAmount = document.getElementById('paymentAmount');
            [addStatus, addAmount].forEach(el => {
                if (el) {
                    el.addEventListener('change', () => this.validatePaymentForm(false));
                    el.addEventListener('input', () => this.validatePaymentForm(false));
                }
            });

            // Payment Validation - Edit Modal
            const editStatus = document.getElementById('editPaymentStatus');
            const editAmount = document.getElementById('editPaymentAmount');
            [editStatus, editAmount].forEach(el => {
                if (el) {
                    el.addEventListener('change', () => this.validatePaymentForm(true));
                    el.addEventListener('input', () => this.validatePaymentForm(true));
                }
            });

            // Product form calculation events
            const productInputs = document.querySelectorAll('#addProductModal input');
            productInputs.forEach(input => {
                if (input.name === 'quantity' || input.name === 'unit_price' || input.name === 'vat_percentage' || input.name === 'discount') {
                    input.addEventListener('input', () => this.calculateEstimatedTotal());
                }
            });

            const editProductInputs = document.querySelectorAll('#editProductModal input');
            editProductInputs.forEach(input => {
                if (input.name === 'quantity' || input.name === 'unit_price' || input.name === 'vat_percentage' || input.name === 'discount') {
                    input.addEventListener('input', () => this.calculateEditEstimatedTotal());
                }
            });

            // Item lookup form
            const itemLookupForm = document.getElementById('itemLookupForm');
            if (itemLookupForm) {
                itemLookupForm.addEventListener('submit', (e) => {
                    e.preventDefault();
                    this.handleItemLookup();
                });
            }

            // Database test connection
            const testDatabaseBtn = document.getElementById('testDatabase');
            if (testDatabaseBtn) {
                testDatabaseBtn.addEventListener('click', () => this.testDatabaseConnection());
            }

            // Test all endpoints
            const testAllEndpointsBtn = document.getElementById('testAllEndpoints');
            if (testAllEndpointsBtn) {
                testAllEndpointsBtn.addEventListener('click', () => this.testAllEndpoints());
            }

            // Single endpoint test buttons
            document.querySelectorAll('.test-single-endpoint').forEach(button => {
                button.addEventListener('click', () => {
                    const name = button.dataset.name;
                    const url = button.dataset.url;
                    this.testSingleEndpoint(name, url).then(result => {
                        const statusCell = button.closest('tr').querySelector('td:nth-child(3)');
                        if (statusCell) {
                            statusCell.innerHTML = `<span class="badge bg-${result.status === 'Online' ? 'success' : 'danger'}">${result.status}</span>`;
                        }
                    });
                });
            });

            // Edit product buttons
            document.querySelectorAll('.edit-product').forEach(button => {
                button.addEventListener('click', () => {
                    const index = parseInt(button.dataset.index);
                    if (!isNaN(index)) {
                        this.editProduct(index);
                    }
                });
            });

            // Edit payment buttons
            document.querySelectorAll('.edit-payment').forEach(button => {
                button.addEventListener('click', () => {
                    const index = parseInt(button.dataset.index);
                    if (!isNaN(index)) {
                        this.editPayment(index);
                    }
                });
            });

            // Form validation
            const forms = document.querySelectorAll('form');
            forms.forEach(form => {
                form.addEventListener('submit', (e) => {
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
                        this.showAlert('Please fill in all required fields', 'warning');
                    }
                });
            });

            // Tab persistence
            const tabLinks = document.querySelectorAll('a[data-bs-toggle="tab"]');
            tabLinks.forEach(tab => {
                tab.addEventListener('shown.bs.tab', (e) => {
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
            this.setupRealTimeUpdates();

            // Set default VAT to 0
            const vatField = document.querySelector('input[name="vat_percentage"]');
            if (vatField && !vatField.value) {
                vatField.value = '0';
            }

            const editVatField = document.getElementById('editVatPercentage');
            if (editVatField && !editVatField.value) {
                editVatField.value = '0';
            }

            // Keyboard shortcuts
            document.addEventListener('keydown', (e) => {
                // Ctrl + T to toggle theme
                if (e.ctrlKey && e.key === 't') {
                    e.preventDefault();
                    this.toggleTheme();
                }
                // Ctrl + R to calculate totals
                if (e.ctrlKey && e.key === 'r') {
                    e.preventDefault();
                    this.calculateTotals();
                }
            });

        } catch (error) {
            console.error('Error setting up event listeners:', error);
        }
    }

    setupApiEventListeners() {
        // Endpoint URL updates
        const endpointSelect = document.getElementById('apiEndpoint');
        if (endpointSelect) {
            endpointSelect.addEventListener('change', () => this.updateEndpointUrl());
        }

        // Clear response button
        const clearResponseBtn = document.getElementById('clearResponse');
        if (clearResponseBtn) {
            clearResponseBtn.addEventListener('click', () => this.clearResponseDisplay());
        }

        // Copy response button
        const copyResponseBtn = document.getElementById('copyResponse');
        if (copyResponseBtn) {
            copyResponseBtn.addEventListener('click', () => this.copyResponseToClipboard());
        }

        // Preview request button
        const previewRequestBtn = document.getElementById('previewRequest');
        if (previewRequestBtn) {
            previewRequestBtn.addEventListener('click', () => this.previewRequest());
        }

        // Custom URL input clears endpoint selection
        const customUrlInput = document.getElementById('customUrl');
        if (customUrlInput) {
            customUrlInput.addEventListener('input', () => {
                if (customUrlInput.value && endpointSelect) {
                    endpointSelect.value = '';
                    const selectedUrlSpan = document.getElementById('selectedUrl');
                    if (selectedUrlSpan) {
                        selectedUrlSpan.textContent = '';
                    }
                }
            });
        }

        // Form submission handling
        const apiRequestForm = document.getElementById('apiRequestForm');
        if (apiRequestForm) {
            apiRequestForm.addEventListener('submit', (e) => {
                e.preventDefault();

                const endpointSelect = document.getElementById('apiEndpoint');
                const customUrlInput = document.getElementById('customUrl');
                const validateCheckbox = document.getElementById('validateBeforeSend');

                if (!endpointSelect.value && !customUrlInput.value) {
                    this.showAlert('Please select an API endpoint or enter a custom URL', 'warning');
                    return;
                }

                // Show loading state
                const submitBtn = document.getElementById('sendRequestBtn');
                if (submitBtn) {
                    submitBtn.disabled = true;
                    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Sending...';
                }

                const formData = new FormData(apiRequestForm);
                if (validateCheckbox && validateCheckbox.checked) {
                    formData.set('validateBeforeSend', 'true');
                } else {
                    formData.delete('validateBeforeSend');
                }

                fetch('/send-request', {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                })
                    .then(response => response.json().then(data => ({ status: response.status, body: data })))
                    .then(({ status, body }) => {
                        this.displayApiResponse(body);

                        if (status === 400 && body.response_text) {
                            try {
                                const parsed = JSON.parse(body.response_text);
                                if (parsed.validation_errors) {
                                    this.showAlert('Validation failed. Please check errors.', 'danger');
                                }
                            } catch (e) { }
                        }
                    })
                    .catch(error => {
                        console.error('Error sending request:', error);
                        this.showAlert('Error sending request: ' + error.message, 'danger');
                    })
                    .finally(() => {
                        if (submitBtn) {
                            submitBtn.disabled = false;
                            submitBtn.innerHTML = '<i class="bi bi-send me-2"></i>Send Request';
                        }
                    });
            });
        }
    }
}

// Initialize application when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    // Initialize the application
    window.onlineOrderTool = new OnlineOrderTool();

    // Check if there's response data to display (from form submission)
    if (window.responseData) {
        window.onlineOrderTool.displayApiResponse(window.responseData);
    }

    // Check if there's cancel response data to display
    if (window.cancelResponseData) {
        window.onlineOrderTool.displayApiResponse(window.cancelResponseData);
    }
});

// Make functions available globally for backward compatibility
function calculateTotals() {
    if (window.onlineOrderTool) {
        window.onlineOrderTool.calculateTotals();
    }
}

function toggleTheme() {
    if (window.onlineOrderTool) {
        window.onlineOrderTool.toggleTheme();
    }
}

function updatePaymentOptions(method, isEdit = false) {
    if (window.onlineOrderTool) {
        window.onlineOrderTool.updatePaymentOptions(method, isEdit);
    }
}