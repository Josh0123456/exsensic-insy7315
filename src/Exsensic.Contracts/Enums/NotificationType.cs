using System.Text.Json.Serialization;

namespace Exsensic.Contracts.Enums;

/// <summary>
/// What an in-app notification is about (docs/CONTRACTS.md §3).
/// The Observer pattern in Exsensic.Core creates notifications from booking events,
/// and the Web uses this value to choose the wording on the Notifications page.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotificationType>))]
public enum NotificationType
{
    /// <summary>A client submitted a new booking.</summary>
    BookingRequested,

    /// <summary>An admin approved the booking.</summary>
    BookingConfirmed,

    /// <summary>An admin rejected the booking.</summary>
    BookingRejected,

    /// <summary>The booking was cancelled by the client or an admin.</summary>
    BookingCancelled,

    /// <summary>The booking moved to a new time slot.</summary>
    BookingRescheduled,

    /// <summary>The service was delivered and the booking marked complete.</summary>
    BookingCompleted,

    /// <summary>A staff member was assigned to a booking.</summary>
    StaffAssigned,
}
