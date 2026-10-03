namespace Exsensic.Contracts.Auth;

/// Role string constants. Identity stores these as-is so the DB and JWT claims never drift.
public static class RoleNames
{
    public const string Client = "Client";
    public const string Staff = "Staff";
    public const string Admin = "Admin";
}
