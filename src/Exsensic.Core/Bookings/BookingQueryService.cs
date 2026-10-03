using System.Linq.Expressions;
using Exsensic.Contracts.Admin;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Entities;
using Exsensic.Core.Exceptions;
using Exsensic.Core.Requirements;
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
    /// <summary>The longest date range the staff schedule and admin list accept, so one request stays small.</summary>
    public const int MaxRangeDays = 62;

    /// <summary>The longest requirements summary shown on a staff schedule row.</summary>
    public const int RequirementsSummaryLength = 120;

    /// <summary>Turns a booking into the columns a list row needs, inside the database query.</summary>
    private static readonly Expression<Func<Booking, SummaryRow>> SummaryProjection = b => new SummaryRow(
        b.Id, b.Reference, b.ServiceId, b.Service.Name, b.Service.Category,
        b.TimeSlot.SlotDate, b.TimeSlot.StartTime, b.TimeSlot.EndTime,
        b.Status, b.ClientUserId, b.StaffUserId);

    private readonly IUserDirectory _users;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the service with the database context, the user directory for names, and the clock for "today".
    /// </summary>
    public BookingQueryService(IAppDbContext db, IUserDirectory users, TimeProvider timeProvider)
    {
        _db = db;
        _users = users;
        _timeProvider = timeProvider;
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
            .Select(SummaryProjection)
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
    /// The staff members an admin can assign to a booking for this service and slot
    /// (GET /admin/staff?serviceId=&amp;timeSlotId=): qualified for the service, with an active account, and
    /// with no other Requested or Confirmed booking overlapping the slot. Sorted by name.
    /// </summary>
    /// <param name="serviceId">The booked service.</param>
    /// <param name="timeSlotId">The booked slot.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="NotFoundException">The service or the slot doesn't exist.</exception>
    public async Task<IReadOnlyList<StaffOptionDto>> ListStaffOptionsAsync(int serviceId, int timeSlotId, CancellationToken ct)
    {
        if (!await _db.Services.AsNoTracking().AnyAsync(s => s.Id == serviceId, ct))
        {
            throw new NotFoundException("Service");
        }

        var slot = await _db.TimeSlots.AsNoTracking().FirstOrDefaultAsync(t => t.Id == timeSlotId, ct)
            ?? throw new NotFoundException("Time slot");

        var qualified = await StaffAvailability.QualifiedStaffAsync(_db, serviceId, ct);
        var busy = await StaffAvailability.BusyStaffAsync(_db, slot, exceptBookingId: null, ct);
        var candidates = qualified.Where(id => !busy.Contains(id)).ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        var jobTitles = await _db.StaffProfiles.AsNoTracking()
            .Where(p => candidates.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.JobTitle, ct);
        var users = await _users.GetContactsAsync(candidates, ct);

        return candidates
            .Where(id => users.TryGetValue(id, out var user) && user.IsActive)
            .Select(id => new StaffOptionDto(id, users[id].FullName, jobTitles.GetValueOrDefault(id) ?? string.Empty))
            .OrderBy(s => s.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// A staff member's own Confirmed and Completed bookings between two dates, ordered by date and time,
    /// each with a short requirements summary so they can prepare (GET /staff/me/bookings). Without dates
    /// it returns the current week, Monday to Sunday (SAST).
    /// </summary>
    /// <param name="staffUserId">The signed-in staff member. Only bookings assigned to them are returned.</param>
    /// <param name="from">The first date, inclusive, or null for this Monday.</param>
    /// <param name="to">The last date, inclusive, or null for the Sunday after <paramref name="from"/>.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="RequestValidationException">The range ends before it starts or is longer than <see cref="MaxRangeDays"/>.</exception>
    public async Task<IReadOnlyList<BookingSummaryDto>> ListStaffScheduleAsync(
        Guid staffUserId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var start = from ?? StartOfWeek(TodaySast());
        var end = to ?? start.AddDays(6);
        EnsureValidRange(start, end);

        var rows = await _db.Bookings.AsNoTracking()
            .Where(b => b.StaffUserId == staffUserId
                && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
                && b.TimeSlot.SlotDate >= start
                && b.TimeSlot.SlotDate <= end)
            .OrderBy(b => b.TimeSlot.SlotDate)
            .ThenBy(b => b.TimeSlot.StartTime)
            .Select(SummaryProjection)
            .ToListAsync(ct);

        var bookingIds = rows.Select(r => r.Id).ToList();
        var answers = (await _db.BookingRequirements.AsNoTracking()
                .Where(r => bookingIds.Contains(r.BookingId))
                .Select(r => new { r.BookingId, r.FieldKey, r.FieldValue })
                .ToListAsync(ct))
            .ToLookup(r => r.BookingId, r => KeyValuePair.Create(r.FieldKey, r.FieldValue));

        var summaries = rows.ToDictionary(r => r.Id, r => SummariseRequirements(r.Category, answers[r.Id]));
        return await ToSummariesAsync(rows, ct, summaries);
    }

    /// <summary>
    /// The admin dashboard (GET /admin/dashboard): a count for every status from one grouped query, how many
    /// Requested bookings are waiting, today's bookings and the next seven days' bookings (SAST, cancelled
    /// bookings left out), ordered by date and time.
    /// </summary>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var grouped = await _db.Bookings.AsNoTracking()
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, ct);

        // Every status is present, even at 0, so the dashboard tiles never have to guess.
        var counts = Enum.GetValues<BookingStatus>().ToDictionary(s => s, s => grouped.GetValueOrDefault(s));

        var today = TodaySast();
        var lastDay = today.AddDays(7);
        var upcoming = await _db.Bookings.AsNoTracking()
            .Where(b => b.Status != BookingStatus.Cancelled
                && b.TimeSlot.SlotDate >= today
                && b.TimeSlot.SlotDate <= lastDay)
            .OrderBy(b => b.TimeSlot.SlotDate)
            .ThenBy(b => b.TimeSlot.StartTime)
            .Select(SummaryProjection)
            .ToListAsync(ct);

        var summaries = await ToSummariesAsync(upcoming, ct);
        return new DashboardDto(
            counts,
            counts[BookingStatus.Requested],
            summaries.Where(b => b.SlotDate == today).ToList(),
            summaries.Where(b => b.SlotDate > today).ToList());
    }

    /// <summary>
    /// Every booking for the admin, filtered by status and slot date, one page at a time
    /// (GET /admin/bookings). Requested bookings come first, oldest request first, so the approval queue
    /// is worked in the order clients asked; the rest follow by when they were made.
    /// </summary>
    /// <param name="status">An optional status to filter by.</param>
    /// <param name="from">An optional first slot date, inclusive.</param>
    /// <param name="to">An optional last slot date, inclusive.</param>
    /// <param name="page">The page number, from 1. Values below 1 are treated as 1.</param>
    /// <param name="pageSize">Items per page, from 1 to <see cref="MaxPageSize"/>.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <exception cref="RequestValidationException">The range ends before it starts.</exception>
    public async Task<PagedResult<BookingSummaryDto>> ListForAdminAsync(
        BookingStatus? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        if (from is DateOnly start && to is DateOnly end && end < start)
        {
            throw RangeError("The end date must be on or after the start date.");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Bookings.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(b => b.Status == status);
        }

        if (from is not null)
        {
            query = query.Where(b => b.TimeSlot.SlotDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(b => b.TimeSlot.SlotDate <= to);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(b => b.Status == BookingStatus.Requested ? 0 : 1)
            .ThenBy(b => b.CreatedAtUtc)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(SummaryProjection)
            .ToListAsync(ct);

        return new PagedResult<BookingSummaryDto>(await ToSummariesAsync(rows, ct), page, pageSize, total);
    }

    /// <summary>
    /// A one-line summary of a booking's requirement answers in the form's order, for example
    /// "Product · Studio · 10 · Shots for the online store", cut to <see cref="RequirementsSummaryLength"/>
    /// characters. Optional notes are left out; the full answers are on the booking's detail page.
    /// </summary>
    internal static string? SummariseRequirements(ServiceCategory category, IEnumerable<KeyValuePair<string, string>> answers)
    {
        var byKey = answers.ToDictionary(a => a.Key, a => a.Value);
        var parts = RequirementTemplates.For(category).Fields
            .Where(f => f.Key != "notes" && byKey.ContainsKey(f.Key))
            .Select(f => byKey[f.Key].ReplaceLineEndings(" ").Trim())
            .Where(v => v.Length > 0)
            .ToList();

        if (parts.Count == 0)
        {
            return null;
        }

        var summary = string.Join(" · ", parts);
        return summary.Length <= RequirementsSummaryLength
            ? summary
            : summary[..(RequirementsSummaryLength - 1)].TrimEnd() + "…";
    }

    /// <summary>Today's date in South African time, from the injected clock.</summary>
    private DateOnly TodaySast() =>
        DateOnly.FromDateTime(_timeProvider.GetUtcNow().ToOffset(SastTime.Offset).DateTime);

    /// <summary>The Monday on or before the given date.</summary>
    private static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>Rejects a range that ends before it starts or is longer than <see cref="MaxRangeDays"/>.</summary>
    private static void EnsureValidRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw RangeError("The end date must be on or after the start date.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
        {
            throw RangeError($"Ask for at most {MaxRangeDays} days at a time.");
        }
    }

    private static RequestValidationException RangeError(string message) =>
        new(new Dictionary<string, string[]> { ["to"] = [message] });

    /// <summary>
    /// Adds client names, company names and staff names to a page of bookings with two batched lookups,
    /// instead of one query per row. Requirement summaries are added when given (the staff schedule).
    /// </summary>
    private async Task<IReadOnlyList<BookingSummaryDto>> ToSummariesAsync(
        IReadOnlyList<SummaryRow> rows, CancellationToken ct, IReadOnlyDictionary<int, string?>? requirementSummaries = null)
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
                requirementSummaries?.GetValueOrDefault(r.Id)))
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
