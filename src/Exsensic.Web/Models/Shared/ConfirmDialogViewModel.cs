namespace Exsensic.Web.Models.Shared;

/// <summary>Accessible confirmation copy, without action or business logic.</summary>
public sealed class ConfirmDialogViewModel
{
    /// <summary>Unique HTML ID matching the trigger's data-confirm-dialog value.</summary>
    public required string Id { get; init; }

    /// <summary>Confirmation heading.</summary>
    public required string Title { get; init; }

    /// <summary>Plain-text explanation of the action.</summary>
    public required string Message { get; init; }

    /// <summary>Label for the confirmation button.</summary>
    public string ConfirmText { get; init; } = "Confirm";

    /// <summary>Label for the cancel button.</summary>
    public string CancelText { get; init; } = "Keep unchanged";
}
