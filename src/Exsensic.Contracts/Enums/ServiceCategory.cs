using System.Text.Json.Serialization;

namespace Exsensic.Contracts.Enums;

/// <summary>
/// The kinds of service Exsensic offers (docs/CONTRACTS.md §3).
/// Each category has its own requirement template, so the booking form asks the right questions.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ServiceCategory>))]
public enum ServiceCategory
{
    /// <summary>Product, team or brand photography.</summary>
    Photoshoot,

    /// <summary>Website design consultations and demonstrations.</summary>
    Website,

    /// <summary>Instagram account consultations.</summary>
    Instagram,

    /// <summary>TikTok content consultations.</summary>
    TikTok,
}
