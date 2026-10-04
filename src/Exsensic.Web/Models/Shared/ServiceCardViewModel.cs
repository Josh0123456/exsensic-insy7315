namespace Exsensic.Web.Models.Shared;

/// <summary>Display-only service summary supplied by a parent screen.</summary>
public sealed class ServiceCardViewModel
{
    /// <summary>Service identifier, retained as component metadata.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Service display name.</summary>
    public required string Name { get; init; }

    /// <summary>Category display label.</summary>
    public required string Category { get; init; }

    /// <summary>Short plain-text description.</summary>
    public required string Description { get; init; }

    /// <summary>Duration supplied by the caller, in minutes.</summary>
    public required int DurationMinutes { get; init; }

    /// <summary>Optional starting price in South African rand.</summary>
    public decimal? StartingPrice { get; init; }

    /// <summary>Optional local details destination.</summary>
    public string? DetailsUrl { get; init; }

    /// <summary>Optional local booking destination.</summary>
    public string? BookingUrl { get; init; }
}
