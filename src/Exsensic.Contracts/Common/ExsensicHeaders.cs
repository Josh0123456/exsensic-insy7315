namespace Exsensic.Contracts.Common;

/// <summary>Custom HTTP headers shared by the Web app and the API.</summary>
public static class ExsensicHeaders
{
    /// <summary>
    /// The visitor's IP address, sent by the Web app on every API call. The API sees the Web app's own IP
    /// for all users, so it rate-limits on this header instead. It can be trusted because the API only
    /// accepts traffic from the Web app (access restriction, docs/operations.md).
    /// </summary>
    public const string ClientIp = "X-Exsensic-Client-IP";
}
