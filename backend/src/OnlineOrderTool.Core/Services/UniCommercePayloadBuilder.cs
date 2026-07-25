using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Services;

public interface IUniCommercePayloadBuilder
{
    Dictionary<string, object?> BuildInvoicePayload(UniCommerceInvoice invoice);
}

public class UniCommercePayloadBuilder : IUniCommercePayloadBuilder
{
    private static string? FormatBirthDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Contains('T')) return value;
        if (value.Length == 10) return $"{value}T00:00:00";
        return value;
    }

    public Dictionary<string, object?> BuildInvoicePayload(UniCommerceInvoice invoice)
    {
        var rowItemsPayload = invoice.RowItems.Select(item =>
        {
            var vatPct = FlatOrderPayloadBuilder.NormalizeVatPercentage(item.VatPercentage);
            var vatDecimal = Math.Round(vatPct / 100m, 4);

            var grossAmount = Math.Round(item.Quantity * item.ItemPrice, 2);
            var rowTotalDiscount = Math.Round(item.ItemDiscount * item.Quantity, 2);
            var itemVat = Math.Round((item.ItemPrice - item.ItemDiscount) * vatDecimal, 2);
            var rowTotalVat = Math.Round(itemVat * item.Quantity, 2);
            var netAmount = Math.Round(grossAmount - rowTotalDiscount + rowTotalVat, 2);

            return new Dictionary<string, object?>
            {
                ["Quantity"] = item.Quantity,
                ["MaterialNumber"] = item.MaterialNumber ?? "",
                ["ItemPrice"] = item.ItemPrice,
                ["ItemDiscount"] = item.ItemDiscount,
                ["RowTotalDiscount"] = rowTotalDiscount,
                ["ItemVat"] = itemVat,
                ["RowTotalVat"] = rowTotalVat,
                ["BatchNumber"] = string.IsNullOrEmpty(item.BatchNumber) ? null : item.BatchNumber,
                ["ExpireDate"] = string.IsNullOrEmpty(item.ExpireDate) ? null : item.ExpireDate,
                ["SerialNumber"] = string.IsNullOrEmpty(item.SerialNumber) ? null : item.SerialNumber,
                ["Barcode"] = item.Barcode ?? "",
                ["ScannedCode"] = string.IsNullOrEmpty(item.ScannedCode) ? null : item.ScannedCode,
                ["GrossAmount"] = grossAmount,
                ["NetAmount"] = netAmount,
                ["VatPercentage"] = vatDecimal,
                ["OfferIdentifier"] = string.IsNullOrEmpty(item.OfferIdentifier) ? null : item.OfferIdentifier
            };
        }).ToList();

        var grossAmountTotal = Math.Round(invoice.RowItems.Sum(r => r.GrossAmount), 2);
        var totalDiscount = Math.Round(invoice.RowItems.Sum(r => r.RowTotalDiscount), 2);
        var totalVat = Math.Round(invoice.RowItems.Sum(r => r.RowTotalVat), 2);
        var deliveryFees = Math.Round(invoice.Delivery.DeliveryFees, 2);
        var netAmountTotal = Math.Round(invoice.RowItems.Sum(r => r.NetAmount) + deliveryFees, 2);

        var paidOnline = Math.Round(invoice.PaidOnlineAmount, 2);
        var paidPoints = Math.Round(invoice.PaidWithPointsAmount, 2);
        var customerCredit = Math.Round(netAmountTotal - paidOnline - paidPoints, 2);

        var creationDate = string.IsNullOrEmpty(invoice.OrderCreationDate)
            ? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff")
            : invoice.OrderCreationDate;

        return new Dictionary<string, object?>
        {
            ["ReferenceNumber"] = invoice.ReferenceNumber ?? "",
            ["OnlineOrderNumber"] = invoice.OnlineOrderNumber ?? "",
            ["IsReturn"] = invoice.IsReturn,
            ["ParentReferenceNumber"] = string.IsNullOrEmpty(invoice.ParentReferenceNumber) ? null : invoice.ParentReferenceNumber,
            ["OrderCreationDate"] = creationDate,
            ["TotalDiscount"] = totalDiscount,
            ["TotalVat"] = totalVat,
            ["GrossAmount"] = grossAmountTotal,
            ["NetAmount"] = netAmountTotal,
            ["CustomerName"] = invoice.CustomerName ?? "",
            ["CustomerCreditAmount"] = customerCredit,
            ["PaidOnlineAmount"] = paidOnline,
            ["PaidWithPointsAmount"] = paidPoints,
            ["InvoiceConsumer"] = new Dictionary<string, object?>
            {
                ["FirstName"] = string.IsNullOrEmpty(invoice.Consumer.FirstName) ? null : invoice.Consumer.FirstName,
                ["MiddleName"] = string.IsNullOrEmpty(invoice.Consumer.MiddleName) ? null : invoice.Consumer.MiddleName,
                ["LastName"] = string.IsNullOrEmpty(invoice.Consumer.LastName) ? null : invoice.Consumer.LastName,
                ["ConsumerCode"] = string.IsNullOrEmpty(invoice.Consumer.ConsumerCode) ? null : invoice.Consumer.ConsumerCode,
                ["Gender"] = string.IsNullOrEmpty(invoice.Consumer.Gender) ? null : invoice.Consumer.Gender,
                ["BirthDate"] = FormatBirthDate(invoice.Consumer.BirthDate),
                ["PrimaryPhoneNumber"] = string.IsNullOrEmpty(invoice.Consumer.PrimaryPhoneNumber) ? null : invoice.Consumer.PrimaryPhoneNumber,
                ["Email"] = string.IsNullOrEmpty(invoice.Consumer.Email) ? null : invoice.Consumer.Email,
                ["NationalId"] = string.IsNullOrEmpty(invoice.Consumer.NationalId) ? null : invoice.Consumer.NationalId,
                ["Nationality"] = string.IsNullOrEmpty(invoice.Consumer.Nationality) ? null : invoice.Consumer.Nationality
            },
            ["DeliveryDetails"] = new Dictionary<string, object?>
            {
                ["DeliveryPhoneNumber"] = string.IsNullOrEmpty(invoice.Delivery.DeliveryPhoneNumber) ? null : invoice.Delivery.DeliveryPhoneNumber,
                ["DeliveryAddress"] = string.IsNullOrEmpty(invoice.Delivery.DeliveryAddress) ? null : invoice.Delivery.DeliveryAddress,
                ["DeliveryLocationUrl"] = string.IsNullOrEmpty(invoice.Delivery.DeliveryLocationUrl) ? null : invoice.Delivery.DeliveryLocationUrl,
                ["DeliveryNotes"] = string.IsNullOrEmpty(invoice.Delivery.DeliveryNotes) ? null : invoice.Delivery.DeliveryNotes,
                ["DeliveryFees"] = deliveryFees
            },
            ["RowItems"] = rowItemsPayload
        };
    }
}
