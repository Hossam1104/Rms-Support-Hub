using System.Text.Json;
using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Services;
using Xunit;

namespace OnlineOrderTool.Tests;

public class PayloadAndValidationTests
{
    private readonly FlatOrderPayloadBuilder _flatBuilder = new();
    private readonly UniCommercePayloadBuilder _uniBuilder = new();
    private readonly FlatOrderValidator _flatValidator = new();
    private readonly UniCommerceValidator _uniValidator = new();

    // NOTE: FlatOrderValidator.cs still validates against the pre-R1 invented
    // field names (client_name, client_code, client_mobile, shipping_address)
    // and a flattened customer_name/customer_number pair instead of the new
    // nested credit_customer_info -- see remediation_plan.md B5. It is R2's
    // job to fix, so these payload-builder tests deliberately do not assert
    // on ValidatePayload() output; doing so today would either fail for
    // reasons unrelated to the builder or silently paper over the mismatch.

    [Fact]
    public void BuildPayload_Ghc_ContainsGhcDeliveryAndContactFields()
    {
        var draft = new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "101",
                ["order_code"] = "ORD-001",
                ["client_first_name"] = "John",
                ["client_last_name"] = "Doe",
                ["client_phone"] = "0501234567",
                ["order_address"] = "Main St",
                ["delivery_date"] = "2026-07-25",
                ["fullfilment_plant"] = "PLANT-1"
            },
            Products = new List<Product>
            {
                new() { ItemCode = "100001", ItemName = "Product 1", Quantity = 2m, UnitPrice = 50m, VatPercentage = 15m }
            },
            Payments = new List<Payment>
            {
                new() { PaymentMethod = "COD", PaymentStatus = "not_payment", PaymentAmount = 115m }
            }
        };

        var payload = _flatBuilder.BuildPayload(draft, FlatVariant.GhcVariant);

        Assert.True(payload.ContainsKey("delivery_date"));
        Assert.Equal("2026-07-25", payload["delivery_date"]);
        Assert.True(payload.ContainsKey("fullfilment_plant"));
        Assert.Equal("PLANT-1", payload["fullfilment_plant"]);
        Assert.True(payload.ContainsKey("order_country_code"));
        Assert.True(payload.ContainsKey("order_phone"));

        var payments = Assert.IsType<List<Dictionary<string, object?>>>(payload["payment_methods_with_options"]);
        Assert.True(payments[0].ContainsKey("card_name"));
        Assert.True(payments[0].ContainsKey("bank_code"));
        Assert.True(payments[0].ContainsKey("credit_customer_info"));
        Assert.False(payments[0].ContainsKey("payment_status"));
    }

    [Fact]
    public void BuildPayload_Upc_OmitsGhcOnlyFields()
    {
        var draft = new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "201",
                ["order_code"] = "UPC-999",
                ["client_first_name"] = "Jane",
                ["client_last_name"] = "Smith",
                ["client_phone"] = "0559876543",
                ["order_address"] = "King Road"
            },
            Products = new List<Product>
            {
                new() { ItemCode = "200002", ItemName = "UPC Item", Quantity = 1m, UnitPrice = 100m, VatPercentage = 15m }
            },
            Payments = new List<Payment>
            {
                new() { PaymentMethod = "Visa", PaymentStatus = "done_payment", PaymentAmount = 115m }
            }
        };

        var payload = _flatBuilder.BuildPayload(draft, FlatVariant.UpcVariant);

        Assert.False(payload.ContainsKey("delivery_date"));
        Assert.False(payload.ContainsKey("fullfilment_plant"));
        Assert.False(payload.ContainsKey("order_country_code"));
        Assert.False(payload.ContainsKey("order_phone"));

        var payments = Assert.IsType<List<Dictionary<string, object?>>>(payload["payment_methods_with_options"]);
        Assert.True(payments[0].ContainsKey("payment_status"));
        Assert.False(payments[0].ContainsKey("card_name"));
        Assert.False(payments[0].ContainsKey("bank_code"));
        Assert.False(payments[0].ContainsKey("credit_customer_info"));
    }

    [Fact]
    public void UpcValidation_BlocksPostToCreditPayment()
    {
        var draft = new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "201",
                ["order_code"] = "UPC-999",
                ["client_first_name"] = "Jane",
                ["client_last_name"] = "Smith",
                ["client_phone"] = "0559876543",
                ["order_address"] = "King Road"
            },
            Products = new List<Product> { new() { ItemCode = "1", ItemName = "P1", Quantity = 1, UnitPrice = 10 } },
            Payments = new List<Payment> { new() { PaymentMethod = "PostToCredit", PaymentStatus = "done_payment", PaymentAmount = 10 } }
        };

        var payload = _flatBuilder.BuildPayload(draft, FlatVariant.UpcVariant);
        var errors = _flatValidator.ValidatePayload(payload, "upc_ecommerce", totalPaid: 10m);

        Assert.Contains(errors, e => e.Contains("PostToCredit payment method is not allowed for UPC"));
    }

    [Fact]
    public void UniCommercePayload_CalculatesGrossNetVatAccurately()
    {
        var invoice = new UniCommerceInvoice
        {
            ReferenceNumber = "REF-888",
            OnlineOrderNumber = "ONLINE-888",
            CustomerName = "AMAZON",
            Delivery = new DeliveryDetails { DeliveryFees = 10m },
            RowItems = new List<RowItem>
            {
                new() { MaterialNumber = "600001", Barcode = "BC600001", Quantity = 2m, ItemPrice = 100m, ItemDiscount = 10m, VatPercentage = 15m }
            }
        };

        var payload = _uniBuilder.BuildInvoicePayload(invoice);

        // Row item: Gross = 200, RowTotalDiscount = 20, ItemVat = (100 - 10)*0.15 = 13.5, RowTotalVat = 27, NetAmount = 200 - 20 + 27 = 207
        Assert.Equal(200m, payload["GrossAmount"]);
        Assert.Equal(20m, payload["TotalDiscount"]);
        Assert.Equal(27m, payload["TotalVat"]);
        Assert.Equal(217m, payload["NetAmount"]); // 207 row net + 10 delivery fees

        var errors = _uniValidator.ValidatePayload(payload);
        Assert.Empty(errors);
    }
}
