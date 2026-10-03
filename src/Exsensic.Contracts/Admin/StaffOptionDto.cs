namespace Exsensic.Contracts.Admin;

/// <summary>
/// One staff member an admin can assign to a booking, returned by
/// GET /api/v1/admin/staff?serviceId=&amp;timeSlotId= (docs/CONTRACTS.md §5–6).
/// Only staff who are qualified for the service and free at that time are listed.
/// </summary>
/// <param name="UserId">The staff member's user id, sent back in ConfirmBookingRequest.</param>
/// <param name="FullName">The staff member's full name.</param>
/// <param name="JobTitle">The staff member's job title, for example "Photographer".</param>
public sealed record StaffOptionDto(Guid UserId, string FullName, string JobTitle);
