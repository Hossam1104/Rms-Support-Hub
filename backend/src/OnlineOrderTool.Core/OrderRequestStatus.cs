namespace OnlineOrderTool.Core;

/// <summary>The RequestOrderHeaders.OrderStatus decode map (1..9), and the
/// resend/cancel eligibility rules derived from it. The resend rule is the
/// current Order Requests contract: New and With_Delegate are blocked.
/// See docs/database-schema.md §2.</summary>
public static class OrderRequestStatus
{
    public static readonly IReadOnlyDictionary<int, string> Labels = new Dictionary<int, string>
    {
        [1] = "New",
        [2] = "Confirmed",
        [3] = "Ready",
        [4] = "With_Delegate",
        [5] = "Rejected",
        [6] = "CanceledByClient",
        [7] = "CanceledByAdmin",
        [8] = "Processing",
        [9] = "Done"
    };

    /// <summary>New orders and orders already assigned to a delegate cannot be
    /// resent. Only known status codes participate in the resend workflow;
    /// unknown values fail closed.</summary>
    public static readonly IReadOnlySet<int> ResendBlockedStatuses = new HashSet<int> { 1, 4 };

    /// <summary>Already rejected, already cancelled (by either party), or
    /// already done -- nothing left to cancel.</summary>
    public static readonly IReadOnlySet<int> CancelBlockedStatuses = new HashSet<int> { 5, 6, 7, 9 };

    public static string GetLabel(int status) =>
        Labels.TryGetValue(status, out var label) ? label : $"Unknown ({status})";

    public static bool IsResendAllowed(int status) => Labels.ContainsKey(status) && !ResendBlockedStatuses.Contains(status);

    public static bool IsCancelAllowed(int status) => !CancelBlockedStatuses.Contains(status);
}
