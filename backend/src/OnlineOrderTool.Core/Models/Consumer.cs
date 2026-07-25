namespace OnlineOrderTool.Core.Models;

public class Consumer
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? ConsumerCode { get; set; }
    public string? Gender { get; set; }
    public string? BirthDate { get; set; }
    public string? PrimaryPhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? NationalId { get; set; }
    public string? Nationality { get; set; }

    /// <summary>UPC-only: resolved from LoyaltyConsumerAddresses (FullAddress,
    /// falling back to composed StreetName/Building/Floor/Landmark/Area).</summary>
    public string? Address { get; set; }
    public string? AddressCode { get; set; }
}
