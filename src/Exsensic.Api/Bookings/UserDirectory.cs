using Exsensic.Core.Bookings;
using Exsensic.Data;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Api.Bookings;

/// <summary>
/// Reads names and email addresses from the Identity users table for Core's booking queries. Lives in the
/// API because only the API and Data may know about ApplicationUser (docs/CONTRACTS.md §1).
/// </summary>
public sealed class UserDirectory : IUserDirectory
{
    private readonly ExsensicDbContext _db;

    /// <summary>
    /// Creates the directory over the application's database context.
    /// </summary>
    public UserDirectory(ExsensicDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns the contact details for the given users in one query. Only the name and email are read,
    /// never password hashes or security stamps.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserContact>();
        }

        return await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserContact(u.Id, u.FullName, u.Email ?? string.Empty))
            .ToDictionaryAsync(u => u.UserId, ct);
    }
}
