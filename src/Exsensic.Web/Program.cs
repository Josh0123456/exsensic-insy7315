using Exsensic.Web.Hosting;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Journeys;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Hosting (Josh): telemetry, proxy headers, health checks, Kestrel settings.
builder.Services.AddExsensicHosting(builder.Configuration);

// Web security shell. Account screens and sign-in are implemented in Step 4.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-Exsensic.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-Exsensic.Auth";
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<JourneyClock>();
builder.Services.AddScoped<AccountSession>();
// Journey adapters connect the screens to the API; a screen shows "not available yet" until its adapter is registered.
builder.Services.AddScoped<IAccountJourney, AccountJourney>();

// Validate once at startup. Development may use loopback HTTP; deployments must use HTTPS.
var configuredBaseUrl = builder.Configuration["Api:BaseUrl"];
if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var apiBaseAddress)
    || string.IsNullOrEmpty(apiBaseAddress.Host)
    || !string.IsNullOrEmpty(apiBaseAddress.UserInfo)
    || !string.IsNullOrEmpty(apiBaseAddress.Query)
    || !string.IsNullOrEmpty(apiBaseAddress.Fragment)
    || (apiBaseAddress.Scheme != Uri.UriSchemeHttps
        && !(builder.Environment.IsDevelopment() && apiBaseAddress.IsLoopback
            && apiBaseAddress.Scheme == Uri.UriSchemeHttp)))
{
    throw new InvalidOperationException(
        "Api:BaseUrl must be an absolute HTTPS URI without credentials, query or fragment. Development also allows loopback HTTP.");
}

apiBaseAddress = new Uri(apiBaseAddress.AbsoluteUri.TrimEnd('/') + "/");
builder.Services.AddSingleton(new ApiClientOptions { BaseAddress = apiBaseAddress });
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddSingleton<ApiProblemMapper>();
builder.Services.AddHttpClient<ApiClient>((services, client) =>
    {
        client.BaseAddress = services.GetRequiredService<ApiClientOptions>().BaseAddress;
        client.Timeout = TimeSpan.FromSeconds(30);
        client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    .AddHttpMessageHandler<BearerTokenHandler>()
    // Default HTTP logging includes URLs; use only our status-only diagnostics.
    .RemoveAllLoggers();

var app = builder.Build();

// Hosting (Josh): must run first so later middleware sees the real scheme and client IP.
// Also adds HSTS and HTTPS redirection outside Development.
app.UseExsensicHosting();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
