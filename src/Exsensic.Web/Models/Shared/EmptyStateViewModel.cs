namespace Exsensic.Web.Models.Shared;

/// <summary>Intentional explanation of an empty collection.</summary>
public sealed class EmptyStateViewModel
{
    /// <summary>Short empty-state heading.</summary>
    public required string Title { get; init; }

    /// <summary>Plain-text explanation and next step.</summary>
    public required string Message { get; init; }

    /// <summary>Optional action label.</summary>
    public string? ActionText { get; init; }

    /// <summary>Optional local action destination.</summary>
    public string? ActionUrl { get; init; }
}
