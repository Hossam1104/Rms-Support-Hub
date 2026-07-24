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

Method 2: Database Lookup (GHC)

Go to "Database Connection" tab

Enter 6-digit material number

Click "Search Item" to retrieve product details

Click "Add to Order" to include the product

Method 2: Database Lookup (UPC)

UPC's item pricing is branch-specific, so the lookup lives inside the "Add Product" dialog itself instead of a separate tab

Fill in the Branch Code in Order Information first — the search uses it

Click "Add Product", then in the "Find Item" box at the top enter either the 6-digit item number or the full 18-digit material number

Click "Find" (or press Enter) — Item Code, Item Name, Unit Price, and VAT % fill in automatically

Set Quantity (and Discount/Offer if applicable), then click "Add Product" to save

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

If you're on UPC, an inline status box appears under the response as soon as the order lands in the database — it shows the order's current status and, if already invoiced, the invoice barcode. Click "View in Order Validation" in that box to jump straight to the order in the Order Validation tab.

7. Order Validation (UPC only)
This tab only appears for UPC E-Commerce. It reads live from the database — separate from the order you're currently building — so you can look up and manage any order that was ever sent, not just the one on your screen.

Searching for Orders

Go to the "Order Validation" tab

Fill in any combination of: Order Number, Client Phone, Branch Code, Status, Date From, Date To — you don't need to fill them all in, only the ones you want to filter by

Click "Search"

Matching orders appear in the results grid, one row per order. If the same order was resent to a different branch, each attempt shows as its own row

Reading the Results Grid

Status is shown as a colored badge with its code and name (e.g. "1 - New", "9 - Done") — see the status list below

Invoice Barcode and Invoice Date are filled in once the order has actually been invoiced; they're blank otherwise

The "Creation vs Invoice" column compares when the order was placed against when it was invoiced, so you can spot orders that took unusually long

Order Status Meanings

1 - New

2 - Confirmed (pharmacist confirmed the order)

3 - Ready (ready to be executed)

4 - With Delegate (executed and invoiced, out for delivery)

5 - Rejected (rejected by the pharmacy)

6 - Canceled By Client

7 - Canceled By Admin

8 - Processing (in the POS cart)

9 - Done (executed and invoiced, picked up in store)

Viewing Order Details

Click the eye icon on any row to open its full details: header info, every line item, and every payment transaction

Resending an Order to a Different Branch

A "resend" button (circular arrow icon) appears on a row, or as a button inside the Details view, only when the order's status allows it — orders that are already With Delegate, Processing, or Done cannot be resent

Click it, enter the new branch code, and click "Resend"

The app resends the order exactly as it was originally sent, only with the new branch code — it does not touch whatever order you currently have open on the Order Dashboard tab

The result appears in the same API Response area as a normal send, and the grid refreshes to show the new branch attempt

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