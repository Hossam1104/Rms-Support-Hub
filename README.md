# Online Order Tool

A comprehensive web application for creating, managing, and exporting pharmacy orders with integration to various APIs and database systems.

## 🚀 Features

- **Order Management**: Create and manage orders with detailed product and customer information
- **Payment Processing**: Support for multiple payment methods including cash, credit cards, and digital wallets
- **API Integration**: Connect to multiple API endpoints for order processing
- **Database Integration**: Query product information from SQL Server database
- **JSON Export**: Export orders in standardized JSON format
- **Theme Customization**: Light/dark mode with color theme options
- **Responsive Design**: Works on desktop and mobile devices

## 💳 Payment Logic & Validation

## Payment Logic & Validation

### Payment Method Rules
- **COD (Cash on Delivery)**: Must have `not_payment` status. Customer name and number are optional.
- **Visa**: Must have `done_payment` status with amount equal to order total. Customer name and number are optional.
- **Digital Wallets** (Tamara, Tabby, MisPay, Emkan, etc.): Must have `done_payment` status with amount equal to order total.
- **Points** (RajhiPoints, QitafPoints, NeqatyPoints): Must have `done_payment` status with amount equal to order total.
- **PostToCredit**: Must have `not_payment` status. Customer name and number are optional.

### Customer Information
For **COD**, **Visa**, and **PostToCredit** payment methods:
- Customer Name and Customer Number fields appear automatically
- These fields are **optional** and can be left blank
- Data is stored in the `credit_customer_info` object within the payment payload if provided

### UI Validation
- Real-time validation prevents invalid payment combinations
- Submit button is disabled until all validation rules pass
- Error messages guide users to correct input

### Auto-Selection
- Payment options with only one choice (e.g., COD, Tamara) are automatically selected

## 📋 Prerequisites

- Python 3.8 or higher
- SQL Server with RMSCashierSrv database
- ODBC drivers for SQL Server

## 🛠️ Installation

### Step 1: Clone or Download the Project
```bash
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
Step 4: Database Configuration
Ensure you have SQL Server installed with the RMSCashierSrv database. Update the database connection settings in config.py if needed:

python
DB_CONFIG = {
    "server": os.environ.get("DB_SERVER", "."),  # Use localhost or server IP
    "database": os.environ.get("DB_DATABASE", "RMSCashierSrv"),
    "username": os.environ.get("DB_USERNAME", "sa"),
    "password": os.environ.get("DB_PASSWORD", "P@ssw0rd"),
    "driver": os.environ.get("DB_DRIVER", "ODBC Driver 17 for SQL Server"),
}
Step 5: Run the Application
bash
python app.py
The application will be available at http://localhost:5002|

 Project Structure
text
online-order-tool/
├── app.py                 # Main Flask application
├── config.py             # Configuration settings
├── requirements.txt      # Python dependencies
├── last_order.json      # Auto-saved last order data
├── index.html           # Main template (previously templates/base.html)
├── style.css            # Custom styles (previously static/css/style.css)
├── script.js            # Custom JS (previously static/js/script.js)
└── README.md           # This file
🆘 Support
For issues and questions:

Check the troubleshooting section above

Verify all dependencies are installed

Ensure database and API endpoints are accessible

Check application logs for detailed error messages

📝 License
This project is for internal use. Please consult your organization's licensing policies.