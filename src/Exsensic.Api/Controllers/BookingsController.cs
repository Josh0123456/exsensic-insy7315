using Exsensic.Api.Security;
using Exsensic.Contracts.Bookings;
using Exsensic.Core.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Exsensic.Api.Controllers;

/// <summary>
/// The client's bookings (docs/CONTRACTS.md §6). Thin: it checks the caller's role, passes the request
/// and the caller's id to <see cref="BookingService"/>, and maps the result to an HTTP response.
/// Errors are thrown by the service and turned into ProblemDetails by the error handler.
/// </summary>
[ApiController]
[Route("api/v1/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly BookingService _bookings;

    /// <summary>
    /// Creates the controller with the booking service.
    /// </summary>
    public BookingsController(BookingService bookings)
    {
        _bookings = bookings;
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
}
