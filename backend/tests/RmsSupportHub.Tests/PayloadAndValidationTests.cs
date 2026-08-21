using System.Text.Json;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Services;
using Xunit;

namespace RmsSupportHub.Tests;

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
                ["order_country_code"] = "SA",
                ["order_phone"] = "0507654321",
                ["delivery_date"] = "2026-07-25",
                ["delivery_from_time"] = "09:00",
                ["delivery_to_time"] = "12:00",
                ["shipping_address_2"] = "Building 2",
                ["fullfilment_plant"] = "PLANT-1"
            },
            Products = new List<Product>
            {
                new() { ItemCode = "100001", ItemName = "Product 1", Quantity = 2m, UnitPrice = 50m, VatPercentage = 15m }
            },
            Payments = new List<Payment>
            {
                new()
                {
                    PaymentMethod = "PostToCredit",
                    PaymentAmount = 115m,
                    CardName = "Local Card",
                    BankCode = "BANK-1",
                    CustomerName = "Credit Customer",
                    CustomerNumber = "CREDIT-1"
                }
            }
        };

        var payload = _flatBuilder.BuildPayload(draft, FlatVariant.GhcVariant);

        Assert.True(payload.ContainsKey("delivery_date"));
        Assert.Equal("2026-07-25", payload["delivery_date"]);
        Assert.Equal("09:00:00", payload["delivery_from_time"]);
        Assert.Equal("12:00:00", payload["delivery_to_time"]);
        Assert.Equal("Building 2", payload["shipping_address_2"]);
        Assert.True(payload.ContainsKey("fullfilment_plant"));
        Assert.Equal("PLANT-1", payload["fullfilment_plant"]);
        Assert.Equal("SA", payload["order_country_code"]);
        Assert.Equal("507654321", payload["order_phone"]);

        var payments = Assert.IsType<List<Dictionary<string, object?>>>(payload["payment_methods_with_options"]);
        Assert.Equal("Local Card", payments[0]["card_name"]);
        Assert.Equal("BANK-1", payments[0]["bank_code"]);
        var creditCustomer = Assert.IsType<Dictionary<string, object?>>(payments[0]["credit_customer_info"]);
        Assert.Equal("CREDIT-1", creditCustomer["customer_number"]);
        Assert.Equal("Credit Customer", creditCustomer["customer_name"]);
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
        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.UpcVariant, totalPaid: 10m);

        Assert.Contains(errors, e => e.Contains("PostToCredit payment method is not allowed for this module"));
    }

    /// <summary>UPC settles only through Visa, Tamara and Tabby. The rule lives
    /// on FlatVariant.AllowedPaymentMethods, so the validator enforces it
    /// independently of whatever the Angular dialog happens to offer.</summary>
    private static OrderDraft PaymentPolicyDraft(string method, string status)
        => new()
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "201",
                ["order_code"] = "POLICY-1",
                ["client_first_name"] = "Jane",
                ["client_last_name"] = "Smith",
                ["client_phone"] = "0559876543",
                ["order_address"] = "King Road"
            },
            Products = new List<Product> { new() { ItemCode = "1", ItemName = "P1", Quantity = 1, UnitPrice = 100, VatPercentage = 15 } },
            Payments = new List<Payment> { new() { PaymentMethod = method, PaymentStatus = status, PaymentAmount = 115m } }
        };

    [Theory]
    [InlineData("Visa")]
    [InlineData("Tamara")]
    [InlineData("Tabby")]
    public void UpcValidation_AcceptsTheThreeAllowedPaymentMethods(string method)
    {
        var payload = _flatBuilder.BuildPayload(PaymentPolicyDraft(method, "done_payment"), FlatVariant.UpcVariant);
        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.UpcVariant, totalPaid: 115m);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("MisPay")]
    [InlineData("Emkan")]
    [InlineData("RajhiPoints")]
    public void UpcValidation_RejectsPaymentMethodsOutsideTheAllowedSet(string method)
    {
        var payload = _flatBuilder.BuildPayload(PaymentPolicyDraft(method, "done_payment"), FlatVariant.UpcVariant);
        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.UpcVariant, totalPaid: 115m);

        Assert.Contains(errors, e => e == $"Payment method '{method}' is not allowed. The allowed payment methods: [Visa, Tamara, Tabby]");
    }

    /// <summary>UPC cash on delivery is the zero-payment shape, never a payment
    /// row -- so an explicit COD row is a malformed payload, not a cash order.</summary>
    [Fact]
    public void UpcValidation_RejectsAnExplicitCodPaymentRow()
    {
        var payload = _flatBuilder.BuildPayload(PaymentPolicyDraft("COD", "not_payment"), FlatVariant.UpcVariant);
        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.UpcVariant, totalPaid: 115m);

        Assert.Contains(errors, e => e == "Payment method 'COD' is not allowed. The allowed payment methods: [Visa, Tamara, Tabby]");
    }

    /// <summary>The status rules still apply to the methods UPC does allow.</summary>
    [Theory]
    [InlineData("Visa")]
    [InlineData("Tamara")]
    [InlineData("Tabby")]
    public void UpcValidation_StillRequiresDonePaymentStatusOnAllowedMethods(string method)
    {
        var payload = _flatBuilder.BuildPayload(PaymentPolicyDraft(method, "not_payment"), FlatVariant.UpcVariant);
        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.UpcVariant, totalPaid: 115m);

        Assert.Contains(errors, e => e.Contains($"{method} payment status must be 'done_payment'"));
    }

    /// <summary>UPC's three-method whitelist is scoped to UPC: GHC keeps the
    /// full legacy method list, including the wallets UPC now refuses.</summary>
    [Theory]
    [InlineData("MisPay")]
    [InlineData("Emkan")]
    [InlineData("RajhiPoints")]
    [InlineData("COD")]
    public void GhcValidation_StillAcceptsTheFullLegacyPaymentMethodList(string method)
    {
        var payload = _flatBuilder.BuildPayload(PaymentPolicyDraft(method, "done_payment"), FlatVariant.GhcVariant);
        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.GhcVariant, totalPaid: 115m);

        Assert.DoesNotContain(errors, e => e.Contains("is not allowed"));
    }

    /// <summary>An order with no payment rows is a valid Cash-on-Delivery
    /// order, not an incomplete one. The builder already emitted the COD shape
    /// for that state; the validator used to reject it before the payload ever
    /// reached the send. Both halves are asserted here so the rule cannot
    /// regress from either side.</summary>
    [Theory]
    [InlineData(true)]  // UPC variant
    [InlineData(false)] // GHC variant
    public void NoPayment_BuildsCashOnDeliveryShapeAndPassesValidation(bool upc)
    {
        var variant = upc ? FlatVariant.UpcVariant : FlatVariant.GhcVariant;
        var draft = new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "201",
                ["order_code"] = "UPC-COD-1",
                ["client_first_name"] = "Jane",
                ["client_last_name"] = "Smith",
                ["client_phone"] = "0559876543",
                ["order_address"] = "King Road",
                ["delivery_date"] = "2026-08-04",
                ["fullfilment_plant"] = "PLANT-1"
            },
            Products = new List<Product>
            {
                new() { ItemCode = "200002", ItemName = "UPC Item", Quantity = 1m, UnitPrice = 100m, VatPercentage = 15m }
            },
            Payments = new List<Payment>()
        };

        var payload = _flatBuilder.BuildPayload(draft, variant);

        Assert.Equal("COD", payload["order_payment_method"]);
        Assert.Equal("not_payment", payload["order_payment_status"]);
        var payments = Assert.IsType<List<Dictionary<string, object?>>>(payload["payment_methods_with_options"]);
        Assert.Empty(payments);

        // No synthetic zero-value payment is fabricated to stand in for one,
        // and totalPaid stays 0 without tripping a coverage rule.
        var errors = _flatValidator.ValidatePayload(payload, variant, totalPaid: 0m);
        Assert.DoesNotContain(errors, e => e.Contains("payment", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Tolerating an absent/empty payment list must not stop the
    /// per-payment rules from running once payments ARE present. The empty
    /// list is now the only thing the Cash-on-Delivery default covers, so
    /// every rule inside the payments loop is asserted here.
    ///
    /// Note what is deliberately NOT asserted: an underpaid digital wallet.
    /// Visa 1.00 against a 115.00 order resolves to order_payment_status
    /// "partially_paid" (FlatOrderPayloadBuilder.DeterminePaymentStatus), and
    /// the full-coverage rule keys off "done_payment" -- so a partial card
    /// payment is a valid state, not an error, both before and after this
    /// change.</summary>
    [Fact]
    public void ExplicitPayment_StillRunsEveryPerPaymentRule()
    {
        var draft = new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "201",
                ["order_code"] = "UPC-998",
                ["client_first_name"] = "Jane",
                ["client_last_name"] = "Smith",
                ["client_phone"] = "0559876543",
                ["order_address"] = "King Road"
            },
            Products = new List<Product> { new() { ItemCode = "1", ItemName = "P1", Quantity = 1, UnitPrice = 100, VatPercentage = 15 } },
            Payments = new List<Payment>
            {
                new() { PaymentMethod = "Visa", PaymentStatus = "not_payment", PaymentAmount = 50m },
                new() { PaymentMethod = "Bitcoin", PaymentStatus = "done_payment", PaymentAmount = 65m },
                new() { PaymentMethod = "PostToCredit", PaymentStatus = "not_payment", PaymentAmount = 0m }
            }
        };

        var payload = _flatBuilder.BuildPayload(draft, FlatVariant.UpcVariant);
        var errors = _flatValidator.ValidatePayload(payload, FlatVariant.UpcVariant, totalPaid: 115m);

        Assert.Contains(errors, e => e.Contains("Payment method 'Bitcoin' is not allowed. The allowed payment methods: [Visa, Tamara, Tabby]"));
        Assert.Contains(errors, e => e.Contains("Visa payment status must be 'done_payment'"));
        Assert.Contains(errors, e => e.Contains("PostToCredit payment method is not allowed for this module."));
    }

    /// <summary>The country code lives in its own key, so the number field
    /// must never repeat it -- including for drafts saved before the rule
    /// existed, which is why the split happens in the builder.</summary>
    [Fact]
    public void BuildPayload_SplitsTheCountryCodeOutOfEveryPhoneField()
    {
        var draft = new OrderDraft
        {
            OrderData = new Dictionary<string, object?>
            {
                ["branch_code"] = "101",
                ["order_code"] = "ORD-002",
                ["client_first_name"] = "John",
                ["client_last_name"] = "Doe",
                ["client_phone"] = "+966556028080",
                ["order_country_code"] = "966",
                ["order_phone"] = "00966556028081",
                ["order_address"] = "Main St"
            },
            Products = new List<Product> { new() { ItemCode = "1", ItemName = "P1", Quantity = 1, UnitPrice = 10 } },
            Payments = new List<Payment>()
        };

        var payload = _flatBuilder.BuildPayload(draft, FlatVariant.GhcVariant);

        Assert.Equal("966", payload["client_country_code"]);
        Assert.Equal("556028080", payload["client_phone"]);
        Assert.Equal("966", payload["order_country_code"]);
        Assert.Equal("556028081", payload["order_phone"]);
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

    [Fact]
    public void GhcValidation_RequiresDeliveryWindowOnlyWhenDeliveryIsEnabled()
    {
        var deliveryDraft = GhcValidationDraft(isDelivery: true);
        var deliveryPayload = _flatBuilder.BuildPayload(deliveryDraft, FlatVariant.GhcVariant);
        var deliveryErrors = _flatValidator.ValidatePayload(deliveryPayload, FlatVariant.GhcVariant, totalPaid: 0m);

        Assert.Contains(deliveryErrors, error => error.Contains("delivery_date", StringComparison.Ordinal));
        Assert.Contains(deliveryErrors, error => error.Contains("delivery_from_time", StringComparison.Ordinal));
        Assert.Contains(deliveryErrors, error => error.Contains("delivery_to_time", StringComparison.Ordinal));

        var nonDeliveryDraft = GhcValidationDraft(isDelivery: false);
        var nonDeliveryPayload = _flatBuilder.BuildPayload(nonDeliveryDraft, FlatVariant.GhcVariant);
        var nonDeliveryErrors = _flatValidator.ValidatePayload(nonDeliveryPayload, FlatVariant.GhcVariant, totalPaid: 0m);

        Assert.DoesNotContain(nonDeliveryErrors, error => error.Contains("required field for delivery order", StringComparison.Ordinal));
    }

    [Fact]
    public void GhcValidationRejectsProductTotalMismatchButAcceptsCompiledRowsThatMatch()
    {
        var payload = _flatBuilder.BuildPayload(GhcValidationDraft(isDelivery: false), FlatVariant.GhcVariant);

        var matchingErrors = _flatValidator.ValidatePayload(payload, FlatVariant.GhcVariant, totalPaid: 0m);
        Assert.DoesNotContain(matchingErrors, error => error.Contains("order_product_total_value", StringComparison.Ordinal));

        payload["order_product_total_value"] = Convert.ToDecimal(payload["order_product_total_value"]) + 1m;
        var mismatchErrors = _flatValidator.ValidatePayload(payload, FlatVariant.GhcVariant, totalPaid: 0m);

        Assert.Contains(mismatchErrors, error => error.Contains("order_product_total_value", StringComparison.Ordinal));
    }

    [Fact]
    public void UniCommercePreservesFourDecimalVatForFractionalItemPrice()
    {
        var invoice = new UniCommerceInvoice
        {
            ReferenceNumber = "UNI-FRACTIONAL-1",
            CustomerName = "AMAZON",
            RowItems = new List<RowItem>
            {
                new() { MaterialNumber = "FRACTIONAL-1", Quantity = 1m, ItemPrice = 34.25m, VatPercentage = 15m }
            }
        };

        var payload = _uniBuilder.BuildInvoicePayload(invoice);
        var rows = Assert.IsType<List<Dictionary<string, object?>>>(payload["RowItems"]);

        Assert.Equal(5.1375m, rows[0]["ItemVat"]);
        Assert.Equal(5.1375m, rows[0]["RowTotalVat"]);
        Assert.Equal(39.3875m, rows[0]["NetAmount"]);
        Assert.Equal(5.1375m, payload["TotalVat"]);
        Assert.Equal(39.3875m, payload["NetAmount"]);
        Assert.Empty(_uniValidator.ValidatePayload(payload));
    }

    private static OrderDraft GhcValidationDraft(bool isDelivery) => new()
    {
        OrderData = new Dictionary<string, object?>
        {
            ["branch_code"] = "101",
            ["order_code"] = "GHC-VALIDATION-1",
            ["client_first_name"] = "Test",
            ["client_last_name"] = "Operator",
            ["client_phone"] = "0500000000",
            ["order_address"] = "Test address",
            ["is_delivery"] = isDelivery
        },
        Products = new List<Product>
        {
            new() { ItemCode = "100001", ItemName = "Test product", Quantity = 1m, UnitPrice = 34.25m, VatPercentage = 15m }
        },
        Payments = new List<Payment>()
    };
}
