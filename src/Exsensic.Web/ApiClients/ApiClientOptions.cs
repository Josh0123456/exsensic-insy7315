namespace Exsensic.Web.ApiClients;

/// <summary>Validated connection settings shared by the API transport and token handler.</summary>
public sealed class ApiClientOptions
{
    /// <summary>Absolute API base URI, including its path prefix and a trailing slash.</summary>
    public required Uri BaseAddress { get; init; }
}
