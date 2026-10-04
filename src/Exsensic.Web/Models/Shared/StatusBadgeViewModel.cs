namespace Exsensic.Web.Models.Shared;

/// <summary>Presentation of a contract-defined booking status; replace the string with the Contracts enum when it exists.</summary>
public sealed class StatusBadgeViewModel
{
    /// <summary>Exactly Requested, Confirmed, Completed or Cancelled.</summary>
    public required string Status { get; init; }
}
