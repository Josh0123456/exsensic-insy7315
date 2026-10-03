namespace Exsensic.Core.Bookings;

/// <summary>
/// Looks up people's names and email addresses for booking screens. Users live in ASP.NET Core Identity,
/// which Core can't reference (docs/CONTRACTS.md §1), so Core asks through this interface and the API
/// supplies the implementation: the same dependency-inversion idea as IAppDbContext.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// Returns the contact details of the given users. Users that don't exist are left out of the result.
    /// </summary>
    /// <param name="userIds">The users to look up.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

/// <summary>
/// A user's display details.
/// </summary>
/// <param name="UserId">The user's id.</param>
/// <param name="FullName">The user's full name.</param>
/// <param name="Email">The user's email address.</param>
public sealed record UserContact(Guid UserId, string FullName, string Email);
