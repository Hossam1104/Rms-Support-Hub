namespace OnlineOrderTool.Core;

/// <summary>The RequestOrderHeaders.OrderStatus decode map (1..9), and the
/// resend/cancel eligibility rules derived from it. Ported from
/// UPC_ORDER_STATUS_LABELS / UPC_RESEND_BLOCKED_STATUSES in
/// _legacy_flask/modules/flat_order.py, plus CancelBlockedStatuses (new --
/// the legacy app never modeled cancel eligibility by status, only resend).
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

    /// <summary>Already executed/invoiced (With_Delegate, Done) or actively in
    /// the POS cart (Processing) -- resending to a different branch would be
    /// meaningless or unsafe.</summary>
    public static readonly IReadOnlySet<int> ResendBlockedStatuses = new HashSet<int> { 4, 8, 9 };

    /// <summary>Already rejected, already cancelled (by either party), or
    /// already done -- nothing left to cancel.</summary>
    public static readonly IReadOnlySet<int> CancelBlockedStatuses = new HashSet<int> { 5, 6, 7, 9 };

    public static string GetLabel(int status) =>
        Labels.TryGetValue(status, out var label) ? label : $"Unknown ({status})";

    public static bool IsResendAllowed(int status) => !ResendBlockedStatuses.Contains(status);

    public static bool IsCancelAllowed(int status) => !CancelBlockedStatuses.Contains(status);
}
