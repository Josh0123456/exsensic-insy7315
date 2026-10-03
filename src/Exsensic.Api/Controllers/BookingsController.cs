using Exsensic.Api.Security;
using Exsensic.Contracts.Bookings;
using Exsensic.Contracts.Common;
using Exsensic.Contracts.Enums;
using Exsensic.Core.Bookings;
using Exsensic.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Exsensic.Api.Controllers;

/// <summary>
/// The client's bookings (docs/CONTRACTS.md §6). Thin: it checks the caller's role and access, passes the
/// request and the caller's id to the booking services, and maps the result to an HTTP response.
/// Errors are thrown by the services and turned into ProblemDetails by the error handler.
/// </summary>
[ApiController]
[Route("api/v1/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly BookingService _bookings;
    private readonly BookingQueryService _queries;
    private readonly IAuthorizationService _authorization;

    /// <summary>
    /// Creates the controller with the booking services and the authorisation service for BookingAccess.
    /// </summary>
    public BookingsController(BookingService bookings, BookingQueryService queries, IAuthorizationService authorization)
    {
        _bookings = bookings;
        _queries = queries;
        _authorization = authorization;
    }

    /// <summary>
    /// Creates a booking for the signed-in client. Returns 201 with the new booking's reference and a
    /// Location header. Rate limited to 10 requests per 10 minutes per user.
    /// </summary>
    /// <param name="request">The service, slot and requirement answers.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ClientOnly)]
    [EnableRateLimiting(RateLimitPolicies.BookingCreate)]
    [ProducesResponseType<BookingCreatedDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BookingCreatedDto>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        var created = await _bookings.CreateAsync(request, User.GetUserId(), ct);
        return Created($"/api/v1/bookings/{created.Id}", created);
    }

    /// <summary>
    /// The signed-in client's own bookings, newest slot first. An unknown status value is 400 validation_failed.
    /// </summary>
    /// <param name="status">Optional status filter: Requested, Confirmed, Completed or Cancelled.</param>
    /// <param name="page">Page number, from 1.</param>
    /// <param name="pageSize">Items per page, default 20, at most 100.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpGet("mine")]
    [Authorize(Policy = AuthorizationPolicies.ClientOnly)]
    public Task<PagedResult<BookingSummaryDto>> Mine(
        BookingStatus? status, int page = 1, int pageSize = BookingQueryService.DefaultPageSize, CancellationToken ct = default) =>
        _queries.ListMineAsync(User.GetUserId(), status, page, pageSize, ct);

    /// <summary>
    /// One booking's full details, for its client, its assigned staff member or an admin. Anyone else gets
    /// 404 not_found, exactly as if the booking didn't exist, so nobody can discover other people's bookings.
    /// </summary>
    /// <param name="id">The booking id.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    [HttpGet("{id:int}")]
    public async Task<BookingDetailDto> Get(int id, CancellationToken ct)
    {
        await EnsureCanAccessAsync(id, ct);
        return await _queries.GetDetailAsync(id, ct);
    }

    /// <summary>
    /// Runs the BookingAccess policy against the booking's client and staff member. A missing booking and a
    /// refused one both throw <see cref="NotFoundException"/>, so the two cases look identical (never 403).
    /// </summary>
    private async Task EnsureCanAccessAsync(int bookingId, CancellationToken ct)
    {
        var parties = await _queries.GetPartiesAsync(bookingId, ct);
        if (parties is null)
        {
            throw new NotFoundException("Booking");
        }

        var resource = new BookingAccessResource(parties.ClientUserId, parties.StaffUserId);
        var result = await _authorization.AuthorizeAsync(User, resource, AuthorizationPolicies.BookingAccess);
        if (!result.Succeeded)
        {
            throw new NotFoundException("Booking");
        }
    }
}
