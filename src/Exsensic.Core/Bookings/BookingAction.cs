namespace Exsensic.Core.Bookings;

/// <summary>
/// The actions that can change a booking (the columns of the transition table in docs/CONTRACTS.md §3,
/// plus Create for the first entry). Kept on each status-change event so observers know what happened:
/// for example a Reject and a Cancel both end in Cancelled, and a Reschedule can stay in Requested.
/// </summary>
public enum BookingAction
{
    /// <summary>The client submitted a new booking.</summary>
    Create,

    /// <summary>An admin approved the booking and assigned staff.</summary>
    Confirm,

    /// <summary>An admin rejected the booking with a reason.</summary>
    Reject,

    /// <summary>The client or an admin moved the booking to another time slot.</summary>
    Reschedule,

    /// <summary>The client or an admin cancelled the booking.</summary>
    Cancel,

    /// <summary>The assigned staff member or an admin marked the service as delivered.</summary>
    Complete,
}
