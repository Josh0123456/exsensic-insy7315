namespace Exsensic.Core.Bookings;

/// <summary>
/// Booking rules that the business may want to change without a code change, read from the
/// "BookingPolicy" configuration section (docs/CONTRACTS.md §9).
/// </summary>
public sealed class BookingPolicyOptions
{
    /// <summary>The configuration section these options are read from.</summary>
    public const string SectionName = "BookingPolicy";

    /// <summary>
    /// How many hours before a confirmed booking starts the client can no longer cancel it themselves
    /// (BookingPolicy:ClientCancelCutoffHours, default 24). Admins can cancel at any time.
    /// </summary>
    public int ClientCancelCutoffHours { get; set; } = 24;
}
