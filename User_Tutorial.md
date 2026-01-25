Creating Your First Order
1. Order Information
Branch Code: Enter your branch code (e.g., "2000")

Order Code: Automatically generated, but you can customize it

Delivery Options:

Set delivery cost if applicable

Choose delivery date and time windows

Add order notes for special instructions

2. Client Information
Fill in customer details: name, phone, email, and address

Include birthdate and gender for customer profiling

Country code defaults to Saudi Arabia (+966)

3. Adding Products
Method 1: Manual Entry

Click "Add Product" button

Fill in product details:

Item Code and Name

Quantity and Unit Price

VAT Percentage (defaults to 15%)

Discounts and offers if applicable

Click "Add Product" to save

Method 2: Database Lookup

Go to "Database Connection" tab

Enter 6-digit material number

Click "Search Item" to retrieve product details

Click "Add to Order" to include the product

4. Setting Up Payments
Click "Add Payment" button

Select payment method from available options:

Cash, Credit Card, Points, Tamara, Tabby, etc.

Set payment status:

not_payment: Payment not completed

done_payment: Payment completed

partially_paid: Partial payment received

Enter payment amount and transaction details

For credit payments, fill in customer information

5. Calculating Totals
Click "Calculate Totals" button to update all financial calculations

View real-time updates in the Quick Stats section

Monitor total paid vs. remaining amount

6. Sending Orders
Go to "API Configuration" tab

Select your API endpoint or enter custom URL

Choose to validate data before sending (recommended)

Click "Send Request" to submit the order

Data Persistence
The application automatically saves your last order

When you start a new order, it loads data from your previous order

Order codes and timestamps are automatically updated for new orders

Use "Load Default" to reset to template data

Use "Clear All" to start completely fresh

Database Integration
Product Lookup
Navigate to "Database Connection" tab

Enter 6-digit material number

Optional: Add customer number or tax code for filtered results

System retrieves: item code, barcode, names, pricing, and VAT information

Testing Connections
Use "Test Connection" to verify database connectivity

Test API endpoints in the "Test Endpoints" tab

Theme Customization
Click the theme toggle button (bottom-right) for light/dark mode

Use the color palette to change accent colors

Preferences are saved automatically

🔧 Troubleshooting
Common Issues
Database Connection Problems
ODBC Driver Issues: Install latest ODBC drivers for SQL Server

Authentication Failed: Verify username and password in configuration

Connection Timeout: Check server address and network connectivity

API Connection Issues
Endpoint Offline: Verify API URLs in config.py

SSL Certificate Errors: Ensure certificates are valid for HTTPS endpoints

CORS Issues: Check if APIs accept requests from your domain

Calculation Issues
Totals Not Updating: Click "Calculate Totals" button

Delivery Cost Not Included: Ensure delivery cost field has a value > 0

VAT Calculation Errors: Check VAT percentage format (use 15 for 15%)

Quick Fixes
Refresh Data: Use "Load Default" then "Calculate Totals"

Clear Cache: Use "Clear All" to start fresh

Check Console: Browser developer tools show JavaScript errors

Server Logs: Check Flask application logs for backend errors

📊 Payment Methods Supported
Method	Options	Typical Use
Cash	cash	In-person payments
Credit Card	visa, mastercard, mada, amex	Online payments
Points	Points	Loyalty program
Digital Wallets	Tamara, Tabby, MisPay, Emkan	Installment payments
Gift Cards	YouGotaGift, OgMoney	Prepaid payments
Credit	PostToCredit	Customer credit accounts