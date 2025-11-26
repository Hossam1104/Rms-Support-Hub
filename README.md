# Online Order Tool
A comprehensive web application for creating, managing, and exporting online orders with integration to various APIs and database systems.

Features
Order Management: Create and manage orders with detailed product and customer information

Payment Processing: Support for multiple payment methods including cash, credit cards, and PostToCredit

API Integration: Connect to multiple API endpoints for order processing

Database Integration: Query product information from SQL Server database

JSON Export: Export orders in standardized JSON format

Theme Customization: Light/dark mode with color theme options

Responsive Design: Works on desktop and mobile devices

Installation
Prerequisites
Python 3.8 or higher

SQL Server with RMSCashierSrv database

ODBC drivers for SQL Server

Step 1: Clone or Download the Project
bash
git clone <your-repository-url>
cd online-order-tool
Step 2: Create Virtual Environment (Recommended)
bash

# On Windows

python -m venv venv
venv\Scripts\activate

# On macOS/Linux

python3 -m venv venv
source venv/bin/activate
Step 3: Install Dependencies
bash
pip install -r requirements.txt
If requirements.txt doesn't exist, install the dependencies manually:

bash
pip install flask pyodbc requests
Step 4: Database Configuration
Ensure you have SQL Server installed with the RMSCashierSrv database. Update the database connection settings in config.py if needed:

python

# Database connection settings

DB_SERVER = '.' # Use localhost or server IP if not local
DB_DATABASE = 'RMSCashierSrv'
DB_USERNAME = 'sa'
DB_PASSWORD = 'P@ssw0rd'
Step 5: Configure API Endpoints
Edit the config.py file to set your API endpoints:

python
API_URLS = {
"Whites Pharmacy - Testing": "https://whitespharmacy.com/api/orders",
"Whites Pharmacy - Production": "https://api.whitespharmacy.com/orders",
"Local Development": "http://localhost:5000/api/orders"
}

CANCEL_API_URLS = {
"Whites Pharmacy - Testing": "https://whitespharmacy.com/api/Order/CancelOrder",
"Whites Pharmacy - Production": "https://api.whitespharmacy.com/Order/CancelOrder",
"Local Development": "http://localhost:5000/api/Order/CancelOrder"
}
Step 6: Run the Application
bash
python app.py
The application will be available at http://localhost:5002

Usage
Creating an Order
Order Information: Fill in branch code, order code, delivery details

Client Information: Add customer details including name, phone, and address

Products: Add products manually or search from database

Payments: Configure payment methods and amounts

Export/Send: Export as JSON or send directly to API

Database Lookup
Use the Database Connection tab to search for products by material number. The system will retrieve:

Item code and barcode

English and Arabic names

Unit price and VAT information

Net price calculations

API Integration
Configure multiple API endpoints for:

Order creation

Order cancellation

Testing connectivity

Project Structure
text
online-order-tool/
├── app.py # Main Flask application
├── config.py # Configuration settings
├── requirements.txt # Python dependencies
├── static/
│ └── css/
│ └── style.css # Custom styles
├── templates/
│ └── base.html # Main template
└── JSON files/ # Exported JSON orders (auto-created)
API Response Format
The application exports orders in the following JSON format:

json
{
"branch_code": "2025",
"order_code": "ORDER-001",
"parent_order_code": "",
"order_creation_date": "2025-09-10T12:23:10.323Z",
"order_notes": "Don't Ring the Bell",
"order_product_total_value": 136.7,
"is_delivery": 1,
"order_delivery_cost": 10.0,
"order_total_discount": 4.57,
"order_final_total_value": 146.7,
"order_payment_method": "done_payment",
"order_status": "new",
"client_country_code": "966",
"client_phone": "551122112",
"client_first_name": "John",
"client_middle_name": "Michael",
"client_last_name": "Doe",
"client_email": "john.doe@example.com",
"client_birthdate": "1991-10-10T12:23:10.323Z",
"client_gender": "Male",
"order_address": "123 Main Street",
"address_code": "11517",
"order_country_code": null,
"order_phone": null,
"order_payment_status": "not_payment",
"order_gps": [21.779006345949554, 39.08578576461103],
"order_products": [...],
"payment_methods_with_options": [...],
"delivery_date": "2025-09-01",
"delivery_from_time": "12:23:10.323",
"delivery_to_time": "03:23:10",
"shipping_address_2": "City Name",
"fullfilment_plant": "1000"
}
Payment Methods Supported
Cash

Credit Card (Visa, MasterCard, Amex)

Debit Card

Bank Transfer

Digital Wallets (Apple Pay, Google Pay, Samsung Pay)

PostToCredit (with customer information)

Points

Tamara

Tabby

MisPay

Emkan

YouGotaGift

OgMoney

Troubleshooting
Database Connection Issues
ODBC Driver Problems:

Install the latest ODBC drivers for SQL Server

Check available drivers with the /check-drivers endpoint

Authentication Issues:

Verify SQL Server authentication mode is enabled

Check username and password in configuration

Connection String:

The application tries multiple drivers automatically

Add Trusted_Connection=no;Encrypt=no; to connection string if needed

API Connection Issues
Test Endpoints: Use the Test Endpoints tab to verify API connectivity

CORS Issues: Ensure APIs accept requests from your domain

SSL Certificates: For HTTPS endpoints, ensure certificates are valid

Theme Issues
Light/Dark Mode: Click the theme toggle button in the bottom-right corner

Color Themes: Use the palette button to change accent colors

Development
Adding New Features
New Payment Methods: Add to PAYMENT_METHODS and PAYMENT_OPTIONS in config.py

API Endpoints: Update the API_URLS dictionary in config.py

Database Fields: Modify the get_item_details function in app.py

Customization
Modify style.css for visual changes

Update base.html for layout changes

Extend app.py for additional functionality

Support
For issues and questions:

Check the troubleshooting section above

Verify all dependencies are installed

Ensure database and API endpoints are accessible

License
This project is for internal use. Please consult your organization's licensing policies.

Changelog
Version 1.0
Initial release with order management

Database integration

API connectivity

JSON export functionality

Theme customization


[//]: # (# Online Order Tool)

[//]: # ()

[//]: # (A comprehensive Flask web application for creating, managing, and submitting pharmacy orders to multiple backend systems. The tool features fast)

[//]: # (product lookup from SQL Server databases, flexible multi-payment handling, and JSON export for traceability.)

[//]: # ()

[//]: # (## Features)

[//]: # ()

[//]: # (- **Order Management**: Create and manage pharmacy orders with detailed product information)

[//]: # (- **Database Integration**: Direct connection to SQL Server for real-time product lookup)

[//]: # (- **Multi-payment Support**: Handle various payment methods including Visa, Points, Tamara, Tabby, and more)

[//]: # (- **API Integration**: Send orders to multiple pharmacy backend systems)

[//]: # (- **Order Cancellation**: Cancel orders with proper reason tracking)

[//]: # (- **Theme Customization**: Light/dark mode with multiple color themes)

[//]: # (- **JSON Export**: Export order data for documentation and traceability)

[//]: # ()

[//]: # (## Installation)

[//]: # ()

[//]: # (1. **Clone the repository**)

[//]: # (   ```bash)

[//]: # (   git clone <repository-url>)

[//]: # (   cd Online_Order_Tool)

[//]: # ()

[//]: # (2. **Create a virtual environment**)

[//]: # (    ```bash)

[//]: # (    python -m venv venv)

[//]: # (    source venv/bin/activate # On Windows: venv\Scripts\activate)

[//]: # (   )

[//]: # (3. **Install dependencies**)

[//]: # (    ```bash)

[//]: # (    pip install -r requirements.txt)

[//]: # (    Configure database connection)

[//]: # ()

[//]: # (# Notes:)

[//]: # (#### 1- Update the database connection settings in config.py if needed)

[//]: # (#### 2- The default uses Windows Authentication with SQL Server)

[//]: # (#### 3- Run the application)

[//]: # (#### 4- Open your browser and navigate to http://localhost:5002)