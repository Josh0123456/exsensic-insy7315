using Exsensic.Contracts.Enums;

namespace Exsensic.Contracts.Requirements;

/// <summary>
/// The requirement form for one service category, returned by
/// GET /api/v1/services/{id}/requirement-template (docs/CONTRACTS.md §5–6).
/// </summary>
/// <param name="Category">The service category this template belongs to.</param>
/// <param name="Fields">The questions in the order they appear on the form.</param>
public sealed record RequirementTemplateDto(ServiceCategory Category, IReadOnlyList<RequirementFieldDto> Fields);
