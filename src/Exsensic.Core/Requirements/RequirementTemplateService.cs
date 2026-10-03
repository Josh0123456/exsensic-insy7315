using Exsensic.Contracts.Enums;
using Exsensic.Contracts.Requirements;
using Exsensic.Core.Abstractions;
using Exsensic.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Exsensic.Core.Requirements;

/// <summary>
/// Finds the requirement form for a service, so the booking wizard can render step 2 with exactly the
/// questions the API will validate against.
/// </summary>
public sealed class RequirementTemplateService
{
    private readonly IAppDbContext _db;

    /// <summary>
    /// Creates the service with the database context used to look up the service's category.
    /// </summary>
    /// <param name="db">The application's database context.</param>
    public RequirementTemplateService(IAppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns the template for an active service's category.
    /// </summary>
    /// <param name="serviceId">The service being booked.</param>
    /// <param name="ct">Cancelled if the request is aborted.</param>
    /// <returns>The category's requirement template.</returns>
    /// <exception cref="NotFoundException">The service doesn't exist or is archived.</exception>
    public async Task<RequirementTemplateDto> GetForServiceAsync(int serviceId, CancellationToken ct)
    {
        var category = await _db.Services.AsNoTracking()
            .Where(s => s.Id == serviceId && s.IsActive)
            .Select(s => (ServiceCategory?)s.Category)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Service");

        return RequirementTemplates.For(category);
    }
}
