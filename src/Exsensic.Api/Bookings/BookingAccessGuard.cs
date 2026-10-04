using System.Security.Claims;
using Exsensic.Api.Security;
using Exsensic.Core.Bookings;
using Exsensic.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;

namespace Exsensic.Api.Bookings;

/// <summary>
/// Runs the BookingAccess policy for one booking (owner client, assigned staff member or admin), so every
/// controller that opens or changes a single booking applies the rule the same way.
/// </summary>
public sealed class BookingAccessGuard
{
    private readonly BookingQueryService _queries;
    private readonly IAuthorizationService _authorization;

    /// <summary>
    /// Creates the guard with the booking queries and the authorisation service.
    /// </summary>
    public BookingAccessGuard(BookingQueryService queries, IAuthorizationService authorization)
    {
        _queries = queries;
        _authorization = authorization;
    }

    /// <summary>
    /// Throws <see cref="NotFoundException"/> unless the user may access the booking. A missing booking and a
    /// refused one look identical (404, never 403), so nobody can discover other people's bookings
    /// (docs/CONTRACTS.md §12, decision 10).
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="bookingId">The booking being opened or changed.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    public async Task EnsureCanAccessAsync(ClaimsPrincipal user, int bookingId, CancellationToken ct)
    {
        var parties = await _queries.GetPartiesAsync(bookingId, ct);
        if (parties is null)
        {
            throw new NotFoundException("Booking");
        }

        var resource = new BookingAccessResource(parties.ClientUserId, parties.StaffUserId);
        var result = await _authorization.AuthorizeAsync(user, resource, AuthorizationPolicies.BookingAccess);
        if (!result.Succeeded)
        {
            throw new NotFoundException("Booking");
        }
    }
}
