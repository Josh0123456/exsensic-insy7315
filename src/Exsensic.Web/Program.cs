using Exsensic.Web.Hosting;
using Exsensic.Web.ApiClients;
using Exsensic.Web.Journeys;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Hosting (Josh): telemetry, proxy headers, health checks, Kestrel settings.
builder.Services.AddExsensicHosting(builder.Configuration);

// MVC forms use antiforgery validation and secure cookie sessions.
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
builder.Services.AddScoped<IAuthApi, AuthApi>();
builder.Services.AddScoped<IBookingsApi, BookingsApi>();
builder.Services.AddScoped<IStaffApi, StaffApi>();
builder.Services.AddScoped<IAdminBookingsApi, AdminBookingsApi>();
// Journey adapters map shared API DTOs to screen models; no persisted state is created in Web.
builder.Services.AddScoped<IAccountJourney, AccountJourney>();
builder.Services.AddScoped<ICatalogJourney, CatalogJourney>();
builder.Services.AddScoped<IBookingWizardJourney, BookingWizardJourney>();
// BookingJourney is also used directly by the admin and staff adapters for the shared booking details.
builder.Services.AddScoped<BookingJourney>();
builder.Services.AddScoped<IBookingJourney>(services => services.GetRequiredService<BookingJourney>());
builder.Services.AddScoped<IAdminJourney, AdminJourney>();
builder.Services.AddScoped<IStaffJourney, StaffJourney>();
// Notifications (Daniel): typed client for the notifications page and the nav badge.
builder.Services.AddScoped<INotificationsApi, NotificationsApi>();
// Admin catalogue and users (Dean): typed clients for the admin management screens.
builder.Services.AddScoped<IAdminCatalogApi, AdminCatalogApi>();
builder.Services.AddScoped<IAdminUsersApi, AdminUsersApi>();

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
        // The free Azure SQL database can take up to about a minute to resume after idling,
        // so allow longer than that before showing "the service took too long".
        client.Timeout = TimeSpan.FromSeconds(75);
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
// Browser-facing failures stay generic in every environment; diagnostics belong in logs.
app.UseExceptionHandler("/Error");

app.UseStatusCodePagesWithReExecute("/Status/{0}");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
