using System.Text.Json.Serialization;

namespace Exsensic.Contracts.Enums;

/// <summary>
/// The four statuses a booking can be in (docs/CONTRACTS.md §3).
/// There is deliberately no Pending, Rejected or Rescheduled value: a rejection is
/// Cancelled with a reason that starts with "Rejected: ", and rescheduling is an action.
/// Status only changes through the State pattern in Exsensic.Core.
/// </summary>
/// <remarks>
/// Sent over HTTP as the name ("Requested"), not a number, so the Web and the API
/// agree on the value and the JSON stays readable. The database also stores the name.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<BookingStatus>))]
public enum BookingStatus
{
    /// <summary>Submitted by the client and waiting for admin approval. A rescheduled booking returns here.</summary>
    Requested,

    /// <summary>Approved by an admin, with a staff member assigned.</summary>
    Confirmed,

    /// <summary>Service delivered, marked by the assigned staff member or an admin. Final.</summary>
    Completed,

    /// <summary>Cancelled by the client or an admin, or rejected by an admin (reason stored). Final.</summary>
    Cancelled,
}
