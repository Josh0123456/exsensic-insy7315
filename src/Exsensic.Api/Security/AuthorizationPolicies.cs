namespace Exsensic.Api.Security;

/// <summary>
/// Names of the authorisation policies used on controllers (docs/CONTRACTS.md §2).
/// Use these constants in [Authorize(Policy = ...)] instead of typing role names.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Only clients (businesses booking services).</summary>
    public const string ClientOnly = "ClientOnly";

    /// <summary>Only Exsensic staff.</summary>
    public const string StaffOnly = "StaffOnly";

    /// <summary>Only Exsensic admins.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Staff or admins.</summary>
    public const string StaffOrAdmin = "StaffOrAdmin";

    /// <summary>The owner client, the assigned staff member, or an admin, checked against a specific booking.</summary>
    public const string BookingAccess = "BookingAccess";
}
