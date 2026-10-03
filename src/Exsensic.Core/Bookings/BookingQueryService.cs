using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Core.Bookings;

/// <summary>
/// Read-only booking queries: lists and details. Kept apart from <see cref="BookingService"/>, which
/// changes bookings, so every query here can use AsNoTracking and project straight to DTOs
/// (docs/CONTRACTS.md §10). Who may see a booking is decided by the API's BookingAccess policy before
/// <see cref="GetDetailAsync"/> is called.
/// </summary>
public sealed class BookingQueryService
{
    /// <summary>The page size used when the caller doesn't ask for one (docs/CONTRACTS.md §6).</summary>
    public const int DefaultPageSize = 20;

    /// <summary>The largest page size a caller may ask for, so one request can't load every booking.</summary>
    public const int MaxPageSize = 100;

    private readonly IAppDbContext _db;
    private readonly IUserDirectory _users;

    /// <summary>
    /// Creates the service with the database context and the user directory for names.
    /// </summary>
    public BookingQueryService(IAppDbContext db, IUserDirectory users)
    {
        _db = db;
        _users = users;
    }

    /// <summary>
    /// Returns the client and the assigned staff member of a booking, which the API needs to run the
    /// BookingAccess check, or null if the booking doesn't exist.
    /// </summary>
    /// <param name="bookingId">The booking to look up.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    public Task<BookingParties?> GetPartiesAsync(int bookingId, CancellationToken ct) =>
        _db.Bookings.AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => new BookingParties(b.ClientUserId, b.StaffUserId))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// The signed-in client's own bookings, newest slot first, one page at a time (GET /bookings/mine).
    /// </summary>
    /// <param name="clientUserId">The signed-in client. Only their bookings are returned.</param>
    /// <param name="status">An optional status to filter by.</param>
    /// <param name="page">The page number, from 1. Values below 1 are treated as 1.</param>
    /// <param name="pageSize">Items per page, from 1 to <see cref="MaxPageSize"/>.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    public async Task<PagedResult<BookingSummaryDto>> ListMineAsync(
        Guid clientUserId, BookingStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Bookings.AsNoTracking().Where(b => b.ClientUserId == clientUserId);
        if (status is not null)
        {
            query = query.Where(b => b.Status == status);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(b => b.TimeSlot.SlotDate)
            .ThenByDescending(b => b.TimeSlot.StartTime)
            .ThenByDescending(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new SummaryRow(
                b.Id, b.Reference, b.ServiceId, b.Service.Name, b.Service.Category,
                b.TimeSlot.SlotDate, b.TimeSlot.StartTime, b.TimeSlot.EndTime,
                b.Status, b.ClientUserId, b.StaffUserId))
            .ToListAsync(ct);

        return new PagedResult<BookingSummaryDto>(await ToSummariesAsync(rows, ct), page, pageSize, total);
    }

    /// <summary>
    /// Everything about one booking, including its requirements and status timeline (GET /bookings/{id}).
    /// </summary>
    /// <param name="bookingId">The booking to load.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="NotFoundException">The booking doesn't exist.</exception>
    public async Task<BookingDetailDto> GetDetailAsync(int bookingId, CancellationToken ct)
    {
        var booking = await _db.Bookings.AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => new
            {
                b.Id,
                b.Reference,
                b.ServiceId,
                ServiceName = b.Service.Name,
                b.Service.Category,
                b.TimeSlotId,
                b.TimeSlot.SlotDate,
                b.TimeSlot.StartTime,
                b.TimeSlot.EndTime,
                b.Status,
                b.ClientUserId,
                b.StaffUserId,
                b.CancellationReason,
                b.CreatedAtUtc,
                b.UpdatedAtUtc,
                b.RowVersion,
                Requirements = b.Requirements.Select(r => new { r.FieldKey, r.FieldValue }).ToList(),
                History = b.StatusHistory
                    .OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id)
                    .Select(h => new { h.FromStatus, h.ToStatus, h.ChangedAtUtc, h.ChangedByUserId, h.Note })
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Booking");

        var profile = await _db.ClientProfiles.AsNoTracking()
            .Where(p => p.UserId == booking.ClientUserId)
            .Select(p => new { p.CompanyName, p.Phone })
            .FirstOrDefaultAsync(ct);

        var userIds = booking.History.Select(h => h.ChangedByUserId).Append(booking.ClientUserId);
        if (booking.StaffUserId is Guid staffId)
        {
            userIds = userIds.Append(staffId);
        }

        var users = await _users.GetContactsAsync(userIds.ToHashSet(), ct);
        var client = users.GetValueOrDefault(booking.ClientUserId);

        return new BookingDetailDto(
            booking.Id,
            booking.Reference,
            booking.ServiceId,
            booking.ServiceName,
            booking.Category,
            booking.TimeSlotId,
            booking.SlotDate,
            booking.StartTime,
            booking.EndTime,
            booking.Status,
            client?.FullName ?? string.Empty,
            profile?.CompanyName ?? string.Empty,
            client?.Email ?? string.Empty,
            profile?.Phone ?? string.Empty,
            booking.StaffUserId,
            NameOf(users, booking.StaffUserId),
            booking.CancellationReason,
            booking.Requirements.ToDictionary(r => r.FieldKey, r => r.FieldValue),
            booking.History
                .Select(h => new BookingStatusHistoryDto(h.FromStatus, h.ToStatus, h.ChangedAtUtc, NameOf(users, h.ChangedByUserId), h.Note))
                .ToList(),
            booking.CreatedAtUtc,
            booking.UpdatedAtUtc,
            Convert.ToBase64String(booking.RowVersion));
    }

    /// <summary>
    /// Adds client names, company names and staff names to a page of bookings with two batched lookups,
    /// instead of one query per row.
    /// </summary>
    private async Task<IReadOnlyList<BookingSummaryDto>> ToSummariesAsync(IReadOnlyList<SummaryRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var clientIds = rows.Select(r => r.ClientUserId).Distinct().ToList();
        var companies = await _db.ClientProfiles.AsNoTracking()
            .Where(p => clientIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.CompanyName, ct);

        var userIds = rows.Select(r => r.ClientUserId)
            .Concat(rows.Where(r => r.StaffUserId is not null).Select(r => r.StaffUserId!.Value))
            .ToHashSet();
        var users = await _users.GetContactsAsync(userIds, ct);

        return rows.Select(r => new BookingSummaryDto(
                r.Id, r.Reference, r.ServiceId, r.ServiceName, r.Category,
                r.SlotDate, r.StartTime, r.EndTime, r.Status,
                NameOf(users, r.ClientUserId) ?? string.Empty,
                companies.GetValueOrDefault(r.ClientUserId) ?? string.Empty,
                NameOf(users, r.StaffUserId),
                RequirementsSummary: null))
            .ToList();
    }

    private static string? NameOf(IReadOnlyDictionary<Guid, UserContact> users, Guid? userId) =>
        userId is Guid id && users.TryGetValue(id, out var user) ? user.FullName : null;

    /// <summary>The columns a booking list needs, read in one query before names are added.</summary>
    private sealed record SummaryRow(
        int Id, string Reference, int ServiceId, string ServiceName, ServiceCategory Category,
        DateOnly SlotDate, TimeOnly StartTime, TimeOnly EndTime, BookingStatus Status,
        Guid ClientUserId, Guid? StaffUserId);
}

/// <summary>
/// Who a booking belongs to: its client and, once confirmed, its assigned staff member.
/// </summary>
/// <param name="ClientUserId">The client who made the booking.</param>
/// <param name="StaffUserId">The assigned staff member, or null before confirmation.</param>
public sealed record BookingParties(Guid ClientUserId, Guid? StaffUserId);
