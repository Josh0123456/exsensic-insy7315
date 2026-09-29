namespace Exsensic.Web.Models.Shared;

/// <summary>One-request, plain-text feedback keys read once by the shared layout.</summary>
public static class ToastKeys
{
    /// <summary>TempData key for a success message.</summary>
    public const string Success = "Ui.SuccessMessage";

    /// <summary>TempData key for an error message.</summary>
    public const string Error = "Ui.ErrorMessage";
}
