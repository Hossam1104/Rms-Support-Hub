import json
import os
import socket
import logging
from datetime import datetime, timedelta
                    continue

            raise Exception("Could not connect with any available driver")

        except Exception as e:
            logger.error(f"Database connection error: {str(e)}")
            return None


class OrderManager:
    """Order data management"""

    @staticmethod
    def initialize_session_data():
        """Initialize or update session data with defaults"""
        if 'order_data' not in session:
            session['order_data'] = session.get('saved_order_data', DEFAULT_DATA.copy())

        # Set current date and time
        current_date = datetime.now().strftime("%Y-%m-%d")
        current_time = datetime.now().strftime("%H:%M:%S")
        one_hour_later = (datetime.now() + timedelta(hours=1)).strftime("%H:%M:%S")

        order_data = session['order_data']

        # Update delivery fields if needed
        if not order_data.get('delivery_date') or order_data.get('delivery_date') in ['CURRENT_DATE', '']:
            order_data['delivery_date'] = current_date

        if not order_data.get('delivery_from_time') or order_data.get('delivery_from_time') in ['CURRENT_TIME', '']:
            order_data['delivery_from_time'] = current_time

        if not order_data.get('delivery_to_time') or order_data.get('delivery_to_time') in ['CURRENT_TIME_PLUS_1H', '']:
            order_data['delivery_to_time'] = one_hour_later

        session['order_data'] = order_data

        if 'products' not in session:
            session['products'] = session.get('saved_products', DEFAULT_DATA['order_products'].copy())

        if 'payments' not in session:
            session['payments'] = session.get('saved_payments', DEFAULT_DATA['payment_methods_with_options'].copy())

        if 'api_endpoint' not in session:
            session['api_endpoint'] = DEFAULT_API_ENDPOINT

    @staticmethod
    def prepare_order_data():
        order_data = session.get('order_data', DEFAULT_DATA.copy())
        products = session.get('products', [])
        payments = session.get('payments', [])

        # Calculate totals with 2 decimal places
        order_product_total_value = round(sum(product.get('row_net_total', 0) for product in products), 2)
        order_total_discount = round(sum(product.get('row_total_discount', 0) for product in products), 2)
        delivery_cost = round(order_data.get('order_delivery_cost', 0), 2)
        order_final_total_value = round(order_product_total_value + delivery_cost, 2)

        # Format time values
        delivery_from_time = format_time(order_data.get('delivery_from_time', ''))
        delivery_to_time = format_time(order_data.get('delivery_to_time', ''))

        # Determine payment status and method
        order_payment_status = determine_payment_status(payments)
        payment_methods_list = [payment.get('payment_method', '') for payment in payments]
        order_payment_method = ",".join(payment_methods_list) if payment_methods_list else "cash"

        # Prepare final order data
        final_order_data = {
            "branch_code": order_data.get('branch_code', ''),
            "order_code": order_data.get('order_code', ''),
            "parent_order_code": order_data.get('parent_order_code', ''),
            "order_creation_date": datetime.now().isoformat() + 'Z',
            "order_notes": order_data.get('order_notes', "Don't Ring the bell"),
            "order_product_total_value": order_product_total_value,
            "is_delivery": order_data.get('is_delivery', 1),
            "order_delivery_cost": delivery_cost,
            "order_total_discount": order_total_discount,
            "order_final_total_value": order_final_total_value,
            "order_payment_method": order_payment_method,
            "order_status": order_data.get('order_status', 'new'),
            "client_country_code": order_data.get('client_country_code', '966'),
            "client_phone": order_data.get('client_phone', ''),
            "client_first_name": order_data.get('client_first_name', ''),
            "client_middle_name": order_data.get('client_middle_name', ''),
            "client_last_name": order_data.get('client_last_name', ''),
            "client_email": order_data.get('client_email', ''),
            "client_birthdate": format_birthdate(order_data.get('client_birthdate', '')),
            "client_gender": order_data.get('client_gender', 'Male'),
            "order_address": order_data.get('order_address', ''),
            "order_payment_status": order_payment_status,
            "order_gps": order_data.get('order_gps', [21.779006345949554, 39.08578576461103]),
            "order_products": products,
            "payment_methods_with_options": payments,
            "delivery_date": order_data.get('delivery_date', ''),
            "delivery_from_time": delivery_from_time,
            "delivery_to_time": delivery_to_time,
            "shipping_address_2": order_data.get('shipping_address_2', ''),
            "fullfilment_plant": order_data.get('fullfilment_plant', '')
        }

        # Fix VAT percentage issues
        final_order_data = fix_vat_percentage_issues(final_order_data)

        # Ensure all numeric values in products have 2 decimal places
        for product in final_order_data['order_products']:
            for key, value in product.items():
                if isinstance(value, float):
                    product[key] = round(value, 2)

            # Ensure VAT percentage is in decimal format for API
            if 'vat_percentage' in product:
                vat = product['vat_percentage']
                if vat > 1:  # If it's in percentage format
                    product['vat_percentage'] = round(vat / 100, 4)

        # Ensure all numeric values in payments have 2 decimal places
        for payment in final_order_data['payment_methods_with_options']:
            for key, value in payment.items():
                if isinstance(value, float):
                    payment[key] = round(value, 2)

        return {k: v for k, v in final_order_data.items() if v is not None}

    @staticmethod
    def validate_order_data(order_data):
        """Validate order data before sending to API"""
        errors = []

        # Required fields validation
        required_fields = ['branch_code', 'order_code', 'client_phone', 'client_first_name',
                           'client_last_name', 'order_address']

        for field in required_fields:
            if not order_data.get(field):
                errors.append(f"Missing required field: {field}")

        # Products validation
        if not order_data.get('order_products'):
            errors.append("No products in the order")
        else:
            # Validate each product
            for i, product in enumerate(order_data['order_products']):
                vat_percentage = product.get('vat_percentage', 0)
                if vat_percentage > 100:
                    errors.append(
                        f"Product {i + 1}: Invalid VAT percentage {vat_percentage}. Should be between 0-100 or 0-1.0")

                if not product.get('item_code'):
                    errors.append(f"Product {i + 1}: Missing item code")

                if not product.get('item_name'):
                    errors.append(f"Product {i + 1}: Missing item name")

        # Order total validation
        if order_data.get('order_final_total_value', 0) <= 0:
            errors.append("Order total must be greater than 0")

        return errors


class ProductCalculator:
    """Product calculation utilities"""

    @staticmethod
    def calculate_product_totals(quantity, unit_price, vat_percentage, discount):
        """Calculate product totals including VAT and discounts"""
        try:
            quantity = float(quantity) if quantity else 0
            unit_price = float(unit_price) if unit_price else 0

            # Normalize VAT percentage first
            vat_percentage = normalize_vat_percentage(vat_percentage)

            # Handle VAT percentage (support both 15 and 0.15 formats)
            if vat_percentage > 1:
                vat_percentage_decimal = vat_percentage / 100
            else:
                vat_percentage_decimal = vat_percentage

            discount = float(discount) if discount else 0

            subtotal = quantity * unit_price
            vat_amount = round(subtotal * vat_percentage_decimal, 2)
            net_total = round(subtotal - discount + vat_amount, 2)
            unit_vat = round(vat_amount / quantity, 2) if quantity > 0 else 0

            return {
                'subtotal': subtotal,
                'vat_amount': vat_amount,
                'net_total': net_total,
                'unit_vat': unit_vat,
                'vat_percentage': vat_percentage,
                'vat_percentage_decimal': vat_percentage_decimal
            }
        except Exception as e:
            logger.error(f"Error calculating product totals: {str(e)}")
            return {'subtotal': 0, 'vat_amount': 0, 'net_total': 0, 'unit_vat': 0, 'vat_percentage': 15.0,
                    'vat_percentage_decimal': 0.15}


# Initialize session data before each request
@app.before_request
def before_request():
    OrderManager.initialize_session_data()


@app.route('/')
def index():
    return render_template('base.html',
                           api_urls=API_URLS,
                           cancel_api_urls=CANCEL_API_URLS,
                           payment_methods=PAYMENT_METHODS,
                           payment_statuses=PAYMENT_STATUSES,
                           payment_options=PAYMENT_OPTIONS,
                           data=session.get('order_data', DEFAULT_DATA),
                           products=session.get('products', []),
                           payments=session.get('payments', []),
                           selected_endpoint=session.get('api_endpoint', DEFAULT_API_ENDPOINT))


@app.route('/remove-product/<int:index>')
def remove_product(index):
    try:
        products = session.get('products', [])
        if 0 <= index < len(products):
            products.pop(index)
            session['products'] = products
            session['saved_products'] = products
            flash('Product removed successfully!', 'success')
        else:
            flash('Invalid product index!', 'danger')

        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error removing product: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/add-payment', methods=['POST'])
def add_payment():
    try:
        payment_method = request.form.get('payment_method')
        payment_status = request.form.get('payment_status', 'done_payment')

        # Auto-set payment status based on method
        if payment_method in ['Visa', 'Points']:
            payment_status = 'done_payment'
        elif payment_method == 'PostToCredit':
            payment_status = 'not_payment'
        else:
            payment_status = payment_status  # Use the selected status

        payment = {
            "payment_method": payment_method,
            "payment_status": payment_status,
            "payment_amount": float(request.form.get('payment_amount', 0)),
            "transaction_id": request.form.get('transaction_id'),
            "payment_option": request.form.get('payment_option'),
            "option_commission": float(request.form.get('option_commission', 0)),
        }

        # Add credit customer info only for PostToCredit
        if payment_method == 'PostToCredit':
            payment["credit_customer_info"] = {
                "customer_number": request.form.get('customer_number', ''),
                "customer_name": request.form.get('customer_name', '')
            }
        else:
            payment["credit_customer_info"] = None

        payments = session.get('payments', [])
        payments.append(payment)
        session['payments'] = payments
        session['saved_payments'] = payments

        flash('Payment method added successfully!', 'success')
        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error adding payment method: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/remove-payment/<int:index>')
def remove_payment(index):
    try:
        payments = session.get('payments', [])
        if 0 <= index < len(payments):
            payments.pop(index)
            session['payments'] = payments
            session['saved_payments'] = payments
            flash('Payment method removed successfully!', 'success')
        else:
            flash('Invalid payment method index!', 'danger')

        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error removing payment method: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/update-order', methods=['POST'])
def update_order():
    try:
        order_data = session.get('order_data', DEFAULT_DATA.copy())
        payments = session.get('payments', [])

        # Determine payment status based on payment methods
        order_payment_status = determine_payment_status(payments)

        # Update order information
        order_data.update({
            'branch_code': request.form.get('branch_code', order_data['branch_code']),
            'order_code': request.form.get('order_code', order_data['order_code']),
            'parent_order_code': request.form.get('parent_order_code', order_data.get('parent_order_code', '')),
            'order_delivery_cost': float(request.form.get('delivery_cost', order_data['order_delivery_cost'])),
            'is_delivery': int(request.form.get('is_delivery', order_data['is_delivery'])),
            'order_status': request.form.get('order_status', order_data['order_status']),
            'order_payment_status': order_payment_status,
            'delivery_date': request.form.get('delivery_date', order_data['delivery_date']),
            'delivery_from_time': request.form.get('delivery_from_time', order_data['delivery_from_time']),
            'delivery_to_time': request.form.get('delivery_to_time', order_data['delivery_to_time']),
            'shipping_address_2': request.form.get('shipping_address_2', order_data['shipping_address_2']),
            'fullfilment_plant': request.form.get('fulfillment_plant', order_data['fullfilment_plant']),
            'order_notes': request.form.get('order_notes', order_data['order_notes']),
            'client_first_name': request.form.get('first_name', order_data['client_first_name']),
            'client_middle_name': request.form.get('middle_name', order_data['client_middle_name']),
            'client_last_name': request.form.get('last_name', order_data['client_last_name']),
            'client_phone': request.form.get('phone', order_data['client_phone']),
            'client_email': request.form.get('email', order_data['client_email']),
            'order_address': request.form.get('address', order_data['order_address']),
            'client_birthdate': request.form.get('birthdate', order_data['client_birthdate']),
            'client_gender': request.form.get('gender', order_data['client_gender']),
            'client_country_code': request.form.get('country_code', order_data['client_country_code'])
        })

        session['order_data'] = order_data
        session['saved_order_data'] = order_data

        flash('Order details updated successfully!', 'success')
        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error updating order: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/calculate-totals')
def calculate_totals():
    try:
        order_data = OrderManager.prepare_order_data()

        return jsonify({
            'products_total': order_data['order_product_total_value'],
            'order_discount': order_data['order_total_discount'],
            'delivery_cost': order_data['order_delivery_cost'],
            'final_total': order_data['order_final_total_value']
        })

    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/export-json')
def export_json():
    try:
        order_data = OrderManager.prepare_order_data()
        return jsonify(order_data)

    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/load-default')
def load_default():
    try:
        default_data = DEFAULT_DATA.copy()

        current_date = datetime.now().strftime("%Y-%m-%d")
        current_time = datetime.now().strftime("%H:%M:%S")
        one_hour_later = (datetime.now() + timedelta(hours=1)).strftime("%H:%M:%S")

        default_data.update({
            'delivery_date': current_date,
            'delivery_from_time': current_time,
            'delivery_to_time': one_hour_later
        })

        session.update({
            'order_data': default_data,
            'products': default_data['order_products'].copy(),
            'payments': default_data['payment_methods_with_options'].copy()
        })

        # Clear saved data
        session.pop('saved_order_data', None)
        session.pop('saved_products', None)
        session.pop('saved_payments', None)

        flash('Default data loaded successfully!', 'success')
        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error loading default data: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/clear-all')
def clear_all():
    try:
        current_date = datetime.now().strftime("%Y-%m-%d")
        current_time = datetime.now().strftime("%H:%M:%S")
        one_hour_later = (datetime.now() + timedelta(hours=1)).strftime("%H:%M:%S")

        session.update({
            'order_data': {
                "branch_code": "",
                "order_code": "",
                "parent_order_code": "",
                "order_delivery_cost": 0,
                "is_delivery": 1,
                "order_status": "new",
                "order_payment_status": "not_payment",
                "delivery_date": current_date,
                "delivery_from_time": current_time,
                "delivery_to_time": one_hour_later,
                "shipping_address_2": "",
                "fullfilment_plant": "",
                "order_notes": "",
                "client_first_name": "",
                "client_middle_name": "",
                "client_last_name": "",
                "client_phone": "",
                "client_email": "",
                "client_birthdate": "1989-04-11T12:00:00.000Z",
                "client_gender": "Male",
                "client_country_code": "966",
                "order_address": ""
            },
            'products': [],
            'payments': []
        })

        # Clear saved data
        session.pop('saved_order_data', None)
        session.pop('saved_products', None)
        session.pop('saved_payments', None)

        flash('All data cleared successfully!', 'success')
        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error clearing data: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/send-request', methods=['POST'])
def send_request():
    try:
        selected_endpoint = request.form.get('api_endpoint')
        custom_url = request.form.get('custom_url', '').strip()

        # Use custom URL if provided, otherwise use the selected endpoint
        if custom_url:
            url = custom_url
        elif selected_endpoint in API_URLS:
            url = API_URLS[selected_endpoint]
        else:
            flash('Please select a valid API endpoint or provide a custom URL', 'danger')
            return redirect(url_for('index'))

        # Save the selected endpoint
        session['api_endpoint'] = selected_endpoint

        # Create the JSON data to send
        order_data = OrderManager.prepare_order_data()

        # Debug: Print the final data being sent
        print("=== FINAL ORDER DATA BEING SENT ===")
        print(json.dumps(order_data, indent=2, ensure_ascii=False))
        print("===================================")

        # Validate the data before sending
        validation_errors = OrderManager.validate_order_data(order_data)
        if validation_errors:
            flash('Validation errors found:', 'danger')
            for error in validation_errors:
                flash(error, 'danger')
            return redirect(url_for('index'))

        # Send POST request
        headers = {'Content-Type': 'application/json'}
        response = requests.post(url, json=order_data, headers=headers, timeout=30)

        # Prepare response data
        response_data = {
            'status_code': response.status_code,
            'response_text': response.text,
            'url_sent': url
        }

        # Handle response
        if response.status_code == 200:
            save_json_file(order_data)
            flash('Request sent successfully! Order created.', 'success')
        elif response.status_code == 400:
            try:
                error_data = response.json()
                error_message = "Validation Error (400): "
                if 'errors' in error_data:
                    for field, errors in error_data['errors'].items():
                        error_message += f"{field}: {', '.join(errors)}. "
                elif 'title' in error_data:
                    error_message += error_data['title']
                flash(error_message, 'warning')
            except:
                flash(f'Validation Error (400): {response.text}', 'warning')
        else:
            flash(f'Server returned status code: {response.status_code}', 'warning')

        return render_template('base.html',
                               api_urls=API_URLS,
                               cancel_api_urls=CANCEL_API_URLS,
                               payment_methods=PAYMENT_METHODS,
                               payment_statuses=PAYMENT_STATUSES,
                               payment_options=PAYMENT_OPTIONS,
                               data=session.get('order_data', DEFAULT_DATA),
                               products=session.get('products', []),
                               payments=session.get('payments', []),
                               selected_endpoint=selected_endpoint,
                               response=response_data)

    except requests.exceptions.RequestException as e:
        flash(f'Request Error: {str(e)}', 'danger')
        return redirect(url_for('index'))
    except Exception as e:
        flash(f'An error occurred: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/get-item-details', methods=['GET'])
def get_item_details():
    try:
        material_number = request.args.get('material_number', type=str)
        customer_number = request.args.get('customer_number', '', type=str)
        sap_tax_code = request.args.get('sap_tax_code', '', type=str)
        sap_mat_generic = request.args.get('sap_mat_generic', '', type=str)

        if not material_number or not material_number.isdigit() or len(material_number) != 6:
            return jsonify({'error': 'Material number must be 6 digits'}), 400

        conn = DatabaseManager.get_db_connection()
        if not conn:
            return jsonify({'error': 'Database connection failed'}), 500

        cursor = conn.cursor()

        query = """
        SELECT TOP 1 
            I.MaterialNumber,
            IUOMB.UniversalBarCode,
            I.Name AS EnglishName,
            I.NativeName AS ArabicName,
            IP.Price AS UnitPrice,
            TT.Rate AS VatRate,
            CAST(ROUND(((IP.Price * TT.Rate)/100) + IP.Price, 2) AS DECIMAL(10,2)) AS NetPrice
            FROM dbo.Items AS I
            LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
            INNER JOIN dbo.ItemUnitOfMeasures AS IUM ON I.Id = IUM.ItemId
            INNER JOIN dbo.ItemUnitOfMeasureBarCodes AS IUOMB ON IUM.Id = IUOMB.ItemUnitOfMeasureId
            LEFT JOIN dbo.ItemPrices AS IP ON IUM.Id = IP.ItemUnitOfMeasureId
            WHERE RIGHT(I.MaterialNumber, 6) = ?
            AND IP.IsActive = 1
            AND IP.Price IS NOT NULL
            AND IP.ToDate > GETDATE()
        """

        params = [material_number]

        # Add optional filters
        if customer_number:
            query += " AND EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerNumber = ? AND IsActive = 1)"
            params.append(customer_number)

        if sap_tax_code:
            query += " AND I.SapTaxCode = ?"
            params.append(sap_tax_code)

        if sap_mat_generic:
            query += " AND I.SapMatGeneric = ?"
            params.append(sap_mat_generic)

        query += " ORDER BY I.Id DESC"

        cursor.execute(query, params)
        row = cursor.fetchone()

        if not row:
            return jsonify({'error': 'No item found with the specified criteria'}), 404

        item_details = {
            'item_code': row[0] if row[0] else f"000000000000{material_number}",
            'item_Barcode': row[1] if row[1] else f"BC{material_number}",
            'item_EN_Name': row[2] if row[2] else f"Item {material_number}",
            'item_AR_Name': row[3] if row[3] else f"صنف {material_number}",
            'unit_price': float(row[4]) if row[4] else 0.0,
            'vat_percentage': float(row[5]) if row[5] else 0.0,
            'net_price': float(row[6]) if row[6] else 0.0
        }

        conn.close()
        return jsonify(item_details)

    except Exception as e:
        return jsonify({'error': f'Database error: {str(e)}'}), 500


@app.route('/get-product/<int:index>')
def get_product(index):
    """Get product data for editing"""
    try:
        products = session.get('products', [])
        if 0 <= index < len(products):
            product = products[index].copy()
            # Convert decimal VAT back to percentage for display and normalize
            current_vat = product.get('vat_percentage', 0.15)

            # Normalize the VAT percentage for display
            if current_vat > 1 and current_vat < 100:
                # Already in percentage format, use as is
                product['vat_percentage'] = current_vat
            elif current_vat >= 100:
                # Normalize extremely large values
                product['vat_percentage'] = normalize_vat_percentage(current_vat)
            else:
                # Convert decimal to percentage
                product['vat_percentage'] = current_vat * 100

            product['index'] = index
            return jsonify(product)
        else:
            return jsonify({'error': 'Invalid product index'}), 404
    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/update-product/<int:index>', methods=['POST'])
def update_product(index):
    try:
        products = session.get('products', [])

        if 0 <= index < len(products):
            # Normalize VAT percentage input
            vat_input = request.form.get('vat_percentage', '15')
            vat_percentage = normalize_vat_percentage(vat_input)

            calculations = ProductCalculator.calculate_product_totals(
                request.form.get('quantity'),
                request.form.get('unit_price'),
                vat_percentage,
                request.form.get('discount')
            )

            product = {
                "item_code": request.form.get('item_code'),
                "item_name": request.form.get('item_name'),
                "quantity": float(request.form.get('quantity', 0)),
                "unit_price": float(request.form.get('unit_price', 0)),
                "vat_percentage": calculations['vat_percentage'],
                "row_total_discount": float(request.form.get('discount', 0)),
                "total_vat_amount": calculations['vat_amount'],
                "row_net_total": calculations['net_total'],
                "unit_vat_amount": calculations['unit_vat'],
                "offer_code": request.form.get('offer_code', ''),
                "offer_message": request.form.get('offer_message', '')
            }

            products[index] = product
            session['products'] = products
            session['saved_products'] = products
            flash('Product updated successfully!', 'success')
        else:
            flash('Invalid product index!', 'danger')

        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error updating product: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/update-payment/<int:index>', methods=['POST'])
def update_payment(index):
    try:
        payments = session.get('payments', [])

        if 0 <= index < len(payments):
            payment_method = request.form.get('payment_method')
            payment_status = request.form.get('payment_status', 'done_payment')

            # Auto-set payment status based on method
            if payment_method in ['Visa', 'Points']:
                payment_status = 'done_payment'
            elif payment_method == 'PostToCredit':
                payment_status = 'not_payment'

            payment = {
                "payment_method": payment_method,
                "payment_status": payment_status,
                "payment_amount": float(request.form.get('payment_amount', 0)),
                "transaction_id": request.form.get('transaction_id'),
                "payment_option": request.form.get('payment_option'),
                "option_commission": float(request.form.get('option_commission', 0)),
            }

            # Add credit customer info only for PostToCredit
            if payment_method == 'PostToCredit':
                payment["credit_customer_info"] = {
                    "customer_number": request.form.get('customer_number', ''),
                    "customer_name": request.form.get('customer_name', '')
                }
            else:
                # Preserve existing credit customer info if any
                existing_payment = payments[index]
                if existing_payment.get('credit_customer_info'):
                    payment["credit_customer_info"] = existing_payment['credit_customer_info']
                else:
                    payment["credit_customer_info"] = None

            payments[index] = payment
            session['payments'] = payments
            session['saved_payments'] = payments
            flash('Payment method updated successfully!', 'success')
        else:
            flash('Invalid payment index!', 'danger')

        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error updating payment method: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/test-single-endpoint', methods=['POST'])
def test_single_endpoint():
    try:
        data = request.get_json()
        url = data['url']
        name = data['name']

        parsed = urlparse(url)
        host = parsed.hostname
        port = parsed.port or (80 if parsed.scheme == 'http' else 443)

        # Test connection
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(3)
        result = sock.connect_ex((host, port))
        sock.close()

        return jsonify({
            'status': 'Online' if result == 0 else 'Offline',
            'name': name,
            'url': url
        })

    except Exception as e:
        return jsonify({'status': 'Error', 'error': str(e)}), 500


@app.route('/test-database-connection', methods=['POST'])
def test_database_connection():
    try:
        if request.is_json:
            data = request.get_json()
            server = data.get('server', '.')
            database = data.get('database', 'RMSCashierSrv')
            username = data.get('username', 'sa')
            password = data.get('password', 'P@ssw0rd')
        else:
            server = request.form.get('server', '.')
            database = request.form.get('database', 'RMSCashierSrv')
            username = request.form.get('username', 'sa')
            password = request.form.get('password', 'P@ssw0rd')

        # Test connection
        conn_str = f'DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={server};DATABASE={database};UID={username};PWD={password};Trusted_Connection=no;Encrypt=no;'
        conn = pyodbc.connect(conn_str)
        conn.close()

        return jsonify({
            'success': True,
            'message': 'Database connection successful!'
        })

    except Exception as e:
        return jsonify({
            'success': False,
            'message': f'Database connection failed: {str(e)}'
        })


@app.route('/add-product-from-db', methods=['POST'])
def add_product_from_db():
    try:
        data = request.get_json()

        # Convert VAT percentage if needed
        vat_percentage = float(data['vat_percentage'])
        if vat_percentage > 1:
            vat_percentage = vat_percentage / 100

        calculations = ProductCalculator.calculate_product_totals(1, data['unit_price'], vat_percentage, 0)

        product = {
            "item_code": data['item_code'],
            "item_name": data['item_EN_Name'],
            "quantity": 1.0,
            "unit_price": float(data['unit_price']),
            "vat_percentage": vat_percentage * 100,  # Store as percentage for display
            "row_total_discount": 0.0,
            "total_vat_amount": calculations['vat_amount'],
            "row_net_total": calculations['net_total'],
            "unit_vat_amount": calculations['unit_vat'],
            "offer_code": "",
            "offer_message": ""
        }

        products = session.get('products', [])
        products.append(product)
        session['products'] = products
        session['saved_products'] = products

        return jsonify({'success': True, 'message': 'Product added successfully!'})

    except Exception as e:
        return jsonify({'success': False, 'error': str(e)}), 500


@app.route('/add-product', methods=['POST'])
def add_product():
    try:
        quantity = float(request.form.get('quantity', 0))
        unit_price = float(request.form.get('unit_price', 0))

        # Normalize VAT percentage input
        vat_input = request.form.get('vat_percentage', '15')
        vat_percentage = normalize_vat_percentage(vat_input)

        calculations = ProductCalculator.calculate_product_totals(quantity, unit_price, vat_percentage,
                                                                  request.form.get('discount', 0))

        product = {
            "item_code": request.form.get('item_code'),
            "item_name": request.form.get('item_name'),
            "quantity": quantity,
            "unit_price": unit_price,
            "vat_percentage": calculations['vat_percentage'],
            "row_total_discount": float(request.form.get('discount', 0)),
            "total_vat_amount": calculations['vat_amount'],
            "row_net_total": calculations['net_total'],
            "unit_vat_amount": calculations['unit_vat'],
            "offer_code": request.form.get('offer_code', ''),
            "offer_message": request.form.get('offer_message', '')
        }

        products = session.get('products', [])
        products.append(product)
        session['products'] = products
        session['saved_products'] = products

        flash('Product added successfully!', 'success')
        return redirect(url_for('index'))

    except Exception as e:
        flash(f'Error adding product: {str(e)}', 'danger')
        return redirect(url_for('index'))


@app.route('/cancel-order', methods=['POST'])
def cancel_order():
    try:
        selected_endpoint = request.form.get('cancel_api_endpoint')
        custom_url = request.form.get('cancel_custom_url', '').strip()
        order_number = request.form.get('order_number')
        reason = request.form.get('reason')

        if custom_url:
            url = custom_url
        elif selected_endpoint in CANCEL_API_URLS:
            url = CANCEL_API_URLS[selected_endpoint]
        else:
            flash('Please select a valid API endpoint or provide a custom URL', 'danger')
            return redirect(url_for('index'))

        cancel_data = {
            "OrderNumber": order_number,
            "Reason": reason,
            "orders_status": "cancelled"
        }

        headers = {'Content-Type': 'application/json'}
        response = requests.post(url, json=cancel_data, headers=headers, timeout=30)

        response_data = {
            'status_code': response.status_code,
            'response_text': response.text,
            'url_sent': url
        }

        if response.status_code == 200:
            flash('Order cancelled successfully!', 'success')
        else:
            flash(f'Error cancelling order. Status code: {response.status_code}', 'danger')

        return render_template('base.html',
                               api_urls=API_URLS,
                               cancel_api_urls=CANCEL_API_URLS,
                               payment_methods=PAYMENT_METHODS,
                               payment_statuses=PAYMENT_STATUSES,
                               payment_options=PAYMENT_OPTIONS,
                               data=session.get('order_data', DEFAULT_DATA),
                               products=session.get('products', []),
                               payments=session.get('payments', []),
                               selected_endpoint=session.get('api_endpoint', DEFAULT_API_ENDPOINT),
                               cancel_response=response_data)

    except requests.exceptions.RequestException as e:
        flash(f'Request Error: {str(e)}', 'danger')
        return redirect(url_for('index'))
    except Exception as e:
        flash(f'An error occurred: {str(e)}', 'danger')
        return redirect(url_for('index'))


def save_json_file(order_data):
    """Save order data to JSON file"""
    try:
        if not os.path.exists('JSON files'):
            os.makedirs('JSON files')

        client_name = order_data.get('client_first_name', 'unknown') + '_' + order_data.get('client_last_name',
                                                                                            'client')
        payment_method = order_data.get('order_payment_method', 'unknown')
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = f"{client_name}_{payment_method}_{timestamp}.json"

        filepath = os.path.join('JSON files', filename)
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(order_data, f, indent=2, ensure_ascii=False)

        return True
    except Exception as e:
        logger.error(f"Error saving JSON file: {str(e)}")
        return False


@app.context_processor
def inject_global_variables():
    return dict(
        api_urls=API_URLS,
        cancel_api_urls=CANCEL_API_URLS,
        payment_methods=PAYMENT_METHODS,
        payment_statuses=PAYMENT_STATUSES,
        payment_options=PAYMENT_OPTIONS
    )


@app.context_processor
def inject_session_data():
    return dict(
        data=session.get('order_data', DEFAULT_DATA),
        products=session.get('products', []),
        payments=session.get('payments', [])
    )


if __name__ == '__main__':
    app.run(debug=True, port=5002)